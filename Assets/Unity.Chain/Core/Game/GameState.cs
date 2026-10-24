using System;


namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Pure game-session rules the HUD shows, ported from Engine.vb / Angel.vb: the angel's HP
    /// (<c>maxHP</c> 30) drops by one when an enemy touches it, at most once per hurt delay
    /// (<c>checkAngelHit</c>, <c>hurtDelay</c> 0.25 s); every mace hit scores damage x 1000
    /// (<c>checkMaceImpact</c>: <c>Score += damage * 1000</c>); the game can be paused; and it is
    /// over when HP runs out. Kept free of MonoBehaviour so it is unit-testable;
    /// the session feeds it contacts, hits and input.
    ///
    /// Engine.vb's <c>renderDisplay</c> drew <c>HP + 1</c> bars and <c>GameOver</c> only ended the
    /// game below zero, so 30 HP took 31 hits (BIBLE D10). <see cref="Settings.HpOvershoot"/> (1 by
    /// default, UNI-T45) reproduces that exactly; 0 is the rebuild's own earlier choice (exactly
    /// <see cref="Hp"/> bars, game over at HP 0).
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

            /// <summary>
            /// Extra "free" HP beyond zero the HUD shows and the game survives before ending (Engine.vb's
            /// <c>renderDisplay</c> drawing <c>HP + 1</c> bars and <c>GameOver</c> ending only below zero, BIBLE
            /// D10): the HUD shows <c>Hp + HpOvershoot</c> bars and the game ends once <c>Hp &lt; 1 - HpOvershoot</c>.
            /// 0 is the rebuild's own earlier choice (exactly <see cref="Hp"/> bars, game over at HP 0); 1 (the
            /// default, UNI-T45) is the original's own: 30 HP takes 31 hits.
            /// </summary>
            public int HpOvershoot;

            public static Settings Default => new Settings
            {
                MaxHp = 30,
                HurtDelay = 0.25f,
                ContactDamage = 1,
                PointsPerDamage = 1000f,
                HpOvershoot = 1,
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
        public int MaxHp => Calc.Max(1, Config.MaxHp);
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
            Hp -= Calc.Max(0, Config.ContactDamage);
            HpChanged?.Invoke(Hp);

            if (Hp < 1 - Config.HpOvershoot)
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
            Hp = Calc.Min(MaxHp, Hp + amount);
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

            var points = Calc.RoundToInt(Calc.Abs(damage) * Config.PointsPerDamage);
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
