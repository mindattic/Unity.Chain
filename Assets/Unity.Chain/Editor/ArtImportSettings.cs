using UnityEditor;
using UnityEngine;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// UNI-T19: every sprite imported under Art/Angel, Art/Enemies, Art/Chain (mace.png, mace2.png,
    /// magnet.png) and Art/Background imports at a native 1-unit-wide baseline
    /// (<c>spritePixelsPerUnit</c> from the texture's own pixel width, so the mapping holds even if the
    /// art is ever resized) -- Angel and Enemies get a <b>centred</b> pivot: angel.json's own doc
    /// comment says it plainly ("the port's sprites are centred"), and the enemies match, both being
    /// physics circles (<c>EnemyEntity.Overlaps</c>, <c>GameWorld.CheckContact</c>) centred on the same
    /// point the view renders at. AngelController and Enemy then scale their renderer from that 1-unit
    /// baseline up to the game's own size figure (angel.json's <c>port.contactRadius</c> x2, the
    /// enemy's own <c>EnemyEntity.Radius</c> x2) every frame, exactly as HeartPickup already does for
    /// heart.png -- so a sprite's native import size here is only ever a baseline, never the final
    /// on-screen size. Art/Chain/link.png is left-edge, Repeat-wrapped sprite metadata that nothing
    /// reads: only its raw texture is used (tiled on the chain's LineRenderer,
    /// SceneBuilder.CreateChain), so its own pivot/alignment is moot. Art/Background/*.png is also
    /// left-edge, as Root.vb's setupVertex quad is: AreaBackdrop stretches whichever area is shown over
    /// Background.vb's quad (area.json's bgWidth x 2 bgHeight, UNI-T40), whatever the picture's own pixel
    /// size, and Art/Room/brick.jpg (Cube.vb's texCube1) is centred and stretched over the brick room's
    /// far wall the same way. Assets outside those folders (Art/Icon,
    /// Art/Items/heart.png already hand-tuned by UNI-T44) are left untouched.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string AngelFolder = "Assets/Unity.Chain/Art/Angel/";
        private const string EnemiesFolder = "Assets/Unity.Chain/Art/Enemies/";
        private const string ChainFolder = "Assets/Unity.Chain/Art/Chain/";
        private const string BackgroundFolder = "Assets/Unity.Chain/Art/Background/";

        /// <summary>Cube.vb's texCube1, gfx/brick.jpg (UNI-T40): centred, AreaBackdrop stretches it over the room's far wall.</summary>
        private const string RoomFolder = "Assets/Unity.Chain/Art/Room/";

        /// <summary>Form1.vb Startup's loading pictures (UNI-T58), 640 x 480 each, drawn stretched over the window.</summary>
        private const string LoadingFolder = "Assets/Unity.Chain/Resources/UI/Load/";

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (path.StartsWith(LoadingFolder))
            {
                // A plain UI texture at its own 640 x 480: not rounded to a power of two, no mipmaps, not tiled.
                var picture = (TextureImporter)assetImporter;
                picture.textureType = TextureImporterType.Default;
                picture.npotScale = TextureImporterNPOTScale.None;
                picture.mipmapEnabled = false;
                picture.wrapMode = TextureWrapMode.Clamp;
                picture.filterMode = FilterMode.Bilinear;
                return;
            }

            bool centered;
            var tiled = false;

            if (path.StartsWith(AngelFolder) || path.StartsWith(EnemiesFolder))
            {
                centered = true;
            }
            else if (path == ChainFolder + "link.png")
            {
                centered = false;
                tiled = true;
            }
            else if (path.StartsWith(ChainFolder))
            {
                // mace.png, mace2.png, magnet.png
                centered = true;
            }
            else if (path.StartsWith(BackgroundFolder))
            {
                centered = false;
            }
            else if (path.StartsWith(RoomFolder))
            {
                centered = true;
            }
            else
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            // A 1-unit-wide native baseline; the view components rescale from this (see class doc).
            importer.GetSourceTextureWidthAndHeight(out var width, out _);
            importer.spritePixelsPerUnit = width;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.Custom);
            settings.spritePivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            importer.SetTextureSettings(settings);
        }
    }
}
