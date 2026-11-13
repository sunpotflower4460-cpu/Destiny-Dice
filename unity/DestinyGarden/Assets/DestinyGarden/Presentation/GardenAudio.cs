using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The island's sound, all synthesized (AudioSynth). A quiet bed of sea; the stream, wind in
    /// the leaves and birdsong grow with the island's vitality; crickets at night; a pad that gains
    /// a voice at each step of harmony. Events (rolls, blooms, the Lucky Seven, a new star, the
    /// rainbow) ring on top. Every layer is a function of saved state: nothing plays that did not happen.
    /// </summary>
    public sealed class GardenAudio : MonoBehaviour
    {
        private AudioSource _sea, _stream, _leaves, _crickets;
        private readonly AudioSource[] _pad = new AudioSource[4];
        private AudioSource _bell, _nature, _fx, _bird;
        public float Volume = 0.8f;
        public bool Muted;

        // Scene state (set from the saved garden by the bootstrap).
        private float _vitality, _night;
        private int _harmony;
        private bool _birds;
        private float _nextBird = 4f;

        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<AudioSource, float> _level = new Dictionary<AudioSource, float>();

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, AudioSynth.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioSource Loop(string name, float[] samples)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = MakeClip(name, samples);
            src.loop = true;
            src.volume = 0f;
            src.playOnAwake = false;
            src.Play();
            _level[src] = 0f;
            return src;
        }

        private void Awake()
        {
            _bell = gameObject.AddComponent<AudioSource>();
            _nature = gameObject.AddComponent<AudioSource>();
            _fx = gameObject.AddComponent<AudioSource>();
            _bird = gameObject.AddComponent<AudioSource>();
            _sea = Loop("sea", AudioSynth.Sea());
            _stream = Loop("stream", AudioSynth.Stream());
            _leaves = Loop("leaves", AudioSynth.LeafWind());
            _crickets = Loop("crickets", AudioSynth.Crickets());
            for (int i = 0; i < _pad.Length; i++) _pad[i] = Loop("pad" + i, AudioSynth.PadLayer(i));
        }

        private void Start() => StartCoroutine(Prewarm());

        /// <summary>Builds the common one-shots one per frame while the title card is up (no hitch later).</summary>
        private IEnumerator Prewarm()
        {
            var names = new List<string> { "bell", "water", "wind", "shimmer", "pluck880", "pluck660", "lucky" };
            for (int i = 0; i < 15; i += 2) names.Add("bloom" + i);
            names.Add("star5"); names.Add("star7"); names.Add("star8"); names.Add("rainbow");
            for (int i = 0; i < 4; i++) names.Add("chirp" + i);
            for (int i = 1; i <= 7; i++) names.Add("face" + i);
            for (int i = 5; i <= 8; i++) names.Add("chord" + i);
            foreach (string n in names)
            {
                Get(n);
                yield return null;
            }
        }

        private AudioClip Get(string name)
        {
            if (_cache.TryGetValue(name, out var c)) return c;
            float[] s;
            if (name == "bell") s = AudioSynth.Bell();
            else if (name == "water") s = AudioSynth.Water();
            else if (name == "wind") s = AudioSynth.Wind();
            else if (name == "shimmer") s = AudioSynth.Shimmer();
            else if (name == "pluck880") s = AudioSynth.Pluck(880);
            else if (name == "pluck660") s = AudioSynth.Pluck(660);
            else if (name == "pluck520") s = AudioSynth.Pluck(520);
            else if (name == "lucky") s = AudioSynth.LuckySeven();
            else if (name == "rainbow") s = AudioSynth.Rainbow();
            else if (name.StartsWith("face")) s = AudioSynth.FaceNote(int.Parse(name.Substring(4)));
            else if (name.StartsWith("chord")) s = AudioSynth.StarChord(int.Parse(name.Substring(5)));
            else if (name.StartsWith("star")) s = AudioSynth.StarUp(int.Parse(name.Substring(4)));
            else if (name.StartsWith("bloom")) s = AudioSynth.Bloom(int.Parse(name.Substring(5)));
            else if (name.StartsWith("sprout")) s = AudioSynth.Sprout(int.Parse(name.Substring(6)));
            else if (name.StartsWith("chirp")) s = AudioSynth.Chirp(int.Parse(name.Substring(5)));
            else return null;
            c = MakeClip(name, s);
            _cache[name] = c;
            return c;
        }

        /// <summary>Called whenever the saved garden is shown: sets what the island sounds like now.</summary>
        public void SetScene(float vitality, int harmony, bool birds, float night)
        {
            _vitality = Mathf.Clamp01(vitality);
            _harmony = harmony;
            _birds = birds;
            _night = Mathf.Clamp01(night);
        }

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        private void Drive(AudioSource src, float target, float master, float speed)
        {
            float cur = _level[src];
            cur = Mathf.MoveTowards(cur, target, speed * Time.unscaledDeltaTime);
            _level[src] = cur;
            src.volume = cur * master;
        }

        private void Update()
        {
            float master = Muted ? 0f : Volume;
            _bell.volume = master;
            _nature.volume = master * 0.8f;
            _fx.volume = master;
            _bird.volume = master;
            float day = 1f - _night;
            Drive(_sea, 0.13f, master, 0.2f);
            Drive(_stream, 0.04f + 0.20f * Smooth(0f, 0.5f, _vitality), master, 0.2f);
            Drive(_leaves, 0.28f * Smooth(0.25f, 0.75f, _vitality) * (0.55f + 0.45f * day), master, 0.2f);
            Drive(_crickets, 0.17f * _night * Smooth(0.3f, 0.6f, _vitality), master, 0.2f);
            for (int i = 0; i < _pad.Length; i++)
            {
                int need = 2 + i * 2; // harmony 2, 4, 6, 8
                Drive(_pad[i], _harmony >= need ? 0.075f : 0f, master, 0.05f);
            }
            if (_birds && day > 0.4f && Time.time >= _nextBird && master > 0f)
            {
                var clip = Get("chirp" + UnityEngine.Random.Range(0, 4));
                if (clip != null)
                {
                    _bird.panStereo = UnityEngine.Random.Range(-0.6f, 0.6f);
                    _bird.PlayOneShot(clip, 0.34f);
                }
                _nextBird = Time.time + UnityEngine.Random.Range(3.5f, 9f);
            }
        }

        private void Shot(AudioSource src, string name, float volume = 1f)
        {
            var clip = Get(name);
            if (clip != null) src.PlayOneShot(clip, volume);
        }

        public void PlayBell() => Shot(_bell, "bell");

        public void PlayNature(string element)
        {
            switch (element)
            {
                case "water": Shot(_nature, "water"); break;
                case "wind": Shot(_nature, "wind"); break;
                case "light": Shot(_nature, "shimmer"); break;
                case "seed": Shot(_nature, "pluck660"); break;
                default: Shot(_nature, "pluck520"); break;
            }
        }

        public void PlayGrowthAccent() => Shot(_fx, "pluck880", 0.9f);

        /// <summary>A flower opened: the note of its species (up to 15 species across three octaves).</summary>
        public void PlayBloom(int speciesIndex) => Shot(_fx, "bloom" + Mathf.Clamp(speciesIndex, 0, 14), 0.9f);

        public void PlaySprout(int speciesIndex) => Shot(_fx, "sprout" + Mathf.Clamp(speciesIndex, 0, 14), 0.8f);

        public void PlayLuckySeven() => Shot(_fx, "lucky");

        public void PlayStarUp(int star) => Shot(_fx, "star" + (star >= 8 ? 8 : star >= 7 ? 7 : 5));

        public void PlayRainbow() => Shot(_fx, "rainbow");

        /// <summary>The rolled face's note (do re mi fa sol la si for faces 1..7), kept under the bell.</summary>
        public void PlayFaceNote(int face) => Shot(_fx, "face" + Mathf.Clamp(face, 1, 7), FaceNoteVolume);

        /// <summary>The prayer star's chord (5..8 points), a quiet swell beneath the garden's sounds.</summary>
        public void PlayStarChord(int points) => Shot(_fx, "chord" + Mathf.Clamp(points, 5, 8), StarChordVolume);

        public const float FaceNoteVolume = 0.42f;
        public const float StarChordVolume = 0.5f;
    }
}
