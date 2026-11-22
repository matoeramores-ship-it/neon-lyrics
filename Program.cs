using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

[assembly: System.Reflection.AssemblyTitle("Neon Lyrics")]
[assembly: System.Reflection.AssemblyProduct("Neon Lyrics")]
[assembly: System.Reflection.AssemblyVersion("2.10.1.0")]
[assembly: System.Reflection.AssemblyDescription("Letras de Spotify en el escritorio de Windows")]

static class Program
{
    [STAThread]
    static void Main()
    {
        bool first;
        using (var instance = new Mutex(true, "Local\\NeonLyricsWindows", out first))
        {
            if (!first)
            {
                MessageBox.Show("Neon Lyrics ya está abierto. Busca su icono junto al reloj.", "Neon Lyrics");
                return;
            }
            WordTimes.LoadReader();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var app = new LyricsApp()) Application.Run(app);
        }
    }
}

sealed class LyricsApp : ApplicationContext
{
    readonly Dictionary<string, Wallpaper> walls = new Dictionary<string, Wallpaper>();
    Screen[] screens = new Screen[0];
    long checkScreensAt;
    WallpaperStyle primaryStyle = WallpaperStyle.Boat;
    Bitmap cover;
    bool fetchingCover, triedHD;
    CancellationTokenSource hdRequest;
    CancellationTokenSource videoRequest;
    VideoBackdrop video;
    long retryVideoAt;
    long retryCoverAt;
    readonly Playback playback = new Playback();
    readonly Lyrics lyrics = new Lyrics();
    readonly Rhythm rhythm = new Rhythm();
    bool quickClock;
    [System.Runtime.InteropServices.DllImport("winmm")] static extern uint timeBeginPeriod(uint ms);
    [System.Runtime.InteropServices.DllImport("winmm")] static extern uint timeEndPeriod(uint ms);
    readonly Stopwatch elapsed = Stopwatch.StartNew();
    readonly Timer poll = new Timer { Interval = 285 };
    readonly Timer paint = new Timer { Interval = 180 };
    readonly NotifyIcon tray;
    readonly Icon icon;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly ToolStripMenuItem status;

    CancellationTokenSource songRequest;
    Track track;
    LyricLine[] lines;
    SongReactions reactions;
    bool showEmojis = true;
    double position, rate, readAt;
    long retryLyricsAt, retryDesktopAt;
    bool drawing = true, reading, couldNotRead, fetching, stopped, videoAllowed;
    string desktopError;
    readonly ToolStripMenuItem boatItem, blackItem, whiteItem, coverItem, lensItem, videoItem, videoStatus;

    public LyricsApp()
    {
        icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
        status = new ToolStripMenuItem("Esperando a Spotify") { Enabled = false };
        try
        {
            string saved = Registry.GetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics", "Principal", "Barco") as string;
            primaryStyle = saved == "Blanco" ? WallpaperStyle.White : saved == "Video" ? WallpaperStyle.Video : saved == "Lente" ? WallpaperStyle.Lens : saved == "Portada" ? WallpaperStyle.Cover : saved == "Negro" ? WallpaperStyle.Black : WallpaperStyle.Boat;
            videoAllowed = Object.Equals(Registry.GetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics", "VideoInternetArchive", 0), 1);
            showEmojis = !Object.Equals(Registry.GetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics", "Comentarios", 1), 0);
            if (primaryStyle == WallpaperStyle.Video && !videoAllowed) primaryStyle = WallpaperStyle.Black;
        }
        catch (System.Security.SecurityException) { }
        boatItem = new ToolStripMenuItem("Fondo del barco", null, (s,e) => Choose(WallpaperStyle.Boat));
        blackItem = new ToolStripMenuItem("Fondo negro", null, (s,e) => Choose(WallpaperStyle.Black));
        whiteItem = new ToolStripMenuItem("Fondo blanco", null, (s,e) => Choose(WallpaperStyle.White));
        coverItem = new ToolStripMenuItem("Portada · Ojo de pez", null, (s,e) => Choose(WallpaperStyle.Cover));
        lensItem = new ToolStripMenuItem("Ojo de pez · Sin portada", null, (s,e) => Choose(WallpaperStyle.Lens));
        videoItem = new ToolStripMenuItem("Vídeo musical · Internet Archive", null, (s,e) =>
        {
            if (primaryStyle == WallpaperStyle.Video) return;
            if (videoAllowed || MessageBox.Show("Mientras este fondo esté activo, se enviarán a Internet Archive el título y el artista de cada canción de Spotify para buscar su videoclip. ¿Activar el modo vídeo?",
                "Vídeo musical", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                videoAllowed = true;
                Choose(WallpaperStyle.Video);
            }
        });
        videoItem.ToolTipText = "Busca por título y artista mientras este fondo está visible.";
        videoStatus = new ToolStripMenuItem("Esperando a Spotify") { Enabled = false };
        CheckStyles();
        var show = new ToolStripMenuItem("Mostrar fondo") { Checked = true, CheckOnClick = true };
        show.CheckedChanged += (s, e) =>
        {
            drawing = show.Checked;
            if (!drawing)
            {
                CancelHD();
                ClearVideo();
                foreach (var wall in walls.Values) wall.Hide();
            }
            else retryDesktopAt = 0;
        };
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(boatItem);
        menu.Items.Add(blackItem);
        menu.Items.Add(whiteItem);
        menu.Items.Add(coverItem);
        menu.Items.Add(lensItem);
        menu.Items.Add(videoItem);
        menu.Items.Add(videoStatus);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(show);
        var emojis = new ToolStripMenuItem("Emojis de la canción") { CheckOnClick = true, Checked = showEmojis };
        emojis.CheckedChanged += (s,e) =>
        {
            showEmojis = emojis.Checked;
            try { Registry.SetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics","Comentarios",showEmojis ? 1 : 0); }
            catch (System.Security.SecurityException) { }
            PaintDesktop();
        };
        menu.Items.Add(emojis);
        var startup = new ToolStripMenuItem("Iniciar con Windows");
        const string runKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
        string launch = "\"" + Application.ExecutablePath + "\"";
        try { startup.Checked = Object.Equals(Registry.GetValue(runKey, "NeonLyricsWallpaper", null), launch); }
        catch (System.Security.SecurityException) { }
        startup.Click += (s,e) =>
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (startup.Checked) key.DeleteValue("NeonLyricsWallpaper", false);
                    else key.SetValue("NeonLyricsWallpaper", launch);
                }
                startup.Checked = !startup.Checked;
            }
            catch (Exception) { tray.ShowBalloonTip(2500, "Neon Lyrics", "Windows no pudo guardar el inicio automático.", ToolTipIcon.Info); }
        };
        menu.Items.Add(startup);
        menu.Items.Add("Salir", null, (s, e) => ExitThread());
        tray = new NotifyIcon { Text = "Neon Lyrics · Fondo de Spotify", Icon = icon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (s,e) => menu.Show(Cursor.Position);
        poll.Tick += async (s, e) => await ReadSpotify();
        paint.Tick += (s, e) => PaintDesktop();
        poll.Start();
        paint.Start();
    }

    async System.Threading.Tasks.Task ReadSpotify()
    {
        if (reading) return;
        reading = true;
        try
        {
            await playback.ReadAsync();
            if (stopped) return;
            couldNotRead = false;
            bool sameSong = playback.Track != null && track != null && track.Key == playback.Track.Key;
            if (!sameSong)
                SelectTrack(playback.Track);
            else track = playback.Track;
            double now = elapsed.Elapsed.TotalSeconds;
            double predicted = position + rate * (now - readAt);
            double error = playback.Position - predicted;
            position = sameSong && rate > 0 && rate == playback.Rate && Math.Abs(error) < .075
                ? predicted + error * .3 : playback.Position;
            rate = playback.Rate;
            readAt = now;
            if (track != null && cover == null && !fetchingCover && elapsed.ElapsedMilliseconds >= retryCoverAt)
                FetchCover(playback.Thumbnail, songRequest.Token);
            if (drawing && primaryStyle == WallpaperStyle.Cover && track != null && cover != null && !triedHD)
                ImproveCover(track);
            if (drawing && primaryStyle == WallpaperStyle.Video && track != null && playback.TimelineAvailable &&
                video == null && videoRequest == null && elapsed.ElapsedMilliseconds >= retryVideoAt)
                FindVideo(track);
            if (track != null && lines == null && !fetching && elapsed.ElapsedMilliseconds >= retryLyricsAt)
                FindLyrics(track, songRequest.Token);
        }
        catch (Exception)
        {
            if (stopped) return;
            couldNotRead = true;
            playback.Reconnect();
            SelectTrack(null);
        }
        finally { reading = false; }
    }

    void SelectTrack(Track next)
    {
        if (songRequest != null)
        {
            songRequest.Cancel();
            songRequest.Dispose();
        }
        songRequest = next == null ? null : new CancellationTokenSource();
        CancelHD();
        ClearVideo();
        if (cover != null) { cover.Dispose(); cover = null; }
        fetchingCover = false;
        retryCoverAt = 0;
        track = next;
        lines = null;
        reactions = next == null ? null : new SongReactions(next,null);
        fetching = false;
        retryLyricsAt = 0;
    }

    async void FindLyrics(Track selected, CancellationToken cancellation)
    {
        fetching = true;
        try
        {
            var result = await lyrics.FindAsync(selected, cancellation);
            if (!cancellation.IsCancellationRequested)
            {
                lines = result;
                reactions = new SongReactions(selected,result);
            }
        }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested) retryLyricsAt = elapsed.ElapsedMilliseconds + 30000;
        }
        finally
        {
            if (!cancellation.IsCancellationRequested) fetching = false;
        }
    }

    async void FetchCover(Windows.Storage.Streams.IRandomAccessStreamReference thumbnail, CancellationToken token)
    {
        fetchingCover = true;
        try
        {
            var result = await Playback.ReadCoverAsync(thumbnail);
            if (token.IsCancellationRequested) { if (result != null) result.Dispose(); }
            else cover = result;
        }
        catch (Exception) { /* The session may be changing artwork. Retry while this song is current. */ }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                fetchingCover = false;
                retryCoverAt = elapsed.ElapsedMilliseconds + 3500;
            }
        }
    }

    void CancelHD()
    {
        if (hdRequest != null) hdRequest.Cancel();
        hdRequest = null;
        triedHD = false;
    }

    async void ImproveCover(Track selected)
    {
        triedHD = true;
        var request = new CancellationTokenSource();
        hdRequest = request;
        try
        {
            var large = await HDCover.Find(selected,cover,request.Token);
            if (large == null) return;
            if (request.IsCancellationRequested) { large.Dispose(); return; }
            var previous = cover;
            cover = large;
            previous.Dispose();
        }
        catch (Exception) { /* Continue using the local artwork if the catalog is unavailable. */ }
        finally
        {
            if (hdRequest == request) hdRequest = null;
            request.Dispose();
        }
    }

    void ClearVideo()
    {
        if (videoRequest != null) videoRequest.Cancel();
        videoRequest = null;
        if (video != null) { video.Dispose(); video = null; }
        retryVideoAt = 0;
        videoStatus.Text = "Esperando a Spotify";
    }

    async void FindVideo(Track selected)
    {
        var request = new CancellationTokenSource();
        videoRequest = request;
        videoStatus.Text = "Buscando videoclip en Internet Archive…";
        string file = null;
        try
        {
            file = await MusicVideos.Find(selected, request.Token);
            if (request.IsCancellationRequested) return;
            retryVideoAt = Int64.MaxValue;
            if (file == null) { videoStatus.Text = "Sin videoclip coincidente · Mostrando portada"; return; }
            video = new VideoBackdrop(file);
            file = null;
            videoStatus.Text = "Abriendo videoclip…";
        }
        catch (Exception)
        {
            if (!request.IsCancellationRequested)
            {
                retryVideoAt = elapsed.ElapsedMilliseconds + 60000;
                videoStatus.Text = "Vídeo no disponible · Se reintentará en un minuto";
            }
        }
        finally
        {
            MusicVideos.Discard(file);
            if (videoRequest == request) videoRequest = null;
            request.Dispose();
        }
    }

    void CheckStyles()
    {
        boatItem.Checked = primaryStyle == WallpaperStyle.Boat;
        blackItem.Checked = primaryStyle == WallpaperStyle.Black;
        whiteItem.Checked = primaryStyle == WallpaperStyle.White;
        coverItem.Checked = primaryStyle == WallpaperStyle.Cover;
        lensItem.Checked = primaryStyle == WallpaperStyle.Lens;
        videoItem.Checked = videoStatus.Visible = primaryStyle == WallpaperStyle.Video;
    }

    void Choose(WallpaperStyle style)
    {
        if (style != WallpaperStyle.Cover) CancelHD();
        if (style != WallpaperStyle.Video) ClearVideo();
        primaryStyle = style;
        CheckStyles();
        string save = style == WallpaperStyle.White ? "Blanco" : style == WallpaperStyle.Video ? "Video" : style == WallpaperStyle.Lens ? "Lente" : style == WallpaperStyle.Cover ? "Portada" : style == WallpaperStyle.Black ? "Negro" : "Barco";
        try
        {
            if (style == WallpaperStyle.Video) Registry.SetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics", "VideoInternetArchive", 1);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics", "Principal", save);
        }
        catch (Exception) { tray.ShowBalloonTip(2500, "Neon Lyrics", "La elección se aplicó solo a esta sesión.", ToolTipIcon.Info); }
        PaintDesktop();
    }

    internal static WallpaperStyle MonitorStyle(bool primary, WallpaperStyle selected)
    {
        return primary ? selected : selected == WallpaperStyle.Black ? WallpaperStyle.Boat : WallpaperStyle.Black;
    }

    void PaintDesktop()
    {
        bool moving = drawing && track != null && playback.Playing;
        rhythm.Active = moving;
        if (moving && !quickClock) quickClock = timeBeginPeriod(1) == 0;
        if (!moving && quickClock) { timeEndPeriod(1); quickClock = false; }
        int interval = moving ? 15 : 180;
        if (paint.Interval != interval) paint.Interval = interval;
        if (!drawing || elapsed.ElapsedMilliseconds < retryDesktopAt) return;
        double positionNow = Math.Max(0, position + rate * (elapsed.Elapsed.TotalSeconds - readAt));
        var frame = track != null && playback.TimelineAvailable ? Frame.At(lines, positionNow) : Frame.At(null, elapsed.Elapsed.TotalSeconds);
        frame.Artwork = cover;
        frame.Playing = moving;
        frame.Pulse = moving ? rhythm.Pulse : 0;
        frame.Level = moving ? rhythm.Level : 0;
        if (showEmojis && playback.TimelineAvailable && reactions != null) reactions.Apply(frame);
        if (video != null && playback.TimelineAvailable)
        {
            try
            {
                video.Sync(positionNow, moving, rate);
                if (video.Failed) throw new InvalidOperationException();
                frame.Video = video.Image;
                frame.VideoRevision = video.Revision;
                if (frame.Video != null) videoStatus.Text = "Videoclip de Internet Archive";
            }
            catch (Exception)
            {
                ClearVideo();
                retryVideoAt = Int64.MaxValue;
                videoStatus.Text = "No se pudo reproducir el videoclip · Mostrando portada";
            }
        }
        try
        {
            if (elapsed.ElapsedMilliseconds >= checkScreensAt)
            {
                screens = Screen.AllScreens;
                foreach (var id in walls.Keys.Where(id => !screens.Any(s => s.DeviceName == id)).ToArray())
                {
                    walls[id].Dispose();
                    walls.Remove(id);
                }
                string suffix = screens.Length > 1 ? " (principal)" : "";
                boatItem.Text = "Fondo del barco" + suffix;
                blackItem.Text = "Fondo negro" + suffix;
                whiteItem.Text = "Fondo blanco" + suffix;
                coverItem.Text = "Portada · Ojo de pez" + suffix;
                lensItem.Text = "Ojo de pez · Sin portada" + suffix;
                checkScreensAt = elapsed.ElapsedMilliseconds + 2000;
            }
            foreach (Screen screen in screens)
            {
                Wallpaper wall;
                if (!walls.TryGetValue(screen.DeviceName, out wall)) walls.Add(screen.DeviceName, wall = new Wallpaper());
                wall.Draw(frame, screen.Bounds, MonitorStyle(screen.Primary, primaryStyle));
            }
            desktopError = null;
        }
        catch (Exception e)
        {
            if (desktopError == null)
                tray.ShowBalloonTip(3000, "Neon Lyrics", "Reconectando el fondo con el escritorio de Windows.", ToolTipIcon.Info);
            desktopError = e.Message;
            retryDesktopAt = elapsed.ElapsedMilliseconds + 3000;
        }
        string label = track == null ? "Esperando a Spotify" : track.Title;
        if (couldNotRead) label = "Esperando conexión con Spotify";
        if (track != null && lines == null) label = fetching ? "Buscando letra…" : "Esperando conexión con LRCLIB";
        if (lines != null && lines.Length == 0) label = "Sin letra sincronizada para esta canción";
        if (track != null && !playback.TimelineAvailable) label = "Esperando el tiempo de Spotify";
        if (desktopError != null) label = "Reconectando el fondo…";
        label = label.Length > 70 ? label.Substring(0, 67) + "…" : label;
        if (status.Text != label) status.Text = label;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true;
            poll.Dispose();
            paint.Dispose();
            if (quickClock) timeEndPeriod(1);
            rhythm.Dispose();
            SelectTrack(null);
            foreach (var wall in walls.Values) wall.Dispose();
            tray.Visible = false;
            tray.Dispose();
            icon.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}










