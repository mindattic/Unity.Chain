using System;
using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Draws the links of one player's chain that wear a skin other than the plain link (UNI-T41): once Form1.vb's U
    /// has put the gems on (<see cref="PlayerEntity.HasGemSkins"/>), links 1 to 5 are drawn as aqua, emerald,
    /// teardrop, tablet and pearl, each a square one link wide (the chain line's width, as the mace's 3.5x is of it)
    /// centred on the drawn link, over the line that draws every link with link.png. Which skin a link wears is the
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
            var count = entity != null && Chain.Simulation != null ? entity.Chain.ActiveLinkCount : 0;
            var plain = entity != null ? entity.Setup.Skins.Link : null;

            for (var i = 0; i < Math.Max(count, _links.Count); i++)
            {
                var skin = i < count ? entity.LinkSkin(i) : null;
                var sprite = skin != null && skin != plain ? SpriteFor(skin) : null;
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
                var position = Chain.GetRenderedLinkPosition(i);
                position.z -= (i + 1) * LinkDepthStep;
                link.transform.position = position;
                link.transform.localScale = Vector3.one * LinkWidth;
            }
        }

        /// <summary>One link across: the chain line's width (each sprite is one world unit wide at scale 1).</summary>
        private float LinkWidth
        {
            get
            {
                var line = Chain.GetComponent<LineRenderer>();
                return line != null ? line.widthMultiplier : 1f;
            }
        }

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
