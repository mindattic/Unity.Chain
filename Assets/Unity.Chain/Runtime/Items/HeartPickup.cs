using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Shows one of the world's hearts (Item.vb's Health10): its <see cref="HeartFloat"/> position in the
    /// original's own units, drawn with the original's heart sprite whose left edge sits on X, hidden
    /// once picked up until the world resets (Root.vb's Reset). The heart itself lives in the
    /// <see cref="GameWorld"/>, which moves it, checks the touch, heals and resets it; this only reads it.
    /// It joins its session's world when play starts (or when the session adds it, <see cref="GameSession.AddHeart"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)] // joins the world before GameSession's Start resets it
    public sealed class HeartPickup : MonoBehaviour
    {
        [Tooltip("The authority whose world holds this heart. Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private HeartFloat _heart;

        /// <summary>The world's heart this shows; an unseated heart shows a heart of its own at the data files' figures.</summary>
        public HeartFloat Rules => _heart ?? (_heart = new HeartFloat(SettingsStore.Current.Heart));

        /// <summary>True once the session has put this heart in its world.</summary>
        public bool IsInWorld { get; private set; }

        public int HealAmount => Rules.HealAmount;

        public bool IsCollected => Rules.PickedUp;

        public GameSession Session
        {
            get => session;
            set => session = value;
        }

        public SpriteRenderer Renderer
        {
            get => spriteRenderer != null ? spriteRenderer : (spriteRenderer = GetComponent<SpriteRenderer>());
            set => spriteRenderer = value;
        }

        /// <summary>Called by <see cref="GameSession.AddHeart"/>: from now on this shows <paramref name="heart"/>.</summary>
        internal void Bind(GameSession owner, HeartFloat heart)
        {
            session = owner;
            _heart = heart;
            IsInWorld = true;
            Refresh();
        }

        /// <summary>Draws the heart where the world has it, hidden once picked up. Runs every frame; public so tests can drive it.</summary>
        public void Refresh()
        {
            var rules = Rules;
            transform.position = new Vector3(rules.X, rules.Y, transform.position.z);
            var sprite = Renderer;
            if (sprite != null)
            {
                sprite.enabled = !rules.PickedUp;

                // Drawn itemSize wide, whatever the sprite's import size.
                var width = sprite.sprite != null ? sprite.sprite.bounds.size.x : 0f;
                if (width > 0f)
                {
                    transform.localScale = Vector3.one * (rules.Config.ItemSize / width);
                }
            }
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            if (session != null && !IsInWorld)
            {
                session.AddHeart(this);
            }

            Refresh();
        }

        private void LateUpdate() => Refresh();
    }
}
