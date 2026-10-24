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
            if (marker != null)
            {
                marker.enabled = IsSeated && !HasOwnView();
                marker.color = Color;
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

        /// <summary>Whether this peer already draws this seat with the scene's own angel (the authority's local seat).</summary>
        private bool HasOwnView()
        {
            var game = Authority != null ? Authority : ChainAuthority.For(Session);
            return game != null && !game.IsRemote && game.Angel != null && game.Angel.Seat == PlayerId;
        }

        private bool Roster(out NetPlayer player)
        {
            player = null;
            return Session != null && Session.Roster != null && Session.Roster.TryGet(OwnerClientId, out player);
        }
    }
}
