using System;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// All sound is synthesized in code (no external assets). One bell per roll,
    /// at most one nature layer at a time, following the design's quiet limits.
    /// </summary>
    public sealed class GardenAudio : MonoBehaviour
    {
        private AudioSource _bell;
        private AudioSource _nature;
        private AudioSource _ambience;
        public float Volume = 0.8f;
        public bool Muted;

        private void Awake()
        {
            _bell = gameObject.AddComponent<AudioSource>();
            _nature = gameObject.AddComponent<AudioSource>();
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.clip = MakeAmbience();
            _ambience.volume = 0.12f;
            _ambience.Play();
        }

        private void Update()
        {
            float v = Muted ? 0f : Volume;
            _bell.volume = v;
            _nature.volume = v * 0.8f;
            _ambience.volume = v * 0.12f;
        }

        public void PlayBell()
        {
            _bell.Stop();
            _bell.clip = MakeBell();
            _bell.Play();
        }

        public void PlayNature(string element)
        {
            _nature.Stop();
            _nature.clip = element switch
            {
                "water" => MakeNoiseBurst(1.6f, 900f),
                "wind" => MakeNoiseBurst(2.2f, 500f),
                "light" => MakeShimmer(),
                "seed" => MakePluck(660f),
                _ => MakePluck(520f)
            };
            _nature.Play();
        }

        public void PlayGrowthAccent()
        {
            _bell.PlayOneShot(MakePluck(880f), 0.5f);
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float[] SineBurst(float seconds, Func<double, double> wave)
        {
            int n = (int)(44100 * seconds);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / 44100.0;
                double env = Math.Exp(-2.2 * t) * Math.Min(1.0, t * 40);
                s[i] = (float)(wave(t) * env * 0.5);
            }
            return s;
        }

        private static AudioClip MakeBell()
        {
            return MakeClip("bell", SineBurst(2.4f, t =>
                Math.Sin(2 * Math.PI * 523.25 * t) * 0.7 +
                Math.Sin(2 * Math.PI * 1567.98 * t) * 0.25 +
                Math.Sin(2 * Math.PI * 2637.02 * t) * 0.12));
        }

        private static AudioClip MakeShimmer()
        {
            return MakeClip("shimmer", SineBurst(1.8f, t =>
                Math.Sin(2 * Math.PI * (1180 + 320 * t) * t) * 0.5 +
                Math.Sin(2 * Math.PI * 1760 * t) * 0.2));
        }

        private static AudioClip MakePluck(float freq)
        {
            return MakeClip("pluck", SineBurst(0.9f, t =>
                Math.Sin(2 * Math.PI * freq * t) * 0.7 + Math.Sin(2 * Math.PI * freq * 2 * t) * 0.2));
        }

        private static AudioClip MakeNoiseBurst(float seconds, float cutoff)
        {
            int n = (int)(44100 * seconds);
            var s = new float[n];
            var rng = new System.Random(12345);
            double lp = 0;
            double alpha = cutoff / 44100.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / 44100.0;
                double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
                double white = rng.NextDouble() * 2 - 1;
                lp += alpha * (white - lp);
                s[i] = (float)(lp * env * 1.4);
            }
            return MakeClip("noise", s);
        }

        private static AudioClip MakeAmbience()
        {
            int n = 44100 * 6;
            var s = new float[n];
            var rng = new System.Random(777);
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double white = rng.NextDouble() * 2 - 1;
                lp += 0.004 * (white - lp);
                double swell = 0.7 + 0.3 * Math.Sin(2 * Math.PI * i / (double)n * 3);
                s[i] = (float)(lp * swell * 1.2);
            }
            return MakeClip("ambience", s);
        }
    }
}
