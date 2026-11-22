using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

enum WallpaperStyle { Black, Boat, Cover, Lens, Video, White }

sealed class Frame
{
    public Bitmap Artwork, Video;
    public int VideoRevision;
    public LyricLine[] Words;
    public int Index;
    public float SlideOffset, Pulse, Level, Accent;
    public bool Playing;
    public double Time;
    public Expression Mood;
    public double ReactionAge;

    public bool PhoneRing(int at)
    {
        var word = Words[at];
        if (word.Rings == 0) return false;
        if (word.Rings == 2) return true;
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int other = at + direction;
            while (other >= 0 && other < Words.Length && Words[other].Text.Length == 0) other += direction;
            if (other >= 0 && other < Words.Length && Math.Abs(Words[other].Seconds-word.Seconds) <= 1.8 &&
                (Words[other].Rings > 0 || Words[other].Icon == "📞")) return true;
        }
        return false;
    }

    public string DisplayWord(int at)
    {
        var word = Words[at];
        if (at > Index || word.Text.Length == 0) return word.Text;
        double end = at + 1 < Words.Length ? Words[at + 1].Seconds : Double.PositiveInfinity;
        return word.HeldText(Time, end);
    }

    public float WaveOpacity
    {
        get
        {
            if (!Playing || Level <= .004f || Words == null || Words.Length == 0 || Index >= 0 && Words[Index].Text.Length != 0) return 0;
            double start = Index < 0 ? 0 : Words[Index].Seconds;
            double end = Index + 1 < Words.Length ? Words[Index + 1].Seconds : Double.PositiveInfinity;
            if (end - start < .65) return 0; // Skip breaths between words.
            double fade = Math.Min(1, Math.Min(Time - start, end - Time) / .18);
            return (float)Math.Max(0, fade) * Math.Min(1, (Level - .004f) * 32);
        }
    }

    public static Frame At(LyricLine[] words, double time)
    {
        int i = words == null ? -1 : Lyrics.At(words, time);
        float slide = 0, accent = 0;
        if (i >= 0)
        {
            double age = Math.Max(0, time - words[i].Seconds);
            accent = (float)(Math.Exp(-age / .115) * (1 - Math.Exp(-age / .018)));
            if (i > 0 && words[i - 1].Text.Length != 0)
            {
                double transition = i + 1 == words.Length ? .10 : Math.Min(.10, (words[i + 1].Seconds - words[i].Seconds) * .45);
                double t = transition > 0 ? Math.Min(1, age / transition) : 1;
                slide = (float)(1 - t*t*(3 - 2*t));
            }
        }
        return new Frame { Words = words, Index = i, Time = time, SlideOffset = slide, Accent = accent };
    }
}

sealed class Scene : IDisposable
{
    static readonly int[] Slots = { -2, 2, -1, 1, 0 };
    readonly System.Collections.Generic.Dictionary<string, GraphicsPath> glyphs = new System.Collections.Generic.Dictionary<string, GraphicsPath>();
    readonly System.Collections.Generic.Dictionary<string, GraphicsPath> objects = new System.Collections.Generic.Dictionary<string, GraphicsPath>();
    Bitmap sky;
    WallpaperStyle skyStyle;
    Bitmap sourceArt, sourceVideo;
    int videoRevision;
    AlbumColors colors;
    readonly FontFamily type = new FontFamily("Arial Black");
    readonly PointF[] wave = new PointF[129];
    static readonly Color Green = Color.FromArgb(181, 255, 10), Pink = Color.FromArgb(255, 28, 184),
        Blue = Color.FromArgb(78, 171, 237), Yellow = Color.FromArgb(255, 208, 19);
    Color WhiteInk { get { return colors == null ? Color.FromArgb(146, 18, 90) : colors.OnWhite; } }
    Color WhiteAccent { get { return colors == null ? Color.FromArgb(69, 51, 98) : colors.AccentOnWhite; } }

    internal Bitmap Backdrop(Size size, WallpaperStyle style, Frame frame)
    {
        Bitmap artwork = frame.Artwork, video = style == WallpaperStyle.Video ? frame.Video : null;
        bool rebuild = sky == null || skyStyle != style || sky.Size != size || sourceArt != artwork || sourceVideo != video;
        if (rebuild)
        {
            if (sky != null) sky.Dispose();
            if (artwork != sourceArt) colors = AlbumColors.From(artwork);
            if (video != null) sky = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            else if ((style == WallpaperStyle.Cover || style == WallpaperStyle.Video) && artwork != null) sky = CoverBackdrop(size, artwork);
            else sky = Background(size, style);
            skyStyle = style;
            sourceArt = artwork;
            sourceVideo = video;
        }
        if (video != null && (rebuild || videoRevision != frame.VideoRevision))
        using (var g = Graphics.FromImage(sky))
        using (var shade = new SolidBrush(Color.FromArgb(85,0,0,0)))
        {
            float zoom = Math.Max((float)size.Width / video.Width, (float)size.Height / video.Height);
            float w = video.Width * zoom, h = video.Height * zoom;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(video, new RectangleF((size.Width-w)/2, (size.Height-h)/2, w, h));
            g.FillRectangle(shade, 0, 0, size.Width, size.Height);
            videoRevision = frame.VideoRevision;
        }
        return sky;
    }

    public void Draw(Graphics g, Size size, Frame frame, WallpaperStyle style)
    {
        g.DrawImageUnscaled(Backdrop(size,style,frame),0,0);
        DrawMotion(g,size,frame,style);
    }

    internal void DrawMotion(Graphics g, Size size, Frame frame, WallpaperStyle style)
    {
        bool boat = style == WallpaperStyle.Boat;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (boat)
        {
            Ocean(g, size, frame.Time);
            Sailboat(g, size, frame.Time);
        }
        EdgeLight(g, size, frame, boat);
        if (frame.Mood != null) DrawMoodEmoji(g,size,frame);
        if (frame.Words == null || frame.Index < 0 || frame.Words[frame.Index].Text.Length == 0)
        {
            float opacity = frame.WaveOpacity;
            if (opacity > 0) DrawWave(g, size, frame, style, opacity);
            return;
        }
        // The large face is drawn last as the neighboring words roll past it.
        foreach (int slot in Slots)
        {
            int at = frame.Index + slot;
            float distance = slot + frame.SlideOffset;
            if (at < 0 || at >= frame.Words.Length || Math.Abs(distance) >= 1.95f) continue;
            string word = frame.Words[at].Text;
            if (String.IsNullOrWhiteSpace(word)) continue;
            var glyph = Glyph(frame.DisplayWord(at));
            bool ring = frame.PhoneRing(at);
            string icon = ring ? "📞" : frame.Words[at].Icon;
            using (var illustrated = icon == null ? null : (GraphicsPath)glyph.Clone())
            {
                if (illustrated != null)
                {
                    double age = frame.Time-frame.Words[at].Seconds;
                    bool moving = slot == 0 || slot == -1 && frame.Time-frame.Words[frame.Index].Seconds < .45;
                    WordMotion.Append(illustrated, ObjectShape(icon), icon, moving ? age : -1, ring);
                }
                var shape = illustrated ?? glyph;
                if (style == WallpaperStyle.Cover || style == WallpaperStyle.Lens || style == WallpaperStyle.Video)
                    LensWord(g, size, shape, distance, frame);
                else Word(g, size, shape, word, at, distance, boat, frame);
            }
        }
    }

    void DrawMoodEmoji(Graphics g, Size size, Frame frame)
    {
        double age = frame.ReactionAge;
        float fade = (float)Math.Max(0, Math.Min(1, Math.Min(age/.20, (SongReactions.VisibleSeconds-age)/.32)));
        if (fade <= 0) return;
        float scale = Math.Min(size.Height/680f, size.Width/400f);
        float centerX = size.Width*.965f - 24*scale;
        float bob = 2*(float)Math.Sin(age*4) - 2.8f*frame.Pulse;
        float centerY = size.Height*.073f + (24+bob+(1-fade)*7)*scale;
        int alpha = (int)(255*fade);
        var save = g.Save();
        try
        {
            g.TranslateTransform(centerX,centerY); g.ScaleTransform(scale,scale);
            using (var faceColor = new SolidBrush(Color.FromArgb(alpha,frame.Mood.Color)))
            using (var outline = new SolidBrush(Color.FromArgb(alpha,44,32,43)))
            {
                g.RotateTransform(-6 + 4*(float)Math.Sin(age*3));
                g.FillEllipse(outline,-24,-24,48,48); g.FillEllipse(faceColor,-22,-22,44,44);
                var face = ObjectShape(frame.Mood.Face);
                var b = face.GetBounds();
                if (b.Width > 0 && b.Height > 0)
                {
                    float z = 34/Math.Max(b.Width,b.Height);
                    g.ScaleTransform(z,z); g.TranslateTransform(-b.X-b.Width/2,-b.Y-b.Height/2);
                    g.FillPath(outline,face);
                }
            }
        }
        finally { g.Restore(save); }
    }

    void DrawWave(Graphics g, Size size, Frame frame, WallpaperStyle style, float opacity)
    {
        bool boat = style == WallpaperStyle.Boat, cover = style == WallpaperStyle.Video || style == WallpaperStyle.Cover && frame.Artwork != null;
        Color ink = colors == null ? (boat ? Green : Pink) : cover ? colors.OnCover : colors.Main;
        Color accent = colors == null ? (boat ? Blue : Yellow) : cover ? colors.OnCoverLight : colors.Light;
        if (style == WallpaperStyle.White) { ink = WhiteInk; accent = WhiteAccent; }
        float h = Math.Min(size.Height, size.Width * 1.2f);
        float centerX = size.Width * (boat ? .30f : .50f);
        float centerY = size.Height * (boat ? .54f : .505f);
        float halfWidth = Math.Min(h * .43f, size.Width * (boat ? .235f : .38f));
        float height = h * .112f * frame.Level;
        for (int i = 0; i < wave.Length; i++)
        {
            float x = 2f * i / (wave.Length - 1) - 1;
            double phase = x * Math.PI * 3 - frame.Time * 5;
            double crest = .78 * Math.Sin(phase) + .22 * Math.Sin(2 * phase + x);
            wave[i] = new PointF(centerX + halfWidth * x, centerY + height * (1 - x*x) * (float)crest);
        }
        float stroke = Math.Max(1.6f, h * .0035f);
        using (var gradient = new LinearGradientBrush(wave[0], wave[wave.Length - 1],
            Color.FromArgb((int)(255 * opacity), ink), Color.FromArgb((int)(255 * opacity), accent)))
        using (var halo = new Pen(Color.FromArgb((int)(65 * opacity), ink), stroke * 3.6f) { LineJoin = LineJoin.Round })
        using (var line = new Pen(gradient, stroke) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLines(halo, wave);
            g.DrawLines(line, wave);
        }
    }

    void Word(Graphics g, Size size, GraphicsPath path, string text, int index, float distance, bool boat, Frame frame)
    {
        float w = size.Width, h = size.Height;
        float abs = Math.Abs(distance);
        float scale = 1 - .64f * Math.Min(1, abs);
        if (abs > 1) scale *= Math.Max(.05f, 1 - (abs - 1));
        float x = w * (boat ? .30f : .50f);
        float y = h * ((boat ? .54f : .505f) + distance * (boat ? .205f : .238f));
        float swing = (float)Math.Sin(Math.Min(1, Math.Abs(abs - Math.Round(abs))) * Math.PI);
        float turn = distance * (boat ? 2 + 5 * swing : 2 + 36 * swing);
        if (!boat) x += (float)Math.Sin(distance * Math.PI) * h * .12f;

        float focusWeight = Math.Max(0, 1 - abs);
        float hit = frame.Playing ? frame.Pulse : 0;
        float punch = hit * .95f + frame.Accent * .66f;
        float direction = (index & 1) == 0 ? 1 : -1;
        scale *= 1 + focusWeight * punch * .145f;
        y -= h * focusWeight * punch * .027f;
        x += direction * h * (.009f * hit + .012f * frame.Accent) * focusWeight;
        turn += direction * focusWeight * (hit * 4.6f + frame.Accent * 3.2f);

        {
            var box = path.GetBounds();
            if (box.Width < 1 || box.Height < 1) return;
            float factor = Math.Min(h * (boat ? .18f : .233f) * scale / 100, w * (boat ? .54f : .89f) / box.Width);
            var save = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(turn);
                g.ScaleTransform(factor, factor);
                g.TranslateTransform(-box.X - box.Width / 2, -box.Y - box.Height / 2);
                bool tag = text.EndsWith(",") && index % 3 != 1 || index % 11 == 2;
                float focus = 1 - Math.Min(1, abs);
                Color ink = Mix(colors == null ? (boat ? Blue : Yellow) : colors.Light, colors == null ? (boat ? Green : Pink) : colors.Main, tag ? Math.Max(.82f, focus) : focus);
                if (skyStyle == WallpaperStyle.White) ink = Mix(WhiteAccent, WhiteInk, focus);
                int opacity = (int)(255 * Math.Min(1, (1.95f - abs) * 1.5f));
                if (tag)
                {
                    var plate = RectangleF.Inflate(box, 11, 13);
                    var plateColor = skyStyle == WallpaperStyle.White ? Mix(Color.White, WhiteInk, .07f) :
                        colors == null ? (boat ? Color.FromArgb(237, 31, 209) : Color.FromArgb(245, 13, 39)) : colors.Dark;
                    if (skyStyle == WallpaperStyle.White) ink = Mix(ink, Color.Black, .10f);
                    using (var tile = Rounded(plate, 6))
                    using (var fill = new SolidBrush(Color.FromArgb(opacity, plateColor)))
                        g.FillPath(fill, tile);
                }
                if (focus > .2f)
                {
                    var rim = colors != null ? colors.Accent : boat ? Color.FromArgb(244, 57, 212) : Color.FromArgb(255, 121, 205);
                    if (skyStyle == WallpaperStyle.White) rim = WhiteAccent;
                    using (var stroke = new Pen(Color.FromArgb((int)((75 + 105 * hit) * focus), rim), 6 + hit * 5) { LineJoin = LineJoin.Round })
                        g.DrawPath(stroke, path);
                }
                using (var brush = new SolidBrush(Color.FromArgb(opacity, ink)))
                using (var thick = new Pen(brush, 3.6f) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(thick, path);
                    g.FillPath(brush, path);
                }
            }
            finally { g.Restore(save); }
        }
    }

    GraphicsPath ObjectShape(string name)
    {
        GraphicsPath shape;
        if (!objects.TryGetValue(name,out shape))
        {
            shape = new GraphicsPath();
            LyricIcons.Draw(shape,name);
            objects.Add(name,shape);
        }
        return shape;
    }

    GraphicsPath Glyph(string display)
    {
        GraphicsPath path;
        if (!glyphs.TryGetValue(display, out path))
        {
            if (glyphs.Count >= 192) ClearGlyphs();
            path = new GraphicsPath();
            path.AddString(display, type, (int)FontStyle.Regular, 130, PointF.Empty, StringFormat.GenericTypographic);
            glyphs.Add(display, path);
        }
        return path;
    }

    void EdgeLight(Graphics g, Size s, Frame f, bool boat)
    {
        float h = s.Height, w = s.Width, unit = Math.Max(1, h / 800f);
        float hit = f.Playing ? f.Pulse : 0;
        var color = colors != null ? colors.Main : boat ? Color.FromArgb(250, 70, 239) : Color.FromArgb(255, 46, 145);
        if (skyStyle == WallpaperStyle.White) color = WhiteInk;
        float inset = 1.7f * unit;
        using (var glow = new Pen(Color.FromArgb((int)(14 + 20*hit),color), unit*(7+8*hit)))
            g.DrawRectangle(glow, inset, inset, w-2*inset, h-2*inset);
        using (var rim = new Pen(Color.FromArgb((int)(62+107*hit),color), unit*(1.2f+2.2f*hit)))
            g.DrawRectangle(rim, inset, inset, w-2*inset, h-2*inset);

        float sweep = (float)(.5 + .42 * Math.Sin(f.Time*1.52));
        float vertical = (float)(.5 + .40 * Math.Sin(f.Time*1.52 + 1.5));
        var hot = colors != null ? colors.Light : boat ? Color.FromArgb(125, 246, 253) : Color.FromArgb(255, 184, 151);
        if (skyStyle == WallpaperStyle.White) hot = WhiteAccent;
        EdgeStroke(g, w*sweep, inset, w*.145f, 0, unit, hit, hot);
        EdgeStroke(g, w*(1-sweep), h-inset, w*.145f, 0, unit, hit, hot);
        EdgeStroke(g, inset, h*vertical, 0, h*.19f, unit, hit, hot);
        EdgeStroke(g, w-inset, h*(1-vertical), 0, h*.19f, unit, hit, hot);
    }

    static void EdgeStroke(Graphics g, float x, float y, float dx, float dy, float unit, float hit, Color hot)
    {
        var start = new PointF(x-dx,y-dy);
        var end = new PointF(x+dx,y+dy);
        using (var brush = new LinearGradientBrush(start,end,Color.Transparent,hot))
        {
            brush.InterpolationColors = new ColorBlend { Positions = new[]{0f,.5f,1f},
                Colors = new[]{Color.FromArgb(0,hot), Color.FromArgb((int)(120+110*hit),hot),Color.FromArgb(0,hot)} };
            using (var pen = new Pen(brush,unit*(2.1f+3*hit))) g.DrawLine(pen,start,end);
        }
    }

    void ClearGlyphs()
    {
        foreach (var glyph in glyphs.Values) glyph.Dispose();
        glyphs.Clear();
    }

    static GraphicsPath Rounded(RectangleF b, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(b.Left,b.Top,d,d,180,90); p.AddArc(b.Right-d,b.Top,d,d,270,90);
        p.AddArc(b.Right-d,b.Bottom-d,d,d,0,90); p.AddArc(b.Left,b.Bottom-d,d,d,90,90);
        p.CloseFigure(); return p;
    }

    static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R+(b.R-a.R)*t), (int)(a.G+(b.G-a.G)*t), (int)(a.B+(b.B-a.B)*t));
    }

    static Bitmap CoverBackdrop(Size size, Bitmap artwork)
    {
        var result = new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(result))
        {
            using (var detailed = ArtDetail.ForDisplay(artwork))
            {
            float zoom = Math.Max((float)size.Width/detailed.Width,(float)size.Height/detailed.Height);
            float width = detailed.Width*zoom, height = detailed.Height*zoom;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(detailed,new RectangleF((size.Width-width)/2,(size.Height-height)/2,width,height));
            }
            using (var shade = new LinearGradientBrush(new Point(0,0),new Point(0,size.Height),
                Color.FromArgb(42,0,0,0),Color.FromArgb(86,0,0,0)))
                g.FillRectangle(shade,0,0,size.Width,size.Height);
        }
        return result;
    }

    internal static PointF LensPoint(float x, float y, Size size, float strength)
    {
        float h = size.Height;
        float dx = x / Math.Min(size.Width*.5f,h*.78f), dy = y / (h*.55f);
        float magnification = 1 + strength/(1+2.7f*(dx*dx+dy*dy));
        return new PointF(size.Width*.5f + x*magnification,h*.508f + y*magnification);
    }

    void LensWord(Graphics g, Size size, GraphicsPath glyph, float distance, Frame frame)
    {
        float radius = Math.Abs(distance), focus = Math.Max(0,1-radius);
        float pulse = frame.Playing ? frame.Pulse : 0;
        float h = size.Height;
        using (var shape = (GraphicsPath)glyph.Clone())
        {
            var box = shape.GetBounds();
            if (box.Width < 1 || box.Height < 1) return;
            shape.Flatten(null,.8f);
            var points = shape.PathPoints;
            float scale = Math.Min(h*.00165f,size.Width*.56f/box.Width)*(1-.42f*Math.Min(radius,1.65f));
            float strength = .70f + pulse*.16f;
            float midX = box.X+box.Width/2, midY = box.Y+box.Height/2;
            for (int j=0; j<points.Length; j++)
            {
                float x = (points[j].X-midX)*scale*(1+focus*pulse*.071f);
                float y = (points[j].Y-midY)*scale*(1-.18f*Math.Min(1,radius)) + distance*h*.20f;
                points[j] = LensPoint(x,y-h*.009f*pulse*focus,size,strength);
            }
            float fade = Math.Min(1,Math.Max(0,1.96f-radius));
            bool imageBehind = skyStyle == WallpaperStyle.Video || skyStyle == WallpaperStyle.Cover && sourceArt != null;
            var ink = Mix(colors == null ? Color.FromArgb(255,221,236) : imageBehind ? colors.OnCoverLight : colors.Light,
                colors == null ? Color.FromArgb(255,37,147) : imageBehind ? colors.OnCover : colors.Main, focus);
            using (var warped = new GraphicsPath(points,shape.PathTypes,shape.FillMode))
            using (var shadow = new Pen(Color.FromArgb((int)(190*fade),colors == null ? Color.FromArgb(31,6,30) : colors.Dark),Math.Max(2,h*.006f)) { LineJoin=LineJoin.Round })
            using (var rim = new Pen(Color.FromArgb((int)(195*fade),colors == null ? Color.FromArgb(255,134,201) : colors.Accent),Math.Max(1,h*(.0014f+.0018f*pulse))) { LineJoin=LineJoin.Round })
            using (var fill = new SolidBrush(Color.FromArgb((int)(255*fade),ink)))
            {
                g.DrawPath(shadow,warped);
                g.DrawPath(rim,warped);
                g.FillPath(fill,warped);
            }
        }
    }

    Bitmap Background(Size size, WallpaperStyle style)
    {
        var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        if (style == WallpaperStyle.White)
        {
            using (var g = Graphics.FromImage(result)) g.Clear(Color.White);
            return result;
        }
        bool boat = style == WallpaperStyle.Boat;
        using (var small = new Bitmap(256, 160))
        using (var graphic = Graphics.FromImage(result))
        {
            for (int j = 0; j < small.Height; j++)
            for (int i = 0; i < small.Width; i++)
            {
                double x = i / 255.0, y = j / 159.0;
                double top = Math.Exp(-y*12) * (.29 + .6*Math.Pow(Math.Cos((x-.52)*5), 6));
                double bottom = Math.Exp(-(1-y)*11) * (.37 + .58*Math.Pow(Math.Cos((x-.27)*5), 4));
                double side = Math.Exp(-Math.Min(x,1-x)*23) * (.25 + .42*Math.Pow(Math.Cos(y*6), 2));
                double band = Math.Min(1, top+bottom+side);
                int red = (int)(9 + (boat ? 181 : 195)*band);
                int green = (int)(8 + (boat ? 22*band : 98*top*(1-x)));
                int blue = (int)(11 + (boat ? 158*band : 20*bottom + 140*(side*x + bottom*x*x)));
                var tint = colors == null ? Color.FromArgb(red,green,Math.Min(255,blue))
                    : AlbumColors.Blend(Color.FromArgb(8,8,10), AlbumColors.Blend(colors.Main,colors.Accent,Math.Min(1,top/(band+.001))*.6+.17), band*.76);
                small.SetPixel(i,j,tint);
            }
            graphic.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphic.Clear(Color.FromArgb(8,8,10));
            graphic.PixelOffsetMode = PixelOffsetMode.Half;
            using (var edges = new ImageAttributes())
            {
                edges.SetWrapMode(WrapMode.TileFlipXY);
                graphic.DrawImage(small,new Rectangle(Point.Empty,size),0,0,small.Width,small.Height,GraphicsUnit.Pixel,edges);
            }
        }
        return result;
    }

    static void Ocean(Graphics g, Size s, double time)
    {
        float w=s.Width, h=s.Height;
        const int rows=50;
        int cols=(int)(w/h*36)+3;
        var grid=new PointF[rows][];
        for (int row=0; row<rows; row++)
        {
            double z=17.0 - .32*row;
            grid[row]=new PointF[2*cols+1];
            for (int c=-cols; c<=cols; c++)
            {
                double x=c*.245;
                double swell=.18*Math.Sin(x*.91+z*.84+time*1.08) + .13*Math.Cos(x*.37-z*1.08-time*.73)
                    + .075*Math.Sin(x*1.42+z*.31-time*.98);
                grid[row][c+cols]=new PointF((float)(w*.5+h*x/z), (float)(h*(.422+(1.22+swell)/z)));
            }
        }
        // A continuous spectrum follows each row; the closer mesh softens into pale wire.
        using (var brush=new LinearGradientBrush(new PointF(-w*.16f,0),new PointF(w*1.16f,0),Color.White,Color.White))
        {
            brush.InterpolationColors = new ColorBlend { Positions=new[]{0f,.16f,.29f,.43f,.56f,.72f,.87f,1f},
                Colors=new[]{Pink,Color.FromArgb(130,92,255),Yellow,Green,Color.Aquamarine,Color.Cyan,Pink,Color.Gold} };
            using (var pen=new Pen(brush,Math.Max(.65f,h*.00115f)))
            {
                for (int row=0;row<rows;row++) g.DrawLines(pen,grid[row]);
            }
        }
        using (var tint=new LinearGradientBrush(new PointF(0,h*.46f),new PointF(0,h*1.1f),
            Color.FromArgb(118,128,248,228),Color.FromArgb(215,250,215,244)))
        using (var pen=new Pen(tint,Math.Max(.65f,h*.0011f)))
        {
            var column = new PointF[rows];
            for (int c=0;c<grid[0].Length;c++)
            {
                for (int row=0;row<rows;row++) column[row]=grid[row][c];
                g.DrawLines(pen,column);
            }
        }
    }

    static void Sailboat(Graphics g, Size s, double t)
    {
        float unit=s.Height*.355f;
        double yaw=.19+.34*Math.Sin(t*.32), roll=.035*Math.Sin(t*1.04);
        float cx=s.Width*.52f, cy=(float)(s.Height*(.565+.009*Math.Sin(t*1.08)));
        Func<double,double,double,PointF> project=(x,y,z) => {
            double side=x*Math.Cos(yaw)+z*Math.Sin(yaw);
            double depth=z*Math.Cos(yaw)-x*Math.Sin(yaw);
            return new PointF(cx+(float)(side*Math.Cos(roll)-y*Math.Sin(roll))*unit,
                cy+(float)(-y*Math.Cos(roll)-side*Math.Sin(roll)+depth*.22)*unit);
        };
        Func<double,double,double,PointF> hull=(u,level,side) => {
            double x=u*(.49+.034*level);
            double z=side*.151*Math.Sqrt(Math.Max(0,1-u*u))*(.51+.49*level);
            double y=-.107+.075*u*u+.125*level+.045*u*u*level;
            return project(x,y,z);
        };
        using (var dark=new SolidBrush(Color.FromArgb(39,25,29)))
        using (var wire=new Pen(Color.FromArgb(249,224,199),Math.Max(.85f,s.Height*.00155f)))
        using (var fine=new Pen(Color.FromArgb(229,190,159),Math.Max(.70f,s.Height*.00115f)))
        {
            var rim=new PointF[34];
            for(int i=0;i<17;i++) { double u=(i-8)/8.0; rim[i]=hull(u,1,-1); rim[33-i]=hull(u,1,1); }
            g.FillPolygon(dark,rim); g.DrawPolygon(wire,rim);
            for(int i=1;i<16;i++) g.DrawLine(fine,rim[i],rim[33-i]);
            for(int i=0;i<16;i++)
            {
                double a=(i-8)/8.0,b=a+.125;
                var cell=new[]{hull(a,1,1),hull(b,1,1),hull(b,0,1),hull(a,0,1)};
                g.FillPolygon(dark,cell); g.DrawPolygon(fine,cell);
                g.DrawLine(fine,hull(a,.52,1),hull(b,.52,1));
            }
            var foot=project(.015,.015,0); var mast=project(.015,.70,0);
            g.DrawLine(wire,foot,mast);
            g.DrawLine(wire,hull(-1,1,1),mast);
            g.DrawLine(fine,hull(-.68,1,1),project(.015,.54,0));
            g.DrawLine(fine,hull(1,1,-1),project(.28,.39,0));
            Func<double,double,PointF> sail=(u,v) => project(.02+u*(.125+.285*v)+.036*Math.Sin(v*Math.PI)*u,
                .077+.611*v+.038*u-.076*u*v,.073*Math.Sin(u*Math.PI)*Math.Sin(v*Math.PI));
            var outline=new[]{sail(0,0),sail(0,1),sail(1,1),sail(1,.5),sail(1,0)};
            g.FillPolygon(dark,outline); g.DrawPolygon(wire,outline);
            for(int i=0;i<=9;i++)
            {
                double f=i/9.0;
                var up=new PointF[9]; var across=new PointF[9];
                for(int j=0;j<9;j++){ up[j]=sail(f,j/8.0); across[j]=sail(j/8.0,f); }
                g.DrawLines(fine,up); g.DrawLines(fine,across);
            }
        }
    }

    public void Dispose() { ClearGlyphs(); foreach(var shape in objects.Values) shape.Dispose(); type.Dispose(); if(sky!=null) sky.Dispose(); }
}










