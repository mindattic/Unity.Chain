using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class EnemyBrainTests
    {
        [Test]
        public void Seek_VelocityIsProportionalToDistance()
        {
            var brain = new EnemyBrain();
            var velocity = brain.Step(Vec2.Zero, new Vec2(2f, -1f), 0.02f);

            Assert.AreEqual(EnemyState.Seek, brain.State);
            Assert.AreEqual(new Vec2(2f, -1f) * brain.Config.SeekRate, velocity);
        }

        [Test]
        public void Hit_KnocksBackByMass_DamagesBySpeed_AndHurts()
        {
            var brain = new EnemyBrain(hp: 10f, mass: 2f);
            var damage = brain.Hit(new Vec2(6f, 8f), damagePerSpeed: 0.5f);

            Assert.AreEqual(5f, damage, 1e-5f);
            Assert.AreEqual(5f, brain.Hp, 1e-5f);
            Assert.AreEqual(new Vec2(3f, 4f), brain.Velocity);
            Assert.AreEqual(EnemyState.Hurt, brain.State);
        }

        [Test]
        public void Hurt_CannotBeHitAgain_UntilItRecovers()
        {
            var brain = new EnemyBrain(hp: 10f);
            brain.Hit(new Vec2(1f, 0f), 0.1f);

            Assert.IsFalse(brain.CanBeHit);
            Assert.AreEqual(0f, brain.Hit(Vec2.One, 0.1f));

            for (var i = 0; i < 100 && brain.State == EnemyState.Hurt; i++)
            {
                brain.Step(Vec2.Zero, Vec2.Zero, 0.02f);
            }

            Assert.AreEqual(EnemyState.Seek, brain.State);
            Assert.IsTrue(brain.CanBeHit);
        }

        [Test]
        public void Hurt_SlowsDownEachStep()
        {
            var brain = new EnemyBrain(hp: 10f);
            brain.Hit(new Vec2(4f, -4f), 0f);
            var before = brain.Velocity.Magnitude;
            brain.Step(Vec2.Zero, Vec2.Zero, 0.02f);

            Assert.Less(brain.Velocity.Magnitude, before);
            Assert.AreEqual(EnemyState.Hurt, brain.State);
        }

        [Test]
        public void LethalHit_Kills()
        {
            var brain = new EnemyBrain(hp: 1f);
            brain.Hit(new Vec2(100f, 0f), 1f);

            Assert.AreEqual(EnemyState.Dead, brain.State);
            Assert.IsFalse(brain.IsAlive);
            Assert.AreEqual(0f, brain.Hp);
            Assert.AreEqual(Vec2.Zero, brain.Step(Vec2.Zero, Vec2.One, 0.02f));
        }

        [Test]
        public void Grabbed_ThenThrown_IsHurtAndThrown()
        {
            var brain = new EnemyBrain(mass: 2f);
            Assert.IsTrue(brain.Grab());
            Assert.AreEqual(EnemyState.Grabbed, brain.State);
            Assert.IsFalse(brain.CanBeHit);

            brain.Throw(new Vec2(5f, 0f));

            Assert.AreEqual(EnemyState.Hurt, brain.State);
            Assert.IsTrue(brain.IsThrown);
            Assert.AreEqual(new Vec2(5f, 0f), brain.Velocity); // the mace carried it: no mass division
        }

        [Test]
        public void Knock_DividesByMass_WithoutDamage()
        {
            var brain = new EnemyBrain(hp: 1f, mass: 4f);
            brain.Knock(new Vec2(8f, 0f));

            Assert.AreEqual(new Vec2(2f, 0f), brain.Velocity);
            Assert.AreEqual(EnemyState.Hurt, brain.State);
            Assert.AreEqual(1f, brain.Hp);
        }

        [Test]
        public void FlockSpeed_IsTheOriginalsPerFrameFigureAtSixtyFps()
        {
            // Engine.vb: flockSpd 0.0005 per frame, each overlapping pair visited twice per frame, 60 fps.
            Assert.AreEqual(0.54f, EnemyBrain.DefaultFlockSpeed, 1e-5f);

            // One 1/60 s step moves an enemy 0.009 per axis per neighbour, the original's 2 x 0.0005 screen units per frame x 9.
            var push = EnemyBrain.FlockPush(new Vec2(1f, 1f), Vec2.Zero, EnemyBrain.DefaultFlockSpeed, 1f / 60f);
            Assert.AreEqual(0.009f, push.X, 1e-6f);
            Assert.AreEqual(0.009f, push.Y, 1e-6f);
        }

        [Test]
        public void FlockPush_PushesApartPerAxis_AndSplitsAPerfectStack()
        {
            var push = EnemyBrain.FlockPush(new Vec2(1f, 0f), new Vec2(0f, 2f), 2f, 0.5f);
            Assert.AreEqual(new Vec2(1f, -1f), push);

            var a = EnemyBrain.FlockPush(Vec2.Zero, Vec2.Zero, 1f, 1f, 1);
            var b = EnemyBrain.FlockPush(Vec2.Zero, Vec2.Zero, 1f, 1f, -1);
            Assert.AreEqual(Vec2.Zero, a + b);
            Assert.AreNotEqual(Vec2.Zero, a);
        }

        // --- Waiting for a Seek slot (UNI-T65) ------------------------------------------------

        private static EnemyBrain Waiting(float distance, float mass = 1f)
        {
            var brain = new EnemyBrain(mass: mass);
            brain.Wait(distance);
            return brain;
        }

        [Test]
        public void Wait_InsideTheHangBack_IsCautious_OutsideIsAlign()
        {
            var config = EnemyBrain.Settings.Default;

            Assert.AreEqual(EnemyState.Cautious, Waiting(config.HangBackNear - 0.1f).State);
            Assert.AreEqual(EnemyState.Align, Waiting(config.HangBackNear + 0.1f).State);
            Assert.IsTrue(Waiting(1f).CanBeHit, "Engine.vb hits anything not Hurt or Grabbed");
        }

        [Test]
        public void Cautious_BacksAwayAtSixSeekRatesOverMass()
        {
            var brain = Waiting(1f, mass: 2f);
            var velocity = brain.Step(new Vec2(1f, 0f), Vec2.Zero, 0.02f);

            Assert.AreEqual(EnemyState.Cautious, brain.State);
            Assert.AreEqual(brain.Config.CautiousRate * 1f / 2f, velocity.X, 1e-5f, "away from the angel, 6 x seekSpd x distance / mass");
            Assert.AreEqual(0f, velocity.Y, 1e-6f);
            Assert.AreEqual(6f * brain.Config.SeekRate, brain.Config.CautiousRate, 1e-5f);
        }

        [Test]
        public void Cautious_PastTheFarEdge_StalksBackInAligned()
        {
            var brain = Waiting(1f);
            var far = brain.Config.HangBackFar + 0.5f;
            var velocity = brain.Step(new Vec2(far, far), Vec2.Zero, 0.02f);

            Assert.AreEqual(EnemyState.Align, brain.State);
            Assert.AreEqual(new Vec2(-1f, -1f) * brain.Config.AlignSpeed, velocity, "seekSpd on each axis toward the angel");
        }

        [Test]
        public void Align_ReachingTheNearEdge_TurnsCautious()
        {
            var brain = Waiting(10f);
            Assert.AreEqual(EnemyState.Align, brain.State);

            var velocity = brain.Step(new Vec2(brain.Config.HangBackNear - 0.01f, 0f), Vec2.Zero, 0.02f);

            Assert.AreEqual(EnemyState.Cautious, brain.State);
            Assert.Greater(velocity.X, 0f, "backing off");
        }

        [Test]
        public void Align_LevelOnAnAxis_HoldsStillOnIt()
        {
            var brain = Waiting(10f);
            var velocity = brain.Step(new Vec2(10f, 0.001f), Vec2.Zero, 0.02f);

            Assert.AreEqual(-brain.Config.AlignSpeed, velocity.X, 1e-5f);
            Assert.AreEqual(-0.001f / 0.02f, velocity.Y, 1e-4f, "only the rest of the way, no shimmer past it");
        }

        [Test]
        public void Promote_TakesTheSeekSlot_HurtCannotBePromoted()
        {
            var brain = Waiting(10f);
            brain.Promote();
            Assert.AreEqual(EnemyState.Seek, brain.State);

            brain.Hit(Vec2.One, 0.01f);
            brain.Promote();
            brain.Wait(10f);
            Assert.AreEqual(EnemyState.Hurt, brain.State);
        }
    }
}
