using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>The lines of Gui.vb's renderMenu, top to bottom, each named for what its key does.</summary>
    public enum MenuEntry
    {
        /// <summary>F1: <c>wireframe = Not wireframe</c> (Root.vb's bRenderWireframe).</summary>
        Wireframe,

        /// <summary>F2: <c>useTextures = Not useTextures</c> (bRenderTextures).</summary>
        Textures,

        /// <summary>F3: <c>backFaceCulling = Not backFaceCulling</c> (bBackFaceCulling).</summary>
        Culling,

        /// <summary>F4: <c>drawFrameRate = Not drawFrameRate</c> (bDrawFPS).</summary>
        Fps,

        /// <summary>F12: <c>toggleScreen = True</c>, the window switches between windowed and fullscreen.</summary>
        Video,

        /// <summary>Escape: <c>bRunning = False</c>, the game ends (the world's <see cref="QuitGame"/>).</summary>
        Quit,
    }

    /// <summary>
    /// Root.vb's option flags, the screen's own view state (they belong to no player and change nothing in the world):
    /// <c>bRenderWireframe</c> (F1), <c>bRenderTextures</c> (F2), <c>bBackFaceCulling</c> (F3), <c>bDrawFPS</c> (F4)
    /// and <c>bMenu</c> (Tab). Root.vb's <c>Reset</c> puts them all back (<see cref="Reset"/>): solid, textured, no
    /// culling, the frame rate shown, the menu closed. The renderer reads the first three (UNI-T56), the HUD the rest.
    /// </summary>
    public sealed class ViewOptions
    {
        private readonly bool _drawFpsAtReset;

        /// <param name="drawFps">hud.json's <c>drawFps</c>: Root.vb's <c>bDrawFPS = True</c> at the start and on every reset.</param>
        public ViewOptions(bool drawFps = true)
        {
            _drawFpsAtReset = drawFps;
            Reset();
        }

        /// <summary>Raised after a flag changes by a toggle (not by <see cref="Reset"/>), with its entry and new state.</summary>
        public event Action<MenuEntry, bool> Toggled;

        /// <summary>Raised after <see cref="ToggleMenu"/> or <see cref="Reset"/> changes whether the menu is open.</summary>
        public event Action<bool> MenuChanged;

        /// <summary>bRenderWireframe: draw the scene as wireframe (F1). False at the start.</summary>
        public bool Wireframe { get; private set; }

        /// <summary>bRenderTextures: draw the scene textured rather than shaded (F2). True at the start.</summary>
        public bool Textured { get; private set; }

        /// <summary>bBackFaceCulling: cull back faces (F3). False at the start (Form1.vb Startup sets it False too).</summary>
        public bool BackFaceCulling { get; private set; }

        /// <summary>bDrawFPS: draw the frame rate (F4). hud.json's <c>drawFps</c> (True) at the start.</summary>
        public bool DrawFps { get; set; }

        /// <summary>bMenu: draw the menu panel (Tab). False at the start.</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>Root.vb's Reset: every flag back to how the game starts. Raises <see cref="MenuChanged"/> if it closes the menu.</summary>
        public void Reset()
        {
            var wasOpen = MenuOpen;
            Wireframe = false;
            Textured = true;
            BackFaceCulling = false;
            DrawFps = _drawFpsAtReset;
            MenuOpen = false;
            if (wasOpen)
            {
                MenuChanged?.Invoke(false);
            }
        }

        /// <summary>Form1.vb's Tab: <c>showMenu = Not showMenu</c>. Returns the new state.</summary>
        public bool ToggleMenu()
        {
            MenuOpen = !MenuOpen;
            MenuChanged?.Invoke(MenuOpen);
            return MenuOpen;
        }

        /// <summary>Whether <paramref name="entry"/> is a flag this holds (F1-F4); F12 and Escape act elsewhere.</summary>
        public static bool IsFlag(MenuEntry entry) => entry <= MenuEntry.Fps;

        /// <summary>The flag behind <paramref name="entry"/> (false for F12 and Escape, which have none).</summary>
        public bool this[MenuEntry entry]
        {
            get
            {
                switch (entry)
                {
                    case MenuEntry.Wireframe: return Wireframe;
                    case MenuEntry.Textures: return Textured;
                    case MenuEntry.Culling: return BackFaceCulling;
                    case MenuEntry.Fps: return DrawFps;
                    default: return false;
                }
            }
        }

        /// <summary>
        /// Form1.vb's F1-F4: flips the flag behind <paramref name="entry"/> and returns its new state. F12 and Escape
        /// hold no flag here (the screen and the world act on them), so they throw.
        /// </summary>
        public bool Toggle(MenuEntry entry)
        {
            bool value;
            switch (entry)
            {
                case MenuEntry.Wireframe: value = Wireframe = !Wireframe; break;
                case MenuEntry.Textures: value = Textured = !Textured; break;
                case MenuEntry.Culling: value = BackFaceCulling = !BackFaceCulling; break;
                case MenuEntry.Fps: value = DrawFps = !DrawFps; break;
                default: throw new ArgumentOutOfRangeException(nameof(entry), entry, "only F1-F4 are flags");
            }

            Toggled?.Invoke(entry, value);
            return value;
        }
    }

    /// <summary>
    /// Gui.vb's renderMenu wording: each line names its key and what pressing it does now, so it follows the flag
    /// (<c>IIf(bRenderWireframe, "F1 - Turn to Solid", "F1 - Turn to Wireframe")</c>). The original's F3 line is
    /// the wrong way round (it offers to enable culling while culling is on); here it names what F3 does (BIBLE D22).
    /// </summary>
    public static class GameMenu
    {
        /// <summary>Every line, in renderMenu's order.</summary>
        public static readonly IReadOnlyList<MenuEntry> Entries = new[]
        {
            MenuEntry.Wireframe, MenuEntry.Textures, MenuEntry.Culling, MenuEntry.Fps, MenuEntry.Video, MenuEntry.Quit,
        };

        /// <summary>The text of <paramref name="entry"/>'s line given the flags in <paramref name="options"/>.</summary>
        public static string Line(MenuEntry entry, ViewOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            switch (entry)
            {
                case MenuEntry.Wireframe: return options.Wireframe ? "F1 - Turn to Solid" : "F1 - Turn to Wireframe";
                case MenuEntry.Textures: return options.Textured ? "F2 - Use Shaded" : "F2 - Use Textured";
                case MenuEntry.Culling: return options.BackFaceCulling ? "F3 - Disable Culling" : "F3 - Enable Culling";
                case MenuEntry.Fps: return options.DrawFps ? "F4 - Hide FPS" : "F4 - Show FPS";
                case MenuEntry.Video: return "F12 - Configure Video";
                case MenuEntry.Quit: return "ESC - Quit";
                default: throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
            }
        }
    }
}
