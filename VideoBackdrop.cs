using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

sealed class VideoBackdrop : IDisposable
{
    readonly MediaPlayer player = new MediaPlayer { IsMuted = true, Volume = 0, ScrubbingEnabled = true };
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly string file;
    RenderTargetBitmap target;
    DrawingVisual visual;
    bool ready, playing, aligned;
    long captureAt, checkAt, settleUntil;
    public bool Failed { get; private set; }
    public Bitmap Image { get; private set; }
    public int Revision { get; private set; }

    public VideoBackdrop(string path)
    {
        file = path;
        player.MediaOpened += (s,e) => ready = true;
        player.MediaFailed += (s,e) => Failed = true;
        try { player.Open(new Uri(path)); }
        catch { player.Close(); throw; }
    }

    public void Sync(double seconds, bool run, double rate)
    {
        if (Failed) return;
        if (!ready || player.NaturalVideoWidth <= 0 || !player.NaturalDuration.HasTimeSpan)
        {
            if (clock.ElapsedMilliseconds > 12000) Failed = true;
            return;
        }
        long now = clock.ElapsedMilliseconds;
        double end = Math.Max(0, player.NaturalDuration.TimeSpan.TotalSeconds - .12);
        double position = Math.Min(end, Math.Max(0, seconds));
        bool advance = run && rate > 0 && seconds < end;
        if (!aligned || now >= checkAt && Math.Abs(player.Position.TotalSeconds - position) > (advance ? .55 : .08))
        {
            player.Position = TimeSpan.FromSeconds(position);
            aligned = true;
            checkAt = now + 500;
            settleUntil = now + 750;
        }
        if (advance && Math.Abs(player.SpeedRatio - rate) > .001) player.SpeedRatio = rate;
        if (advance != playing)
        {
            if (advance) player.Play(); else player.Pause();
            playing = advance;
            settleUntil = now + 750;
        }
        if (now < captureAt || !advance && Image != null && now > settleUntil) return;
        captureAt = now + 40;
        if (target == null)
        {
            double scale = Math.Min(1, Math.Min(1280.0 / player.NaturalVideoWidth, 720.0 / player.NaturalVideoHeight));
            int w = Math.Max(1, (int)Math.Round(player.NaturalVideoWidth * scale)), h = Math.Max(1, (int)Math.Round(player.NaturalVideoHeight * scale));
            target = new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
            visual = new DrawingVisual();
            using (var draw = visual.RenderOpen()) draw.DrawVideo(player, new System.Windows.Rect(0,0,w,h));
            Image = new Bitmap(w,h,PixelFormat.Format32bppPArgb);
        }
        target.Render(visual);
        var data = Image.LockBits(new Rectangle(0,0,Image.Width,Image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try { target.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Stride * data.Height, data.Stride); }
        finally { Image.UnlockBits(data); }
        Revision++;
    }

    public void Dispose()
    {
        player.Close();
        if (Image != null) Image.Dispose();
        MusicVideos.Discard(file);
    }
}
