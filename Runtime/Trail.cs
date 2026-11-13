using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace PlayNotes
{
    // trail.txt: one line for each thing that happened, as  seconds <tab> kind <tab> words.
    // The layout is the promise between the recorder, the speech-to-text step and whoever reads a session: Documentation~/session-format.md.
    internal sealed class Trail : IDisposable
    {
        internal const string FormatVersion = "1";

        readonly object gate = new object();
        readonly Stopwatch clock;
        StreamWriter file;
        double flushedAt;

        internal Trail(string path, Stopwatch clock)
        {
            this.clock = clock;
            file = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16);
        }

        internal void Line(string kind, string words)
        {
            lock (gate)
            {
                if (file == null) return;
                double now = clock.Elapsed.TotalSeconds;
                file.WriteLine(Format(now, kind, words));
                // (to the disk every two seconds: a crash is exactly when the last lines are wanted)
                if (now - flushedAt < 2) return;
                flushedAt = now;
                file.Flush();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                file?.Dispose();
                file = null;
            }
        }

        internal static string Format(double seconds, string kind, string words)
        {
            return seconds.ToString("0.00", CultureInfo.InvariantCulture) + "\t" + Kind(kind) + "\t" + Clean(words);
        }

        // one word, so a line always splits into three
        internal static string Kind(string kind)
        {
            string s = Clean(kind).Replace(' ', '_');
            return s.Length > 0 ? s : "note";
        }

        // a tab or a line break inside the words would break the line in two
        internal static string Clean(string words)
        {
            if (string.IsNullOrEmpty(words)) return "";
            var sb = new StringBuilder(words.Length);
            foreach (char c in words) sb.Append(c == '\t' || c == '\n' || c == '\r' ? ' ' : c);
            return sb.ToString().Trim();
        }
    }
}
