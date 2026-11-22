using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class Wallpaper : NativeWindow, IDisposable
{
    readonly Scene scene = new Scene();
    Rectangle bounds;
    Bitmap pixels, backdrop;
    BitmapData staticPixels;
    IntPtr surface;
    WallpaperStyle staticStyle;
    Bitmap staticCover;
    IntPtr memoryDC, bitmapHandle, previousObject;
    IntPtr iconView, backgroundWorker;
    long checkOrderAt;
    WallpaperStyle lastStyle;
    int presentations;
    Frame last;

    public void Draw(Frame frame, Rectangle screen, WallpaperStyle style)
    {
        if (Handle == IntPtr.Zero || !IsWindow(Handle) || bounds != screen)
        {
            Attach(screen);
            last = null;
        }
        long now = DateTime.UtcNow.Ticks;
        if (iconView != IntPtr.Zero && now >= checkOrderAt)
        {
            checkOrderAt = now + TimeSpan.TicksPerMillisecond * 400;
            var parent = GetParent(Handle);
            iconView = FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
            backgroundWorker = FindWindowEx(parent, IntPtr.Zero, "WorkerW", null);
            for (IntPtr above = GetWindow(Handle, 3); above != IntPtr.Zero; above = GetWindow(above, 3))
            {
                if (above == iconView) break;
                if (iconView == IntPtr.Zero || above != backgroundWorker) continue;
                if (!SetWindowPos(Handle, iconView, 0, 0, 0, 0, 0x0013)) throw new Win32Exception();
                last = null;
                break;
            }
        }
        bool newVideo = style == WallpaperStyle.Video && (last == null || last.Video != frame.Video || last.VideoRevision != frame.VideoRevision);
        if (!newVideo && last != null && lastStyle == style && last.Artwork == frame.Artwork && last.Words == frame.Words && last.Index == frame.Index &&
            last.Time == frame.Time && last.Pulse == frame.Pulse && last.Level == frame.Level && last.Playing == frame.Playing && last.Mood == frame.Mood && last.ReactionAge == frame.ReactionAge) return;
        if (style == WallpaperStyle.Black && !frame.Playing && last != null && lastStyle == style && last.Words == null && frame.Words == null && last.Artwork == frame.Artwork && last.Playing == false) return;
        if (pixels == null || pixels.Size != bounds.Size) CreateSurface();
        if (newVideo || staticPixels == null || staticStyle != style || staticCover != frame.Artwork)
        {
            UnlockBackdrop();
            backdrop = scene.Backdrop(bounds.Size, style, frame);
            staticPixels = backdrop.LockBits(new Rectangle(Point.Empty,bounds.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            staticStyle = style;
            staticCover = frame.Artwork;
        }
        CopyMemory(surface,staticPixels.Scan0,new UIntPtr((uint)(pixels.Width * pixels.Height * 4)));
        using (var graphics = Graphics.FromImage(pixels)) scene.DrawMotion(graphics, bounds.Size, frame, style);
        Present();
        presentations++;
        last = frame;
        lastStyle = style;
    }

    void CreateSurface()
    {
        ReleaseSurface();
        memoryDC = CreateCompatibleDC(IntPtr.Zero);
        if (memoryDC == IntPtr.Zero) throw new Win32Exception();
        var header = new BitmapInfo { Size = 40, Width = bounds.Width, Height = -bounds.Height, Planes = 1, Bits = 32 };
        IntPtr address;
        bitmapHandle = CreateDIBSection(memoryDC, ref header, 0, out address, IntPtr.Zero, 0);
        if (bitmapHandle == IntPtr.Zero) throw new Win32Exception();
        previousObject = SelectObject(memoryDC, bitmapHandle);
        if (previousObject == IntPtr.Zero || previousObject == new IntPtr(-1)) throw new Win32Exception();
        surface = address;
        pixels = new Bitmap(bounds.Width, bounds.Height, bounds.Width * 4, PixelFormat.Format32bppPArgb, address);
    }

    [DllImport("kernel32", EntryPoint="RtlMoveMemory")] static extern void CopyMemory(IntPtr dest,IntPtr src,UIntPtr count);
    void UnlockBackdrop()
    {
        if (staticPixels != null) { backdrop.UnlockBits(staticPixels); staticPixels = null; }
    }
    void ReleaseSurface()
    {
        UnlockBackdrop();
        if (pixels != null) { pixels.Dispose(); pixels = null; }
        if (previousObject != IntPtr.Zero && previousObject != new IntPtr(-1)) SelectObject(memoryDC, previousObject);
        if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
        if (memoryDC != IntPtr.Zero) DeleteDC(memoryDC);
        memoryDC = bitmapHandle = previousObject = IntPtr.Zero;
    }
    void Attach(Rectangle screen)
    {
        Hide();
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) throw new InvalidOperationException("No se encontró el escritorio de Windows.");
        IntPtr worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        {
            IntPtr result;
            SendMessageTimeout(progman, 0x052C, new IntPtr(13), IntPtr.Zero, 2, 600, out result);
            SendMessageTimeout(progman, 0x052C, new IntPtr(13), new IntPtr(1), 2, 600, out result);
            worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        }

        IntPtr icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        IntPtr parent = progman;
        if (worker == IntPtr.Zero || icons == IntPtr.Zero)
        {
            parent = IntPtr.Zero;
            EnumWindows((top, data) =>
            {
                if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true;
                parent = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return parent == IntPtr.Zero;
            }, IntPtr.Zero);
            icons = IntPtr.Zero;
        }
        if (parent == IntPtr.Zero) throw new InvalidOperationException("Windows no abrió la capa del fondo.");

        // Position the wallpaper between Explorer's wallpaper layer and the desktop icons.
        var origin = new NativePoint { X = screen.X, Y = screen.Y };
        if (!ScreenToClient(parent, ref origin)) throw new Win32Exception();
        CreateHandle(new CreateParams { Caption = "Neon Lyrics · Fondo", Parent = parent, Style = 0x40000000,
            ExStyle = 0x08080000, X = origin.X, Y = origin.Y, Width = screen.Width, Height = screen.Height });
        if (!SetWindowPos(Handle, icons, origin.X, origin.Y, screen.Width, screen.Height, 0x0010))
            throw new Win32Exception();
        iconView = icons;
        backgroundWorker = worker;
        bounds = screen;
    }

    void Present()
    {
        if (last == null)
        {
            if (!SetLayeredWindowAttributes(Handle, 0, 255, 2)) throw new Win32Exception();
            ShowWindow(Handle, 4);
        }
        if (!RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x101)) throw new Win32Exception();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x8061) { message.Result = new IntPtr(presentations); return; }
        if (message.Msg == 0x000F && memoryDC != IntPtr.Zero)
        {
            PaintData paint;
            IntPtr dc = BeginPaint(Handle, out paint);
            try { BitBlt(dc, 0, 0, bounds.Width, bounds.Height, memoryDC, 0, 0, 0x00CC0020); }
            finally { EndPaint(Handle, ref paint); }
            return;
        }
        base.WndProc(ref message);
    }
    [StructLayout(LayoutKind.Sequential)] struct PaintData
    {
        public IntPtr dc; public int erase, left, top, right, bottom, restore, update;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=32)] public byte[] reserved;
    }
    [DllImport("user32")] static extern IntPtr BeginPaint(IntPtr w, out PaintData data);
    [DllImport("user32")] static extern bool EndPaint(IntPtr w, ref PaintData data);
    [DllImport("user32", SetLastError=true)] static extern bool RedrawWindow(IntPtr w, IntPtr rect, IntPtr region, uint flags);
    [DllImport("user32", SetLastError=true)] static extern bool SetLayeredWindowAttributes(IntPtr w, uint color, byte alpha, uint flags);
    [DllImport("gdi32")] static extern bool BitBlt(IntPtr dest,int x,int y,int width,int height,IntPtr src,int sx,int sy,uint operation);
    public void Hide()
    {
        if (Handle == IntPtr.Zero) return;
        if (IsWindow(Handle)) DestroyHandle();
        else ReleaseHandle();
        last = null;
    }

    public void Dispose()
    {
        Hide();
        ReleaseSurface();
        scene.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Blend { public byte Operation, Flags, Opacity, Alpha; }
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, Bits;
        public uint Compression, ImageSize; public int PixelsX, PixelsY; public uint Colors, ImportantColors;
    }
    [DllImport("gdi32", SetLastError = true)] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo header,
        uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("user32")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32")] static extern IntPtr GetWindow(IntPtr window, uint command);
    delegate bool EnumWindow(IntPtr window, IntPtr data);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string name, string caption);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string caption);
    [DllImport("user32")] static extern bool EnumWindows(EnumWindow callback, IntPtr data);
    [DllImport("user32")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32", SetLastError = true)] static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("user32", SetLastError = true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32")] static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32")] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint ms, out IntPtr result);
    [DllImport("user32", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr desktop, IntPtr position,
        ref NativePoint size, IntPtr source, ref NativePoint origin, uint key, ref Blend blend, uint flags);
    [DllImport("gdi32", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32", SetLastError = true)] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32")] static extern bool DeleteDC(IntPtr dc);
}








