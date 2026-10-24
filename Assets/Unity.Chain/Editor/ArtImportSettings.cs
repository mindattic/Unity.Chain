using UnityEditor;
using UnityEngine;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// UNI-T19: the original drew every sprite with Root.vb's <c>setupVertex</c>, a quad spanning
    /// X:[0, width], Y:[-height, height] -- i.e. the sprite's pivot sits on its left edge, vertically
    /// centred, and its drawn size is the data files' own figure (angelSize/enemySize 0.3, linkSize
    /// 0.025, world units). This postprocessor reproduces that for every sprite imported under
    /// Art/Angel, Art/Enemies, Art/Chain and Art/Background, deriving <c>spritePixelsPerUnit</c> from
    /// each texture's own pixel width so the mapping holds even if the source art is ever resized --
    /// instead of hand-maintaining .meta files per sprite. Art/Chain/mace*.png and magnet.png use a
    /// centred pivot at a 1-unit size instead (MaceController's own documented assumption: "the
    /// visual is one world unit across at scale 1"). Art/Background/*.png get the same left-edge,
    /// 1-unit sprite that SceneBuilder's Background stretches non-uniformly to Background.vb's quad
    /// (bgWidth x 2*bgHeight, UNI-T40 owns the rest: the B/N toggle, the brick-room fallback, the
    /// data-driven area list). Assets outside those folders (Art/Icon, Art/Items/heart.png, already
    /// hand-tuned by UNI-T44) are left untouched.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string AngelFolder = "Assets/Unity.Chain/Art/Angel/";
        private const string EnemiesFolder = "Assets/Unity.Chain/Art/Enemies/";
        private const string ChainFolder = "Assets/Unity.Chain/Art/Chain/";
        private const string BackgroundFolder = "Assets/Unity.Chain/Art/Background/";

        private const float AngelAndEnemySize = 0.3f; // angel.json's angelSize / Enemy.vb's enemySize
        private const float LinkSize = 0.025f; // chain.json's linkSize
        private const float MaceVisualSize = 1f; // MaceController's "one world unit at scale 1"
        private const float BackgroundUnitSize = 1f; // SceneBuilder scales this non-uniformly to the quad

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            float worldSize;
            bool centered;
            var tiled = false;

            if (path.StartsWith(AngelFolder) || path.StartsWith(EnemiesFolder))
            {
                worldSize = AngelAndEnemySize;
                centered = false;
            }
            else if (path == ChainFolder + "link.png")
            {
                worldSize = LinkSize;
                centered = false;
                tiled = true;
            }
            else if (path.StartsWith(ChainFolder))
            {
                // mace.png, mace2.png, magnet.png
                worldSize = MaceVisualSize;
                centered = true;
            }
            else if (path.StartsWith(BackgroundFolder))
            {
                worldSize = BackgroundUnitSize;
                centered = false;
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

            importer.GetSourceTextureWidthAndHeight(out var width, out _);
            importer.spritePixelsPerUnit = width / worldSize;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.Custom);
            settings.spritePivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            importer.SetTextureSettings(settings);
        }
    }
}
