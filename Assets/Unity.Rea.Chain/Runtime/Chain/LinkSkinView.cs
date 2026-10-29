using System;
using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Draws one player's chain link by link, as Chain.vb's renderChain does (U.CHN-T94): every link up to the mace on
    /// its own linkSize quad, from the link's X and centred on its Y, in the skin it wears: link.png, or once Form1.vb's
    /// U has put the gems on (<see cref="PlayerEntity.HasGemSkins"/>, UNI-T41) aqua, emerald, teardrop, tablet and pearl
    /// on links 1 to 5. With chain.json's <c>port.chainLine</c> (an option) the port's tiled line draws the plain links
    /// instead and only the gems are drawn here. Which skin a link wears is the
    /// world's (<see cref="PlayerEntity.LinkSkin"/>): the mace's link stays the mace (BIBLE V25) and the parked links
    /// past it are not drawn. As Chain.vb draws link 0 up to the mace in order, a later link is drawn over an earlier
    /// one. The pictures are wired by their chain.json names (<see cref="Skins"/>); the component changes nothing in
    /// the world.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after ChainController, so the skins track this frame's drawn chain
    [RequireComponent(typeof(ChainController))]
    public sealed class LinkSkinView : MonoBehaviour
    {
        /// <summary>The skins' sorting order: over the chain line and the angel, under the mace (10).</summary>
        public const int SortingOrder = 5;

        /// <summary>How far each later link is drawn towards the camera, so link 5 sits over link 4.</summary>
        private const float LinkDepthStep = 0.0001f;

        /// <summary>One skin's picture, under its chain.json name.</summary>
        [Serializable]
        public struct SkinArt
        {
            public SkinArt(string name, Sprite sprite)
            {
                Name = name;
                Sprite = sprite;
            }

            public string Name;
            public Sprite Sprite;
        }

        [Tooltip("Every link skin's picture, by its chain.json name.")]
        [SerializeField] private SkinArt[] skins = Array.Empty<SkinArt>();

        private readonly List<SpriteRenderer> _links = new List<SpriteRenderer>();
        private ChainController _chain;
        private bool? _plainLinks;

        /// <summary>
        /// Whether plain links are drawn here as link.png (true by default, as renderChain draws them) rather than by the
        /// chain's tiled line (chain.json's <c>port.chainLine</c>). Setting it overrides the data for this view.
        /// </summary>
        public bool DrawsPlainLinks
        {
            get => _plainLinks ?? !Chain.Settings.Chain.Port.ChainLine;
            set => _plainLinks = value;
        }

        public ChainController Chain => _chain != null ? _chain : _chain = GetComponent<ChainController>();

        /// <summary>The skin pictures, by name.</summary>
        public SkinArt[] Skins
        {
            get => skins;
            set => skins = value ?? Array.Empty<SkinArt>();
        }

        /// <summary>The picture of skin <paramref name="name"/>, or null if none is wired.</summary>
        public Sprite SpriteFor(string name)
        {
            foreach (var art in skins)
            {
                if (art.Name == name)
                {
                    return art.Sprite;
                }
            }

            return null;
        }

        /// <summary>The renderer drawing link <paramref name="link"/>'s skin, or null if that link is drawn by the line alone.</summary>
        public SpriteRenderer RendererFor(int link) =>
            link >= 0 && link < _links.Count && _links[link] != null && _links[link].enabled ? _links[link] : null;

        /// <summary>Places a sprite on every link that wears a skin other than the plain link, and hides the rest.</summary>
        public void RenderSkins()
        {
            var entity = Chain.Entity;
            // Online as a client the chain is the remote authority's, whose snapshots carry no link skins (U.CHN-T73):
            // none are drawn over it rather than this screen's own world's on the wrong links.
            var count = entity != null && Chain.Simulation != null && !Chain.IsShowingAuthority ? entity.Chain.ActiveLinkCount : 0;
            var plain = entity != null ? entity.Setup.Skins.Link : null;
            var drawsPlain = DrawsPlainLinks;
            var line = Chain.GetComponent<LineRenderer>();
            if (line != null)
            {
                // The line still carries the drawn chain's positions; it is only seen when it draws the plain links.
                line.enabled = !drawsPlain;
            }

            for (var i = 0; i < Math.Max(count, _links.Count); i++)
            {
                var skin = i < count ? entity.LinkSkin(i) : null;
                var sprite = skin != null && (drawsPlain || skin != plain) ? SpriteFor(skin) : null;
                if (sprite == null)
                {
                    if (i < _links.Count && _links[i] != null)
                    {
                        _links[i].enabled = false;
                    }

                    continue;
                }

                var link = LinkRenderer(i);
                link.sprite = sprite;
                link.enabled = true;
                // Chain.vb draws each link's linkSize quad from the link's X, centred on its Y (setupVertex), whatever
                // the picture's own pivot (link.png's is its left edge, the gems' their centre).
                var width = LinkWidth;
                var pivot = PivotFraction(sprite);
                var position = Chain.GetRenderedLinkPosition(i) + new Vector3(width * pivot.x, width * (pivot.y - 0.5f), 0f);
                position.z -= (i + 1) * LinkDepthStep;
                link.transform.position = position;
                link.transform.localScale = Vector3.one * width;
            }
        }

        /// <summary>Where a picture's pivot sits in it, as a fraction of its width and height.</summary>
        private static Vector2 PivotFraction(Sprite sprite)
        {
            var rect = sprite.rect;
            return rect.width > 0f && rect.height > 0f
                ? new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height)
                : new Vector2(0.5f, 0.5f);
        }

        /// <summary>One link across: chain.json's linkSize quad (each sprite is one world unit wide at scale 1; U.CHN-T93).</summary>
        public float LinkWidth => Chain.Settings.Mapping.Length(Chain.Settings.Chain.LinkSize);

        private SpriteRenderer LinkRenderer(int index)
        {
            while (_links.Count <= index)
            {
                _links.Add(null);
            }

            if (_links[index] == null)
            {
                var go = new GameObject($"Link {index}");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrder;
                _links[index] = renderer;
            }

            return _links[index];
        }

        // The link sprites are children of the chain, so they go with it.
        private void LateUpdate() => RenderSkins();
    }
}
