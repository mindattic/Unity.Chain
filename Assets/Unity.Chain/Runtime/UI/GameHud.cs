using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Screen-space HUD drawn with IMGUI (no canvas, prefab or font asset needed), laid out like
    /// Engine.vb's <c>renderDisplay</c>: the HP bar and score top-left, the grappled enemy's HP
    /// under them while the grapple holds one, a centered "- PAUSE -" banner while paused, and a
    /// game-over banner with a restart prompt. Text scales with the screen height. Reads
    /// everything from <see cref="GameSession"/> and <see cref="GrappleController"/>; the strings
    /// come from <see cref="HudText"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        [Tooltip("Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("Shows the grappled enemy's HP while it holds one. Falls back to the scene's GrappleController.")]
        [SerializeField] private GrappleController grapple;

        [Header("Look")]
        [Tooltip("HUD text height as a fraction of the screen height.")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float textScale = 0.03f;
        [Tooltip("Inset of the HUD from the screen's top-left corner, as a fraction of the screen height.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float margin = 0.02f;
        [SerializeField] private Color statusColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color grappleColor = new Color(1f, 1f, 0.2f);
        [SerializeField] private Color bannerColor = Color.white;

        private GUIStyle _label;
        private GUIStyle _shadow;
        private GUIStyle _banner;
        private GUIStyle _subtitle;

        public GameSession Session
        {
            get => session;
            set => session = value;
        }

        public GrappleController Grapple
        {
            get => grapple;
            set => grapple = value;
        }

        /// <summary>The HP of the enemy the grapple holds, or null when it holds none.</summary>
        public float? GrappledHp => grapple != null && grapple.Target is Enemy enemy && enemy.IsAlive
            ? enemy.Hp
            : (float?)null;

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            if (grapple == null)
            {
                grapple = FindAnyObjectByType<GrappleController>();
            }
        }

        private void OnGUI()
        {
            if (session == null)
            {
                return;
            }

            EnsureStyles();
            var state = session.State;
            var lineHeight = Mathf.Max(8, Mathf.RoundToInt(Screen.height * textScale));
            _label.fontSize = lineHeight;
            _shadow.fontSize = lineHeight;
            _banner.fontSize = lineHeight * 2;
            _subtitle.fontSize = lineHeight;

            var inset = Screen.height * margin;
            var x = inset;
            var y = inset;
            var width = Screen.width - inset * 2f;

            Line(HudText.Hp(state.Hp), statusColor, x, ref y, width, lineHeight);
            Line(HudText.Score(state.Score), statusColor, x, ref y, width, lineHeight);

            var grappled = GrappledHp;
            if (grappled.HasValue)
            {
                y += lineHeight * 0.5f;
                Line(HudText.Grappled(grappled.Value), grappleColor, x, ref y, width, lineHeight);
            }

            if (state.IsGameOver)
            {
                Banner(HudText.GameOver, null, 1f - state.AmbientLevel);
            }
            else if (state.IsPaused)
            {
                Banner(HudText.Paused, null);
            }
        }

        private void Line(string text, Color color, float x, ref float y, float width, int lineHeight)
        {
            var rect = new Rect(x, y, width, lineHeight * 1.4f);
            var offset = Mathf.Max(1f, lineHeight / 12f);
            GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), text, _shadow);
            _label.normal.textColor = color;
            GUI.Label(rect, text, _label);
            y += lineHeight * 1.3f;
        }

        private void Banner(string title, string subtitle, float dim = 0.45f)
        {
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, dim);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = previous;

            _banner.normal.textColor = bannerColor;
            var height = _banner.fontSize * 1.5f;
            GUI.Label(new Rect(0f, Screen.height * 0.5f - height, Screen.width, height), title, _banner);

            if (!string.IsNullOrEmpty(subtitle))
            {
                _subtitle.normal.textColor = bannerColor;
                GUI.Label(new Rect(0f, Screen.height * 0.5f + height * 0.1f, Screen.width, height), subtitle, _subtitle);
            }
        }

        private void EnsureStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
            };
            _shadow = new GUIStyle(_label);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            _banner = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _subtitle = new GUIStyle(_banner) { fontStyle = FontStyle.Normal };
        }
    }
}
