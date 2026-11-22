using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

sealed class AlbumColors
{
    public readonly Color Main, Accent, Light, Dark, OnCover, OnCoverLight, OnWhite, AccentOnWhite;

    AlbumColors(Color main, Color accent, Color background)
    {
        Main = Lift(main);
        Accent = Lift(accent);
        Light = Blend(Accent, Color.White, .52);
        Dark = Blend(main, Color.Black, .83);
        OnCover = Readable(Main,accent,background);
        OnCoverLight = Readable(Light,main,background);
        OnWhite = Readable(main, main, Color.White);
        AccentOnWhite = Readable(accent, accent, Color.White);
    }

    static double Linear(byte v)
    {
        double s=v/255.0;
        return s<=.04045 ? s/12.92 : Math.Pow((s+.055)/1.055,2.4);
    }

    static double Luma(Color c) { return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B); }
    static double Contrast(double a,double b) { return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05); }
    static Color Readable(Color preferred, Color alternative, Color bg)
    {
        double back=Luma(bg);
        if (Contrast(Luma(preferred),back)>=4.5) return preferred;
        Color end=Contrast(0,back)>Contrast(1,back) ? Color.Black : Color.White;
        Color start=Contrast(Luma(alternative),back)>Contrast(Luma(preferred),back) ? alternative : preferred;
        for(int i=0;i<=10;i++)
        {
            Color ink=Blend(start,end,i/10.0);
            if (Contrast(Luma(ink),back)>=4.5) return ink;
        }
        return end;
    }

    static Color Lift(Color c)
    {
        double brightness = .24*c.R + .62*c.G + .14*c.B;
        return Blend(c, Color.White, Math.Max(0,(125-brightness)/(255-brightness+1)));
    }

    internal static Color Blend(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
    }

    public static AlbumColors From(Bitmap art)
    {
        if (art == null) return null;
        // Group close hues so that texture within a dominant color counts together.
        var count = new int[29];
        var red = new int[29];
        var green = new int[29];
        var blue = new int[29];
        var weight = new double[29];
        int middleR=0,middleG=0,middleB=0, samples=0;
        using (var sample = ArtDetail.Sample(art,new Size(40,40)))
        for (int y=0;y<40;y++)
        for (int x=0;x<40;x++)
        {
            Color c = sample.GetPixel(x,y);
            if (c.A < 128) continue;
            if(x>=10 && x<30 && y>=14 && y<26)
            { middleR+=c.R; middleG+=c.G; middleB+=c.B; samples++; }
            int max = Math.Max(c.R,Math.Max(c.G,c.B)), min = Math.Min(c.R,Math.Min(c.G,c.B));
            int chroma = max-min;
            int bin = chroma < 24 ? 24 + max/52 : (int)((c.GetHue()+7.5)/15)%24;
            count[bin]++; red[bin]+=c.R; green[bin]+=c.G; blue[bin]+=c.B;
            weight[bin] += chroma < 24 ? .30 : .75+chroma/90.0;
        }
        int first = Array.IndexOf(weight,Max(weight));
        if (count[first] == 0) return null;
        Color main = Color.FromArgb(red[first]/count[first],green[first]/count[first],blue[first]/count[first]);
        int second = first;
        double best = 0;
        for (int i=0;i<count.Length;i++)
        {
            if (i==first || count[i]<16) continue;
            int dr=red[i]/count[i]-main.R, dg=green[i]/count[i]-main.G, db=blue[i]/count[i]-main.B;
            double score = weight[i]*(dr*dr+dg*dg+db*db);
            if (score>best) { best=score; second=i; }
        }
        var accent = second==first ? Blend(main,Color.White,.34) : Color.FromArgb(red[second]/count[second],green[second]/count[second],blue[second]/count[second]);
        var middle = samples==0 ? main : Color.FromArgb(middleR/samples,middleG/samples,middleB/samples);
        return new AlbumColors(main,accent,Blend(middle,Color.Black,.25));
    }

    static double Max(double[] values)
    {
        double max=0;
        foreach (double v in values) max=Math.Max(max,v);
        return max;
    }
}

static class ArtDetail
{
    internal static Bitmap Sample(Bitmap source, Size size)
    {
        var result=new Bitmap(size.Width,size.Height);
        using (var g=Graphics.FromImage(result))
        {
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(source,new Rectangle(Point.Empty,size),0,0,source.Width,source.Height,GraphicsUnit.Pixel);
        }
        return result;
    }

    // A bounded luma-only unsharp mask restores edge definition after bicubic resizing.
    public static Bitmap ForDisplay(Bitmap source)
    {
        double scale = Math.Min(2, 1500.0/Math.Max(source.Width,source.Height));
        if (scale <= 1) return new Bitmap(source);
        int w = Math.Max(1,(int)(source.Width*scale)), h = Math.Max(1,(int)(source.Height*scale));
        var result = new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        using (var attr = new ImageAttributes())
        {
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.Half;
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(source,new Rectangle(0,0,w,h),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attr);
        }
        var data = result.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try
        {
            var bytes=new byte[data.Stride*h];
            Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
            var lum=new byte[w*h];
            for(int y=0;y<h;y++)
            for(int x=0;x<w;x++)
            {
                int k=y*data.Stride+4*x;
                lum[y*w+x]=(byte)((29*bytes[k]+150*bytes[k+1]+77*bytes[k+2])>>8);
            }
            for(int y=1;y<h-1;y++)
            for(int x=1;x<w-1;x++)
            {
                int i=y*w+x,k=y*data.Stride+x*4;
                int detail=(4*lum[i]-lum[i-1]-lum[i+1]-lum[i-w]-lum[i+w])/5;
                detail=Math.Max(-10,Math.Min(10,detail));
                for(int c=0;c<3;c++) bytes[k+c]=(byte)Math.Max(0,Math.Min(255,bytes[k+c]+detail));
            }
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
        }
        finally {result.UnlockBits(data);}
        return result;
    }
}


