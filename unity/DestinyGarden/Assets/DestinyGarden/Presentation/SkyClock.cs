using UnityEngine;

namespace DestinyGarden.Presentation
{
    public struct SkyState
    {
        public Color Sun, Fill, Ambient, Haze, SeaTint;
        public float SunIntensity, FillIntensity, Elevation, Azimuth, Night, Bloom;
    }

    /// <summary>
    /// Time of day, driven only by the number of saved rolls: each roll moves the island a sixteenth of
    /// a day, so a visitor who rolls fifteen times sees morning, noon, gold, dusk and a moonlit night.
    /// Presentation only; the same saved garden always shows the same hour.
    /// </summary>
    public static class SkyClock
    {
        public const int RollsPerDay = 16;
        public const float StartPhase = 0.16f; // a clear morning

        public static float PhaseFor(float rolls) => Mathf.Repeat(StartPhase + rolls / RollsPerDay, 1f);

        private struct Key
        {
            public float At; public Color Sun, Fill, Ambient, Haze, Sea;
            public float SunI, FillI, Elev, Az, Night, Bloom;
        }

        private static Key K(float at, Color sun, float sunI, float elev, float az, Color fill, float fillI,
            Color ambient, Color haze, Color sea, float night, float bloom) =>
            new Key { At = at, Sun = sun, SunI = sunI, Elev = elev, Az = az, Fill = fill, FillI = fillI, Ambient = ambient,
                Haze = haze, Sea = sea, Night = night, Bloom = bloom };

        private static readonly Key[] Keys =
        {
            // dawn
            K(0.00f, new Color(1.00f, 0.68f, 0.54f), 1.70f, 15f, -75f, new Color(0.45f, 0.40f, 0.62f), 0.55f,
                new Color(0.62f, 0.57f, 0.68f), new Color(0.56f, 0.46f, 0.54f), new Color(0.80f, 0.70f, 0.78f), 0.15f, 0.95f),
            // morning
            K(0.16f, new Color(1.00f, 0.86f, 0.66f), 2.40f, 35f, -58f, new Color(0.21f, 0.45f, 0.60f), 0.45f,
                new Color(0.58f, 0.62f, 0.72f), new Color(0.30f, 0.40f, 0.52f), new Color(0.55f, 0.75f, 0.85f), 0.00f, 0.85f),
            // noon
            K(0.38f, new Color(1.00f, 0.97f, 0.88f), 2.60f, 62f, -42f, new Color(0.25f, 0.46f, 0.62f), 0.40f,
                new Color(0.62f, 0.67f, 0.76f), new Color(0.34f, 0.46f, 0.58f), new Color(0.56f, 0.80f, 0.90f), 0.00f, 0.80f),
            // golden afternoon
            K(0.60f, new Color(1.00f, 0.82f, 0.55f), 2.30f, 30f, -25f, new Color(0.30f, 0.36f, 0.55f), 0.50f,
                new Color(0.62f, 0.58f, 0.62f), new Color(0.42f, 0.42f, 0.50f), new Color(0.72f, 0.74f, 0.74f), 0.00f, 0.95f),
            // sunset
            K(0.76f, new Color(1.00f, 0.68f, 0.50f), 2.0f, 17f, -8f, new Color(0.40f, 0.32f, 0.60f), 0.60f,
                new Color(0.62f, 0.52f, 0.62f), new Color(0.52f, 0.36f, 0.46f), new Color(0.82f, 0.66f, 0.68f), 0.35f, 1.05f),
            // moonlit night (kept bright enough to read: a luminous blue)
            K(0.88f, new Color(0.62f, 0.72f, 1.00f), 1.10f, 40f, -30f, new Color(0.22f, 0.30f, 0.55f), 0.55f,
                new Color(0.36f, 0.43f, 0.64f), new Color(0.13f, 0.18f, 0.34f), new Color(0.34f, 0.46f, 0.74f), 1.00f, 1.20f),
            // back to dawn
            K(1.00f, new Color(1.00f, 0.68f, 0.54f), 1.70f, 15f, -75f, new Color(0.45f, 0.40f, 0.62f), 0.55f,
                new Color(0.62f, 0.57f, 0.68f), new Color(0.56f, 0.46f, 0.54f), new Color(0.80f, 0.70f, 0.78f), 0.15f, 0.95f),
        };

        public static SkyState Evaluate(float phase)
        {
            phase = Mathf.Repeat(phase, 1f);
            int i = 0;
            while (i < Keys.Length - 2 && phase > Keys[i + 1].At) i++;
            var a = Keys[i]; var b = Keys[i + 1];
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.At, b.At, phase));
            return new SkyState
            {
                Sun = Color.Lerp(a.Sun, b.Sun, t), Fill = Color.Lerp(a.Fill, b.Fill, t),
                Ambient = Color.Lerp(a.Ambient, b.Ambient, t), Haze = Color.Lerp(a.Haze, b.Haze, t),
                SeaTint = Color.Lerp(a.Sea, b.Sea, t),
                SunIntensity = Mathf.Lerp(a.SunI, b.SunI, t), FillIntensity = Mathf.Lerp(a.FillI, b.FillI, t),
                Elevation = Mathf.Lerp(a.Elev, b.Elev, t), Azimuth = Mathf.Lerp(a.Az, b.Az, t),
                Night = Mathf.Lerp(a.Night, b.Night, t), Bloom = Mathf.Lerp(a.Bloom, b.Bloom, t)
            };
        }

        /// <summary>Where the decorative sun (0) or moon (1) hangs in the sky behind the island; y below 0 = below the horizon.</summary>
        public static Vector3 BodyPosition(float phase, bool moon)
        {
            // The sun is up from 0.02 to 0.80; the moon from 0.78 to 1.04 (wrapping past dawn).
            float p = Mathf.Repeat(phase, 1f);
            float frac;
            if (!moon) frac = Mathf.InverseLerp(0.02f, 0.80f, p);
            else frac = Mathf.InverseLerp(0.78f, 1.04f, p < 0.5f ? p + 1f : p);
            float ang = Mathf.PI * (1f - frac);
            // Inside the sky dome (radius 45 around the island), behind the island as seen from the camera.
            return new Vector3(7.5f + Mathf.Cos(ang) * 28f, 2.5f + Mathf.Sin(ang) * 10.5f, 7.5f + 26f);
        }
    }

    /// <summary>
    /// Watches frame times and says when to step quality down (slow GPUs on the web). Pure logic:
    /// feed it frame times; it returns true once when the average over a window is too slow. After a
    /// warm-up and a cool-down, so loading hitches or a hidden tab never trigger it.
    /// </summary>
    public sealed class QualityMonitor
    {
        public const float Window = 3f, Warmup = 5f, Cooldown = 4f, MinFps = 30f;
        public int Level { get; private set; }
        public const int MaxLevel = 3;
        private float _time, _acc, _wait; private int _frames;

        public bool Feed(float dt)
        {
            if (dt <= 0f || dt > 0.5f) return false; // pause, tab switch or a loading hitch
            _time += dt;
            if (_time < Warmup) return false;
            if (_wait > 0f) { _wait -= dt; _acc = 0f; _frames = 0; return false; }
            _acc += dt; _frames++;
            if (_acc < Window) return false;
            float fps = _frames / _acc;
            _acc = 0f; _frames = 0;
            if (fps < MinFps && Level < MaxLevel)
            {
                Level++;
                _wait = Cooldown;
                return true;
            }
            return false;
        }
    }
}
