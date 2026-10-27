using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>UNI-T41: the U key's gem link skins as drawn, over the line, the mace left alone.</summary>
    public class LinkSkinViewTests
    {
        private static readonly string[] Names = { "link", "aqua", "emerald", "teardrop", "tablet", "pearl" };

        private TestRig _rig;
        private LinkSkinView _view;
        private Sprite[] _sprites;

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _view = _rig.Chain.gameObject.AddComponent<LinkSkinView>();
            _sprites = new Sprite[Names.Length];
            var art = new LinkSkinView.SkinArt[Names.Length];
            for (var i = 0; i < Names.Length; i++)
            {
                var texture = _rig.Track(new Texture2D(16, 16));
                _sprites[i] = _rig.Track(Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f));
                _sprites[i].name = Names[i];
                art[i] = new LinkSkinView.SkinArt(Names[i], _sprites[i]);
            }

            _view.Skins = art;
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        private void Render()
        {
            _rig.Step();
            _rig.Chain.RenderChain(1f);
            _view.RenderSkins();
        }

        private bool SkinLinks() => _rig.Session.Submit(new SkinLinks(_rig.Id));

        [Test]
        public void BeforeU_TheLineDrawsEveryLink()
        {
            Render();

            for (var i = 0; i <= _rig.Player.MaceIndex; i++)
            {
                Assert.IsNull(_view.RendererFor(i), $"link {i}");
            }
        }

        [Test]
        public void U_DrawsLinksOneToFiveAsTheGems_OneLinkWideOnTheDrawnLink()
        {
            Assert.IsTrue(SkinLinks());
            Render();

            Assert.IsNull(_view.RendererFor(0), "the anchor keeps link.png (the line)");
            var width = _view.LinkWidth;
            for (var i = 1; i <= 5; i++)
            {
                var renderer = _view.RendererFor(i);
                Assert.IsNotNull(renderer, $"link {i}");
                Assert.AreSame(_sprites[i], renderer.sprite, $"link {i} is {Names[i]}");
                Assert.AreEqual(LinkSkinView.SortingOrder, renderer.sortingOrder);
                Vector2 drawn = _rig.Chain.GetRenderedLinkPosition(i) + new Vector3(width / 2f, 0f, 0f);
                Assert.That(Vector2.Distance(drawn, renderer.transform.position), Is.LessThan(1e-4f));
                Assert.AreEqual(width, renderer.transform.localScale.x, 1e-6f);
            }

            Assert.Less(_view.RendererFor(5).transform.position.z, _view.RendererFor(1).transform.position.z, "a later link is drawn over an earlier one");
            Assert.IsNull(_view.RendererFor(6), "link 6 is the mace (MaceController draws it)");
        }

        [Test]
        public void TheMaceLink_KeepsTheMace_AndShowsItsGemOnceTheMaceMovesOn()
        {
            while (_rig.Mace.MaceIndex > 3)
            {
                _rig.Mace.Shorten();
            }

            SkinLinks();
            Render();
            Assert.IsNull(_view.RendererFor(3), "the mace's link is not changed");
            Assert.IsNull(_view.RendererFor(4), "past the mace nothing is drawn");
            Assert.AreSame(_sprites[2], _view.RendererFor(2).sprite);

            _rig.Mace.Lengthen();
            Render();
            Assert.AreSame(_sprites[3], _view.RendererFor(3).sprite, "teardrop once the mace is link 4");
        }

        [Test]
        public void TheGems_SurviveAReset()
        {
            SkinLinks();
            _rig.Session.Submit(new ResetGame(_rig.Id));
            Render();

            Assert.AreSame(_sprites[5], _view.RendererFor(5).sprite);
        }
    }
}
