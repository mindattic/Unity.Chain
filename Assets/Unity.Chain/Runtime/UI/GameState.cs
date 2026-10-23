using System;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Pure game-session rules the HUD shows, ported from Engine.vb / Angel.vb: the angel's HP
    /// (<c>maxHP</c> 30) drops by one when an enemy touches it, at most once per hurt delay
    /// (<c>checkAngelHit</c>, <c>hurtDelay</c> 0.25 s); every mace hit scores damage x 1000
    /// (<c>checkMaceImpact</c>: <c>Score += damage * 1000</c>); the game can be paused; and it is
    /// over when HP runs out. Kept free of MonoBehaviour so it is unit-testable;
    /// <see cref="GameSession"/> feeds it contacts, hits and input.
    ///
    /// One deliberate difference: Angel.vb drew <c>HP + 1</c> bars and only ended the game below
    /// zero, so 30 HP took 31 hits. Here the HUD shows exactly <see cref="Hp"/> bars and the game
    /// ends when HP reaches 0.
    /// </summary>
    public sealed class GameState
    {
        public struct Settings
        {
            /// <summary>Starting and maximum HP (Angel.vb's maxHP).</summary>
            public int MaxHp;
            /// <summary>Seconds after a hurt before the angel can be hurt again (Angel.vb's hurtDelay).</summary>
            public float HurtDelay;
            /// <summary>HP lost per enemy contact.</summary>
            public int ContactDamage;
            /// <summary>Score per point of mace damage (Engine.vb: 1000).</summary>
            public float PointsPerDamage;

            public static Settings Default => new Settings
            {
                MaxHp = 30,
                HurtDelay = 0.25f,
                ContactDamage = 1,
                PointsPerDamage = 1000f,
            };
        }

        /// <summary>Ambient light at full (Root.vb: <c>fadeOut = 255</c>).</summary>
        public const int FullAmbient = 255;
        /// <summary>Ambient light lost per frame once the game is over (Engine.vb: <c>fadeOut -= 5</c>).</summary>
        public const int FadeStep = 5;
        /// <summary>The game resets once the ambient light is no brighter than this (Engine.vb: <c>fadeOut &gt; 6</c> keeps fading).</summary>
        public const int FadeFloor = 6;

        private float _lastHurtTime = float.NegativeInfinity;

        public GameState() : this(Settings.Default)
        {
        }

        public GameState(Settings settings)
        {
            Config = settings;
            Reset();
        }

        public Settings Config;

        public int Hp { get; private set; }
        public int MaxHp => Mathf.Max(1, Config.MaxHp);
        public int Score { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>
        /// Ambient light level 0-255 (Engine.vb's <c>fadeOut</c>): full while playing, fading down by
        /// <see cref="FadeStep"/> per <see cref="AdvanceGameOver"/> call after game over.
        /// </summary>
        public int Ambient { get; private set; } = FullAmbient;

        /// <summary>Ambient light as 0-1, for the presentation to dim the scene with.</summary>
        public float AmbientLevel => Ambient / (float)FullAmbient;

        /// <summary>True while play is running: not paused and not over.</summary>
        public bool IsPlaying => !IsPaused && !IsGameOver;

        /// <summary>Raised with the new HP each time the angel is hurt or healed.</summary>
        public event Action<int> HpChanged;

        /// <summary>Raised once when HP runs out.</summary>
        public event Action GameOver;

        /// <summary>Back to a fresh game: full HP, no score, unpaused.</summary>
        public void Reset()
        {
            Hp = MaxHp;
            Score = 0;
            IsPaused = false;
            IsGameOver = false;
            Ambient = FullAmbient;
            _lastHurtTime = float.NegativeInfinity;
        }

        /// <summary>
        /// An enemy is touching the angel at time <paramref name="now"/> (seconds). Costs
        /// <see cref="Settings.ContactDamage"/> HP unless the angel was hurt less than
        /// <see cref="Settings.HurtDelay"/> ago, or play is not running. Returns true if HP was lost.
        /// </summary>
        public bool TryHurt(float now)
        {
            if (!IsPlaying || now < _lastHurtTime + Config.HurtDelay)
            {
                return false;
            }

            _lastHurtTime = now;
            Hp = Mathf.Max(0, Hp - Mathf.Max(0, Config.ContactDamage));
            HpChanged?.Invoke(Hp);

            if (Hp <= 0)
            {
                IsGameOver = true;
                IsPaused = false;
                GameOver?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// One frame of Engine.vb's <c>GameOver</c>: while the game is over and the ambient light is
        /// above <see cref="FadeFloor"/>, it drops by <see cref="FadeStep"/>; otherwise the fade is
        /// finished and the game is due to reset itself. This does not reset: the session resets
        /// (and reloads the enemies) in one place, so nothing shows a full-light, full-HP frame of
        /// the old scene. Call once per fixed step. Returns true once the fade is finished.
        /// </summary>
        public bool AdvanceGameOver()
        {
            if (!IsGameOver)
            {
                return false;
            }

            if (Ambient > FadeFloor)
            {
                Ambient -= FadeStep;
                return false;
            }

            return true;
        }

        /// <summary>Restores HP up to the maximum (Item.vb's health pickups). Returns the HP gained.</summary>
        public int Heal(int amount)
        {
            if (IsGameOver || amount <= 0)
            {
                return 0;
            }

            var before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            if (Hp != before)
            {
                HpChanged?.Invoke(Hp);
            }

            return Hp - before;
        }

        /// <summary>Scores a mace hit of <paramref name="damage"/>. Returns the points added.</summary>
        public int AddHitScore(float damage)
        {
            if (IsGameOver)
            {
                return 0;
            }

            var points = Mathf.RoundToInt(Mathf.Abs(damage) * Config.PointsPerDamage);
            Score += points;
            return points;
        }

        /// <summary>Adds flat points (pickups). Ignored once the game is over.</summary>
        public void AddScore(int points)
        {
            if (!IsGameOver)
            {
                Score += points;
            }
        }

        /// <summary>Pauses or resumes. A finished game cannot be paused. Returns the new paused state.</summary>
        public bool SetPaused(bool paused)
        {
            IsPaused = paused && !IsGameOver;
            return IsPaused;
        }

        public bool TogglePause() => SetPaused(!IsPaused);
    }
}
