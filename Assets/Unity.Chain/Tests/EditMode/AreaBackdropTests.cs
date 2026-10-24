using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T40: <see cref="AreaBackdrop"/> draws Background.vb's quad with the current area stretched over it, B and N
    /// switch beach and utopia, and Cube.vb's brick room sits behind it where the camera's eye sees its far wall.
    /// </summary>
    public class AreaBackdropTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<Object> _objects = new List<Object>();
        private AreaBackdrop _backdrop;
        private SpriteRenderer _quad;
        private SpriteRenderer _room;
        private Sprite _beach;
        private Sprite _utopia;

        private static GameSettings Settings => SettingsStore.Current;

        [SetUp]
        public void SetUp()
        {
            // Pictures of other shapes and pivots than the shipped ones: the quad is what they are fitted to.
            _beach = MakeSprite(512, 512, new Vector2(0f, 0.5f));
            _utopia = MakeSprite(5306, 472, new Vector2(0.5f, 0.5f));

            var go = Track(new GameObject("Background"));
            _backdrop = go.AddComponent<AreaBackdrop>();
            _quad = Track(new GameObject("Area")).AddComponent<SpriteRenderer>();
            _room = Track(new GameObject("BrickRoom")).AddComponent<SpriteRenderer>();
            _room.sprite = MakeSprite(512, 512, new Vector2(0.5f, 0.5f));

            _backdrop.Areas = new[]
            {
                new AreaBackdrop.AreaArt("beach", _beach),
                new AreaBackdrop.AreaArt("utopia", _utopia),
            };
            _backdrop.Backdrop = _quad;
            _backdrop.Room = _room;
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
        }

        [Test]
        public void StartsOnTheBeach_FillingBackgroundVbsQuad()
        {
            Assert.AreEqual("beach", _backdrop.Current);
            Assert.AreSame(_beach, _quad.sprite);
            Assert.IsTrue(_quad.enabled);
            AssertCovers(Settings.Backdrop.Quad, _quad.bounds);
        }

        [Test]
        public void N_ShowsUtopia_StretchedOverTheSameQuad_AndB_TheBeachAgain()
        {
            Assert.IsTrue(_backdrop.Press(AreaKey.N));
            Assert.AreEqual("utopia", _backdrop.Current);
            Assert.AreSame(_utopia, _quad.sprite);
            AssertCovers(Settings.Backdrop.Quad, _quad.bounds);

            Assert.IsTrue(_backdrop.Press(AreaKey.B));
            Assert.AreSame(_beach, _quad.sprite);
            AssertCovers(Settings.Backdrop.Quad, _quad.bounds);
        }

        [Test]
        public void B_IsNoToggle()
        {
            Assert.IsFalse(_backdrop.Press(AreaKey.B));
            Assert.IsFalse(_backdrop.Press(AreaKey.B));
            Assert.AreSame(_beach, _quad.sprite);
            Assert.IsTrue(_quad.enabled, "the unreachable bBackground toggle is not ported");
        }

        [Test]
        public void AnAreaWithNoPicture_LeavesTheBrickRoomShowing()
        {
            _backdrop.Areas = new[] { new AreaBackdrop.AreaArt("beach", _beach) };

            _backdrop.Press(AreaKey.N);

            Assert.IsFalse(_quad.enabled);
            Assert.IsTrue(_room.enabled);
        }

        [Test]
        public void TheRoom_IsBehindTheBackdrop()
        {
            Assert.Less(_room.sortingOrder, _quad.sortingOrder, "renderCube draws before renderBackground");
            Assert.Less(_quad.sortingOrder, 0, "both behind the play field's sprites");
        }

        [Test]
        public void TheRoom_WithNoRig_IsTheFarWallFromCameraVbsEye()
        {
            _backdrop.PlaceRoom();

            var b = Settings.Backdrop;
            var expected = Backdrop.FarWall(Vec2.Zero, Settings.DebugCameraSettings.EyeZ, b.RoomSize, b.RoomDepth);
            AssertCovers(expected, _room.bounds);
            // From z -1 the far wall is 20 times as far as the plane: a twentieth of 67 x 55, around the whole backdrop.
            Assert.AreEqual(b.RoomSize.X / 20f, _room.bounds.size.x, Tolerance);
            Assert.Greater(_room.bounds.size.x, _quad.bounds.size.x);
            Assert.Greater(_room.bounds.size.y, _quad.bounds.size.y);
        }

        [Test]
        public void TheRoom_FollowsTheRigsEye()
        {
            var cameraGo = Track(new GameObject("Camera"));
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            var rig = cameraGo.AddComponent<CameraRig>();
            _backdrop.Rig = rig;

            rig.Strafe(+1, 1f);
            _backdrop.PlaceRoom();

            var eye = rig.Eye;
            Assert.Greater(eye.x, 0f, "numpad 6 moved the eye");
            var b = Settings.Backdrop;
            AssertCovers(Backdrop.FarWall(new Vec2(eye.x, eye.y), eye.z, b.RoomSize, b.RoomDepth), _room.bounds);
            Assert.Greater(_room.bounds.center.x, 0f, "the wall drifts with the eye");
            Assert.Less(_room.bounds.center.x, eye.x, "but behind it, at its parallax");
        }

        private static void AssertCovers(Rect2 expected, Bounds actual)
        {
            Assert.AreEqual(expected.XMin, actual.min.x, Tolerance, "left");
            Assert.AreEqual(expected.XMax, actual.max.x, Tolerance, "right");
            Assert.AreEqual(expected.YMin, actual.min.y, Tolerance, "bottom");
            Assert.AreEqual(expected.YMax, actual.max.y, Tolerance, "top");
        }

        private Sprite MakeSprite(int width, int height, Vector2 pivot)
        {
            var texture = Track(new Texture2D(width, height));
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, width);
            return Track(sprite);
        }

        private T Track<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }
    }
}
