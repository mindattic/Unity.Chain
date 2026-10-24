using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T26: the GameSession's seams for online play. With none of them set it is local play exactly as before; a
    /// client's session forwards its commands instead of changing its own world; the authority seats a remote player
    /// tuned as its own angel is; and while a session runs Escape does not end the application. The network itself is
    /// tested in PlayMode (Tests/Online, on the Unity.Multiplayer test kit).
    /// </summary>
    public class OnlineSeamTests
    {
        private TestRig _rig;

        [SetUp]
        public void SetUp() => _rig = new TestRig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [Test]
        public void WithNoForward_CommandsApplyToTheSessionsOwnWorld()
        {
            Assert.IsFalse(_rig.Session.IsRemote);
            var start = _rig.Player.Flight.Position;
            Assert.IsTrue(_rig.Session.Submit(new SteerAngel(_rig.Id, new Vec2(100f, 0f), 0.02f)));
            Assert.Greater(_rig.Player.Flight.Position.X, start.X);
        }

        [Test]
        public void AForwardedSession_SendsEveryCommandButQuit_AndItsWorldStaysAsItWas()
        {
            var sent = new List<GameCommand>();
            _rig.Session.Forward = command =>
            {
                sent.Add(command);
                return true;
            };
            var start = _rig.Player.Flight.Position;

            Assert.IsTrue(_rig.Session.IsRemote);
            Assert.IsTrue(_rig.Session.Submit(new SteerAngel(_rig.Id, new Vec2(100f, 0f), 0.02f)));
            Assert.IsTrue(_rig.Session.Submit(new TogglePause(_rig.Id)));
            Assert.AreEqual(2, sent.Count);
            Assert.IsInstanceOf<SteerAngel>(sent[0]);
            Assert.AreEqual(start, _rig.Player.Flight.Position, "the screen's own angel did not move");
            Assert.IsFalse(_rig.Session.State.IsPaused, "nor did its own world pause");

            _rig.Session.HoldsQuit = true;
            Assert.IsFalse(_rig.Session.Submit(new QuitGame(_rig.Id)), "Escape is the kit's in-game menu online");
            Assert.AreEqual(2, sent.Count, "Quit is never forwarded");

            _rig.Session.Forward = null;
            Assert.IsFalse(_rig.Session.IsRemote);
        }

        [Test]
        public void HoldsQuit_KeepsEscapeFromEndingTheGame()
        {
            var quits = 0;
            _rig.Session.Quitting += () => quits++;

            _rig.Session.HoldsQuit = true;
            Assert.IsFalse(_rig.Session.Submit(new QuitGame(_rig.Id)));
            Assert.AreEqual(0, quits);

            _rig.Session.HoldsQuit = false;
            _rig.Session.Submit(new QuitGame(_rig.Id)); // edit mode: QuitApplication does nothing outside play mode
            Assert.AreEqual(1, quits, "local play's Escape quits as before");
        }

        [Test]
        public void TheAuthority_SeatsARemotePlayer_TunedAsItsOwnAngel_AndTakesThemOutWhenTheyLeave()
        {
            var remoteId = new PlayerId(5);
            var remote = _rig.Session.AddRemotePlayer(remoteId);

            Assert.IsNotNull(remote);
            Assert.AreSame(remote, _rig.Session.World.FindPlayer(remoteId));
            Assert.AreSame(remote, _rig.Session.AddRemotePlayer(remoteId), "one player per seat");
            Assert.AreEqual(_rig.Player.Setup.LinkCount, remote.Setup.LinkCount);
            Assert.AreEqual(_rig.Player.Setup.Flight.Bounds, remote.Setup.Flight.Bounds);
            Assert.AreEqual(_rig.Player.Setup.Start, remote.Setup.Start);

            var start = remote.Flight.Position;
            _rig.Session.Submit(new SteerAngel(remoteId, new Vec2(100f, 0f), 0.02f));
            Assert.Greater(remote.Flight.Position.X, start.X, "the remote seat's commands fly its own angel");

            Assert.IsFalse(_rig.Session.RemoveRemotePlayer(_rig.Id), "a seat with an angel on this screen stays");
            Assert.IsNotNull(_rig.Session.World.FindPlayer(_rig.Id));
            Assert.IsTrue(_rig.Session.RemoveRemotePlayer(remoteId));
            Assert.IsNull(_rig.Session.World.FindPlayer(remoteId));
            Assert.IsFalse(_rig.Session.RemoveRemotePlayer(remoteId));
        }
    }
}
