using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Background music (Sound.vb's <c>playMusic</c>): one looping track on its own 2D
    /// <see cref="AudioSource"/>, started on play, optionally from a random point in the track
    /// (Sound.vb's <c>rndMusicPos</c>) so every run does not open on the same bar. Volume and
    /// mute are the player profile's music volume and mute (<see cref="PlayerProfile"/>:
    /// <c>Profiles.Current.Settings.Common</c>, stock Sound.vb's full musicVolume), separate from
    /// <see cref="ChainAudio"/>'s sound-effect ones as bMusic/bSound were, and follow them when they change.
    /// The random start is audio.json's rndMusicPos, overridable at run time. Every reset of the session
    /// jumps the playing track to a new random point, as Root.vb's Reset calls <c>rndMusicPos</c>
    /// (<see cref="JumpToRandomPoint"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Whose resets jump the music to a random point (Root.vb's Reset). Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;

        private GameSession _subscribed;

        // audio.json, read on first use; the property overrides it at run time.
        private bool _tuned;
        private bool randomStartPosition;

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

        /// <summary>The profile's music volume (0-1); setting it changes the profile's for the run (<see cref="PlayerProfile.Save"/> keeps it).</summary>
        public float Volume
        {
            get => PlayerProfile.Preferences.MusicVolume;
            set
            {
                PlayerProfile.Preferences.MusicVolume = value;
                PlayerProfile.NotifyChanged();
                Apply();
            }
        }

        /// <summary>The profile's music mute.</summary>
        public bool Muted
        {
            get => PlayerProfile.Preferences.MuteMusic;
            set
            {
                PlayerProfile.Preferences.MuteMusic = value;
                PlayerProfile.NotifyChanged();
                Apply();
            }
        }

        public bool PlayOnStart
        {
            get => playOnStart;
            set => playOnStart = value;
        }

        /// <summary>The session whose resets jump the music.</summary>
        public GameSession Session
        {
            get => session;
            set
            {
                Unsubscribe();
                session = value;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }
            }
        }

        public bool RandomStartPosition
        {
            get => Tuned().randomStartPosition;
            set => Tuned().randomStartPosition = value;
        }

        private MusicPlayer Tuned()
        {
            if (!_tuned)
            {
                randomStartPosition = SettingsStore.Current.AudioTuning.RandomMusicStart;
                _tuned = true;
            }

            return this;
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
            var preferences = PlayerProfile.Preferences;
            source.volume = preferences.MusicVolume;
            source.mute = preferences.MuteMusic;
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

            var start = Tuned().randomStartPosition ? RandomStart(music.length, Random.value) : 0f;
            Source.time = start;
            if (Application.isPlaying)
            {
                Source.Play();
            }

            return start;
        }

        public void Stop() => Source.Stop();

        /// <summary>
        /// Root.vb's Reset: <c>rndMusicPos</c> moves the track to a random point without restarting it.
        /// Does nothing unless <see cref="RandomStartPosition"/> (audio.json's rndMusicPos) is on or there
        /// is no track. Returns the new position in seconds, or -1 if it did not move.
        /// </summary>
        public float JumpToRandomPoint()
        {
            if (music == null || !Tuned().randomStartPosition)
            {
                return -1f;
            }

            var at = RandomStart(music.length, Random.value);
            Source.time = at;
            LastRandomPoint = at;
            return at;
        }

        /// <summary>Where the last <see cref="JumpToRandomPoint"/> sent the track, in seconds (-1 before any).</summary>
        public float LastRandomPoint { get; private set; } = -1f;

        private void OnRestarted() => JumpToRandomPoint();

        private void Subscribe()
        {
            if (_subscribed == session)
            {
                return;
            }

            Unsubscribe();
            if (session != null)
            {
                session.Restarted += OnRestarted;
                _subscribed = session;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Restarted -= OnRestarted;
                _subscribed = null;
            }
        }

        private void OnEnable()
        {
            PlayerProfile.Changed += Apply;
            Subscribe();
        }

        private void OnDisable()
        {
            PlayerProfile.Changed -= Apply;
            Unsubscribe();
        }

        /// <summary>A start point <paramref name="t"/> (0-1) of the way into a track of <paramref name="length"/> seconds, kept clear of its very end.</summary>
        public static float RandomStart(float length, float t)
        {
            if (length <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(t) * length * 0.999f;
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            Apply();
        }

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
