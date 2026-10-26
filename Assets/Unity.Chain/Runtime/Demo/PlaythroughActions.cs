using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// U.CHN-T62: the moves a scripted run forces through the same public API real input goes through
    /// (<see cref="GameSession.Submit"/>, <see cref="GameSession.Step"/>, <see cref="GameWorld.SpawnEnemy"/>,
    /// <see cref="AngelFlight.Teleport"/>), so a kill, a hit, a heart pickup or a game over happens on cue
    /// instead of waiting on chance.
    ///
    /// Every method here steps the world itself (<see cref="GameSession.Step"/>, "public so tests can drive
    /// it"), so it is meant for a driver that owns the clock outright: the PlayMode test that walks this same
    /// path. A live running scene already steps its session once a physics tick on its own
    /// (<see cref="GameSession.FixedUpdate"/>); <see cref="ScriptedPlaythrough"/>, which plays out in that real
    /// scene for the built player's <c>-demo</c> capture, submits and spawns through the lower-level calls
    /// these are built from (<see cref="GameSession.Submit"/>, <see cref="GameWorld.SpawnEnemy"/>,
    /// <see cref="GrappleController.SetButtonHeld"/>, <see cref="AngelFlight.Teleport"/>) and lets that
    /// automatic step carry them, paced with real waits instead of calling <see cref="GameSession.Step"/>
    /// again on top of it.
    /// </summary>
    public static class PlaythroughActions
    {
        /// <summary>
        /// Winds the chain up like a flail: <paramref name="steps"/> commands of a circling mouse delta
        /// (<see cref="SteerAngel"/>), each applied as its own step of <paramref name="stepSeconds"/>. Public
        /// so both the demo and the test drive the same flight and swing.
        /// </summary>
        public static void FlyAndSwing(GameSession session, PlayerId player, int steps, float stepSeconds, float radius = 6f)
        {
            for (var i = 0; i < steps; i++)
            {
                var angle = (i / (float)steps) * Mathf.PI * 4f;
                var delta = new Vec2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                session.Submit(new SteerAngel(player, delta, stepSeconds));
                session.Step(stepSeconds);
            }
        }

        /// <summary>Puts a fresh, alive enemy exactly at a world position, so the very next step meets it there.</summary>
        public static EnemyEntity SpawnEnemyAt(GameSession session, Vector2 worldPosition, float? hp = null) =>
            session.World.SpawnEnemy(worldPosition.ToCore(), hp: hp);

        /// <summary>
        /// Arms the grapple over an enemy placed on the mace, lets one step catch it, then releases it: a real
        /// catch and throw (<see cref="GrappleController.SetButtonHeld"/>), not a shortcut around the rule.
        /// Returns the enemy that was caught (or null if the catch missed).
        /// </summary>
        public static EnemyEntity GrappleCatchAndThrow(GameSession session, ChainController chain, GrappleController grapple, MaceController mace, float stepSeconds)
        {
            SpawnEnemyAt(session, chain.GetLinkPosition(mace.MaceIndex), hp: 999f);
            grapple.SetButtonHeld(true);
            session.Step(stepSeconds);
            var caught = grapple.HeldEnemy;
            session.Step(stepSeconds);
            grapple.SetButtonHeld(false);
            session.Step(stepSeconds);
            return caught;
        }

        /// <summary>
        /// Puts a one-hit-point enemy exactly where the mace already is, mid-swing: the very next step's sweep
        /// hits and kills it for real (<c>GameWorld</c>'s mace step), rather than killing it directly. Returns that enemy.
        /// </summary>
        public static EnemyEntity KillAnEnemyWithTheMace(GameSession session, ChainController chain, MaceController mace, float stepSeconds)
        {
            var enemy = SpawnEnemyAt(session, chain.GetLinkPosition(mace.MaceIndex), hp: 0.01f);
            session.Step(stepSeconds);
            return enemy;
        }

        /// <summary>
        /// Puts a live enemy on the angel for one step: <c>GameWorld.CheckContact</c> costs real HP (at most once
        /// per <c>hurtDelay</c>), exactly as a real collision would. Call it a few times, each its own step, for a
        /// few hits that do not end the round.
        /// </summary>
        public static void TakeAHit(GameSession session, AngelController angel, float stepSeconds)
        {
            SpawnEnemyAt(session, angel.transform.position, hp: 999f);
            session.Step(stepSeconds);
        }

        /// <summary>
        /// Where to put the angel so <c>HeartFloat.TryCollect</c>'s <c>rectIntersect</c> (Root.vb's, corners only)
        /// actually overlaps, with real margin rather than landing exactly on an edge:
        /// <list type="bullet">
        /// <item>Y: <see cref="HeartFloat.AngelRect"/> sits entirely <em>above</em> the angel's position (its
        /// Bottom is <see cref="HeartFloat.Settings.AngelRectBottom"/> above it, its Top a further
        /// <c>AngelSize / 2</c> up), so landing on the heart's own Y only grazes the top edge of its rectangle
        /// (<see cref="HeartFloat.BuildRect"/> centres the heart's own rect on its Y). Subtracting
        /// <c>AngelRectBottom</c> puts the angel rectangle's own bottom edge at the heart's Y, inside its
        /// rectangle with margin on both sides, so the hover bob (angel.json's hoverMax) cannot jitter it out.</item>
        /// <item>X: the heart's rectangle is <c>RectWidth</c> wide from its own X, not centred on it; landing
        /// exactly on the heart's X leaves zero margin on the near edge, so the heart's own slow sideways sway
        /// moving a fraction of a frame during the very step that checks it (hearts advance before
        /// <see cref="GameWorld.CheckHearts"/> runs, inside the same <see cref="GameWorld.Step"/>) is enough to
        /// miss. Landing at the middle of the heart's width keeps full margin either way.</item>
        /// </list>
        /// </summary>
        public static Vec2 HeartTeleportTarget(HeartFloat rules) =>
            new Vec2(rules.X + rules.Config.RectWidth / 2f, rules.Y - rules.Config.AngelRectBottom);

        /// <summary>
        /// Teleports the angel onto the heart (<see cref="AngelFlight.Teleport"/>, <see cref="HeartTeleportTarget"/>)
        /// and steps once: the real <c>checkItemCollect</c> (<see cref="GameWorld.CheckHearts"/>) picks it up,
        /// heals and scores it.
        /// </summary>
        public static void CollectTheHeart(GameSession session, AngelController angel, HeartPickup heart, float stepSeconds)
        {
            angel.Flight?.Teleport(HeartTeleportTarget(heart.Rules));
            session.Step(stepSeconds);
        }

        /// <summary>
        /// Forces the round to end the way a real beating would: a fresh, unkillable enemy is put back on the
        /// angel every step until <see cref="GameState.IsGameOver"/> (the same <c>CheckContact</c> path
        /// <see cref="TakeAHit"/> uses, just kept up until HP runs out). Returns the number of steps it took.
        /// </summary>
        public static int ForceGameOverByContact(GameSession session, AngelController angel, float stepSeconds = 0.3f, int maxSteps = 200)
        {
            var steps = 0;
            while (!session.State.IsGameOver && steps < maxSteps)
            {
                SpawnEnemyAt(session, angel.transform.position, hp: 999f);
                session.Step(stepSeconds);
                steps++;
            }

            return steps;
        }

        /// <summary>
        /// Runs <c>Engine.vb GameOver</c>'s fade all the way through (<c>GameWorld.StepGameOver</c>'s per-step
        /// ambient drop) to the automatic reset, with no extra time passing beyond what the fade itself takes
        /// (GameWorld.Step does nothing for a zero delta, so each step carries <paramref name="stepSeconds"/>). Returns once the world has
        /// reset itself, or <paramref name="maxSteps"/> is reached.
        /// </summary>
        public static void RunGameOverFadeToReset(GameSession session, float stepSeconds = 0.02f, int maxSteps = 200)
        {
            var steps = 0;
            while (session.State.IsGameOver && steps < maxSteps)
            {
                session.Step(stepSeconds);
                steps++;
            }
        }
    }
}
