using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>One of frmConfig.vb's <c>cbo_resolution</c> items: its label ("640 x 480") and the size it picks.</summary>
    public readonly struct ConfigResolution
    {
        public ConfigResolution(string label, DisplayResolution size)
        {
            Label = label;
            Size = size;
        }

        public string Label { get; }
        public DisplayResolution Size { get; }

        public override string ToString() => Label;
    }

    /// <summary>
    /// hud.json's <c>config</c>: frmConfig.vb's form. <c>ClientSize</c> 296 x 262 (shown centred, in pixels of the
    /// original's window like the rest of the HUD), <c>cbo_resolution</c>'s items, and what <c>btnDefault</c> picks
    /// (windowed, 640 x 480; its 16-bit colour depth is dropped, a Unity build has no colour depth to pick).
    /// </summary>
    public sealed class ConfigScreenData
    {
        private ConfigScreenData(Vec2 size, ConfigResolution[] resolutions, WindowMode defaultWindowMode, int defaultResolution)
        {
            Size = size;
            Resolutions = resolutions;
            DefaultWindowMode = defaultWindowMode;
            DefaultResolution = defaultResolution;
        }

        /// <summary>frmConfig's <c>ClientSize</c>: 296 x 262 pixels.</summary>
        public Vec2 Size { get; }

        /// <summary><c>cbo_resolution</c>'s items, in order: 640 x 480, 800 x 600, 1024 x 768.</summary>
        public IReadOnlyList<ConfigResolution> Resolutions { get; }

        /// <summary><c>btnDefault</c>'s <c>opt_window.Checked = True</c>: windowed.</summary>
        public WindowMode DefaultWindowMode { get; }

        /// <summary><c>btnDefault</c>'s <c>cbo_resolution.Items.Item(0)</c>: the index into <see cref="Resolutions"/>.</summary>
        public int DefaultResolution { get; }

        /// <summary>The index of <paramref name="size"/> in <see cref="Resolutions"/>, or -1.</summary>
        public int IndexOf(DisplayResolution size)
        {
            for (var i = 0; i < Resolutions.Count; i++)
            {
                if (Resolutions[i].Size.Equals(size))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static ConfigScreenData Read(SettingsReader parent)
        {
            var r = parent.Section("config");
            var size = r.Pair("size", 1f);

            var list = r.Section("resolutions");
            var names = list.Names();
            var resolutions = new ConfigResolution[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                var pair = list.Pair(names[i], DisplayResolution.Min, DisplayResolution.Max);
                resolutions[i] = new ConfigResolution(names[i], new DisplayResolution((int)pair.X, (int)pair.Y));
            }

            list.Done();
            r.Check(resolutions.Length > 0, "resolutions", "must list at least one resolution");

            var defaults = r.Section("default");
            var mode = defaults.Choice<WindowMode>("windowMode");
            defaults.Check(mode == WindowMode.Windowed || mode == WindowMode.Fullscreen, "windowMode", "must be Windowed or Fullscreen (frmConfig's two Display Mode options)");
            var resolution = defaults.Text("resolution");
            var index = Array.FindIndex(resolutions, x => x.Label == resolution);
            defaults.Check(index >= 0 || resolution.Length == 0, "resolution", $"must name one of resolutions (is \"{resolution}\")");
            defaults.Done();
            r.Done();

            return new ConfigScreenData(size, resolutions, mode, Math.Max(0, index));
        }
    }

    /// <summary>
    /// frmConfig.vb, the settings screen F12 and the menu's "F12 - Configure Video" open (UNI-T59): what the form's
    /// controls hold while it is open. <see cref="Load"/> is <c>frm_config_Load</c> (from the player's profile rather
    /// than video.cfg), <see cref="Default"/> is <c>btnDefault_Click</c>, <see cref="Submit"/> is <c>btnSubmit_Click</c>
    /// (into the profile rather than video.cfg; the screen puts it into effect at once instead of "Settings will not take
    /// effect UNTIL next restart", BIBLE V21). The resolution list is enabled only in fullscreen
    /// (<c>fra_res.Enabled</c>), as the original's windowed engine ignored it. The Sound group's Music, Sfx and Ambient
    /// sliders were disabled and never read in the original; here they set the profile's volumes (BIBLE D28). The colour
    /// depth is not ported (a Unity build has none to choose).
    /// </summary>
    public sealed class ConfigForm
    {
        private int _resolution;
        private float _music;
        private float _sfx;
        private float _ambient;

        public ConfigForm(ConfigScreenData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Default();
        }

        /// <summary>hud.json's <c>config</c>.</summary>
        public ConfigScreenData Data { get; }

        /// <summary>Raised after any value changes (a control, <see cref="Load"/> or <see cref="Default"/>).</summary>
        public event Action Changed;

        /// <summary><c>opt_fullscreen.Checked</c>; false is <c>opt_window</c>.</summary>
        public bool Fullscreen { get; private set; }

        /// <summary><c>fra_res.Enabled</c>: the resolution can be picked only in fullscreen.</summary>
        public bool ResolutionEnabled => Fullscreen;

        /// <summary><c>cbo_resolution</c>'s selected index into <see cref="ConfigScreenData.Resolutions"/>.</summary>
        public int ResolutionIndex => _resolution;

        /// <summary>The selected resolution.</summary>
        public ConfigResolution Resolution => Data.Resolutions[_resolution];

        /// <summary>The Music Volume slider, a 0-1 gain.</summary>
        public float MusicVolume => _music;

        /// <summary>The Sfx Volume slider, a 0-1 gain.</summary>
        public float SfxVolume => _sfx;

        /// <summary>The Ambient Volume slider, a 0-1 gain.</summary>
        public float AmbientVolume => _ambient;

        /// <summary><c>opt_window</c> / <c>opt_fullscreen</c>.</summary>
        public void SetFullscreen(bool fullscreen)
        {
            if (Fullscreen != fullscreen)
            {
                Fullscreen = fullscreen;
                Changed?.Invoke();
            }
        }

        /// <summary><c>cbo_resolution_SelectedIndexChanged</c>; ignored while the list is disabled (windowed). Returns whether it took.</summary>
        public bool SelectResolution(int index)
        {
            if (!ResolutionEnabled || index < 0 || index >= Data.Resolutions.Count)
            {
                return false;
            }

            if (_resolution != index)
            {
                _resolution = index;
                Changed?.Invoke();
            }

            return true;
        }

        public void SetMusicVolume(float value) => SetVolume(ref _music, value);
        public void SetSfxVolume(float value) => SetVolume(ref _sfx, value);
        public void SetAmbientVolume(float value) => SetVolume(ref _ambient, value);

        /// <summary>
        /// <c>frm_config_Load</c>: the controls show the player's settings. The display mode is the profile's (fullscreen
        /// or borderless count as fullscreen), else the screen's own (<paramref name="fullScreenNow"/>); the resolution is
        /// the profile's if it is one of the list's, else the default one (the original left the list blank and wrote 0).
        /// </summary>
        public void Load(PlayerPreferences preferences, bool fullScreenNow)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            Fullscreen = preferences.WindowMode.HasValue
                ? preferences.WindowMode.Value == WindowMode.Fullscreen || preferences.WindowMode.Value == WindowMode.Borderless
                : fullScreenNow;
            var index = preferences.Resolution.HasValue ? Data.IndexOf(preferences.Resolution.Value) : -1;
            _resolution = index >= 0 ? index : Data.DefaultResolution;
            _music = preferences.MusicVolume;
            _sfx = preferences.SfxVolume;
            _ambient = preferences.AmbientVolume;
            Changed?.Invoke();
        }

        /// <summary>
        /// <c>btnDefault_Click</c>: windowed at 640 x 480 (hud.json's <c>config.default</c>). Like the original it only
        /// sets the controls; nothing is kept until <see cref="Submit"/>. It never touched the Sound group, nor does this.
        /// </summary>
        public void Default()
        {
            Fullscreen = Data.DefaultWindowMode == WindowMode.Fullscreen;
            _resolution = Data.DefaultResolution;
            Changed?.Invoke();
        }

        /// <summary>
        /// <c>btnSubmit_Click</c>: writes the controls into <paramref name="preferences"/> (window mode, resolution and the
        /// three volumes); the caller saves them to the profile and puts them into effect.
        /// </summary>
        public void Submit(PlayerPreferences preferences)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            preferences.WindowMode = Fullscreen ? WindowMode.Fullscreen : WindowMode.Windowed;
            preferences.Resolution = Resolution.Size;
            preferences.MusicVolume = _music;
            preferences.SfxVolume = _sfx;
            preferences.AmbientVolume = _ambient;
        }

        private void SetVolume(ref float field, float value)
        {
            var v = float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
            if (field != v)
            {
                field = v;
                Changed?.Invoke();
            }
        }
    }
}
