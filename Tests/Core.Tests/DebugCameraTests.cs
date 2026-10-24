using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class DebugCameraTests
    {
        private const float Eps = 1e-5f;

        // Camera.vb's own figures at one frame a second: one "second" held is one of the original's frames.
        private static DebugCamera Original() => new DebugCamera(DebugCamera.Settings.Original);

        [Test]
        public void Starts_AtCameraVbsCam_LookingAtTheOrigin()
        {
            var cam = Original();

            Assert.AreEqual(Vec2.Zero, cam.Eye);
            Assert.AreEqual(-1f, cam.EyeZ, "cam = (0, 0, -1)");
            Assert.AreEqual(Vec2.Zero, cam.Target, "target = (0, 0, 0)");
            Assert.AreEqual(1f, cam.Zoom, Eps);
            Assert.IsFalse(cam.IsMoved);
            Assert.AreEqual(0.7853982f, cam.Config.Fov, Eps, "pi / 4");
            Assert.AreEqual(4f / 3f, cam.Config.Aspect, Eps);
        }

        [Test]
        public void NumpadPlusAndEnter_MoveTheEyeOnZ_ByCamSpd()
        {
            var cam = Original();

            cam.MoveForward(+1, 1f);
            Assert.AreEqual(-0.988f, cam.EyeZ, Eps, "move_forward: cam.Z += camSPD");
            Assert.AreEqual(0.988f, cam.Zoom, Eps, "nearer: the view shows less");

            cam.MoveForward(-1, 2f);
            Assert.AreEqual(-1.012f, cam.EyeZ, Eps, "move_backward: cam.Z -= camSPD");
            Assert.AreEqual(Vec2.Zero, cam.Eye);
            Assert.AreEqual(Vec2.Zero, cam.Target);
        }

        [Test]
        public void Forward_StopsShortOfThePlane_BibleD26()
        {
            var cam = Original();

            cam.MoveForward(+1, 1000f);

            Assert.AreEqual(-0.012f, cam.EyeZ, Eps, "minDistance in front of z 0, where the original walked through it");
            Assert.Greater(cam.Distance, 0f);
        }

        [Test]
        public void Numpad4And6_StrafeEyeAndTargetTogether()
        {
            var cam = Original();

            Assert.AreEqual(0.012f, cam.Strafe(+1, 1f), Eps, "numpad 6, strafe_left: +camSPD");
            Assert.AreEqual(new Vec2(0.012f, 0f), cam.Eye);
            Assert.AreEqual(new Vec2(0.012f, 0f), cam.Target);

            cam.Strafe(-1, 3f);
            Assert.AreEqual(-0.024f, cam.Eye.X, Eps, "numpad 4, strafe_right: -camSPD");
            Assert.AreEqual(-0.024f, cam.Target.X, Eps);
            Assert.AreEqual(Vec2.Zero, cam.Slant, "still looking straight at the plane");
            Assert.AreEqual(1f, cam.Zoom, Eps);
        }

        [Test]
        public void Numpad8And2_LiftEyeAndTarget_By002()
        {
            var cam = Original();

            cam.Lift(+1, 1f);
            Assert.AreEqual(0.02f, cam.Eye.Y, Eps, "move_up");
            Assert.AreEqual(0.02f, cam.Target.Y, Eps);

            cam.Lift(-1, 2f);
            Assert.AreEqual(-0.02f, cam.Eye.Y, Eps, "move_down");
            Assert.AreEqual(-0.02f, cam.Target.Y, Eps);
        }

        [Test]
        public void XAndZ_MoveTheEyeAlone_ByQuarterOnXAndY()
        {
            var cam = Original();

            cam.Isometric(+1, 1f);
            Assert.AreEqual(new Vec2(0.25f, 0.25f), cam.Eye, "isometric: cam.X and cam.Y += 0.25");
            Assert.AreEqual(Vec2.Zero, cam.Target, "the target stays: the eye looks back at it at a slant");
            Assert.AreEqual(new Vec2(-0.25f, -0.25f), cam.Slant);
            Assert.AreEqual(System.MathF.Sqrt(0.25f * 0.25f * 2f + 1f), cam.Zoom, Eps, "further from the target");

            cam.Isometric(-1, 1f);
            Assert.AreEqual(Vec2.Zero, cam.Eye, "deisometric");
            Assert.AreEqual(1f, cam.Zoom, Eps);
        }

        [Test]
        public void LookStraight_PutsTheTargetUnderTheEye_AsShakeScreensRestore()
        {
            var cam = Original();
            cam.Isometric(+1, 1f);

            cam.LookStraight();

            Assert.AreEqual(new Vec2(0.25f, 0.25f), cam.Target, "target.X = cam.X, target.Y = cam.Y");
            Assert.AreEqual(Vec2.Zero, cam.Slant);
        }

        [Test]
        public void Recenter_IsRootVbsReset()
        {
            var cam = Original();
            cam.Isometric(+1, 2f);
            cam.Strafe(+1, 5f);
            cam.MoveForward(+1, 10f);

            cam.Recenter();

            Assert.AreEqual(Vec2.Zero, cam.Eye);
            Assert.AreEqual(Vec2.Zero, cam.Target);
            Assert.AreEqual(-1f, cam.EyeZ);
            Assert.IsFalse(cam.IsMoved);
        }

        [Test]
        public void Speeds_ArePerSecond_AtTheOriginalsFrameRate()
        {
            // camSPD 0.012 a frame at 60 fps and a length scale of 9: 6.48 world units a second.
            var cam = new DebugCamera(new DebugCamera.Settings(0.785f, 4f / 3f, -9f, 0.012f * 9f * 60f, 0.02f * 9f * 60f, 0.25f * 9f * 60f, 0.108f));

            cam.Strafe(+1, 1f / 60f);
            Assert.AreEqual(0.108f, cam.Eye.X, Eps, "one frame held moves camSPD x 9");

            cam.Recenter();
            cam.Isometric(+1, 0.5f);
            Assert.AreEqual(0.25f * 9f * 30f, cam.Eye.X, 1e-3f, "half a second: 30 of the original's frames");
        }
    }
}
