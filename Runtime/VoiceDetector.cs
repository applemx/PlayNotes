using System;

namespace PlayNotes
{
    // Whether someone is speaking into one microphone, from its sound a quarter of a second at a time.
    // Used for two things only: the pictures come faster while a voice is going, and the badge can say that nothing is being heard.
    // What was said is never decided here; the speech-to-text step listens to the whole recording afterwards.
    internal sealed class VoiceDetector
    {
        // (the least a voice has to stand at, of full scale: a quiet speaker on a desk microphone measured .003-.009, and a threshold
        // of .012 called a whole evening of talking "no voice")
        const float Least = .0025f;

        float floor = 1f;
        int loud;
        float level;
        float loudest;
        volatile bool voice;

        /// <summary>The last block's peak, 0-1: what the badge swells with.</summary>
        internal float Level => level;

        /// <summary>The loudest peak so far.</summary>
        internal float Loudest => loudest;

        /// <summary>A voice has stood over the quiet for half a second together.</summary>
        internal bool Voice => voice;

        /// <summary>16-bit little-endian mono samples.</summary>
        internal void Feed(byte[] pcm, int count)
        {
            int peak = 0;
            long sum = 0;
            for (int i = 0; i + 1 < count; i += 2)
            {
                int s = (short)(pcm[i] | pcm[i + 1] << 8);
                sum += s * s;
                if (s < 0) s = -s;
                if (s > peak) peak = s;
            }
            level = peak / 32768f;
            if (level > loudest) loudest = level;
            // (a voice is what stands over this microphone's own quiet, and the quiet is followed slowly up and at once down.
            // By the peaks of a block a webcam's room noise alone counted as voice in 338 blocks of 833; by their power, in none)
            float power = (float)Math.Sqrt(sum / (double)Math.Max(1, count / 2)) / 32768f;
            floor = Math.Min(floor * 1.01f, Math.Max(power, .0003f));
            loud = power > Math.Max(floor * 3f, Least) ? loud + 1 : 0;
            voice = loud >= 2;
        }
    }
}
