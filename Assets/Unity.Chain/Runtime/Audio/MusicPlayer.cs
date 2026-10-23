using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Background music (Sound.vb's <c>playMusic</c>): one looping track on its own 2D
    /// <see cref="AudioSource"/>, started on play, optionally from a random point in the track
    /// (Sound.vb's <c>rndMusicPos</c>) so every run does not open on the same bar. Volume and
    /// mute are separate from <see cref="ChainAudio"/>'s SFX volume, as bMusic/bSound were.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.6f;
        [SerializeField] private bool muted;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Start each run from a random point in the track (Sound.vb's rndMusicPos).")]
        [SerializeField] private bool randomStartPosition;

        private AudioSource _source;

        public AudioSource Source => _source != null ? _source : _source = GetComponent<AudioSource>();

        public AudioClip Music
        {
            get => music;
            set
            {
                if (music == value)
                {
                    return;
                }

                var wasPlaying = IsPlaying;
                music = value;
                Apply();
                if (wasPlaying)
                {
                    Play();
                }
            }
        }

        public float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                Apply();
            }
        }

        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                Apply();
            }
        }

        public bool PlayOnStart
        {
            get => playOnStart;
            set => playOnStart = value;
        }

        public bool RandomStartPosition
        {
            get => randomStartPosition;
            set => randomStartPosition = value;
        }

        public bool IsPlaying => Source.isPlaying;

        /// <summary>Pushes the clip, looping, 2D, volume and mute onto the AudioSource.</summary>
        public void Apply()
        {
            var source = Source;
            if (source.clip != music)
            {
                source.clip = music;
            }

            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.mute = muted;
        }

        /// <summary>
        /// Starts the track (from a random point if <see cref="RandomStartPosition"/>). Returns
        /// the start time in seconds, or -1 if there is no track.
        /// </summary>
        public float Play()
        {
            Apply();
            if (music == null)
            {
                return -1f;
            }

            var start = randomStartPosition ? RandomStart(music.length, Random.value) : 0f;
            Source.time = start;
            if (Application.isPlaying)
            {
                Source.Play();
            }

            return start;
        }

        public void Stop() => Source.Stop();

        /// <summary>A start point <paramref name="t"/> (0-1) of the way into a track of <paramref name="length"/> seconds, kept clear of its very end.</summary>
        public static float RandomStart(float length, float t)
        {
            if (length <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(t) * length * 0.999f;
        }

        private void Awake() => Apply();

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnValidate()
        {
            if (_source != null || TryGetComponent(out _source))
            {
                Apply();
            }
        }
    }
}
