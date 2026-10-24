namespace MindAttic.Chain.Core
{
    /// <summary>
    /// What the scripter holds back (U.CHN-T61). The original reads its keys two ways, and the scripter stops both:
    /// <list type="bullet">
    /// <item><see cref="KeysSuspended"/>, Form1.vb's <c>OnKeyDown</c>: while <c>bScripter</c> every key types into the
    /// console instead of doing its job (Escape, Pause and P, R, Tab, F1-F4, F12, L, B, N, J, U, M, F, E, 0, Insert,
    /// Delete, Home, End, PageUp, PageDown).</item>
    /// <item><see cref="PlaySuspended"/>, Input.vb's <c>getInput</c>: skipped while <c>bScripter</c> or while a script
    /// runs (<c>bScriptRunning</c>, <c>CHANGE SCRIPT RUNNING ON</c>): the mouse (steering and the grapple), the stick,
    /// <c>[</c> and <c>]</c>, Minus and Equals, and the camera's numpad, X and Z keys.</item>
    /// </list>
    /// The readers ask the session's gate before acting. It is the local screen's state, not the world's, and it never
    /// changes the game.
    /// <para>The online menus (U.CHN-T26, the Unity.Multiplayer kit's front end) hold both back too while one is open,
    /// so typing an address or a join code, or clicking a button, never steers, grapples or presses a game key. The
    /// scripter and the menus each set their own half (<see cref="Set"/>, <see cref="SetMenuOpen"/>).</para>
    /// </summary>
    public sealed class InputGate
    {
        private bool _scripterOpen;
        private bool _scriptRunning;
        private bool _menuOpen;

        /// <summary>Form1.vb's normal keys are held back: the scripter is open (or closed this frame), or an online menu is.</summary>
        public bool KeysSuspended => _scripterOpen || _menuOpen;

        /// <summary>Input.vb's <c>getInput</c> is held back: the scripter is open, a script is running, or an online menu is open.</summary>
        public bool PlaySuspended => _scripterOpen || _scriptRunning || _menuOpen;

        /// <summary>Whether an online menu (the kit's front end) holds input back.</summary>
        public bool MenuOpen => _menuOpen;

        /// <summary>Sets the gate from Root.vb's <c>bScripter</c> and Script.vb's <c>bScriptRunning</c>.</summary>
        public void Set(bool scripterOpen, bool scriptRunning)
        {
            _scripterOpen = scripterOpen;
            _scriptRunning = scriptRunning;
        }

        /// <summary>Sets the online menus' half of the gate (U.CHN-T26); the scripter's half is untouched.</summary>
        public void SetMenuOpen(bool open) => _menuOpen = open;

        /// <summary>Lets the scripter's half through (the menus' half is theirs to clear).</summary>
        public void Clear() => Set(false, false);
    }
}
