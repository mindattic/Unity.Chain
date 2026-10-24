using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Item.vb's one Health10 heart in the scene: it shows <see cref="HeartFloat"/>'s state (its
    /// position in the original's own units, drawn with the original's heart sprite whose left edge
    /// sits on X), disappears when the angel picks it up and stays gone until <see cref="ResetHeart"/>
    /// (Root.vb's Reset). <see cref="GameSession"/> does the touch check and the healing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)] // moves before GameSession (-100) checks the touch, as the original moves then collects
    public sealed class HeartPickup : MonoBehaviour
    {
        /// <summary>True once a round has been reset: scene reloads then start the heart as Root.vb's Reset does.</summary>
        public static bool AfterReset { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() => AfterReset = false;

        [SerializeField] private SpriteRenderer spriteRenderer;

        private HeartFloat _rules;

        /// <summary>The heart's rules, tuned from item.json (in world units, through the settings registry's mapping).</summary>
        public HeartFloat Rules => _rules ?? (_rules = new HeartFloat(SettingsStore.Current.Heart));

        public int HealAmount => Rules.HealAmount;

        public bool IsCollected => Rules.PickedUp;

        public SpriteRenderer Renderer
        {
            get => spriteRenderer != null ? spriteRenderer : (spriteRenderer = GetComponent<SpriteRenderer>());
            set => spriteRenderer = value;
        }

        /// <summary>Picks the heart up if the angel at <paramref name="angelPosition"/> touches it (Item.vb's checkItemCollect).</summary>
        public bool TryCollect(Vector2 angelPosition)
        {
            if (!Rules.TryCollect(angelPosition.x, angelPosition.y))
            {
                return false;
            }

            Apply();
            return true;
        }

        /// <summary>Root.vb's Reset for the heart: back to the start and drawn again.</summary>
        public void ResetHeart()
        {
            Rules.Reset();
            Apply();
        }

        /// <summary>Runs <paramref name="deltaTime"/> seconds of original frames; public so tests can drive it.</summary>
        public int Step(float deltaTime)
        {
            var frames = Rules.Advance(deltaTime);
            if (frames > 0)
            {
                Apply();
            }

            return frames;
        }

        private void Apply()
        {
            transform.position = new Vector3(Rules.X, Rules.Y, transform.position.z);
            var sprite = Renderer;
            if (sprite != null)
            {
                sprite.enabled = !Rules.PickedUp;

                // Drawn itemSize wide, whatever the sprite's import size (16 px is 0.05 at the 1:1 mapping).
                var width = sprite.sprite != null ? sprite.sprite.bounds.size.x : 0f;
                if (width > 0f)
                {
                    transform.localScale = Vector3.one * (Rules.Config.ItemSize / width);
                }
            }
        }

        private void Awake()
        {
            if (AfterReset)
            {
                Rules.Reset();
            }

            Apply();
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);
    }
}
