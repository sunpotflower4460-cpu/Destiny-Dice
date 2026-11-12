using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DestinyGarden.Presentation;
using UnityEngine;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class PresentationTests
    {
        private static Dictionary<string, float[]> AllSounds()
        {
            var d = new Dictionary<string, float[]>
            {
                ["sea"] = AudioSynth.Sea(), ["stream"] = AudioSynth.Stream(), ["leaves"] = AudioSynth.LeafWind(),
                ["crickets"] = AudioSynth.Crickets(), ["bell"] = AudioSynth.Bell(), ["water"] = AudioSynth.Water(),
                ["wind"] = AudioSynth.Wind(), ["shimmer"] = AudioSynth.Shimmer(), ["pluck880"] = AudioSynth.Pluck(880),
                ["lucky"] = AudioSynth.LuckySeven(), ["rainbow"] = AudioSynth.Rainbow(),
                ["star5"] = AudioSynth.StarUp(5), ["star7"] = AudioSynth.StarUp(7), ["star8"] = AudioSynth.StarUp(8),
                ["bloom0"] = AudioSynth.Bloom(0), ["bloom7"] = AudioSynth.Bloom(7), ["bloom14"] = AudioSynth.Bloom(14),
                ["sprout3"] = AudioSynth.Sprout(3), ["chirp0"] = AudioSynth.Chirp(0), ["chirp3"] = AudioSynth.Chirp(3)
            };
            for (int i = 0; i < 4; i++) d["pad" + i] = AudioSynth.PadLayer(i);
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
        public void Sounds_AreFinite_Quiet_AndDeterministic()
        {
            var a = AllSounds(); var b = AllSounds();
            string outDir = Environment.GetEnvironmentVariable("DG_AUDIO_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            foreach (var kv in a)
            {
                float peak = 0f;
                foreach (float f in kv.Value) { Assert.IsFalse(float.IsNaN(f) || float.IsInfinity(f), kv.Key + " has a non-finite sample"); peak = Math.Max(peak, Math.Abs(f)); }
                Assert.That(peak, Is.InRange(0.3f, 0.7f), kv.Key + " peak");
                CollectionAssert.AreEqual(kv.Value, b[kv.Key], kv.Key + " must be deterministic");
                if (!string.IsNullOrEmpty(outDir)) WriteWav(Path.Combine(outDir, kv.Key + ".wav"), kv.Value);
            }
        }

        [Test]
        public void TonalLoops_AreSeamless()
        {
            foreach (var name in new[] { "pad0", "pad1", "pad2", "pad3", "crickets" })
            {
                var s = AllSounds()[name];
                float seam = Math.Abs(s[0] - s[s.Length - 1]);
                Assert.Less(seam, 0.08f, name + " seam");
            }
        }

        [Test]
        public void IslandSeed_IsDeterministic_DiffersBetweenGardens_AndKeepsTheGardenClear()
        {
            var a1 = IslandSeed.FromRunId("run-aaaa"); var a2 = IslandSeed.FromRunId("run-aaaa"); var b = IslandSeed.FromRunId("run-bbbb");
            Assert.AreEqual(a1.Value, a2.Value);
            CollectionAssert.AreEqual(a1.River, a2.River);
            Assert.AreEqual(a1.Trees.Length, a2.Trees.Length);
            Assert.AreNotEqual(a1.Value, b.Value);
            int differing = 0;
            for (int i = 0; i < 40; i++)
            {
                var s = IslandSeed.FromValue(1000 + i * 7919);
                // The garden's own ground is never touched: the river hugs it at a distance and ends in the sea.
                foreach (var p in s.River) Assert.GreaterOrEqual(Vector2.Distance(p, IslandSeed.Center), 6.9f, "river stays out of the garden");
                Assert.Greater(Vector2.Distance(s.River[s.River.Length - 1], IslandSeed.Center), 12f, "river reaches the sea");
                Assert.GreaterOrEqual(s.Trees.Length, 8, "trees");
                foreach (var t in s.Trees)
                {
                    float deg = Mathf.Repeat(t.Angle * Mathf.Rad2Deg, 360f);
                    Assert.IsFalse(deg > 235f && deg < 305f, "no tree across the default front view");
                }
                foreach (var st in s.Stones) Assert.That(st.Radius, Is.InRange(9.2f, 11f));
                Assert.That(s.StartPhase, Is.InRange(0f, 1f));
                if (Mathf.Abs(Mathf.DeltaAngle(s.RidgeAngle * Mathf.Rad2Deg, a1.RidgeAngle * Mathf.Rad2Deg)) > 20f) differing++;
            }
            Assert.Greater(differing, 20, "islands should differ in where their highlands lie");
        }

        [Test]
        public void SkyClock_StartsInMorning_ReachesNight_AndWraps()
        {
            var morning = SkyClock.Evaluate(SkyClock.PhaseFor(0));
            Assert.Less(morning.Night, 0.05f);
            var night = SkyClock.Evaluate(SkyClock.PhaseFor(12));
            Assert.Greater(night.Night, 0.5f, "roll 12 of the day is already night");
            var next = SkyClock.Evaluate(SkyClock.PhaseFor(SkyClock.RollsPerDay));
            Assert.AreEqual(morning.SunIntensity, next.SunIntensity, 1e-4f, "one full day later it is morning again");
            for (int r = 0; r < 40; r++)
            {
                var s = SkyClock.Evaluate(SkyClock.PhaseFor(r));
                Assert.That(s.SunIntensity, Is.InRange(0.5f, 3f));
                Assert.That(s.Night, Is.InRange(0f, 1f));
            }
            Assert.Greater(SkyClock.BodyPosition(SkyClock.PhaseFor(0), false).y, 4f);
        }

        [Test]
        public void QualityMonitor_StepsDownOnlyWhenSlow_AfterWarmup_AndNeverPastMax()
        {
            var fast = new QualityMonitor();
            for (int i = 0; i < 60 * 20; i++) Assert.IsFalse(fast.Feed(1f / 60f));
            Assert.AreEqual(0, fast.Level);

            var slow = new QualityMonitor();
            int steps = 0;
            for (int i = 0; i < 20 * 20 * 4; i++) if (slow.Feed(1f / 20f)) steps++; // 20 fps for 80 s
            Assert.AreEqual(QualityMonitor.MaxLevel, slow.Level);
            Assert.AreEqual(QualityMonitor.MaxLevel, steps);

            var hitch = new QualityMonitor();
            for (int i = 0; i < 10; i++) Assert.IsFalse(hitch.Feed(2f), "a hidden tab or loading pause is ignored");
            Assert.AreEqual(0, hitch.Level);
        }
    }
}
