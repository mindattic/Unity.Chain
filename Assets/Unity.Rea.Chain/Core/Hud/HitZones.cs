using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Zone.vb's hit box overlay (UNI-T56): where renderZone draws its boxes, read from the world and changing nothing.
    /// loadZone builds one box, <see cref="Settings.Size"/> from its top-left corner down and to the right; renderZone
    /// moves a copy to every enemy (its corner at the enemy's centre) and one to the mace (rectMace's top-left), and
    /// draws each as a line strip around the box. Root.vb's <c>bZone</c> (E) says whether it is drawn
    /// (<see cref="ViewOptions.Zones"/>).
    ///
    /// The original's renderZone is broken three ways, and here it plays as it was meant to (BIBLE D23): nothing ever
    /// calls it (oneFrameRender lists every other render call but not this one, so the overlay never shows); it draws
    /// only <c>maxZone</c> + 1 = 6 boxes, so 9 of the 14 enemies have none; and it moves box 0 to the enemy and then to
    /// the mace on every pass, so enemy 0 never has one. Here every enemy in the world has a box, and each player's
    /// mace one more while the chain follows (with F off the mace hits nothing, BIBLE D17, so it has no hit box).
    /// </summary>
    public static class HitZones
    {
        /// <summary>The overlay's figures in world units (hud.json's <c>zone</c>, mapped by space.json).</summary>
        public readonly struct Settings
        {
            public Settings(Vec2 size, Vec2 enemyCorner, Vec2 maceCorner)
            {
                Size = size;
                EnemyCorner = enemyCorner;
                MaceCorner = maceCorner;
            }

            /// <summary>loadZone's box: Right - Left across and Top - Bottom tall (0.05 x 0.05 of the screen).</summary>
            public Vec2 Size { get; }

            /// <summary>An enemy's box's top-left corner, from the enemy's centre.</summary>
            public Vec2 EnemyCorner { get; }

            /// <summary>The mace's box's top-left corner (rectMace's Left, Top), from the mace's centre.</summary>
            public Vec2 MaceCorner { get; }

            /// <summary>Maps hud.json's <paramref name="zone"/> (screen units) into world units through <paramref name="mapping"/>.</summary>
            public static Settings From(HudZone zone, OriginalSpace mapping)
            {
                if (zone == null)
                {
                    throw new ArgumentNullException(nameof(zone));
                }

                // The box's own offset from its corner (loadZone's Left and Top, both 0 as shipped) moves the corner.
                var offset = new Vec2(zone.Left, zone.Top);
                return new Settings(
                    mapping.Length(new Vec2(zone.Right - zone.Left, zone.Top - zone.Bottom)),
                    mapping.Length(zone.EnemyCorner + offset),
                    mapping.Length(zone.MaceCorner + offset));
            }
        }

        /// <summary>The box whose top-left corner is <paramref name="corner"/> (y up, so it hangs below the corner).</summary>
        public static Rect2 Box(Vec2 corner, Vec2 size) => new Rect2(corner.X, corner.Y - size.Y, size.X, size.Y);

        /// <summary>
        /// Every box renderZone draws for <paramref name="world"/> now, into <paramref name="into"/> (cleared first):
        /// each following player's mace first (Zone.vb's box 0), then every enemy in the world, in its order.
        /// Returns how many there are.
        /// </summary>
        public static int Collect(GameWorld world, Settings settings, List<Rect2> into)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var player in world.Players)
            {
                if (ChainFollow.MaceHits(player.IsFollowing))
                {
                    into.Add(Box(player.MacePosition + settings.MaceCorner, settings.Size));
                }
            }

            foreach (var enemy in world.Enemies)
            {
                if (enemy.InWorld)
                {
                    into.Add(Box(enemy.Position + settings.EnemyCorner, settings.Size));
                }
            }

            return into.Count;
        }

        /// <summary>
        /// loadZone's line strip around <paramref name="box"/>, in its order: upper right, upper left, lower left,
        /// lower right and back to upper right (five points, four lines).
        /// </summary>
        public static Vec2[] Strip(Rect2 box) => new[]
        {
            new Vec2(box.XMax, box.YMax),
            new Vec2(box.XMin, box.YMax),
            new Vec2(box.XMin, box.YMin),
            new Vec2(box.XMax, box.YMin),
            new Vec2(box.XMax, box.YMax),
        };
    }
}
