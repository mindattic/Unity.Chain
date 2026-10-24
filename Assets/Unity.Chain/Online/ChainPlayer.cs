using System;
using MindAttic.Chain.Core;
using Unity.Multiplayer;      // NetEntity, NetPlayer
using Unity.Multiplayer.Core; // [ToServer], [Synced], NetSender
using UnityEngine;

namespace MindAttic.Chain.Online
{
    /// <summary>
    /// One online player (U.CHN-T26): the NetworkManager's Player Prefab, so every connection gets one, carrying its
    /// seat. The server gives it the connection's seat from the session's roster (<see cref="Seat"/>, stable across a
    /// reconnect), and the seat is the <see cref="Core.PlayerId"/> its commands play as in the one authority's
    /// <see cref="GameWorld"/>: the host's own seat is the scene's angel; any other seat the authority adds to its world
    /// (<see cref="GameSession.AddRemotePlayer"/>) for as long as the player is here.
    /// <para>On the owning client its screen's <see cref="GameSession"/> forwards every command its input readers make
    /// (<see cref="GameSession.Forward"/>) to <see cref="Send"/>, instead of changing its own world; the server decodes
    /// each one as this seat's (<see cref="CommandCodec"/>), so a client only ever moves its own angel, and submits it.
    /// The server writes the seat's angel and mace back as <see cref="Angel"/> and <see cref="Mace"/>, which every peer
    /// reads, and the whole world travels in <see cref="ChainWorldSync"/>'s snapshots.</para>
    /// <para>The owning client's screen draws those snapshots (U.CHN-T73): every frame it hands the newest to its
    /// <see cref="GameSession.ShowAuthority"/> with this seat, so its own angel, chain, mace, enemies, hearts and HUD show
    /// the authority's game as it plays. Every other seat a peer has no angel of its own for is drawn by its marker: the
    /// angel sprite in the player's colour and, when wired, its chain (<see cref="ChainLine"/>) and mace
    /// (<see cref="MaceMarker"/>) as the authority has them.</para>
    /// </summary>
    public partial class ChainPlayer : NetEntity
    {
        /// <summary>The seat before the server has given one.</summary>
        public const int NoSeat = -1;

        /// <summary>
        /// Most commands a second the server takes from one player: SteerAngel goes once a frame, so this is a fast
        /// display's frame rate with room for the button and key commands beside it.
        /// </summary>
        public const int CommandRate = 300;

        // client -> server: one encoded GameCommand (CommandCodec), in the order the client made them.
        [ToServer(RateLimit = CommandRate, Burst = CommandRate * 2)]
        public partial void Send(byte[] command);

        // Written only by the server; every peer, late joiners included, reads them through Seat, Angel and Mace.
        [Synced] private int _seat = NoSeat;
        [Synced] private Vector2 _angel;
        [Synced] private Vector2 _mace;

        [Tooltip("Drawn at the seat's angel on a peer that has no angel of its own for this seat (optional).")]
        [SerializeField] private SpriteRenderer marker;
        [Tooltip("Draws the seat's chain beside the marker, from the hand to the mace (optional).")]
        [SerializeField] private LineRenderer chainLine;
        [Tooltip("Drawn at the seat's mace beside the marker (optional; one world unit across at scale 1).")]
        [SerializeField] private SpriteRenderer maceMarker;

        private Vector3[] _links = Array.Empty<Vector3>();

        private bool _addedToWorld;
        private Func<GameCommand, bool> _forward;

        /// <summary>Server: the authority this player's commands go to. Null on a client.</summary>
        public GameSession Authority { get; private set; }

        /// <summary>Owning client: the screen whose commands this player carries to the server. Null elsewhere.</summary>
        public GameSession Screen { get; private set; }

        /// <summary>The player id this seat plays as in the authority's world (P-1 until seated).</summary>
        public PlayerId PlayerId => new PlayerId(Seat);

        /// <summary>Whether the server has seated this player.</summary>
        public bool IsSeated => Seat != NoSeat;

        /// <summary>Server: commands received from this player and applied by the authority.</summary>
        public int CommandsApplied { get; private set; }

        /// <summary>Server: received bytes that did not decode to a command, and were ignored.</summary>
        public int CommandsRefused { get; private set; }

        /// <summary>The owner's name, from the roster ("Player" until the roster has it).</summary>
        public string DisplayName => Roster(out NetPlayer player) ? player.DisplayName : "Player";

        /// <summary>The marker's chain line (null when the prefab has none).</summary>
        public LineRenderer ChainLine => chainLine;

        /// <summary>The marker's mace sprite (null when the prefab has none).</summary>
        public SpriteRenderer MaceMarker => maceMarker;

        /// <summary>Whether this peer draws this seat with its marker (no angel of its own shows it).</summary>
        public bool ShowsMarker => IsSpawned && IsSeated && !HasOwnView();

        /// <summary>The owner's colour, from the roster (their pick, or their seat's palette colour).</summary>
        public Color32 Color => Roster(out NetPlayer player) ? player.UnityColor() : new Color32(255, 255, 255, 255);

        /// <summary>
        /// Sends <paramref name="command"/> to the authority as this seat's (the owning client's own call; what its
        /// screen's <see cref="GameSession.Forward"/> does for every command). False for one that is never sent
        /// (<see cref="QuitGame"/>) or before the player is spawned.
        /// </summary>
        public bool SendCommand(GameCommand command)
        {
            if (!IsSpawned || !CommandCodec.IsSent(command))
            {
                return false;
            }

            Send(CommandCodec.Encode(command));
            return true;
        }

        protected override void OnSpawned()
        {
            if (IsServer)
            {
                Seat = Owner.HasSeat ? Owner.SeatIndex : 0;
                Authority = ChainAuthority.For(Session);
                if (Authority != null)
                {
                    _addedToWorld = Authority.World.FindPlayer(PlayerId) == null;
                    Authority.AddRemotePlayer(PlayerId);
                    WriteBack();
                }
            }

            if (IsOwner && !IsServer)
            {
                Screen = ChainAuthority.For(Session);
                if (Screen != null)
                {
                    _forward = SendCommand;
                    Screen.Forward = _forward;
                }
            }
        }

        protected override void OnDespawned()
        {
            if (Authority != null && _addedToWorld)
            {
                Authority.RemoveRemotePlayer(PlayerId);
            }

            if (Screen != null && Screen.Forward == _forward)
            {
                Screen.Forward = null;
            }

            if (Screen != null)
            {
                Screen.StopShowingAuthority();
            }

            Authority = null;
            Screen = null;
            _forward = null;
            _addedToWorld = false;
        }

        // Runs on the server for every Send: the bytes are this seat's, whatever player id the client put in them.
        private void OnSend(byte[] command)
        {
            if (Authority == null || !IsSeated)
            {
                return;
            }

            var decoded = CommandCodec.Decode(command, PlayerId);
            if (decoded == null)
            {
                CommandsRefused++;
                return;
            }

            Authority.Submit(decoded);
            CommandsApplied++;
        }

        // In Update, before any view draws in LateUpdate: the owning client's screen draws the authority's newest world,
        // its own angel as this seat (U.CHN-T73).
        private void Update()
        {
            var sync = IsSpawned && Screen != null && IsSeated && Screen.IsRemote ? ChainWorldSync.For(Session) : null;
            if (sync != null && sync.Latest != null)
            {
                Screen.ShowAuthority(sync.Latest, PlayerId);
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                WriteBack();
            }

            transform.position = Angel;
            var shown = ShowsMarker;
            if (marker != null)
            {
                marker.enabled = shown;
                marker.color = Color;
            }

            DrawChain(shown);
        }

        /// <summary>The marker's chain and mace: the seat's links as the authority has them (its own world on the server, the newest snapshot elsewhere).</summary>
        private void DrawChain(bool shown)
        {
            var count = shown ? GatherLinks() : 0;
            if (chainLine != null)
            {
                chainLine.enabled = count > 1;
                chainLine.useWorldSpace = true;
                chainLine.positionCount = count;
                for (var i = 0; i < count; i++)
                {
                    chainLine.SetPosition(i, _links[i]);
                }
            }

            if (maceMarker != null)
            {
                maceMarker.enabled = count > 0;
                if (count > 0)
                {
                    maceMarker.transform.position = _links[count - 1];
                }
            }
        }

        /// <summary>Fills the link buffer with this seat's links, hand to mace; returns how many.</summary>
        private int GatherLinks()
        {
            var player = Authority != null ? Authority.World.FindPlayer(PlayerId) : null;
            if (player != null)
            {
                var count = player.MaceIndex + 1;
                EnsureLinks(count);
                for (var i = 0; i < count; i++)
                {
                    var link = player.Chain.GetPosition(i);
                    _links[i] = new Vector3(link.X, link.Y, 0f);
                }

                return count;
            }

            var sync = ChainWorldSync.For(Session);
            if (sync != null && sync.Latest != null && sync.Latest.TryGetPlayer(PlayerId, out var shown) && shown.Links != null)
            {
                EnsureLinks(shown.Links.Length);
                for (var i = 0; i < shown.Links.Length; i++)
                {
                    _links[i] = new Vector3(shown.Links[i].X, shown.Links[i].Y, 0f);
                }

                return shown.Links.Length;
            }

            return 0;
        }

        private void EnsureLinks(int count)
        {
            if (_links.Length < count)
            {
                _links = new Vector3[count];
            }
        }

        /// <summary>Server: the seat's angel and mace, from the authority's world.</summary>
        private void WriteBack()
        {
            var player = Authority != null && IsSeated ? Authority.World.FindPlayer(PlayerId) : null;
            if (player == null)
            {
                return;
            }

            Angel = player.AngelPosition.ToUnity();
            Mace = player.MacePosition.ToUnity();
        }

        /// <summary>
        /// Whether this peer already draws this seat with the scene's own angel: the authority's local seat, or, on a
        /// client showing the authority's world, the seat that client plays as (U.CHN-T73).
        /// </summary>
        private bool HasOwnView()
        {
            var game = Authority != null ? Authority : ChainAuthority.For(Session);
            if (game == null || game.Angel == null)
            {
                return false;
            }

            return game.IsRemote ? game.Shown != null && game.ShownSeat == PlayerId : game.Angel.Seat == PlayerId;
        }

        private bool Roster(out NetPlayer player)
        {
            player = null;
            return Session != null && Session.Roster != null && Session.Roster.TryGet(OwnerClientId, out player);
        }
    }
}
