#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !PLAYNOTES_DISABLED
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PlayNotes
{
    // Everything PlayNotes does with pictures is in this file.
    //
    // Between the start and the end of a session, pictures of this program's own window go into the session's folder (shots/s<seconds>.jpg):
    // one every few seconds, faster while a voice is heard, and one just after each click. Never the desktop, never another window.
    // In the editor the window is the editor's; in a build it is the game's.
    //
    // By Windows' PrintWindow on a thread of its own, and not Unity's ScreenCapture: that stalls the frame it is called in (116 ms measured
    // on a 1440p game), and the notes are worth nothing if taking them changes how the game feels.
    internal sealed class WindowShots
    {
        const double AfterClick = .35;
        const long LeastFreeBytes = 2L * 1024 * 1024 * 1024;

        readonly string folder;
        readonly Stopwatch clock;
        readonly Func<double> voiceAt;
        readonly double speaking, quiet;
        readonly IntPtr window;
        Thread thread;
        volatile bool stop;
        volatile string trouble = "";
        double wantAt;
        int count;

        /// <summary>The pointer is free and showing (not locked to the middle of the view): worth marking in the pictures.</summary>
        internal volatile bool PointerFree;

        WindowShots(string folder, Stopwatch clock, Func<double> voiceAt, double speaking, double quiet)
        {
            this.folder = folder;
            this.clock = clock;
            this.voiceAt = voiceAt;
            this.speaking = speaking;
            this.quiet = quiet;
            window = OwnWindow();
        }

        static IntPtr OwnWindow()
        {
            var own = Process.GetCurrentProcess();
            if (own.MainWindowHandle != IntPtr.Zero) return own.MainWindowHandle;
            // (a player shown inside another program's window, by Unity's -parentHWND, has no window of its own at the top:
            // it is the child of the given one that belongs to this process)
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (!string.Equals(args[i], "-parentHWND", StringComparison.OrdinalIgnoreCase)) continue;
                string written = args[i + 1];
                bool hex = written.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                if (!long.TryParse(hex ? written.Substring(2) : written, hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out long parent)) break;
                var child = IntPtr.Zero;
                for (int guard = 0; guard < 64; guard++)
                {
                    child = FindWindowEx(new IntPtr(parent), child, null, null);
                    if (child == IntPtr.Zero) break;
                    GetWindowThreadProcessId(child, out uint owner);
                    if (owner == (uint)own.Id) return child;
                }
                break;
            }
            return IntPtr.Zero;
        }

        internal int Count => count;

        internal string Trouble => trouble;

        internal static WindowShots Start(string folder, Stopwatch clock, Func<double> voiceAt, double speaking, double quiet)
        {
            var shots = new WindowShots(folder, clock, voiceAt, speaking, quiet);
            if (shots.window == IntPtr.Zero)
            {
                shots.trouble = "no window to take pictures of";
                return shots;
            }
            shots.thread = new Thread(shots.Loop) { IsBackground = true, Name = "PlayNotes pictures" };
            shots.thread.Start();
            return shots;
        }

        /// <summary>A click has just landed: what it opened is worth a picture of its own.</summary>
        internal void SoonAfterClick()
        {
            Volatile.Write(ref wantAt, clock.Elapsed.TotalSeconds + AfterClick);
        }

        internal void Close()
        {
            stop = true;
            thread?.Join(3000);
        }

        void Loop()
        {
            IntPtr memory = IntPtr.Zero, bitmap = IntPtr.Zero, bits = IntPtr.Zero;
            int width = 0, height = 0;
            byte[] pixels = null;
            double last = -9, diskChecked = -99;
            bool diskFull = false;
            try
            {
                while (!stop)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    double want = Volatile.Read(ref wantAt);
                    bool clicked = want > 0 && now >= want && now - last >= .3;
                    double gap = now - voiceAt() < 4 ? speaking : quiet;
                    if (diskFull || !(clicked || now - last >= gap))
                    {
                        Thread.Sleep(15);
                        continue;
                    }
                    if (clicked) Volatile.Write(ref wantAt, 0);
                    last = now;
                    if (!GetClientRect(window, out var rc)) continue;
                    int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
                    // (a minimized window has no size)
                    if (w <= 0 || h <= 0) continue;
                    if (w != width || h != height)
                    {
                        if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                        if (memory != IntPtr.Zero) DeleteDC(memory);
                        var screen = GetDC(window);
                        memory = CreateCompatibleDC(screen);
                        // (a positive height: rows from the bottom up, as a Unity texture's are, which the encoder takes them to be)
                        var info = new BitmapInfoHeader { Size = 40, Width = w, Height = h, Planes = 1, BitCount = 32, Compression = 0 };
                        bitmap = CreateDIBSection(screen, ref info, 0, out bits, IntPtr.Zero, 0);
                        SelectObject(memory, bitmap);
                        ReleaseDC(window, screen);
                        width = w;
                        height = h;
                        pixels = new byte[w * h * 4];
                    }
                    // (1: the inside of the window only, without its title bar. 2: the content as composed; without it a Direct3D window comes out black)
                    if (!PrintWindow(window, memory, 1 | 2)) continue;
                    GdiFlush();
                    Marshal.Copy(bits, pixels, 0, pixels.Length);
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte blue = pixels[i];
                        pixels[i] = pixels[i + 2];
                        pixels[i + 2] = blue;
                        pixels[i + 3] = 255;
                    }
                    if (PointerFree && GetCursorPos(out var p) && ScreenToClient(window, ref p)) MarkPointer(pixels, w, h, p.X, p.Y);
                    var jpg = ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h, (uint)(w * 4), 75);
                    File.WriteAllBytes(Path.Combine(folder, "shots", "s" + now.ToString("000000.0", CultureInfo.InvariantCulture) + ".jpg"), jpg);
                    Interlocked.Increment(ref count);
                    if (now - diskChecked > 60)
                    {
                        diskChecked = now;
                        diskFull = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace < LeastFreeBytes;
                        if (diskFull) trouble = "disk nearly full: pictures stopped";
                    }
                }
            }
            catch (Exception e)
            {
                trouble = "pictures: " + e.Message;
            }
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
        }

        // Where the pointer is: "this" is so often what it rests on. A ring, dark round red, to be seen on anything.
        static void MarkPointer(byte[] px, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            const int R = 20;
            int row = h - 1 - y;
            for (int j = Math.Max(0, row - R); j <= Math.Min(h - 1, row + R); j++)
            {
                for (int i = Math.Max(0, x - R); i <= Math.Min(w - 1, x + R); i++)
                {
                    float d = Mathf.Sqrt((i - x) * (i - x) + (j - row) * (j - row));
                    bool red = d >= 14 && d <= 17;
                    bool dark = d > 17 && d <= 19.5f || d >= 11.5f && d < 14;
                    if (!red && !dark) continue;
                    int at = (j * w + i) * 4;
                    px[at] = (byte)(red ? 255 : 0);
                    px[at + 1] = (byte)(red ? 40 : 0);
                    px[at + 2] = (byte)(red ? 30 : 0);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Rect32
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Point32
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public uint Size;
            public int Width, Height;
            public ushort Planes, BitCount;
            public uint Compression, SizeImage;
            public int XPels, YPels;
            public uint ClrUsed, ClrImportant;
        }

        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out Rect32 r);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point32 p);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window, ref Point32 p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr what);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr what);
        [DllImport("gdi32.dll")] static extern bool GdiFlush();
    }
}
#endif
