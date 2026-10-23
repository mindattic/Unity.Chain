using System.Collections.Generic;
using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class ChainAudioTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<(ChainSound sound, AudioClip clip, float volume)> _played = new List<(ChainSound, AudioClip, float)>();
        private ChainController _chain;
        private ChainAudio _audio;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Chain"));
            _chain = root.AddComponent<ChainController>();
            _chain.Rebuild();
            _audio = root.AddComponent<ChainAudio>();
            _audio.Chain = _chain;
            _audio.MaceIndex = 6;
            _audio.Played += (s, c, v) => _played.Add((s, c, v));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            _objects.Clear();
            _played.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        private AudioClip Clip(string name) => Track(AudioClip.Create(name, 4410, 1, 44100, false));

        private Enemy SpawnEnemy(Vector2 position, float hp = 100f)
        {
            var go = Track(new GameObject("Enemy"));
            go.transform.position = position;
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = 0.15f;
            var enemy = go.AddComponent<Enemy>();
            enemy.StartingHp = hp;
            enemy.FixedMass = 1f;
            Physics2D.SyncTransforms();
            return enemy;
        }

        private void DragChain(int steps, float stride)
        {
            for (var i = 0; i < steps; i++)
            {
                _chain.transform.position = new Vector3(i * stride, 0f, 0f);
                _chain.Simulate(1f / 50f);
            }
        }

        [Test]
        public void HangingChain_PlaysNoSwing()
        {
            for (var i = 0; i < 500; i++) // let the chain fall from the anchor and settle
            {
                _chain.Simulate(1f / 50f);
            }

            for (var i = 0; i < 50; i++)
            {
                _chain.Simulate(1f / 50f);
                Assert.IsFalse(_audio.Step(1f / 50f));
            }

            Assert.IsEmpty(_played);
        }

        [Test]
        public void FastSwing_PlaysASwingClip()
        {
            var whoosh = Clip("whoosh");
            _audio.SetClips(ChainSound.Swing, new[] { whoosh });
            _chain.gameObject.AddComponent<MaceController>().ApplyToChain(); // the mace (index 6) is the end link

            // Whirl the hand in a circle (radius 1, 1.5 turns a second) like a flail.
            var swung = false;
            var topSpeed = 0f;
            for (var i = 0; i < 200 && !swung; i++)
            {
                var angle = i * (1f / 50f) * 1.5f * 2f * Mathf.PI;
                _chain.transform.position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                _chain.Simulate(1f / 50f);
                swung = _audio.Step(1f / 50f);
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
            _chain.gameObject.AddComponent<MaceController>().ApplyToChain();
            var x = 0f;
            for (var i = 0; i < 250; i++) // five seconds flying right at 8 units/s
            {
                x += 8f / 50f;
                _chain.transform.position = new Vector3(x, 0f, 0f);
                _chain.Simulate(1f / 50f);
                var swung = _audio.Step(1f / 50f);
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
            var detector = _chain.gameObject.AddComponent<MaceHitDetector>();
            detector.Chain = _chain;
            detector.MaceIndex = 6;
            detector.HitRadius = 0.25f;
            _audio.HitDetector = detector;

            DragChain(10, 0.3f);
            SpawnEnemy(detector.MacePosition);
            Assert.AreEqual(1, detector.Step());
            Assert.AreEqual(1, _played.Count);
            Assert.AreEqual(ChainSound.Impact, _played[0].sound);

            _played.Clear();
            DragChain(1, 0.3f);
            SpawnEnemy(detector.MacePosition + new Vector2(0f, 0.01f), hp: 0.0001f);
            detector.Step();
            CollectionAssert.Contains(_played.ConvertAll(p => p.sound), ChainSound.EnemyDeath);
        }

        [Test]
        public void Grapple_PlaysCatchAndThrow()
        {
            var grapple = _chain.gameObject.AddComponent<GrappleController>();
            grapple.Chain = _chain;
            grapple.UseMouseButton = false;
            grapple.MaceIndex = 6;
            _audio.Grapple = grapple;

            DragChain(10, 0.3f);
            grapple.SetButtonHeld(true);
            grapple.Step();
            Assert.IsTrue(grapple.TryCatch(SpawnEnemy(grapple.MacePosition)));
            Assert.AreEqual(ChainSound.GrappleCatch, _played[_played.Count - 1].sound);

            grapple.SetButtonHeld(false);
            grapple.Step();
            Assert.AreEqual(ChainSound.GrappleThrow, _played[_played.Count - 1].sound);
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
        public void DetachedComponents_NoLongerTriggerSounds()
        {
            var detector = _chain.gameObject.AddComponent<MaceHitDetector>();
            detector.Chain = _chain;
            detector.MaceIndex = 6;
            _audio.HitDetector = detector;
            _audio.HitDetector = null;

            DragChain(10, 0.3f);
            SpawnEnemy(detector.MacePosition);
            Assert.AreEqual(1, detector.Step());
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
