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
    /// </summary>
    public sealed class InputGate
    {
        /// <summary>Form1.vb's normal keys are held back: the scripter is open (or closed this frame).</summary>
        public bool KeysSuspended { get; private set; }

        /// <summary>Input.vb's <c>getInput</c> is held back: the scripter is open, or a script is running.</summary>
        public bool PlaySuspended { get; private set; }

        /// <summary>Sets the gate from Root.vb's <c>bScripter</c> and Script.vb's <c>bScriptRunning</c>.</summary>
        public void Set(bool scripterOpen, bool scriptRunning)
        {
            KeysSuspended = scripterOpen;
            PlaySuspended = scripterOpen || scriptRunning;
        }

        /// <summary>Lets everything through.</summary>
        public void Clear() => Set(false, false);
    }
}
