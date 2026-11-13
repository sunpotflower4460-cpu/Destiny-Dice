using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DestinyGarden.Tests
{
    public class SacredTonesTests
    {
        private static Dictionary<string, float[]> Tones()
        {
            var d = new Dictionary<string, float[]>();
            for (int f = 1; f <= 7; f++) d["face" + f] = AudioSynth.FaceNote(f);
            for (int p = 5; p <= 8; p++) d["chord" + p] = AudioSynth.StarChord(p);
            return d;
        }

        private static void WriteWav(string path, float[] s)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int n = s.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(AudioSynth.Rate); w.Write(AudioSynth.Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(n);
                foreach (float f in s) w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, f)) * 32767));
            }
        }

        [Test]
        public void SolfegeTones_AreFinite_Quiet_AndDeterministic()
        {
            var a = Tones(); var b = Tones();
            string outDir = Environment.GetEnvironmentVariable("DG_AUDIO_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            foreach (var kv in a)
            {
                float peak = 0f;
                foreach (float f in kv.Value)
                {
                    Assert.IsFalse(float.IsNaN(f) || float.IsInfinity(f), kv.Key + " has a non-finite sample");
                    peak = Math.Max(peak, Math.Abs(f));
                }
                Assert.That(peak, Is.InRange(0.3f, 0.5f), kv.Key + " peak (kept below the other one-shots)");
                CollectionAssert.AreEqual(kv.Value, b[kv.Key], kv.Key + " must be deterministic");
                if (!string.IsNullOrEmpty(outDir)) WriteWav(Path.Combine(outDir, kv.Key + ".wav"), kv.Value);
            }
            if (!string.IsNullOrEmpty(outDir))
                foreach (var kv in new Dictionary<string, float[]>
                         {
                             ["bell"] = AudioSynth.Bell(), ["star7"] = AudioSynth.StarUp(7), ["lucky"] = AudioSynth.LuckySeven(),
                             ["bloom7"] = AudioSynth.Bloom(7), ["pluck880"] = AudioSynth.Pluck(880)
                         })
                    WriteWav(Path.Combine(outDir, "ref-" + kv.Key + ".wav"), kv.Value);
        }

        [Test]
        public void Solfege_IsDMajor_FromDoToUpperDo()
        {
            double[] expected = { 293.66, 329.63, 369.99, 392.00, 440.00, 493.88, 554.37, 587.33 };
            for (int i = 0; i < 8; i++) Assert.AreEqual(expected[i], AudioSynth.SolfegeNote(i + 1), 0.01);
            Assert.AreEqual(587.33, AudioSynth.SolfegeNote(1, 1), 0.01);
            Assert.AreEqual(AudioSynth.SolfegeNote(8), AudioSynth.SolfegeNote(1, 1), 0.01, "upper do is do an octave up");
        }

        [Test]
        public void StarLoops_DrawFiveSixSevenAndEightPointedStars()
        {
            var five = PrayerStarSigil.StarLoops(5);
            Assert.AreEqual(1, five.Count); Assert.AreEqual(5, five[0].Length);
            var six = PrayerStarSigil.StarLoops(6);
            Assert.AreEqual(2, six.Count, "the hexagram is two triangles");
            Assert.IsTrue(six.All(l => l.Length == 3));
            Assert.AreEqual(7, PrayerStarSigil.StarLoops(7)[0].Length);
            Assert.AreEqual(8, PrayerStarSigil.StarLoops(8)[0].Length);
            Assert.AreEqual(0, PrayerStarSigil.StarLoops(0).Count);
            foreach (int n in new[] { 5, 6, 7, 8 })
                foreach (var loop in PrayerStarSigil.StarLoops(n))
                {
                    foreach (var v in loop) Assert.AreEqual(1f, v.magnitude, 1e-4f);
                    // Every point of a loop is distinct, and the loop skips around the circle (a star, not a polygon).
                    Assert.AreEqual(loop.Length, loop.Select(v => (Mathf.Round(v.x * 1000), Mathf.Round(v.y * 1000))).Distinct().Count());
                    float side = Vector2.Distance(loop[0], loop[1]);
                    float polygonSide = 2f * Mathf.Sin(Mathf.PI / n);
                    Assert.Greater(side, polygonSide * 1.4f, n + "-pointed star edge");
                }
        }

        [Test]
        public void SevenHint_IsTheOppositeFace_AndLastFaceIsRead()
        {
            Assert.AreEqual(4, DestinyGarden.Domain.PrayerStar.FaceCompletingSeven(3));
            Assert.AreEqual(6, DestinyGarden.Domain.PrayerStar.FaceCompletingSeven(1));
            Assert.AreEqual(0, DestinyGarden.Domain.PrayerStar.FaceCompletingSeven(7), "nothing completes seven after the seventh face");
            Assert.AreEqual(0, DestinyGarden.Domain.PrayerStar.FaceCompletingSeven(0));
            for (int f = 1; f <= 6; f++)
                Assert.IsTrue(DestinyGarden.Domain.PrayerStar.IsLuckyNumber(DestinyGarden.Domain.PrayerStar.FaceCompletingSeven(f), f));
            var h = new List<RoundSummary>
            {
                new RoundSummary { WorldRevision = 0, Resolved = true },
                new RoundSummary { WorldRevision = 1, Resolved = true, FaceId = 2, PrayerStar = 6 },
                new RoundSummary { WorldRevision = 1, Resolved = false, FaceId = 5 }
            };
            Assert.AreEqual(2, PrayerStarSigil.LastFace(h), "an unresolved round is not the last face");
            Assert.AreEqual(0, PrayerStarSigil.LastFace(new List<RoundSummary>()));
        }

        [Test]
        public void RollBell_IsUpperDo()
        {
            // The bell's fundamental (largest partial) sits at D5 so it agrees with the face notes.
            var s = AudioSynth.Bell();
            int n = 8192, start = (int)(0.02 * AudioSynth.Rate);
            double best = 0, bestF = 0;
            for (double f = 500; f <= 680; f += 0.5)
            {
                double re = 0, im = 0;
                for (int i = 0; i < n; i++)
                {
                    double ph = 2 * Math.PI * f * i / AudioSynth.Rate;
                    re += s[start + i] * Math.Cos(ph); im += s[start + i] * Math.Sin(ph);
                }
                double m = re * re + im * im;
                if (m > best) { best = m; bestF = f; }
            }
            Assert.AreEqual(587.33, bestF, 3.0);
        }

        [Test]
        public void LastStar_ReadsTheLastResolvedRoll()
        {
            Assert.AreEqual(5, PrayerStarSigil.LastStar(new List<RoundSummary>()));
            var h = new List<RoundSummary>
            {
                new RoundSummary { WorldRevision = 0, Resolved = true },
                new RoundSummary { WorldRevision = 1, Resolved = true, PrayerStar = 7 },
                new RoundSummary { WorldRevision = 1, Resolved = false }
            };
            Assert.AreEqual(7, PrayerStarSigil.LastStar(h));
            h.Add(new RoundSummary { WorldRevision = 2, Resolved = true, PrayerStar = 0 });
            Assert.AreEqual(5, PrayerStarSigil.LastStar(h), "older records without a prayer star show the star of life");
        }
    }
}
