using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T78: Engine.vb's oneFrameRender draws only renderCube, renderBackground and the - PAUSE - banner while
    /// paused, so the camera then shows only the backdrop's layer (the area and the brick room), and every sprite of the
    /// play field (angel, chain, mace, link skins, enemies, heart, zones) comes back on resume.
    /// </summary>
    public class PauseViewTests
    {
        private TestRig _rig;
        private Camera _camera;
        private CameraRig _cameraRig;
        private AreaBackdrop _backdrop;

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            var cameraGo = _rig.Track(new GameObject("Camera"));
            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.cullingMask = ~0;
            _cameraRig = cameraGo.AddComponent<CameraRig>();
            _cameraRig.Session = _rig.Session;

            var backdropGo = _rig.Track(new GameObject("Background"));
            _backdrop = backdropGo.AddComponent<AreaBackdrop>();
            new GameObject("Area").transform.SetParent(backdropGo.transform, false);
            new GameObject("BrickRoom").transform.SetParent(backdropGo.transform, false);
            _backdrop.PutOnLayer();
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [Test]
        public void TheBackdropAndRoom_DrawOnTheBackdropLayer()
        {
            Assert.AreEqual(AreaBackdrop.Layer, _backdrop.gameObject.layer);
            foreach (Transform child in _backdrop.transform)
            {
                Assert.AreEqual(AreaBackdrop.Layer, child.gameObject.layer, child.name);
            }

            Assert.AreNotEqual(AreaBackdrop.Layer, _rig.Angel.gameObject.layer, "the angel is play field, not backdrop");
            Assert.AreNotEqual(AreaBackdrop.Layer, _rig.Chain.gameObject.layer, "so are the chain and mace");
        }

        [Test]
        public void Paused_ShowsOnlyTheBackdrop_AndResumeBringsThePlayFieldBack()
        {
            _cameraRig.ApplyPauseView();
            Assert.IsFalse(_cameraRig.ShowsOnlyBackdrop, "playing: everything is drawn");
            Assert.AreEqual(~0, _camera.cullingMask);

            Assert.IsTrue(_rig.Session.Submit(new Pause(_rig.Id, true)));
            _cameraRig.ApplyPauseView();
            Assert.IsTrue(_cameraRig.ShowsOnlyBackdrop);
            Assert.AreEqual(AreaBackdrop.OnlyBackdropMask, _camera.cullingMask, "renderCube and renderBackground only");
            Assert.AreEqual(0, _camera.cullingMask & (1 << _rig.Angel.gameObject.layer), "the angel's layer is culled");

            Assert.IsTrue(_rig.Session.Submit(new Pause(_rig.Id, false)));
            _cameraRig.ApplyPauseView();
            Assert.IsFalse(_cameraRig.ShowsOnlyBackdrop);
            Assert.AreEqual(~0, _camera.cullingMask, "the camera's own mask is back");
        }
    }
}
