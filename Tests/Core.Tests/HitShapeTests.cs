using System;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// What contacts are tested with (U.CHN-T88): Angel.vb's, Enemy.vb's and Chain.vb's rectangles through Root.vb's
    /// rectIntersect by default, the mace's rectangle swept along its move (BIBLE D12), circles only as a data option.
    /// </summary>
    public class HitShapeTests
    {
        private const float Dt = 1f / 50f;
        private static readonly PlayerId P0 = new PlayerId(0);

        private static GameWorld World(HitShape shape, float enemyRadius, out PlayerEntity player)
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 0;
            settings.Enemies.Radius = enemyRadius;
            settings.Enemies.Shape = shape;
            settings.Enemies.Brain.SeekRate = 0f;
            var world = new GameWorld(settings, new Random(1));
            var setup = PlayerSetup.Default;
            setup.ContactShape = shape;
            setup.MaceShape = shape;
            player = world.AddPlayer(P0, setup);
            return world;
        }

        // --- Angel contact -----------------------------------------------------------------

        [Test]
        public void Contact_RectangleMissesWhereTheCircleHits()
        {
            // Enemy 0.4 to the angel's left: its rectangle ends at -0.04, short of the angel's left edge at 0.
            var rect = World(HitShape.Rectangle, 0.2f, out _);
            rect.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);
            rect.Step(0.1f);
            Assert.AreEqual(30, rect.State.Hp);

            var circle = World(HitShape.Circle, 0.2f, out _);
            circle.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);
            circle.Step(0.1f);
            Assert.AreEqual(29, circle.State.Hp);
        }

        [Test]
        public void Contact_RectangleHitsWhenTheAngelsEdgeIsInsideTheEnemys()
        {
            var world = World(HitShape.Rectangle, 0.2f, out _);
            world.SpawnEnemy(new Vec2(0.1f, 0.3f), 1f, 100f);
            world.Step(0.1f);
            Assert.AreEqual(29, world.State.Hp);
        }

        [Test]
        public void Contact_OnlyTheAngelsEdgesCount_AsRectIntersectTestsThem()
        {
            // The enemy's rectangle (y 0.53..1.25) lies wholly between the angel's top and bottom edges (0.27 and 1.35):
            // they overlap, but neither of the angel's horizontal edges is inside the enemy's, so rectIntersect says no.
            var world = World(HitShape.Rectangle, 0.2f, out _);
            world.SpawnEnemy(new Vec2(0.27f, 0.8f), 1f, 100f);
            world.Step(0.1f);
            Assert.AreEqual(30, world.State.Hp);
        }

        // --- The mace ----------------------------------------------------------------------

        private static Vec2 Settle(GameWorld world, PlayerEntity player)
        {
            for (var i = 0; i < 150; i++)
            {
                world.Step(Dt);
            }

            world.TakeEvents();
            return player.MacePosition;
        }

        [Test]
        public void Mace_RectangleHitsAnEnemyItsEdgeIsInside()
        {
            var world = World(HitShape.Rectangle, 0.15f, out var player);
            var link = Settle(world, player);
            world.SpawnEnemy(link + new Vec2(0.3f, 0f), 1f, 100f);
            world.Step(Dt);
            Assert.AreEqual(1, world.TakeEvents().OfType<EnemyHit>().Count());
        }

        [Test]
        public void Mace_RectangleMissesWhereTheCircleHits()
        {
            // 0.4 to the left of the link: Chain.vb's rectMace starts at link X + 0.01 (0.09 here), the enemy's rectangle
            // ends 0.36 right of its centre, so they do not meet; a 0.3 circle on a 0.25 mace would.
            var rect = World(HitShape.Rectangle, 0.3f, out var rectPlayer);
            var link = Settle(rect, rectPlayer);
            rect.SpawnEnemy(link + new Vec2(-0.4f, 0f), 1f, 100f);
            rect.Step(Dt);
            Assert.IsEmpty(rect.TakeEvents().OfType<EnemyHit>());

            var circle = World(HitShape.Circle, 0.3f, out var circlePlayer);
            link = Settle(circle, circlePlayer);
            circle.SpawnEnemy(link + new Vec2(-0.4f, 0f), 1f, 100f);
            circle.Step(Dt);
            Assert.AreEqual(1, circle.TakeEvents().OfType<EnemyHit>().Count());
        }

        [Test]
        public void RectCast_SweepsThroughATargetBetweenTwoSteps()
        {
            var mover = new HeartRect(0f, 0.5f, 0.5f, 0f);
            var target = new HeartRect(4.9f, 0.5f, 5.4f, 0f);

            Assert.IsFalse(HeartFloat.RectIntersect(mover, target), "the start alone misses");
            Assert.IsFalse(HeartFloat.RectIntersect(new HeartRect(10f, 0.5f, 10.5f, 0f), target), "the end alone misses");
            Assert.IsTrue(Sweep.RectCast(mover, new Vec2(10f, 0f), target, 0.5f, out var fraction));
            Assert.AreEqual(0.44f, fraction, 0.03f, "first touching where the mover's right edge reaches the target's left");
        }

        [Test]
        public void RectCast_MissesATargetOffThePath_AndHitsOneItStartsInside()
        {
            var mover = new HeartRect(0f, 0.5f, 0.5f, 0f);

            Assert.IsFalse(Sweep.RectCast(mover, new Vec2(10f, 0f), new HeartRect(4f, 3.5f, 4.5f, 3f), 0.5f, out _));
            Assert.IsTrue(Sweep.RectCast(mover, new Vec2(10f, 0f), new HeartRect(0.1f, 0.4f, 0.4f, 0.1f), 0.3f, out var fraction));
            Assert.AreEqual(0f, fraction);
        }

        // --- Flocking ----------------------------------------------------------------------

        [Test]
        public void Flocking_RectanglesPushEnemiesApartWhereCirclesDoNot()
        {
            // 0.3 apart: the 0.15-radius circles just miss (reach 0.3), the 0.585-wide rectangles overlap.
            float Gap(HitShape shape)
            {
                var world = World(shape, 0.15f, out _);
                var a = world.SpawnEnemy(new Vec2(6f, 6f), 1f, 1f);
                var b = world.SpawnEnemy(new Vec2(6.3f, 6f), 1f, 1f);
                world.Step(Dt);
                return b.Position.X - a.Position.X;
            }

            Assert.Greater(Gap(HitShape.Rectangle), 0.3f);
            Assert.AreEqual(0.3f, Gap(HitShape.Circle), 1e-5f);
        }

        // --- The data ----------------------------------------------------------------------

        private static MemorySettingsSource Patched(string file, Func<string, string> change)
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            return new MemorySettingsSource(shipped).Set(file, change(shipped.Read(file)));
        }

        [Test]
        public void Shipped_UsesTheOriginalsRectangles()
        {
            var settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            var player = settings.Player;
            var enemies = settings.World.Enemies;

            Assert.AreEqual(HitShape.Rectangle, player.ContactShape);
            Assert.AreEqual(HitShape.Rectangle, player.MaceShape);
            Assert.AreEqual(HitShape.Rectangle, enemies.Shape);

            // Angel.vb: X..X + 0.06 by Y + 0.03..Y + angelSize / 2, all x the length scale 9.
            Assert.AreEqual(0f, player.AngelBox.Left, 1e-5f);
            Assert.AreEqual(0.54f, player.AngelBox.Right, 1e-5f);
            Assert.AreEqual(0.27f, player.AngelBox.Bottom, 1e-5f);
            Assert.AreEqual(1.35f, player.AngelBox.Top, 1e-5f);

            // Enemy.vb: centre - 0.025..centre + 0.04 by Y - 0.03..Y + 0.05.
            Assert.AreEqual(-0.225f, enemies.Box.Left, 1e-5f);
            Assert.AreEqual(0.36f, enemies.Box.Right, 1e-5f);
            Assert.AreEqual(-0.27f, enemies.Box.Bottom, 1e-5f);
            Assert.AreEqual(0.45f, enemies.Box.Top, 1e-5f);

            // Chain.vb: X + 0.01..X + maceSize by Y - maceSize / 2 + 0.01..Y + maceSize / 2.
            Assert.AreEqual(0.09f, player.MaceBox.Left, 1e-5f);
            Assert.AreEqual(0.675f, player.MaceBox.Right, 1e-5f);
            Assert.AreEqual(-0.2475f, player.MaceBox.Bottom, 1e-5f);
            Assert.AreEqual(0.3375f, player.MaceBox.Top, 1e-5f);
        }

        [Test]
        public void Circles_StayReachableAsDataOptions()
        {
            var angel = GameSettings.Load(Patched(GameSettings.AngelFile, t => t.Replace("\"contactShape\": \"Rectangle\"", "\"contactShape\": \"Circle\"")));
            var chain = GameSettings.Load(Patched(GameSettings.ChainFile, t => t.Replace("\"hitShape\": \"Rectangle\"", "\"hitShape\": \"Circle\"")));
            var enemies = GameSettings.Load(Patched(GameSettings.EnemiesFile, t => t.Replace("\"shape\": \"Rectangle\"", "\"shape\": \"Circle\"")));

            Assert.AreEqual(HitShape.Circle, angel.Player.ContactShape);
            Assert.AreEqual(HitShape.Circle, chain.Player.MaceShape);
            Assert.AreEqual(HitShape.Circle, enemies.World.Enemies.Shape);
        }
    }
}
