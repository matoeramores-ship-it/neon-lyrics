using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Check
{
    [STAThread]
    static void Main(string[] args)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        WordTimes.LoadReader();
        if (Array.IndexOf(args, "--fps") >= 0) { Measure(); return; }
        if (Array.IndexOf(args, "--hd-fixture") >= 0) { TestCatalog(); return; }
        if (Array.IndexOf(args, "--video-fixture") >= 0) { TestVideoPlayback(); return; }
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-MX");
        var lines = Lyrics.Parse("[ar:Demo]\n[00:03.125][00:05.40] Noche \n[00:08.001]\n[offset:100]");
        Require(lines.Length == 3 && lines[0].Text == "Noche" && lines[2].Text == "", "Parser");
        Require(Lyrics.At(lines, 3.02) == -1 && Lyrics.At(lines, 3.03) == 0 && Lyrics.At(lines, 7.91) == 2, "Límites");
        Require(Lyrics.At(lines, 6) == 1 && Lyrics.At(lines, 2) == -1, "Cambios de posición");
        var accents = new Onsets();
        for (int i = 0; i < 80; i++) accents.Update(.01f, .012);
        float hit = accents.Update(.8f, .016);
        for (int i = 0; i < 80; i++) accents.Update(0, .012);
        Require(hit > .5f && accents.Update(0, .012) < .05f, "Ataque y caída del pulso de audio");
        var song = new Track("Canción", "Autor", "Álbum", 180);
        var mismatch = new Lyrics.Record { trackName = "Canción", artistName = "Otra persona", duration = 180, syncedLyrics = "[00:01.00] Otra" };
        var correct = new Lyrics.Record { trackName = "Canción", artistName = "Autor", duration = 181, syncedLyrics = "[00:01.00] Correcta" };
        Require(Lyrics.MatchTrack(new[] { mismatch, correct }, song)[0].Text == "Correcta", "Coincidencia");
        var record = new Lyrics.Record { duration = 8, hasWordSync = true,
            lyricsfile = "lines:\n  - text: 'Luz de mar'\n    start_ms: 1200\n    end_ms: 4000\n    words:\n      - { text: 'Luz ', start_ms: 1200 }\n      - { text: 'de ', start_ms: 1570 }\n      - { text: 'mar', start_ms: 3000, end_ms: 3880 }" };
        var exact = WordTimes.FromRecord(record);
        Require(exact.Length >= 3 && exact[1].Text == "de" && exact[1].Seconds == 1.57, "Marcas por palabra");
        Require(exact[Lyrics.At(exact, 3.879)].Text == "mar" && exact[Lyrics.At(exact, 3.88)].Text == "", "Final cantado y silencio");
        record.lyricsfile = "lines:\n  - text: 'corazón azul'\n    start_ms: 1000\n    end_ms: 6200\n    words:\n      - { text: co, start_ms: 1000 }\n      - { text: ra, start_ms: 1300 }\n      - { text: zón, start_ms: 1550, end_ms: 5400 }\n      - { text: azul, start_ms: 5350, end_ms: 6200 }";
        var sustained = WordTimes.FromRecord(record);
        Require(sustained.Length == 3 && sustained[0].Text == "corazón" && sustained[0].Seconds == 1 &&
            sustained[1].Text == "azul" && sustained[1].Seconds == 5.4, "Palabra completa y marcas solapadas");
        var before = Frame.At(sustained, 5.399);
        var change = Frame.At(sustained, 5.4);
        Require(before.Index == 0 && before.SlideOffset == 0 && change.Index == 1 && change.SlideOffset == 1 &&
            Frame.At(sustained, 5.46).SlideOffset < .5f && Frame.At(sustained, 5.51).SlideOffset == 0, "Transición después de cantar hasta la última sílaba");
        Require(Frame.At(sustained, 5.399).Index == before.Index, "Retroceso durante la transición");
        var estimated = WordTimes.Estimate(Lyrics.Parse("[00:01.00]Luz de mar,\n[00:14.00]otra vez"), 18);
        Require(estimated[1].Seconds > 5 && estimated[2].Seconds > 9 && estimated[Lyrics.At(estimated, 13.999)].Text == "mar," &&
            estimated[Lyrics.At(estimated, 14)].Text == "otra", "Frase larga usa todo su intervalo");
        var rests = WordTimes.Estimate(Lyrics.Parse("[00:01.00]Luz de mar,\n[00:09.00]\n[00:14.00]otra vez"), 18);
        Require(rests[Lyrics.At(rests, 8.999)].Text == "mar," && rests[Lyrics.At(rests, 9)].Text == "" && rests[Lyrics.At(rests, 13.99)].Text == "", "Interludio con marca de silencio");
        record.hasWordSync = false;
        record.lyricsfile = "lines:\n  - { text: 'Luz de mar', start_ms: 1000, end_ms: 7500 }\n  - { text: 'otra vez', start_ms: 14000, end_ms: 16500 }";
        var rows = WordTimes.FromRecord(record);
        Require(rows[Lyrics.At(rows, 7.499)].Text == "mar" && rows[Lyrics.At(rows, 7.5)].Text == "" &&
            rows[Lyrics.At(rows, 14)].Text == "otra" && rows[Lyrics.At(rows, 16.5)].Text == "", "Final de frase explícito");
        var frame = Frame.At(estimated, estimated[2].Seconds+.21);
        Require(frame.Index == 2 && frame.SlideOffset == 0 && Frame.At(estimated, estimated[2].Seconds-.02).SlideOffset == 0, "Sin adelanto en letras estimadas");
        Require(LyricsApp.MonitorStyle(true,WallpaperStyle.Cover) == WallpaperStyle.Cover && LyricsApp.MonitorStyle(false,WallpaperStyle.Boat) == WallpaperStyle.Black && LyricsApp.MonitorStyle(false,WallpaperStyle.Black) == WallpaperStyle.Boat, "Asignación de dos pantallas");
        TestArtwork(frame);
        TestIcons();
        TestInstrumentalWave();
        TestVideoBackdrop();
        TestHeldVocals();
        TestReactions();
        foreach (bool boat in new[]{true,false})
        using (var image = new Bitmap(1280,720))
        using (var graphics = Graphics.FromImage(image))
        using (var scene = new Scene())
        {
            frame = Frame.At(WordTimes.Estimate(Lyrics.Parse("[00:00.00]Sobre el mar, brillan las olas"), 4), 1.9);
            scene.Draw(graphics, image.Size, frame, boat ? WallpaperStyle.Boat : WallpaperStyle.Black);
            image.Save(boat ? "Boat.png" : "Black.png", ImageFormat.Png);
        }        Console.WriteLine("Parser, selección, cambios de posición y render aprobados.");

        if (Array.IndexOf(args, "--interactive") < 0) return;
        Application.EnableVisualStyles();
        using (var wall = new Wallpaper())
        {
            wall.Draw(frame, Screen.PrimaryScreen.Bounds, WallpaperStyle.Boat);
            wall.Draw(frame, Screen.PrimaryScreen.Bounds, WallpaperStyle.Black);
            wall.Draw(frame, Screen.PrimaryScreen.Bounds, WallpaperStyle.Lens);
            Require(wall.Handle != IntPtr.Zero, "Capa de fondo");
            Console.WriteLine("Ventana del fondo creada y presentada.");
            wall.Hide();
            Require(wall.Handle == IntPtr.Zero, "Retirar fondo");
        }

        var source = new Playback();
        source.ReadAsync().GetAwaiter().GetResult();
        if (source.Track != null)
            Console.WriteLine("Spotify detectado. Estado activo: {0}. Tiempo disponible: {1}. Duración: {2:F1}s.",
                source.Playing, source.TimelineAvailable, source.Track.Duration);
        else Console.WriteLine("Sin sesión de Spotify durante la prueba.");

        if (source.Track != null)
        {
            using (var art = Playback.ReadCoverAsync(source.Thumbnail).GetAwaiter().GetResult())
            {
                Console.WriteLine("Portada de Spotify: {0}", art == null ? "ausente" : art.Size.ToString());
                if (art != null)
                using (var render = new Bitmap(1024,576))
                using (var graphics = Graphics.FromImage(render))
                using (var scene = new Scene())
                {
                    var inCover = Object.Equals(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\NeonLyrics","Principal",""),"Portada");
                    using (var hd = inCover ? HDCover.Find(source.Track,art,CancellationToken.None).GetAwaiter().GetResult() : null)
                    {
                    Console.WriteLine("Fuente verificada para el fondo: {0}", (hd ?? art).Size);
                    frame.Artwork = hd ?? art;
                    scene.Draw(graphics, render.Size, frame, WallpaperStyle.Cover);
                    render.Save("CoverCheck.png");
                    scene.Draw(graphics, render.Size, frame, WallpaperStyle.Lens);
                    render.Save("LensCheck.png");
                    frame.Artwork = null;
                    }
                }
            }
            Console.WriteLine("Metadatos locales: {0} / {1} / {2}", source.Track.Title, source.Track.Artist, source.Track.Album);
            var actual = new Lyrics().FindAsync(source.Track, CancellationToken.None).GetAwaiter().GetResult();
            var live = Frame.At(actual, Math.Max(0, source.Position));
            Console.WriteLine("Pista actual: {0} cambios de palabra. Índice para el reloj de Spotify: {1}.", actual.Length, live.Index);
        }
        Console.WriteLine("Pantallas físicas detectadas: {0}.", Screen.AllScreens.Length);
        var test = new Track("Never Gonna Give You Up", "Rick Astley", "", 213);
        var synced = new Lyrics().FindAsync(test, CancellationToken.None).GetAwaiter().GetResult();
        Require(synced.Length > 0, "Catálogo de LRCLIB");
        Require(synced.Any(c => c.Text.Equals("never", StringComparison.OrdinalIgnoreCase)), "Palabras de la canción de referencia");
        var first = Array.FindIndex(synced, c => c.Text.Length > 0);
        Require(first >= 0 && first + 1 < synced.Length && Frame.At(synced, synced[first+1].Seconds).Index == first+1, "Avance del tema de referencia");
        Console.WriteLine("Tema de prueba: {0} cambios recuperados y avance verificado.", synced.Length);
        if(Array.IndexOf(args, "--exe") >= 0) CheckExecutable();
    }

    static void TestReactions()
    {
        var song = new Track("Demo","Autor","",90);
        var cues = Lyrics.Parse("[00:01]te\n[00:01.4]extraño\n[00:02]ring\n[00:02.6]ring\n[00:04]teléfono\n[00:06]wedding\n[00:06.4]ring\n[00:15]no\n[00:15.3]te\n[00:15.6]amo\n[00:30]baila\n[00:30.4]conmigo");
        var reactions = new SongReactions(song,cues);
        var live = Frame.At(cues,2.73);
        live.Playing = true; live.Level = .5f;
        reactions.Apply(live);
        Require(live.Mood != null && live.Mood.Face == "🥲" && live.PhoneRing(2) && live.PhoneRing(3) && !live.PhoneRing(6),"Nostalgia y dos timbrazos, distintos de un anillo");
        Require(Frame.At(Lyrics.Parse("[00:00]¡Ring-ring!"),.3).PhoneRing(0), "Timbre en una palabra");
        var hurt = Frame.At(cues,16); hurt.Playing = true; reactions.Apply(hurt);
        var dancing = Frame.At(cues,31); dancing.Playing = true; reactions.Apply(dancing);
        Require(hurt.Mood.Face == "🥺" && dancing.Mood.Face == "😎", "Reacciones al desamor y al baile");
        var rewind = Frame.At(cues,2.73); rewind.Playing = true; reactions.Apply(rewind);
        Require(live.Mood == rewind.Mood && rewind.ReactionAge == live.ReactionAge, "Emoji sincronizado al retroceder");
        var paused = Frame.At(cues,2.73); reactions.Apply(paused);
        Require(paused.Mood == null, "Reacción en pausa");
        foreach (var pair in new[] {"📞","🦋","drop","🚗","heart","🎸","eye"})
        using (var shape = new System.Drawing.Drawing2D.GraphicsPath())
        using (var first = new System.Drawing.Drawing2D.GraphicsPath())
        using (var second = new System.Drawing.Drawing2D.GraphicsPath())
        {
            LyricIcons.Draw(shape,pair);
            first.AddRectangle(new Rectangle(5,5,80,50)); second.AddRectangle(new Rectangle(5,5,80,50));
            WordMotion.Append(first,shape,pair,.11,true); WordMotion.Append(second,shape,pair,.38,true);
            Require(!first.PathPoints.SequenceEqual(second.PathPoints), "Movimiento de "+pair);
            if (pair == "📞") Require(first.PointCount > second.PointCount, "Ondas visibles al timbrar");
        }
        foreach (WallpaperStyle style in Enum.GetValues(typeof(WallpaperStyle)))
        using (var scene = new Scene())
        using (var bmp = new Bitmap(800,450))
        using (var g = Graphics.FromImage(bmp))
        {
            scene.Draw(g,bmp.Size,live,style);
            if (style == WallpaperStyle.White)
            {
                bmp.Save("ReaccionesPrueba.png",ImageFormat.Png);
                using (var reference = new Bitmap(800,450))
                using (var canvas = Graphics.FromImage(reference))
                {
                    var withoutEmoji = Frame.At(cues,live.Time);
                    withoutEmoji.Playing = live.Playing; withoutEmoji.Level = live.Level;
                    scene.Draw(canvas,reference.Size,withoutEmoji,style);
                    int changed = 0;
                    for (int y=0; y<100; y++)
                    for (int x=0; x<bmp.Width; x++)
                    {
                        if (bmp.GetPixel(x,y) == reference.GetPixel(x,y)) continue;
                        Require(x >= 738 && x <= 774 && y >= 28 && y <= 68, "Solo el emoji ocupa la esquina");
                        changed++;
                    }
                    Require(changed > 300,"Emoji visible");
                }
            }
        }
    }

    static void TestHeldVocals()
    {
        var record = new Lyrics.Record { duration = 6, lyricsfile = "lines:\n  - text: 'you stay'\n    start_ms: 0\n    end_ms: 4000\n    words:\n      - { text: you, start_ms: 0, end_ms: 2400 }\n      - { text: stay, start_ms: 3000, end_ms: 3250 }" };
        var words = WordTimes.FromRecord(record);
        Require(Frame.At(words,.25).DisplayWord(0) == "you" && Frame.At(words,1.0).DisplayWord(0) == "youuuu", "Crecimiento progresivo de you");
        var held = Frame.At(words,2.25);
        Require(held.DisplayWord(0).Length > 12 && held.DisplayWord(0).TrimEnd('u') == "yo" && words[0].Text == "you", "Vocal larga sin modificar la letra original");
        held.Playing = false;
        Require(held.DisplayWord(0) == Frame.At(words,2.25).DisplayWord(0) && Frame.At(words,.1).DisplayWord(0) == "you", "Pausa y retroceso");
        Require(words[Frame.At(words,2.4).Index].Text == "" && Frame.At(words,3.20).DisplayWord(2) == "stay", "Silencio y palabra breve");
        var bounded = new LyricLine(0, "you", .3);
        Require(bounded.HeldText(1.9,2) == "you", "Respetar el final real antes del siguiente inicio");
        Require(new LyricLine(0,"¡SÍ!",3).HeldText(2,3).EndsWith("ÍÍ!") && new LyricLine(0,"love,",3).HeldText(2,3).EndsWith("oove,"), "Acento, mayúsculas, puntuación y e muda");
        var estimate = WordTimes.Estimate(Lyrics.Parse("[00:00]you\n[00:03]go"),4);
        Require(Frame.At(estimate,2).DisplayWord(0).Length > 4 && Frame.At(estimate,2).DisplayWord(1) == "go", "Duración por línea y palabra siguiente");
        using (var scene = new Scene())
        using (var bmp = new Bitmap(800,450))
        using (var g = Graphics.FromImage(bmp))
        using (var pale = new Bitmap(32,32))
        using (var paint = Graphics.FromImage(pale))
        {
            paint.Clear(Color.LightYellow); held.Artwork = pale;
            scene.Draw(g, bmp.Size, held, WallpaperStyle.White);
            Require(bmp.GetPixel(140,90).ToArgb() == Color.White.ToArgb(), "Fondo blanco con portada pálida");
            int dark = 0;
            for (int y=160; y<290; y+=2)
            for (int x=45; x<755; x+=2)
                if (bmp.GetPixel(x,y).GetBrightness() < .55f) dark++;
            Require(dark > 400, "Vocal legible sobre blanco");
            held.Artwork = null;
            scene.Draw(g, bmp.Size, held, WallpaperStyle.White);
            bmp.Save("BlancoPrueba.png",ImageFormat.Png);
        }
    }

    static void TestIcons()
    {
        Require(LyricIcons.Match("¿OJOS?")=="eye" && LyricIcons.Match("hands,")=="hands" &&
            LyricIcons.Match("corazón…")=="heart" && LyricIcons.Match("somewhere")==null,"Asociaciones de iconos");
        Require(LyricIcons.Match("¡LLÁMAME!") == LyricIcons.Match("calling") && LyricIcons.Match("¿Sueños?") == "💤" &&
            LyricIcons.Match("butterflies…") == "🦋" && LyricIcons.Match("mariposamente") == null,"Palabras nuevas en ambos idiomas");
        var shapes = new System.Collections.Generic.HashSet<string>();
        var samples = "show hands amor shine moon sol fire lluvia mar música mundo rayo tiempo smile flor people " +
            "desamor labios pensando sueño dime teléfono carta recuerdo oído voz auriculares guitarra tambor piano " +
            "casa puerta llave candado pasos correr carretera coche tren avión maleta ancla barco " +
            "nube viento hielo arcoíris montaña bosque hoja mariposa alas luz diamante reina anillo " +
            "máscara retrato espejo página espera imán dado fiesta discoteca copa dinero cubito brújula galaxia fantasma diablo ángel nada";
        foreach (var text in samples.Split(' '))
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddRectangle(new Rectangle(10,10,80,50));
            LyricIcons.Append(path,text);
            var b=path.GetBounds();
            Require(text=="nada" ? b.Right==90 : b.Right>100 && b.Bottom<=61,"Espacio reservado para "+text);
            Require(shapes.Add(String.Join(";", path.PathPoints.Select(pt => pt.ToString()))), "Silueta distinta para "+text);
        }
        var words=Lyrics.Parse("[00:00.00]mil\n[00:01.00]mariposas\n[00:02.00]vuelan");
        var live=Frame.At(words,1.35);
        live.Playing=true; live.Pulse=.45f;
        foreach (WallpaperStyle style in Enum.GetValues(typeof(WallpaperStyle)))
        using (var scene=new Scene())
        using (var bmp=new Bitmap(800,450))
        using (var g=Graphics.FromImage(bmp))
        {
            scene.Draw(g,bmp.Size,live,style);
            if (style==WallpaperStyle.Lens) bmp.Save("IconosPrueba.png",ImageFormat.Png);
        }
    }

    static void TestInstrumentalWave()
    {
        var song = new Track("Demo", "Autor", "Álbum", 40);
        var record = new Lyrics.Record { trackName = song.Title, artistName = song.Artist, duration = 40, instrumental = true };
        var instrumental = Lyrics.MatchTrack(new[] { record }, song);
        var wave = Frame.At(instrumental, 12);
        wave.Playing = true;
        var meter = new Onsets();
        for (int i = 0; i < 60; i++) meter.Update(.01f,.012);
        float quietLevel = meter.Level;
        for (int i = 0; i < 60; i++) meter.Update(.81f,.012);
        float loudLevel = meter.Level;
        Require(quietLevel > .09f && loudLevel > quietLevel * 8, "Nivel sostenido de Spotify");
        wave.Level = loudLevel;
        Require(wave.WaveOpacity == 1, "Pista instrumental confirmada");
        var cues = WordTimes.Estimate(Lyrics.Parse("[00:02]voz\n[00:04][Instrumental]\n[00:10]vuelve\n[00:11]♪ ♫\n[00:11.4]voz"), 14);
        foreach (double t in new[] { 1.0, 5.0, 2.0, 10.0, 11.2 })
        {
            var f = Frame.At(cues, t);
            f.Playing = true; f.Level = quietLevel;
            Require((f.WaveOpacity > 0) == (t == 1 || t == 5), "Intro, solo, regreso de la voz y respiración");
        }
        foreach (var unknown in new LyricLine[][] { null, new LyricLine[0] })
        {
            var f = Frame.At(unknown, 5);
            f.Playing = true; f.Level = quietLevel;
            Require(f.WaveOpacity == 0, "Disponibilidad de la letra desconocida");
        }
        for (int i = 0; i < 60; i++) meter.Update(0,.012);
        wave.Level = meter.Level;
        Require(wave.WaveOpacity == 0, "Silencio");
        wave.Level = loudLevel; wave.Playing = false;
        Require(wave.WaveOpacity == 0, "Pausa");
        using (var artwork = new Bitmap(32,32))
        using (var paint = Graphics.FromImage(artwork))
        {
            paint.Clear(Color.SteelBlue);
            wave.Artwork = artwork;
            foreach (WallpaperStyle style in Enum.GetValues(typeof(WallpaperStyle)))
            using (var scene = new Scene())
            using (var loud = new Bitmap(480,270))
            using (var quiet = new Bitmap(480,270))
            using (var paused = new Bitmap(480,270))
            {
                wave.Playing = true; wave.Level = loudLevel;
                using (var g = Graphics.FromImage(loud)) scene.Draw(g, loud.Size, wave, style);
                wave.Level = quietLevel;
                using (var g = Graphics.FromImage(quiet)) scene.Draw(g, quiet.Size, wave, style);
                wave.Playing = false;
                using (var g = Graphics.FromImage(paused)) scene.Draw(g, paused.Size, wave, style);
                int center = (int)(paused.Height * (style == WallpaperStyle.Boat ? .54f : .505f));
                int quietHeight = 0, loudHeight = 0;
                for (int y = 85; y < 175; y += 2)
                for (int x = 75; x < 325; x += 2)
                {
                    Color background = paused.GetPixel(x,y);
                    if (quiet.GetPixel(x,y) != background) quietHeight = Math.Max(quietHeight, Math.Abs(y-center));
                    if (loud.GetPixel(x,y) != background) loudHeight = Math.Max(loudHeight, Math.Abs(y-center));
                }
                Require(quietHeight > 0 && loudHeight > 2 * quietHeight, "Altura de onda según volumen en " + style);
            }
        }
    }

    static void TestVideoBackdrop()
    {
        var t = new Track("Canción", "Artista", "", 200);
        var item = new MusicVideos.Item { identifier = "ejemplo", title = "Artista - Canción (Official Music Video)" };
        Require(MusicVideos.Rank(item,t) > 0, "Coincidencia de videoclip");
        item.title += " (AI Cover)";
        Require(MusicVideos.Rank(item,t) == 0, "Rechazar otra versión");
        item.title = "Canción"; item.creator = new[] { "Otro" };
        Require(MusicVideos.Rank(item,t) == 0, "Rechazar artista incorrecto");
        using (var scene = new Scene())
        using (var film = new Bitmap(48,32))
        using (var art = new Bitmap(32,32))
        using (var ink = Graphics.FromImage(film))
        using (var a = Graphics.FromImage(art))
        {
            a.Clear(Color.Blue);
            var f = Frame.At(null, 1);
            f.Artwork = art; f.Video = film;
            ink.Clear(Color.Red);
            var size = new Size(100,80);
            Require(scene.Backdrop(size, WallpaperStyle.Video, f).GetPixel(50,40).R > 120,"Fotograma de fondo");
            ink.Clear(Color.Lime); f.VideoRevision++;
            Require(scene.Backdrop(size, WallpaperStyle.Video, f).GetPixel(50,40).G > 120,"Fotograma siguiente");
            f.Video = null;
            Require(scene.Backdrop(size, WallpaperStyle.Video, f).GetPixel(50,40).B > 100,"Portada al retirar el vídeo");
        }
    }

    static void TestVideoPlayback()
    {
        string file = MusicVideos.Find(new Track("Never Gonna Give You Up", "Rick Astley", "", 213), CancellationToken.None).GetAwaiter().GetResult();
        Require(file != null, "Búsqueda y descarga del videoclip de prueba");
        try
        {
            using (var player = new VideoBackdrop(file))
            {
                PumpVideo(player, 12, false, 2200);
                Require(player.Image != null && !player.Failed, "Decodificación de vídeo");
                using (var first = new Bitmap(player.Image, 48, 32))
                {
                    PumpVideo(player, 87, false, 1800);
                    using (var second = new Bitmap(player.Image, 48, 32))
                    {
                        int changed = 0;
                        for (int y = 0; y < 32; y++)
                        for (int x = 0; x < 48; x++)
                            if (first.GetPixel(x,y) != second.GetPixel(x,y)) changed++;
                        Require(changed > 480, "Salto a otro fotograma");
                    }
                }
                int stopped = player.Revision;
                PumpVideo(player, 87, false, 850);
                Require(stopped == player.Revision, "Imagen fija en pausa");
                PumpVideo(player, 87, true, 750);
                Require(player.Revision > stopped + 3,"Reanudación de fotogramas");
                Console.WriteLine("Clip real {0}x{1}. Búsqueda, decodificación, pausa, salto y reanudación aprobados.", player.Image.Width, player.Image.Height);
            }
            Require(!File.Exists(file), "Limpieza del videoclip temporal");
        }
        finally { MusicVideos.Discard(file); }
    }

    static void PumpVideo(VideoBackdrop video, double start, bool playing, int ms)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < ms)
        {
            Application.DoEvents();
            video.Sync(start + (playing ? timer.Elapsed.TotalSeconds : 0), playing, playing ? 1 : 0);
            Thread.Sleep(12);
        }
    }

    static void TestCatalog()
    {
        using (var client = new System.Net.Http.HttpClient())
        {
            string url="https://api.deezer.com/album/302127";
            string json=client.GetStringAsync(url).GetAwaiter().GetResult();
            var data = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(json);
            var artist = (System.Collections.Generic.Dictionary<string,object>)data["artist"];
            var demo = new Track("",(string)artist["name"],(string)data["title"],0);
            byte[] bytes=client.GetByteArrayAsync((string)data["cover_medium"]).GetAwaiter().GetResult();
            using (var memory=new MemoryStream(bytes))
            using (var thumb=new Bitmap(memory))
            using (var result=HDCover.Find(demo,thumb,CancellationToken.None).GetAwaiter().GetResult())
            {
                Require(result!=null && result.Width>thumb.Width,"Portada de catálogo de mayor resolución");
                Console.WriteLine("Caso de prueba {0}: {1} -> {2}",demo.Album,thumb.Size,result.Size);
            }
        }
    }

    static void TestArtwork(Frame frame)
    {
        using (var art = new Bitmap(100,100))
        using (var mirror = new Bitmap(100,100))
        using (var g = Graphics.FromImage(art))
        using (var other = Graphics.FromImage(mirror))
        using (var scene = new Scene())
        using (var output = new Bitmap(480,270))
        using (var canvas = Graphics.FromImage(output))
        {
            g.Clear(Color.Crimson); g.FillRectangle(Brushes.LightCyan,0,0,30,100);
            other.Clear(Color.Crimson); other.FillRectangle(Brushes.LightCyan,70,0,30,100);
            var palette = AlbumColors.From(art);
            Require(palette.Main.R > palette.Main.G+50, "Dominante de portada");
            Require(!HDCover.Matches(art,mirror) && HDCover.Matches(art,art),"Verificación de la edición");
            frame.Artwork=art;
            scene.Draw(canvas,output.Size,frame,WallpaperStyle.Lens);
            Color plain=output.GetPixel(60,140);
            frame.Artwork=mirror;
            scene.Draw(canvas,output.Size,frame,WallpaperStyle.Lens);
            Require(Math.Abs(plain.R-output.GetPixel(60,140).R)<3 && plain.R<50,"Lente sin imagen y con paleta");
            scene.Draw(canvas,output.Size,frame,WallpaperStyle.Cover);
            Require(output.GetPixel(60,140).R>100,"Cambio a portada visible");
            using (var detailed=ArtDetail.ForDisplay(art)) Require(detailed.Width==200,"Mejora local");
            frame.Artwork=null;
        }
    }

    static void Measure()
    {
        IntPtr layer = FindWindowEx(FindWindow("Progman",null), IntPtr.Zero, null,"Neon Lyrics · Fondo");
        Require(layer != IntPtr.Zero,"Fondo de escritorio para la medición");
        var playback = new Playback();
        playback.ReadAsync().GetAwaiter().GetResult();
        using (var rhythm = new Rhythm())
        {
            rhythm.Active = true;
            Thread.Sleep(400);
            long start = SendMessage(layer,0x8061,IntPtr.Zero,IntPtr.Zero).ToInt64();
            var clock = Stopwatch.StartNew();
            float peak = 0, low = 1;
            while(clock.ElapsedMilliseconds < 4500)
            {
                peak = Math.Max(peak, rhythm.Pulse);
                low = Math.Min(low, rhythm.Pulse);
                Thread.Sleep(13);
            }
            long end = SendMessage(layer,0x8061,IntPtr.Zero,IntPtr.Zero).ToInt64();
            Console.WriteLine("Spotify activo={0}; fotogramas={1}; fps dibujados={2:F1}; pulso de Spotify={3:F3}..{4:F3}",
                playback.Playing,end-start,(end-start)/clock.Elapsed.TotalSeconds,low,peak);
        }
    }
    [DllImport("user32")] static extern IntPtr SendMessage(IntPtr w,uint message,IntPtr wp,IntPtr lp);

    static void CheckExecutable()
    {
        var exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neon Lyrics.exe");
        using (var app = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden }))
        {
            uint thread = 0;
            try
            {
                var wait = Stopwatch.StartNew();
                var progman = FindWindow("Progman", null);
                while (wait.ElapsedMilliseconds < 7000 && !app.HasExited)
                {
                    IntPtr layer = FindWindowEx(progman, IntPtr.Zero, null, "Neon Lyrics · Fondo");
                    if (layer != IntPtr.Zero && IsWindowVisible(layer))
                    {
                        uint owner;
                        uint layerThread = GetWindowThreadProcessId(layer, out owner);
                        if (owner == app.Id)
                        {
                            thread = layerThread;
                            var icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                            Require(icons != IntPtr.Zero && GetWindow(icons, 2) == layer, "Fondo detrás de los iconos");
                            break;
                        }
                    }
                    Thread.Sleep(120);
                }
                Require(thread != 0, "Inicio del ejecutable");
                Console.WriteLine("El ejecutable abrió un fondo visible detrás de los iconos.");
            }
            finally
            {
                if (thread != 0) PostThreadMessage(thread, 0x0012, IntPtr.Zero, IntPtr.Zero);
                if (!app.WaitForExit(3000)) app.Kill();
            }
            Require(app.ExitCode == 0, "Salida limpia del ejecutable");
        }
    }

    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string name, string caption);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string caption);
    [DllImport("user32")] static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32")] static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);

    static void Require(bool condition, string test)
    {
        if (!condition) throw new InvalidOperationException(test);
    }
}











