using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The original hid the Windows cursor and took the mouse exclusively through DirectInput
    /// (<c>Form1.Startup</c>'s <c>Windows.Forms.Cursor.Hide()</c> plus the mouse device's
    /// <c>CooperativeLevelFlags.Exclusive</c>). UNI-T38 restores that as the game's default, from the first
    /// frame, in a player build: the cursor is hidden and confined to the window rather than free to wander
    /// onto another monitor or app. <see cref="AngelController"/>'s own lock (while it is enabled) layers a
    /// tighter, centred lock on top of this for steering; this is the baseline the rest of the app -- menus,
    /// the loading screen (UNI-T58) -- runs under.
    /// </summary>
    public static class CursorPolicy
    {
        /// <summary>The lock state this policy asks for.</summary>
        public const CursorLockMode LockState = CursorLockMode.Confined;

        /// <summary>Applies the policy (player builds only; the Editor's own cursor is left alone).</summary>
        public static void Apply()
        {
            if (Application.isEditor)
            {
                return;
            }

            Cursor.lockState = LockState;
            Cursor.visible = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStart() => Apply();
    }
}
