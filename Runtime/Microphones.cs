#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !PLAYNOTES_DISABLED
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PlayNotes
{
    // Everything PlayNotes does with sound is in this file.
    //
    // Between the start and the end of a session, each real microphone of the PC is written to its own file in the session's folder
    // (mic<n>.wav, 16 kHz, one channel). Nothing is sent anywhere, and nothing is open outside a session.
    //
    // Every microphone, not "the default one": which input a person speaks into is not something a program can know, and an hour of
    // silence from the wrong one is the worst thing that can happen here. The speech-to-text step picks the one that heard best.
    //
    // Through Windows' own waveIn, on a thread of its own, and not Unity's Microphone: that is one device at a time, on the main thread.
    internal sealed class Microphones
    {
        internal const int Rate = 16000;

        readonly List<Mic> mics = new List<Mic>();
        readonly Stopwatch clock;
        Thread thread;
        volatile bool stop;
        volatile string trouble = "";
        double voiceAt = -99;

        Microphones(Stopwatch clock)
        {
            this.clock = clock;
        }

        internal int Count => mics.Count;

        internal string Trouble => trouble;

        /// <summary>When a voice was last heard, in the session's seconds.</summary>
        internal double VoiceAt => Volatile.Read(ref voiceAt);

        /// <summary>The loudest microphone's level just now, 0-1.</summary>
        internal float Level
        {
            get
            {
                float level = 0;
                foreach (var m in mics) level = Math.Max(level, m.Detector.Level);
                return level;
            }
        }

        internal bool Voice
        {
            get
            {
                foreach (var m in mics)
                    if (m.Detector.Voice) return true;
                return false;
            }
        }

        internal static Microphones Open(string folder, Trail trail, Stopwatch clock, string[] alsoSkip)
        {
            var all = new Microphones(clock);
            uint count = waveInGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                var caps = new WaveInCaps();
                if (waveInGetDevCapsW(new UIntPtr(i), ref caps, (uint)Marshal.SizeOf<WaveInCaps>()) != 0) continue;
                string name = caps.Name ?? "";
                bool skip = MicFilter.IsVirtual(name, alsoSkip);
                trail.Line("mic", i + "  " + name + (skip ? "  (skipped)" : ""));
                if (skip) continue;
                var mic = Mic.Open(i, name, Path.Combine(folder, "mic" + i + ".wav"), out string why);
                if (mic != null) all.mics.Add(mic);
                if (why.Length > 0) trail.Line("trouble", "mic " + i + ": " + why);
            }
            all.thread = new Thread(all.Loop) { IsBackground = true, Name = "PlayNotes audio" };
            all.thread.Start();
            return all;
        }

        internal void Close()
        {
            stop = true;
            thread?.Join(3000);
        }

        internal void Describe(StringBuilder sb)
        {
            foreach (var m in mics)
            {
                sb.Append(" | ").Append(m.Name).Append(' ').Append(m.Written / (Rate * 2)).Append(" s peak ");
                sb.Append(m.Detector.Loudest.ToString("0.0000", CultureInfo.InvariantCulture));
            }
        }

        void Loop()
        {
            try
            {
                double flushed = 0;
                while (!stop)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    foreach (var m in mics)
                    {
                        m.Pump();
                        if (m.Detector.Voice) Volatile.Write(ref voiceAt, now);
                    }
                    if (now - flushed > 3)
                    {
                        flushed = now;
                        foreach (var m in mics) m.Flush();
                    }
                    Thread.Sleep(15);
                }
            }
            catch (Exception e)
            {
                trouble = "audio: " + e.Message;
            }
            foreach (var m in mics)
            {
                try
                {
                    m.Close();
                }
                catch (Exception e)
                {
                    trouble = "audio close: " + e.Message;
                }
            }
        }

        sealed class Mic
        {
            // (a quarter of a second to a block, twelve blocks going round)
            const int Blocks = 12;
            const int BlockBytes = 8000;
            const int Done = 1;

            // (WAVEHDR, whose fields sit at different places in a 32-bit and a 64-bit program)
            static readonly int HeaderBytes = IntPtr.Size == 8 ? 48 : 32;
            static readonly int LengthAt = IntPtr.Size;
            static readonly int RecordedAt = IntPtr.Size + 4;
            static readonly int FlagsAt = IntPtr.Size == 8 ? 24 : 16;

            internal readonly VoiceDetector Detector = new VoiceDetector();
            internal string Name;
            internal long Written;

            IntPtr handle;
            readonly IntPtr[] header = new IntPtr[Blocks];
            readonly IntPtr[] data = new IntPtr[Blocks];
            readonly byte[] copy = new byte[BlockBytes];
            int next;
            FileStream file;

            internal static Mic Open(uint id, string name, string path, out string why)
            {
                why = "";
                var format = new WaveFormat { Tag = 1, Channels = 1, SamplesPerSec = Rate, BytesPerSec = Rate * 2, BlockAlign = 2, Bits = 16, Extra = 0 };
                int r = waveInOpen(out var h, id, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                if (r != 0)
                {
                    why = "could not be opened (" + r + ")";
                    return null;
                }
                var m = new Mic { handle = h, Name = name };
                m.file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
                WriteHeader(m.file, 0);
                for (int i = 0; i < Blocks; i++)
                {
                    m.data[i] = Marshal.AllocHGlobal(BlockBytes);
                    m.header[i] = Marshal.AllocHGlobal(HeaderBytes);
                    for (int b = 0; b < HeaderBytes; b++) Marshal.WriteByte(m.header[i], b, 0);
                    Marshal.WriteIntPtr(m.header[i], 0, m.data[i]);
                    Marshal.WriteInt32(m.header[i], LengthAt, BlockBytes);
                    waveInPrepareHeader(h, m.header[i], (uint)HeaderBytes);
                    waveInAddBuffer(h, m.header[i], (uint)HeaderBytes);
                }
                r = waveInStart(h);
                if (r != 0) why = "could not be started (" + r + ")";
                return m;
            }

            internal void Pump() => Drain(true);

            void Drain(bool again)
            {
                for (int guard = 0; guard < Blocks; guard++)
                {
                    if ((Marshal.ReadInt32(header[next], FlagsAt) & Done) == 0) return;
                    int got = Marshal.ReadInt32(header[next], RecordedAt);
                    if (got > 0)
                    {
                        Marshal.Copy(data[next], copy, 0, got);
                        file.Write(copy, 0, got);
                        Written += got;
                        Detector.Feed(copy, got);
                    }
                    if (again)
                    {
                        waveInAddBuffer(handle, header[next], (uint)HeaderBytes);
                    }
                    else
                    {
                        // (taken out of the round: a block read twice at the end would write its sound twice)
                        Marshal.WriteInt32(header[next], FlagsAt, Marshal.ReadInt32(header[next], FlagsAt) & ~Done);
                    }
                    next = (next + 1) % Blocks;
                }
            }

            internal void Flush() => file.Flush();

            internal void Close()
            {
                waveInStop(handle);
                waveInReset(handle);
                Drain(false);
                for (int i = 0; i < Blocks; i++)
                {
                    Marshal.WriteInt32(header[i], FlagsAt, Marshal.ReadInt32(header[i], FlagsAt) | Done);
                    waveInUnprepareHeader(handle, header[i], (uint)HeaderBytes);
                }
                waveInClose(handle);
                handle = IntPtr.Zero;
                for (int i = 0; i < Blocks; i++)
                {
                    Marshal.FreeHGlobal(header[i]);
                    Marshal.FreeHGlobal(data[i]);
                    header[i] = IntPtr.Zero;
                    data[i] = IntPtr.Zero;
                }
                // (the length is known only now; a session cut short by a crash keeps a header that says 0, and the reader allows for it)
                file.Seek(0, SeekOrigin.Begin);
                WriteHeader(file, Written);
                file.Dispose();
            }

            static void WriteHeader(Stream s, long dataBytes)
            {
                using (var w = new BinaryWriter(s, Encoding.ASCII, true))
                {
                    w.Write(Encoding.ASCII.GetBytes("RIFF"));
                    w.Write((uint)(36 + dataBytes));
                    w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)1);
                    w.Write(Rate);
                    w.Write(Rate * 2);
                    w.Write((short)2);
                    w.Write((short)16);
                    w.Write(Encoding.ASCII.GetBytes("data"));
                    w.Write((uint)dataBytes);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WaveFormat
        {
            public ushort Tag, Channels;
            public uint SamplesPerSec, BytesPerSec;
            public ushort BlockAlign, Bits, Extra;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WaveInCaps
        {
            public ushort Mid, Pid;
            public uint DriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Formats;
            public ushort ChannelCount, Reserved;
        }

        [DllImport("winmm.dll")] static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int waveInGetDevCapsW(UIntPtr id, ref WaveInCaps caps, uint size);
        [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr handle, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern int waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] static extern int waveInStop(IntPtr handle);
        [DllImport("winmm.dll")] static extern int waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] static extern int waveInClose(IntPtr handle);
    }
}
#endif
