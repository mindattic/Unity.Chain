using System.Collections.Generic;
using MindAttic.Chain;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class ChainAudioTests
    {
        private readonly List<(ChainSound sound, AudioClip clip, float volume)> _played = new List<(ChainSound, AudioClip, float)>();
        private TestRig _rig;
        private ChainAudio _audio;

        [SetUp]
        public void SetUp()
        {
            // The audio is bound with the chain when the player is seated.
            _rig = new TestRig(beforeJoin: rig =>
            {
                _audio = rig.Chain.gameObject.AddComponent<ChainAudio>();
                _audio.Chain = rig.Chain;
            });
            _audio.Played += (s, c, v) => _played.Add((s, c, v));
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _played.Clear();
        }

        private AudioClip Clip(string name) => _rig.Track(AudioClip.Create(name, 4410, 1, 44100, false));

        [Test]
        public void HangingChain_PlaysNoSwing()
        {
            for (var i = 0; i < 500; i++) // let the chain fall from the hand and settle
            {
                _rig.Step();
            }

            for (var i = 0; i < 50; i++)
            {
                _rig.Step();
                Assert.IsFalse(_audio.Step(TestRig.Dt));
            }

            Assert.IsEmpty(_played);
        }

        [Test]
        public void FastSwing_PlaysASwingClip()
        {
            var whoosh = Clip("whoosh");
            _audio.SetClips(ChainSound.Swing, new[] { whoosh });

            // Whirl the hand in a circle (radius 1, 1.5 turns a second) like a flail.
            var swung = false;
            var topSpeed = 0f;
            for (var i = 0; i < 200 && !swung; i++)
            {
                var angle = i * TestRig.Dt * 1.5f * 2f * Mathf.PI;
                _rig.MoveAngelTo(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                _rig.Step();
                swung = _audio.Step(TestRig.Dt);
                topSpeed = Mathf.Max(topSpeed, _audio.LastSwingSpeed);
            }

            Assert.IsTrue(swung, $"whirling the chain should whip the mace past the swing speed (top speed {topSpeed})");
            Assert.AreEqual(1, _played.Count);
            Assert.AreEqual(ChainSound.Swing, _played[0].sound);
            Assert.AreSame(whoosh, _played[0].clip);
            Assert.That(_played[0].volume, Is.GreaterThan(0f).And.LessThanOrEqualTo(_audio.Volume));
        }

        [Test]
        public void SteadyFlight_TrailingTheMace_IsNotASwing()
        {
            var x = 0f;
            for (var i = 0; i < 250; i++) // five seconds flying right at 8 units/s
            {
                x += 8f / 50f;
                _rig.MoveAngelTo(new Vector2(x, 0f));
                _rig.Step();
                var swung = _audio.Step(TestRig.Dt);
                if (i >= 150)
                {
                    Assert.IsFalse(swung, $"step {i}: relative speed {_audio.LastSwingSpeed}");
                }
            }

            Assert.Less(_audio.LastSwingSpeed, _audio.SwingRearmSpeed);
        }

        [Test]
        public void MaceHit_PlaysAnImpact_AndALethalOne_AlsoADeath()
        {
            _rig.Swing();
            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            _rig.Step();
            Assert.AreEqual(1, _played.Count);
            Assert.AreEqual(ChainSound.Impact, _played[0].sound);

            _played.Clear();
            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity() + new Vector2(0f, 0.01f), hp: 0.0001f);
            _rig.Step();
            CollectionAssert.Contains(_played.ConvertAll(p => p.sound), ChainSound.EnemyDeath);
        }

        [Test]
        public void Grapple_PlaysCatchAndThrow()
        {
            _rig.Swing();
            _rig.Grapple.SetButtonHeld(true);
            _rig.Step();
            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            _rig.Step();
            Assert.IsNotNull(_rig.Grapple.HeldEnemy);
            Assert.AreEqual(ChainSound.GrappleCatch, _played[_played.Count - 1].sound);

            _rig.Grapple.SetButtonHeld(false);
            _rig.Step();
            Assert.AreEqual(ChainSound.GrappleThrow, _played[_played.Count - 1].sound);
        }

        [Test]
        public void AnotherPlayersHits_AreNotThisChainsSounds()
        {
            _audio.Play(new EnemyHit(new PlayerId(7), new EnemyId(1), 1f, Vec2.One, 1000, true));
            _audio.Play(new Grabbed(new PlayerId(7), new EnemyId(1)));
            Assert.IsEmpty(_played);

            _audio.Play(new EnemyHit(_rig.Id, new EnemyId(1), 1f, Vec2.One, 1000, false));
            Assert.AreEqual(ChainSound.Impact, _played[0].sound);
        }

        [Test]
        public void MutedOrEmpty_StillReports_ButAtZeroVolumeOrNoClip()
        {
            Assert.IsNull(_audio.Play(ChainSound.Impact));
            Assert.IsNull(_played[0].clip);

            _audio.SetClips(ChainSound.Impact, new[] { Clip("hit") });
            _audio.Muted = true;
            Assert.IsNotNull(_audio.Play(ChainSound.Impact));
            Assert.AreEqual(0f, _played[1].volume);
        }

        [Test]
        public void DetachedAudio_NoLongerPlaysTheSessionsEvents()
        {
            _audio.Chain = null;

            _rig.Swing();
            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            _rig.Step();
            Assert.IsEmpty(_played);
        }
    }

    public class MusicPlayerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Apply_SetsUpALooping2DSource()
        {
            _go = new GameObject("Music");
            var player = _go.AddComponent<MusicPlayer>();
            var clip = AudioClip.Create("track", 44100, 1, 44100, false);
            try
            {
                player.Music = clip;
                player.Volume = 0.4f;
                player.Muted = true;

                var source = _go.GetComponent<AudioSource>();
                Assert.AreSame(clip, source.clip);
                Assert.IsTrue(source.loop);
                Assert.IsFalse(source.playOnAwake);
                Assert.AreEqual(0f, source.spatialBlend);
                Assert.AreEqual(0.4f, source.volume, 1e-5f);
                Assert.IsTrue(source.mute);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Play_WithoutATrack_ReturnsMinusOne()
        {
            _go = new GameObject("Music");
            Assert.AreEqual(-1f, _go.AddComponent<MusicPlayer>().Play());
        }

        [Test]
        public void RandomStart_StaysInsideTheTrack()
        {
            Assert.AreEqual(0f, MusicPlayer.RandomStart(120f, 0f));
            Assert.AreEqual(60f * 0.999f, MusicPlayer.RandomStart(120f, 0.5f), 1e-3f);
            Assert.Less(MusicPlayer.RandomStart(120f, 1f), 120f);
            Assert.AreEqual(0f, MusicPlayer.RandomStart(0f, 0.7f));
        }
    }
}
