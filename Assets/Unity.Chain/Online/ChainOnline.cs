using MindAttic.Chain.Core;
using MindAttic.Components.Unity;
using MindAttic.Components.Unity.Core;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.UI;
using UnityEngine;

namespace MindAttic.Chain.Online
{
    /// <summary>
    /// Online play beside local play (U.CHN-T26), on the Unity.Multiplayer kit's drop-in front end. The game opens
    /// exactly as before, straight into local play, with the kit's menus hidden; O (the rebuild's key: the original had
    /// no online play) opens its main menu: Host, Join by IP, Join by code, Settings, Play alone and Quit. Hosting shows
    /// the kit's lobby (the join code and the roster) and Start begins the online game; joining forwards this screen's
    /// commands to the host (<see cref="ChainPlayer"/>); Escape opens the kit's in-game menu instead of quitting while a
    /// session runs (<see cref="GameSession.HoldsQuit"/>).
    /// <para>While a menu is open it holds the game's input back (<see cref="InputGate.SetMenuOpen"/>), frees the cursor
    /// for the mouse, and, on the main menu outside a session, pauses the local game as the config screen does. The
    /// server spawns the world sync (<see cref="ChainWorldSync"/>) when its session starts, a dedicated server included.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)] // after the GameSession, before the input readers ask the gate
    public sealed class ChainOnline : MonoBehaviour
    {
        /// <summary>The main menu's button that closes the menus and goes back to local play.</summary>
        public const string PlayAloneLabel = "Play alone";

        [SerializeField] private NetFrontEnd frontEnd;
        [SerializeField] private GameSession game;
        [Tooltip("The ChainWorldSync prefab (a NetworkObject, a NetKey and ChainWorldSync) the server spawns for each session.")]
        [SerializeField] private GameObject worldSyncPrefab;

        private ChainControls _controls;
        private bool _pausedForMenu;
        private bool _cursorFreed;
        private CursorLockMode _savedLock;
        private bool _savedVisible;

        public NetFrontEnd FrontEnd
        {
            get => frontEnd;
            set => frontEnd = value;
        }

        public GameSession Game
        {
            get => game;
            set => game = value;
        }

        public GameObject WorldSyncPrefab
        {
            get => worldSyncPrefab;
            set => worldSyncPrefab = value;
        }

        /// <summary>Whether one of the kit's menus is open on this screen (the game's input is held back).</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>Whether this screen is in an online session (hosting, joined, or connecting).</summary>
        public bool InSession => frontEnd != null && frontEnd.State != FrontEndState.Menu;

        /// <summary>Opens the kit's main menu over local play (what O does).</summary>
        public void OpenMenu()
        {
            if (frontEnd != null && !InSession)
            {
                frontEnd.MenusVisible = true;
                Reconcile();
            }
        }

        /// <summary>Closes the main menu and goes back to local play (the menu's Play alone).</summary>
        public void CloseMenu()
        {
            if (frontEnd != null && !InSession)
            {
                frontEnd.MenusVisible = false;
                Reconcile();
            }
        }

        private void Awake()
        {
            if (frontEnd == null)
            {
                frontEnd = GetComponent<NetFrontEnd>();
            }

            if (game == null)
            {
                game = FindAnyObjectByType<GameSession>();
            }

            if (frontEnd == null)
            {
                return;
            }

            // Local play first, exactly as before: the menus stay hidden until O opens them.
            frontEnd.MenusVisible = false;
            frontEnd.AddMainMenuItems(s => s.AddButton(PlayAloneLabel, CloseMenu, ButtonVariant.Primary));
            frontEnd.SessionStarted += OnSessionStarted;
            frontEnd.Left += OnLeft;
            frontEnd.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (frontEnd != null)
            {
                frontEnd.SessionStarted -= OnSessionStarted;
                frontEnd.Left -= OnLeft;
                frontEnd.StateChanged -= OnStateChanged;
            }

            _controls?.Dispose();
            _controls = null;
        }

        private void OnEnable()
        {
            _controls ??= new ChainControls();
            _controls.OnlineMenu.Enable();
        }

        private void OnDisable()
        {
            _controls?.OnlineMenu.Disable();
            if (game != null)
            {
                game.InputGate.SetMenuOpen(false);
            }

            RestoreCursor();
        }

        private void Update()
        {
            // O opens the menu over local play. Closing is the menu's own Play alone, so typing an "o" into a field never closes it.
            if (_controls != null && _controls.OnlineMenu.WasPressedThisFrame() && !MenuOpen
                && !GameSession.KeysSuspended(game))
            {
                OpenMenu();
            }

            Reconcile();
        }

        private void OnSessionStarted(NetSession session)
        {
            if (game != null)
            {
                game.HoldsQuit = true;
            }

            // A headless server (-mode server) is paced to the tick rate once it serves, not at launch, so a headless
            // local run (batch-mode tests) keeps the frame pacing it always had.
            if (session.IsServer && HeadlessBootstrap.IsHeadless && NetBootstrap.Instance != null)
            {
                HeadlessBootstrap.Apply(NetBootstrap.Instance.BuildConfig().TickRate);
            }

            if (session.IsServer && worldSyncPrefab != null)
            {
                ChainWorldSync.Serve(session, worldSyncPrefab);
            }
        }

        private void OnLeft(Disconnection why)
        {
            if (game == null)
            {
                return;
            }

            game.HoldsQuit = false;
            if (game.IsRemote)
            {
                // Back from someone else's game: this screen's own world starts over, as local play.
                game.Forward = null;
                game.Restart();
            }
        }

        private void OnStateChanged(FrontEndState state) => Reconcile();

        /// <summary>Sets the input gate, the cursor and the menu pause from what the front end shows now.</summary>
        private void Reconcile()
        {
            var open = frontEnd != null && frontEnd.HasMenus
                && ((frontEnd.MenusVisible && frontEnd.MainMenu != null && frontEnd.MainMenu.IsOpen)
                    || (frontEnd.PauseMenu != null && frontEnd.PauseMenu.gameObject.activeInHierarchy && frontEnd.PauseMenu.IsOpen));
            MenuOpen = open;
            if (game != null)
            {
                game.InputGate.SetMenuOpen(open);
            }

            if (open)
            {
                FreeCursor();
            }
            else
            {
                RestoreCursor();
            }

            // Pause the local game behind the main menu, as the config screen does; never someone else's, and never online.
            var wantPause = open && !InSession && game != null && !game.IsRemote;
            if (wantPause && !_pausedForMenu && !game.State.IsPaused && !game.State.IsGameOver)
            {
                _pausedForMenu = game.Submit(new Pause(SeatOf(game), true));
            }
            else if (!wantPause && _pausedForMenu)
            {
                _pausedForMenu = false;
                if (game != null && !game.IsRemote && game.State.IsPaused)
                {
                    game.Submit(new Pause(SeatOf(game), false));
                }
            }
        }

        private void FreeCursor()
        {
            if (_cursorFreed)
            {
                return;
            }

            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorFreed = true;
        }

        private void RestoreCursor()
        {
            if (!_cursorFreed)
            {
                return;
            }

            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            _cursorFreed = false;
        }

        private static PlayerId SeatOf(GameSession session) => session.Angel != null ? session.Angel.Seat : new PlayerId(0);
    }
}
