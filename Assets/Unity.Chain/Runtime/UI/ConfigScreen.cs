using System;
using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Chain
{
    /// <summary>
    /// frmConfig.vb as a UI Toolkit panel built in code (UNI-T59), which <see cref="GameHud"/> puts over its own layers
    /// and opens on F12 and the menu's "F12 - Configure Video". It lays out the form's two groups: Video (Display Mode
    /// windowed or fullscreen, the Resolution list enabled only in fullscreen, Default and Submit) and Sound (Music, Sfx
    /// and Ambient Volume sliders), centred at hud.json's <c>config.size</c>, with a close box as the form's own. The
    /// controls show and change a Core <see cref="ConfigForm"/>; Submit raises <see cref="Submitted"/>, the close box
    /// <see cref="CloseRequested"/>, and the HUD saves and applies (it changes nothing itself).
    /// </summary>
    public sealed class ConfigScreen : VisualElement
    {
        public const string WindowedLabel = "Windowed";
        public const string FullscreenLabel = "Fullscreen";

        private static readonly Color Back = new Color(0.10f, 0.10f, 0.12f, 0.94f);
        private static readonly Color Edge = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color Text = new Color(0.92f, 0.92f, 0.92f, 1f);

        private readonly RadioButtonGroup _mode;
        private readonly DropdownField _resolution;
        private readonly Slider _music;
        private readonly Slider _sfx;
        private readonly Slider _ambient;

        /// <param name="data">hud.json's <c>config</c>.</param>
        /// <param name="scale">Original-window pixels to panel pixels (<see cref="GameHud.Scale"/>).</param>
        public ConfigScreen(ConfigScreenData data, float scale)
        {
            Form = new ConfigForm(data);
            name = "hud-config";
            pickingMode = PickingMode.Position;

            style.position = Position.Absolute;
            style.left = Length.Percent(50);
            style.top = Length.Percent(50);
            style.width = data.Size.X * scale;
            style.minHeight = data.Size.Y * scale;
            style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            style.backgroundColor = Back;
            SetBorder(this, Edge, 1f);
            style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = 4f * scale;
            style.color = Text;
            style.fontSize = 8.25f * 96f / 72f * scale; // WinForms' default font, 8.25 pt

            // The form's caption ("Configure") and its close box.
            var caption = new VisualElement { name = "config-caption" };
            caption.style.flexDirection = FlexDirection.Row;
            caption.style.justifyContent = Justify.SpaceBetween;
            caption.style.alignItems = Align.Center;
            caption.Add(new Label("Configure") { name = "config-title" });
            var close = new Button(() => CloseRequested?.Invoke()) { name = "config-close", text = "X" };
            caption.Add(close);
            Add(caption);

            var groups = new VisualElement { name = "config-groups" };
            groups.style.flexDirection = FlexDirection.Row;
            groups.style.alignItems = Align.FlexStart;
            Add(groups);

            // fraVideo: fra_mode, fra_res, then btnDefault and btnSubmit.
            var video = Group("config-video", "Video");
            video.style.flexGrow = 1f;
            video.style.flexBasis = 0f;
            _mode = new RadioButtonGroup("Display Mode", new List<string> { WindowedLabel, FullscreenLabel }) { name = "config-mode" };
            _mode.RegisterValueChangedCallback(e => Form.SetFullscreen(e.newValue == 1));
            Stack(_mode);
            video.Add(_mode);

            _resolution = new DropdownField("Resolution", data.Resolutions.Select(r => r.Label).ToList(), 0) { name = "config-resolution" };
            _resolution.RegisterValueChangedCallback(_ =>
            {
                if (!Form.SelectResolution(_resolution.index))
                {
                    Refresh();
                }
            });
            Stack(_resolution);
            video.Add(_resolution);

            var buttons = new VisualElement { name = "config-buttons" };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(Form.Default) { name = "config-default", text = "Default" });
            buttons.Add(new Button(Submit) { name = "config-submit", text = "Submit" });
            video.Add(buttons);
            groups.Add(video);

            // fraSound: Music, Sfx, then Ambient, top to bottom (GroupBox3, 2, 1).
            var sound = Group("config-sound", "Sound");
            sound.style.flexGrow = 1f;
            sound.style.flexBasis = 0f;
            _music = Volume(sound, "config-music", "Music Volume", Form.SetMusicVolume);
            _sfx = Volume(sound, "config-sfx", "Sfx Volume", Form.SetSfxVolume);
            _ambient = Volume(sound, "config-ambient", "Ambient Volume", Form.SetAmbientVolume);
            groups.Add(sound);

            Form.Changed += Refresh;
            Refresh();
        }

        /// <summary>What the controls hold.</summary>
        public ConfigForm Form { get; }

        /// <summary>Submit was pressed: the HUD writes <see cref="Form"/> into the profile, saves and applies it.</summary>
        public event Action Submitted;

        /// <summary>The close box was pressed: the HUD closes the screen without keeping anything.</summary>
        public event Action CloseRequested;

        /// <summary><c>btnSubmit_Click</c>.</summary>
        public void Submit() => Submitted?.Invoke();

        /// <summary>Shows the form's values in the controls (without raising their change callbacks).</summary>
        public void Refresh()
        {
            _mode.SetValueWithoutNotify(Form.Fullscreen ? 1 : 0);
            _resolution.SetValueWithoutNotify(Form.Resolution.Label);
            _resolution.SetEnabled(Form.ResolutionEnabled);
            _music.SetValueWithoutNotify(Form.MusicVolume);
            _sfx.SetValueWithoutNotify(Form.SfxVolume);
            _ambient.SetValueWithoutNotify(Form.AmbientVolume);
        }

        private static VisualElement Group(string name, string title)
        {
            var group = new VisualElement { name = name };
            group.style.marginLeft = group.style.marginRight = group.style.marginTop = group.style.marginBottom = 4f;
            group.style.paddingLeft = group.style.paddingRight = group.style.paddingTop = group.style.paddingBottom = 4f;
            SetBorder(group, Edge, 1f);
            var heading = new Label(title) { name = name + "-title" };
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            group.Add(heading);
            return group;
        }

        private static Slider Volume(VisualElement parent, string name, string label, Action<float> set)
        {
            // The caption is its own label above a label-less slider: a stacked BaseSlider lays its absolute tracker out
            // against the row it expects, so the track drifts under the next caption.
            var caption = new Label(label) { name = name + "-label" };
            caption.style.marginTop = 4f;
            caption.style.marginLeft = 3f;
            parent.Add(caption);
            var slider = new Slider(0f, 1f) { name = name, showInputField = false };
            slider.style.marginTop = 2f;
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            parent.Add(slider);
            return slider;
        }

        /// <summary>The field's label above it, as frmConfig's group boxes caption their control, rather than beside it.</summary>
        private static void Stack<T>(BaseField<T> field)
        {
            field.style.flexDirection = FlexDirection.Column;
            field.style.alignItems = Align.Stretch;
            field.style.marginTop = 4f;
            field.labelElement.style.minWidth = 0f;
            field.labelElement.style.width = StyleKeyword.Auto;
            field.labelElement.style.marginBottom = 2f;
        }

        private static void SetBorder(VisualElement element, Color colour, float width)
        {
            element.style.borderLeftColor = element.style.borderRightColor = colour;
            element.style.borderTopColor = element.style.borderBottomColor = colour;
            element.style.borderLeftWidth = element.style.borderRightWidth = width;
            element.style.borderTopWidth = element.style.borderBottomWidth = width;
        }
    }
}
