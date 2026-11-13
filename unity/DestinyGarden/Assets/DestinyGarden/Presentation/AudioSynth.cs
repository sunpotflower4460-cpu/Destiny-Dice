using System;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Every sound of the game, synthesized in plain C# (no UnityEngine, no audio assets).
    /// Pure and deterministic, so the sounds can be written to WAV by a test and measured offline
    /// (Tests/AudioSynthTests -> claude-music-ear). Everything is tuned to D major pentatonic so
    /// that any combination of chimes, pads and birds stays consonant.
    /// </summary>
    public static class AudioSynth
    {
        public const int Rate = 44100;

        // D major pentatonic: D4 E4 F#4 A4 B4, then the same notes up the octaves.
        private static readonly double[] Penta = { 293.66, 329.63, 369.99, 440.00, 493.88 };

        public static double Note(int index)
        {
            int i = Math.Max(0, index);
            return Penta[i % 5] * Math.Pow(2.0, (i / 5) % 3);
        }

        // ---------------------------------------------------------------- building blocks

        private sealed class Biquad
        {
            private double _b0, _b1, _b2, _a1, _a2, _z1, _z2;

            private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
            {
                _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
            }

            public static Biquad Low(double f, double q)
            {
                double w = 2 * Math.PI * f / Rate, c = Math.Cos(w), a = Math.Sin(w) / (2 * q);
                return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
            }

            public static Biquad High(double f, double q)
            {
                double w = 2 * Math.PI * f / Rate, c = Math.Cos(w), a = Math.Sin(w) / (2 * q);
                return new Biquad((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
            }

            public static Biquad Band(double f, double q)
            {
                double w = 2 * Math.PI * f / Rate, c = Math.Cos(w), a = Math.Sin(w) / (2 * q);
                return new Biquad(a, 0, -a, 1 + a, -2 * c, 1 - a);
            }

            public double Process(double x)
            {
                double y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return y;
            }
        }

        private static Func<double> White(int seed)
        {
            var r = new Random(seed);
            return () => r.NextDouble() * 2.0 - 1.0;
        }

        private static float[] Finish(double[] x, double peak, double fadeOut = 0.03, double fadeIn = 0.002)
        {
            double max = 0;
            for (int i = 0; i < x.Length; i++) max = Math.Max(max, Math.Abs(x[i]));
            double g = max > 1e-9 ? peak / max : 0.0;
            var o = new float[x.Length];
            int fo = (int)(fadeOut * Rate), fi = (int)(fadeIn * Rate);
            for (int i = 0; i < x.Length; i++)
            {
                double e = 1.0;
                if (i < fi) e = (double)i / fi;
                int fromEnd = x.Length - 1 - i;
                if (fromEnd < fo) e *= (double)fromEnd / fo;
                o[i] = (float)(x[i] * g * e);
            }
            return o;
        }

        /// <summary>Equal-power seamless loop of n samples from raw (n + x samples long).</summary>
        private static double[] LoopFrom(double[] raw, int n, int x)
        {
            var o = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i < x)
                {
                    double th = Math.PI / 2 * i / x;
                    o[i] = raw[i] * Math.Sin(th) + raw[n + i] * Math.Cos(th);
                }
                else o[i] = raw[i];
            }
            return o;
        }

        private static void AllPass(double[] x, int d, double g)
        {
            var buf = new double[d]; int idx = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double bo = buf[idx];
                double v = x[i] + g * bo;
                x[i] = bo - g * v;
                buf[idx] = v;
                if (++idx == d) idx = 0;
            }
        }

        /// <summary>A small hall: four damped combs and two all-passes. Returns dry + wet tail.</summary>
        private static double[] Reverb(double[] x, double wet, double tail)
        {
            int n = x.Length + (int)(tail * Rate);
            var dry = new double[n];
            Array.Copy(x, dry, x.Length);
            var acc = new double[n];
            foreach (int d in new[] { 1307, 1637, 1811, 1931 })
            {
                double g = Math.Pow(10.0, -3.0 * d / (Rate * Math.Max(0.2, tail)));
                var buf = new double[d]; int idx = 0; double lp = 0;
                for (int i = 0; i < n; i++)
                {
                    double y = buf[idx];
                    lp = y * 0.62 + lp * 0.38;
                    buf[idx] = dry[i] + lp * g;
                    if (++idx == d) idx = 0;
                    acc[i] += y;
                }
            }
            AllPass(acc, 225, 0.5);
            AllPass(acc, 556, 0.5);
            var o = new double[n];
            for (int i = 0; i < n; i++) o[i] = dry[i] + acc[i] * 0.22 * wet;
            return o;
        }

        /// <summary>A struck bell: inharmonic partials with their own decays, plus a low hum.</summary>
        private static void Strike(double[] dst, double startSec, double f, double gain, double decayMul)
        {
            // ratio, level, decay seconds
            double[,] p =
            {
                { 1.0, 1.0, 1.7 }, { 2.0, 0.42, 1.15 }, { 2.756, 0.28, 0.85 }, { 5.404, 0.10, 0.5 }, { 0.5, 0.30, 2.0 }
            };
            int start = (int)(startSec * Rate);
            for (int k = 0; k < p.GetLength(0); k++)
            {
                double fr = f * p[k, 0];
                if (fr > 9000) continue;
                double dec = p[k, 2] * decayMul;
                int len = Math.Min(dst.Length - start, (int)(dec * 5 * Rate));
                double phase = k * 0.7;
                for (int i = 0; i < len; i++)
                {
                    double t = i / (double)Rate;
                    double env = Math.Min(1.0, t / 0.004) * Math.Exp(-t / dec);
                    dst[start + i] += Math.Sin(2 * Math.PI * fr * t + phase) * gain * p[k, 1] * env;
                }
            }
        }

        // ---------------------------------------------------------------- ambient loops

        /// <summary>The sea around the island: a deep, slow swell with a little hiss.</summary>
        public static float[] Sea()
        {
            int n = Rate * 10, x = Rate / 2, warm = Rate / 5;
            var w = White(11); var lp = Biquad.Low(380, 0.7); var bp = Biquad.Band(1100, 0.6);
            var raw = new double[n + x];
            for (int i = -warm; i < n + x; i++)
            {
                double v = w();
                double s = lp.Process(v) * 1.0 + bp.Process(v) * 0.07; // hiss kept ~15 dB under the swell
                if (i >= 0) raw[i] = s * (0.62 + 0.38 * Math.Sin(2 * Math.PI * 3 * i / (double)n + 0.4));
            }
            return Finish(LoopFrom(raw, n, x), 0.6, 0, 0);
        }

        /// <summary>The little river: bright babble with bubbling pops.</summary>
        public static float[] Stream()
        {
            int n = Rate * 6, x = Rate / 2, warm = Rate / 5, total = n + x;
            var w = White(21); var bp = Biquad.Band(2700, 0.9); var envLp = Biquad.Low(16, 0.7);
            var s = new double[total]; var e = new double[total];
            for (int i = -warm; i < total; i++)
            {
                double v = w();
                double a = bp.Process(v), b = envLp.Process(w());
                if (i >= 0) { s[i] = a; e[i] = b; }
            }
            double sd = 0;
            for (int i = 0; i < total; i++) sd += e[i] * e[i];
            sd = Math.Sqrt(sd / total) + 1e-12;
            for (int i = 0; i < total; i++) s[i] *= 0.6 + 0.4 * Math.Tanh(e[i] / sd);
            var r = new Random(22);
            for (int k = 0; k < 16; k++)
            {
                int t0 = r.Next(0, total - Rate / 10);
                double f0 = 500 + r.NextDouble() * 400, ph = 0;
                int len = (int)(0.045 * Rate);
                for (int i = 0; i < len; i++)
                {
                    double u = i / (double)len;
                    ph += 2 * Math.PI * (f0 * (1 + 1.2 * u)) / Rate;
                    s[t0 + i] += Math.Sin(ph) * Math.Sin(Math.PI * u) * 0.9;
                }
            }
            return Finish(LoopFrom(s, n, x), 0.6, 0, 0);
        }

        /// <summary>Wind through leaves: bandpassed noise in slow gusts.</summary>
        public static float[] LeafWind()
        {
            int n = Rate * 8, x = Rate / 2, warm = Rate / 5;
            var w = White(31); var bp = Biquad.Band(1500, 0.55); var lp = Biquad.Low(4500, 0.7);
            var raw = new double[n + x];
            for (int i = -warm; i < n + x; i++)
            {
                double s = lp.Process(bp.Process(w()));
                if (i < 0) continue;
                double gust = 0.3 + 0.7 * Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 2 * i / (double)n + 1.0), 2);
                raw[i] = s * gust * (0.85 + 0.15 * Math.Sin(2 * Math.PI * 7 * i / (double)n));
            }
            return Finish(LoopFrom(raw, n, x), 0.6, 0, 0);
        }

        /// <summary>Night insects: small bundles of pulses, periodic so the loop is seamless.</summary>
        public static float[] Crickets()
        {
            int n = Rate * 4;
            var o = new double[n];
            for (int k = 0; k < 16; k++)
                for (int p = 0; p < 3; p++)
                {
                    int start = (int)((k * 0.25 + p * 0.055) * Rate), len = (int)(0.03 * Rate);
                    for (int i = 0; i < len; i++)
                    {
                        double u = i / (double)len, t = (start + i) / (double)Rate;
                        o[start + i] += (Math.Sin(2 * Math.PI * 4300 * t) + 0.5 * Math.Sin(2 * Math.PI * 4650 * t)) * Math.Pow(Math.Sin(Math.PI * u), 2);
                    }
                }
            return Finish(o, 0.5, 0, 0);
        }

        /// <summary>
        /// One layer of the harmony pad. Layers add as the island's harmony grows:
        /// 0 = open fifth, 1 = the third, 2 = the ninth, 3 = a shimmering sixth.
        /// Frequencies are rounded to whole cycles per loop, so the loop is exactly seamless.
        /// </summary>
        public static float[] PadLayer(int layer)
        {
            const double T = 12.0;
            int n = (int)(Rate * T);
            double[] notes; double trem;
            switch (layer)
            {
                case 0: notes = new[] { 146.83, 220.00 }; trem = 0; break;
                case 1: notes = new[] { 369.99, 185.00 }; trem = 1; break;
                case 2: notes = new[] { 329.63, 440.00 }; trem = 2; break;
                default: notes = new[] { 493.88, 587.33 }; trem = 6; break;
            }
            var o = new double[n];
            foreach (double f0 in notes)
            {
                double f = Math.Round(f0 * T) / T;
                for (int i = 0; i < n; i++)
                {
                    double t = i / (double)Rate;
                    double v = Math.Sin(2 * Math.PI * (f - 1 / T) * t) + Math.Sin(2 * Math.PI * f * t) + Math.Sin(2 * Math.PI * (f + 1 / T) * t)
                               + 0.2 * Math.Sin(2 * Math.PI * 2 * f * t);
                    double tr = trem > 0 ? 1 + 0.25 * Math.Sin(2 * Math.PI * trem * i / (double)n) : 1.0;
                    o[i] += v * tr;
                }
            }
            return Finish(o, 0.5, 0, 0);
        }

        // ---------------------------------------------------------------- one-shots

        /// <summary>A bird: two to four little syllables, sweeping up or down.</summary>
        public static float[] Chirp(int variant)
        {
            var r = new Random(300 + variant);
            int syll = 2 + r.Next(3);
            double f0 = 2800 + variant * 260 + r.Next(400);
            int n = (int)((syll * 0.115 + 0.05) * Rate);
            var o = new double[n];
            for (int s = 0; s < syll; s++)
            {
                int start = (int)(s * 0.115 * Rate), len = (int)(0.07 * Rate);
                double ph = 0, sweep = s % 2 == 0 ? 0.45 : -0.3;
                for (int i = 0; i < len; i++)
                {
                    double u = i / (double)len;
                    ph += 2 * Math.PI * f0 * (1 + sweep * Math.Sin(Math.PI * u)) / Rate;
                    double env = Math.Pow(Math.Sin(Math.PI * u), 2);
                    o[start + i] += (Math.Sin(ph) + 0.18 * Math.Sin(2 * ph)) * env;
                }
            }
            return Finish(o, 0.5, 0.02);
        }

        /// <summary>The bell that answers each roll: upper do (D5), so the face's note that follows stays in D major.</summary>
        public static float[] Bell(double f = 587.33)
        {
            var d = new double[(int)(3.2 * Rate)];
            Strike(d, 0, f, 1.0, 1.0);
            return Finish(Reverb(d, 0.35, 2.2), 0.55);
        }

        /// <summary>A plucked string (Karplus-Strong).</summary>
        public static float[] Pluck(double f, double seconds = 1.4)
        {
            int n = (int)(seconds * Rate), len = Math.Max(2, (int)(Rate / f));
            var r = new Random(41);
            var buf = new double[len];
            double mean = 0;
            for (int j = 0; j < len; j++) { buf[j] = r.NextDouble() * 2 - 1; mean += buf[j]; }
            mean /= len;
            for (int j = 0; j < len; j++) buf[j] -= mean; // no DC: the string must not carry a constant offset
            for (int j = 0; j < len; j++) buf[j] = 0.5 * (buf[j] + buf[(j + 1) % len]);
            var o = new double[n]; int idx = 0;
            for (int i = 0; i < n; i++)
            {
                o[i] = buf[idx];
                buf[idx] = 0.5 * (buf[idx] + buf[(idx + 1) % len]) * 0.9975;
                if (++idx == len) idx = 0;
            }
            double avg = 0;
            for (int i = 0; i < n; i++) avg += o[i];
            avg /= n;
            for (int i = 0; i < n; i++) o[i] -= avg; // remove the residual DC of the whole note
            return Finish(Reverb(o, 0.25, 1.5), 0.5);
        }

        /// <summary>A water blessing: a pouring of noise that brightens, with bubbles.</summary>
        public static float[] Water()
        {
            int n = (int)(2.0 * Rate);
            var w = White(51); var b1 = Biquad.Band(800, 0.8); var b2 = Biquad.Band(1700, 0.8); var envLp = Biquad.Low(14, 0.7);
            var s = new double[n]; var e = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = w();
                double u = i / (double)n;
                s[i] = b1.Process(v) * (1 - u) + b2.Process(v) * u;
                e[i] = envLp.Process(w());
            }
            double sd = 0;
            for (int i = 0; i < n; i++) sd += e[i] * e[i];
            sd = Math.Sqrt(sd / n) + 1e-12;
            for (int i = 0; i < n; i++) s[i] *= Math.Pow(Math.Sin(Math.PI * i / n), 0.8) * (0.6 + 0.4 * Math.Tanh(e[i] / sd));
            return Finish(Reverb(s, 0.2, 1.2), 0.5);
        }

        /// <summary>A wind blessing: a soft sweep from low to mid.</summary>
        public static float[] Wind()
        {
            int n = (int)(2.6 * Rate);
            var w = White(61); var b1 = Biquad.Band(520, 0.8); var b2 = Biquad.Band(900, 0.8);
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = w(), u = i / (double)n;
                s[i] = (b1.Process(v) * (1 - u) + b2.Process(v) * u) * Math.Pow(Math.Sin(Math.PI * u), 2);
            }
            return Finish(s, 0.45, 0.05);
        }

        /// <summary>A light blessing: scattered high pings in a hall.</summary>
        public static float[] Shimmer()
        {
            var d = new double[(int)(2.4 * Rate)];
            var r = new Random(71);
            for (int k = 0; k < 16; k++)
            {
                int start = (int)(r.NextDouble() * 1.2 * Rate);
                double f = 1500 + r.NextDouble() * 2500, g = 0.3 + r.NextDouble() * 0.3;
                int len = (int)(0.9 * Rate);
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    double t = i / (double)Rate;
                    d[start + i] += Math.Sin(2 * Math.PI * f * t) * g * Math.Min(1.0, t / 0.003) * Math.Exp(-t / 0.28);
                }
            }
            return Finish(Reverb(d, 0.45, 2.0), 0.5);
        }

        /// <summary>A seed settling: a short, soft drop in pitch.</summary>
        public static float[] Sprout(int index)
        {
            double f = Note(index) * 0.5;
            var d = new double[(int)(0.7 * Rate)];
            var w = White(81); var hp = Biquad.High(3000, 0.7);
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                double t = i / (double)Rate;
                ph += 2 * Math.PI * f * (1 + 0.25 * Math.Exp(-t / 0.05)) / Rate;
                d[i] = Math.Sin(ph) * Math.Exp(-t / 0.16) * 0.9 + hp.Process(w()) * Math.Exp(-t / 0.008) * 0.25;
            }
            return Finish(Reverb(d, 0.25, 0.8), 0.35);
        }

        /// <summary>A flower opening: its own note of the pentatonic scale, with a sparkle above.</summary>
        public static float[] Bloom(int index)
        {
            var d = new double[(int)(2.8 * Rate)];
            double f = Note(index);
            Strike(d, 0, f, 1.0, 0.9);
            Strike(d, 0.09, f * 2, 0.45, 0.7);
            double top = f * 4 > 4200 ? f * 2 : f * 4;
            for (int k = 0; k < 3; k++) Strike(d, 0.18 + k * 0.11, top, 0.16, 0.25);
            return Finish(Reverb(d, 0.5, 2.4), 0.5);
        }

        /// <summary>The seventh face appears: three bells climbing, with sparkle.</summary>
        public static float[] LuckySeven()
        {
            var d = new double[(int)(3.8 * Rate)];
            Strike(d, 0.00, 587.33, 1.0, 1.0);
            Strike(d, 0.07, 880.00, 0.8, 1.0);
            Strike(d, 0.14, 1479.98, 0.7, 0.8);
            for (int k = 0; k < 5; k++) Strike(d, 0.3 + k * 0.1, Note(7 + k) * 2, 0.15, 0.3);
            return Finish(Reverb(d, 0.5, 2.5), 0.6);
        }

        /// <summary>The island's star grows: a chord that opens with five, seven or eight points.</summary>
        public static float[] StarUp(int points)
        {
            double[] fs = points >= 8 ? new[] { 293.66, 440.00, 739.99, 987.77 }
                : points >= 7 ? new[] { 293.66, 369.99, 440.00, 659.25 }
                : new[] { 293.66, 440.00, 587.33 };
            var d = new double[(int)(4.6 * Rate)];
            for (int k = 0; k < fs.Length; k++) Strike(d, k * 0.13, fs[k], 0.9, 1.6);
            return Finish(Reverb(d, 0.55, 3.2), 0.5);
        }

        /// <summary>The rainbow rises: a slow chord swell with a rising run of bell notes.</summary>
        public static float[] Rainbow()
        {
            double dur = 8.0;
            var d = new double[(int)(dur * Rate)];
            foreach (double f in new[] { 293.66, 440.00, 739.99, 1174.66, 880.00 })
            {
                for (int i = 0; i < d.Length; i++)
                {
                    double t = i / (double)Rate;
                    double att = Math.Min(1.0, t / 3.2); att = att * att * (3 - 2 * att);
                    double rel = Math.Min(1.0, (dur - t) / 2.8); rel = rel * rel * (3 - 2 * rel);
                    d[i] += (Math.Sin(2 * Math.PI * f * t) + 0.6 * Math.Sin(2 * Math.PI * (f + 0.4) * t)) * att * rel * 0.25;
                }
            }
            for (int k = 0; k < 14; k++) Strike(d, 0.9 + k * 0.26, Note(k) * 2, 0.35, 0.55);
            return Finish(Reverb(d, 0.5, 3.6), 0.55);
        }
        // ---------------------------------------------------------------- the prayer star (solfège)

        // D major: do re mi fa sol la si do (D4 E4 F#4 G4 A4 B4 C#5 D5). The seven faces are the seven
        // notes; the eight-pointed star completes the octave.
        private static readonly double[] Solfege = { 293.66, 329.63, 369.99, 392.00, 440.00, 493.88, 554.37, 587.33 };

        /// <summary>Scale degree 1..8 (do .. upper do) of D major, octave 0 = D4.</summary>
        public static double SolfegeNote(int degree, int octave = 0)
        {
            int d = Math.Max(1, Math.Min(8, degree));
            return Solfege[d - 1] * Math.Pow(2.0, octave);
        }

        /// <summary>A soft glass tone: a quick but gentle attack, few partials, a long quiet tail.</summary>
        private static void Glass(double[] dst, double startSec, double f, double gain, double decay, double attack = 0.012)
        {
            double[,] p = { { 1.0, 1.0, 1.0 }, { 2.0, 0.16, 0.55 }, { 3.0, 0.045, 0.35 }, { 1.0016, 0.35, 1.1 } };
            int start = (int)(startSec * Rate);
            for (int k = 0; k < p.GetLength(0); k++)
            {
                double fr = f * p[k, 0];
                if (fr > 9000) continue;
                double dec = decay * p[k, 2];
                int len = Math.Min(dst.Length - start, (int)(dec * 6 * Rate));
                for (int i = 0; i < len; i++)
                {
                    double t = i / (double)Rate;
                    double a = Math.Min(1.0, t / attack); a = a * a * (3 - 2 * a);
                    dst[start + i] += Math.Sin(2 * Math.PI * fr * t + k * 0.9) * gain * p[k, 1] * a * Math.Exp(-t / dec);
                }
            }
        }

        /// <summary>The note of a rolled face: face n sings degree n (1 = do .. 7 = si), an octave up.</summary>
        public static float[] FaceNote(int face)
        {
            var d = new double[(int)(2.6 * Rate)];
            double f = SolfegeNote(Math.Max(1, Math.Min(7, face)), 1);
            Glass(d, 0, f, 1.0, 0.75);
            return Finish(Reverb(d, 0.55, 2.2), 0.42);
        }

        /// <summary>
        /// The prayer star settles: 5 = do-sol (an open fifth), 6 = do-mi-sol (yin and yang joined),
        /// 7 = do-mi-sol-si (the lucky number), 8 = the scale climbs do..do and the octave chord opens.
        /// A slow swell, so it sits under the other sounds rather than on top of them.
        /// </summary>
        public static float[] StarChord(int points)
        {
            double dur = points >= 8 ? 5.2 : 4.2;
            var d = new double[(int)(dur * Rate)];
            double t0 = 0;
            if (points >= 8)
            {
                for (int k = 1; k <= 8; k++) Glass(d, (k - 1) * 0.12, SolfegeNote(k, 1), 0.42, 0.5);
                t0 = 0.95;
            }
            int[] degrees = points >= 8 ? new[] { 1, 5, 8, 10 } : points == 7 ? new[] { 1, 3, 5, 7 }
                : points == 6 ? new[] { 1, 3, 5 } : new[] { 1, 5 };
            for (int k = 0; k < degrees.Length; k++)
            {
                int deg = degrees[k];
                double f = deg > 8 ? SolfegeNote(deg - 7, 1) : SolfegeNote(deg);
                double start = t0 + k * 0.09;
                // A swelling sine pair (slightly detuned) gives the sustained "held breath" of the chord.
                int s0 = (int)(start * Rate);
                double hold = dur - start;
                for (int i = 0; s0 + i < d.Length; i++)
                {
                    double t = i / (double)Rate;
                    double att = Math.Min(1.0, t / 0.6); att = att * att * (3 - 2 * att);
                    double rel = Math.Exp(-Math.Max(0, t - 0.6) / (hold * 0.32));
                    d[s0 + i] += (Math.Sin(2 * Math.PI * f * t) + 0.5 * Math.Sin(2 * Math.PI * (f * 1.0021) * t + 0.7)) * att * rel * 0.30;
                }
                Glass(d, start, f * 2, 0.22, 0.9);
            }
            if (points == 7) for (int k = 0; k < 3; k++) Glass(d, t0 + 0.5 + k * 0.16, SolfegeNote(7, 2), 0.10, 0.4);
            return Finish(Reverb(d, 0.6, 3.0), 0.45, 0.6);
        }

        /// <summary>
        /// The garden's cycle (lifecycle): "return" a flower goes back to the soil (sol -> do, falling a fifth),
        /// "fruit" it leaves its seed (sol -> do, then the upper do: the line goes on), "wither" a seedling ends
        /// (a low open fifth, do-sol). Soft attacks and a long tail, meant to sit under everything else.
        /// </summary>
        public static float[] CycleTone(string kind)
        {
            var d = new double[(int)(3.4 * Rate)];
            switch (kind)
            {
                case "fruit":
                    Glass(d, 0.00, SolfegeNote(5), 0.75, 0.70, 0.03);
                    Glass(d, 0.26, SolfegeNote(1), 0.80, 0.85, 0.03);
                    Glass(d, 0.62, SolfegeNote(1, 1), 0.55, 0.90, 0.03);
                    break;
                case "wither":
                    // A hollow open fifth, low and brief: a quiet ending.
                    Glass(d, 0.00, SolfegeNote(1, -1), 0.9, 1.1, 0.05);
                    Glass(d, 0.04, SolfegeNote(5, -1), 0.7, 1.0, 0.05);
                    break;
                default:
                    Glass(d, 0.00, SolfegeNote(5), 0.75, 0.70, 0.03);
                    Glass(d, 0.30, SolfegeNote(1), 0.85, 0.95, 0.03);
                    break;
            }
            return Finish(Reverb(d, 0.6, 2.6), 0.40, 0.5);
        }
    }
}
