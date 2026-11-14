using DestinyGarden.Domain;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    public struct SkyState
    {
        public Color Sun, Fill, Ambient, Haze, SeaTint;
        public float SunIntensity, FillIntensity, Elevation, Azimuth, Night, Bloom;
        public Color Horizon, Zenith;
        public Vector3 SunDirection, MoonDirection;
        public float SunVisibility, MoonVisibility, Twilight, CloudCover, Moisture;
    }

    /// <summary>
    /// Time of day, driven only by the number of saved rolls: each roll moves the island a sixteenth of
    /// a day, so a visitor who rolls fifteen times sees morning, noon, gold, dusk and a moonlit night.
    /// Presentation only; the same saved garden always shows the same hour.
    /// </summary>
    public static class SkyClock
    {
        public const int RollsPerDay = 16;
        public static float StartPhase = 0.16f; // a clear morning unless the island begins at another hour

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
            K(0.00f, new Color(1.00f, 0.77f, 0.61f), 1.70f, 15f, -75f, new Color(0.54f, 0.42f, 0.70f), 0.58f,
                new Color(0.68f, 0.61f, 0.72f), new Color(0.59f, 0.46f, 0.61f), new Color(0.82f, 0.69f, 0.87f), 0.15f, 0.95f),
            // morning
            K(0.16f, new Color(1.00f, 0.82f, 0.68f), 2.12f, 35f, -58f, new Color(0.55f, 0.48f, 0.73f), 0.57f,
                new Color(0.70f, 0.65f, 0.76f), new Color(0.53f, 0.47f, 0.62f), new Color(0.71f, 0.80f, 0.91f), 0.00f, 0.89f),
            // noon
            K(0.38f, new Color(1.00f, 0.95f, 0.82f), 2.45f, 62f, -42f, new Color(0.39f, 0.53f, 0.69f), 0.49f,
                new Color(0.68f, 0.69f, 0.78f), new Color(0.44f, 0.52f, 0.67f), new Color(0.61f, 0.86f, 0.91f), 0.00f, 0.80f),
            // golden afternoon
            K(0.60f, new Color(1.00f, 0.79f, 0.59f), 2.23f, 30f, -25f, new Color(0.50f, 0.39f, 0.67f), 0.57f,
                new Color(0.67f, 0.60f, 0.72f), new Color(0.56f, 0.45f, 0.60f), new Color(0.83f, 0.74f, 0.87f), 0.00f, 0.95f),
            // sunset
            K(0.76f, new Color(1.00f, 0.71f, 0.55f), 1.90f, 17f, -8f, new Color(0.54f, 0.35f, 0.72f), 0.63f,
                new Color(0.63f, 0.54f, 0.71f), new Color(0.58f, 0.38f, 0.56f), new Color(0.86f, 0.67f, 0.85f), 0.35f, 1.05f),
            // moonlit ink and lavender shadows
            K(0.88f, new Color(0.68f, 0.73f, 1.00f), 1.10f, 40f, -30f, new Color(0.39f, 0.31f, 0.67f), 0.58f,
                new Color(0.43f, 0.44f, 0.68f), new Color(0.24f, 0.22f, 0.43f), new Color(0.54f, 0.55f, 0.80f), 1.00f, 1.20f),
            // back to dawn
            K(1.00f, new Color(1.00f, 0.77f, 0.61f), 1.70f, 15f, -75f, new Color(0.54f, 0.42f, 0.70f), 0.58f,
                new Color(0.68f, 0.61f, 0.72f), new Color(0.59f, 0.46f, 0.61f), new Color(0.82f, 0.69f, 0.87f), 0.15f, 0.95f),
        };

        public static SkyState Evaluate(float phase, WeatherId weather = WeatherId.Clear)
        {
            phase = Mathf.Repeat(phase, 1f);
            int i = 0;
            while (i < Keys.Length - 2 && phase > Keys[i + 1].At) i++;
            var a = Keys[i]; var b = Keys[i + 1];
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.At, b.At, phase));
            var sky = new SkyState
            {
                Sun = Color.Lerp(a.Sun, b.Sun, t), Fill = Color.Lerp(a.Fill, b.Fill, t),
                Ambient = Color.Lerp(a.Ambient, b.Ambient, t), Haze = Color.Lerp(a.Haze, b.Haze, t),
                SeaTint = Color.Lerp(a.Sea, b.Sea, t),
                SunIntensity = Mathf.Lerp(a.SunI, b.SunI, t), FillIntensity = Mathf.Lerp(a.FillI, b.FillI, t),
                Elevation = Mathf.Lerp(a.Elev, b.Elev, t), Azimuth = Mathf.Lerp(a.Az, b.Az, t),
                Night = Mathf.Lerp(a.Night, b.Night, t), Bloom = Mathf.Lerp(a.Bloom, b.Bloom, t)
            };
            float wet = weather == WeatherId.Drizzle ? 1f : weather == WeatherId.Cloudy ? 0.55f : 0f;
            sky.Moisture = wet;
            sky.CloudCover = Mathf.Lerp(0.12f, 0.86f, wet);
            sky.Twilight = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((phase - 0.75f) * Mathf.PI * 2f)), 13f)
                * (1f - sky.Night) * (1f - 0.46f * wet);
            float daylight = Mathf.InverseLerp(0.02f, 0.81f, phase);
            sky.SunDirection = Direction(Mathf.Lerp(-29f, 17f, daylight), 0.5f + 10f * Mathf.Sin(daylight * Mathf.PI));
            float moonPhase = Mathf.InverseLerp(0.78f, 1.03f, phase < 0.5f ? phase + 1f : phase);
            sky.MoonDirection = Direction(Mathf.Lerp(-22f, -9f, moonPhase), 1.1f + 2.8f * Mathf.Sin(moonPhase * Mathf.PI));
            sky.SunVisibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.09f, phase))
                * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.77f, 0.84f, phase)) * (1f - wet * 0.63f);
            sky.MoonVisibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.27f, 0.95f, sky.Night)) * (1f - wet * 0.56f);

            float noon = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((phase - 0.39f) * Mathf.PI * 2f)), 4f);
            var daylightHorizon = Color.Lerp(new Color(0.39f, 0.215f, 0.31f), new Color(0.32f, 0.44f, 0.59f), noon);
            var daylightZenith = Color.Lerp(new Color(0.076f, 0.055f, 0.17f), new Color(0.092f, 0.19f, 0.37f), noon);
            var dayHorizon = Color.Lerp(daylightHorizon, new Color(0.18f, 0.20f, 0.29f), wet * 0.76f);
            var dayZenith = Color.Lerp(daylightZenith, new Color(0.058f, 0.077f, 0.16f), wet * 0.76f);
            sky.Horizon = Color.Lerp(dayHorizon, new Color(0.041f, 0.046f, 0.113f), sky.Night);
            sky.Zenith = Color.Lerp(dayZenith, new Color(0.009f, 0.011f, 0.043f), sky.Night);
            sky.Horizon = Color.Lerp(sky.Horizon, new Color(0.49f, 0.215f, 0.22f), sky.Twilight * 0.84f);
            sky.Zenith = Color.Lerp(sky.Zenith, new Color(0.10f, 0.052f, 0.18f), sky.Twilight * 0.63f);
            return sky;
        }

        private static Vector3 Direction(float azimuth, float elevation)
        {
            float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
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
