using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// Builds the game's one playable scene (UNI-T16: the repo shipped with no .unity scene and an
    /// empty EditorBuildSettings, so <c>BuildPlayer</c> always failed with "0 scenes"). Generating
    /// it from the runtime components' own public API, instead of hand-maintaining a .unity asset,
    /// keeps the scene in sync whenever a component's wiring changes: rerun this after editing
    /// <c>AngelController</c>, <c>ChainController</c> and friends, from the menu or in batch mode
    /// via <c>-executeMethod MindAttic.Chain.EditorTools.SceneBuilder.BuildMainScene</c>.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string LineMaterialPath = "Assets/Unity.Chain/Materials/ChainLine.mat";
        private const string ZoneMaterialPath = "Assets/Unity.Chain/Materials/ZoneLine.mat";

        [MenuItem("MindAttic/Chain/Build Main Scene")]
        public static void BuildMainScene()
        {
            // Interactive use (the menu item) must not silently discard unsaved work in whatever
            // scene is open; batch mode (CI, -executeMethod) has no user to ask and no unsaved
            // work worth protecting.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // The URP 2D pipeline first, so the sprites added below take its lit default material
            // and the SceneLight's global light shows on them (UNI-T18).
            RenderPipelineBuilder.Ensure();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBackground();
            var levelBounds = CreateLevelBounds();
            var angel = CreateAngel(levelBounds);
            var rig = CreateCamera(levelBounds);
            var (chain, grapple) = CreateChain();
            angel.Chain = chain;
            var session = CreateGameSession(angel, levelBounds);
            // Root.vb's Reset recentres the camera and jumps the music: both follow the session's resets.
            rig.Session = session;
            var sceneLight = CreateSceneLight(session);
            var hud = CreateGameHud(session, grapple, sceneLight);
            CreateHitZoneOverlay(session, hud);
            CreateEnemySpawner(session);
            CreateHeart(session);
            CreateMusicPlayer(session);
            CreateWindowAspectLock();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) !);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"SceneBuilder: wrote {ScenePath} and registered it in Build Settings.");
        }

        private static LevelBounds CreateLevelBounds()
        {
            var go = new GameObject("LevelBounds");
            return go.AddComponent<LevelBounds>();
        }

        private const string AngelArtPath = "Assets/Unity.Chain/Art/Angel/";

        private static AngelController CreateAngel(LevelBounds levelBounds)
        {
            var go = new GameObject("Angel");
            var spriteRenderer = go.AddComponent<SpriteRenderer>();

            var handSocketGo = new GameObject("HandSocket");
            handSocketGo.transform.SetParent(go.transform, false);

            var angel = go.AddComponent<AngelController>();
            angel.LevelBounds = levelBounds;
            // Neither has a public setter: AngelController finds both via GetComponentInChildren
            // / LevelBounds.Find() when unset, but wiring them here makes the scene's intent
            // explicit instead of relying on that runtime fallback. handSocketTransform follows the
            // hand the world hangs link 0 from.
            SetPrivateField(angel, "spriteRenderer", spriteRenderer);
            SetPrivateField(angel, "handSocketTransform", handSocketGo.transform);

            // Angel.vb's 4-frame-per-facing flap (gfx/angel/angel_left1-4, angel_right1-4; UNI-T19).
            SetPrivateFieldArray(angel, "rightFrames", LoadSprites(AngelArtPath, "angel_right", 1, 4));
            SetPrivateFieldArray(angel, "leftFrames", LoadSprites(AngelArtPath, "angel_left", 1, 4));

            return angel;
        }

        private static CameraRig CreateCamera(LevelBounds levelBounds)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<CameraRig>();
            rig.Level = levelBounds;
            return rig;
        }

        private const string ChainArtPath = "Assets/Unity.Chain/Art/Chain/";

        private static (ChainController chain, GrappleController grapple) CreateChain()
        {
            var go = new GameObject("Chain");

            var line = go.AddComponent<LineRenderer>();
            var lineMaterial = LoadOrCreateLineMaterial();
            // sharedMaterial, not material: the latter auto-instantiates on first read, which would
            // leave the link texture on a throwaway instance instead of the committed ChainLine.mat.
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;

            // Chain.vb drew every link with gfx/chain/link.png (UNI-T19); tiled along the line so it
            // repeats per link instead of stretching one texture the chain's whole length.
            var linkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChainArtPath + "link.png");
            if (linkSprite != null)
            {
                line.textureMode = LineTextureMode.Tile;
                lineMaterial.mainTexture = linkSprite.texture;
                lineMaterial.mainTextureScale = new Vector2(25f, 1f); // ~chain.json's maxLinks
            }

            // The chain's player is seated by the GameSession, which binds these to the world.
            var chain = go.AddComponent<ChainController>();
            var mace = go.AddComponent<MaceController>();
            var grapple = go.AddComponent<GrappleController>();

            // Chain.vb drew the mace link's own quad at 3.5x (texMace); MaceController assumes this
            // visual is one world unit across at scale 1 (gfx/chain/mace/mace.png, UNI-T19).
            var maceVisualGo = new GameObject("MaceVisual");
            maceVisualGo.transform.SetParent(go.transform, false);
            var maceRenderer = maceVisualGo.AddComponent<SpriteRenderer>();
            maceRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChainArtPath + "mace.png");
            maceRenderer.sortingOrder = 10; // drawn over the chain line and the angel
            SetPrivateField(mace, "maceVisual", maceVisualGo.transform);

            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var chainAudio = go.AddComponent<ChainAudio>();
            chainAudio.Chain = chain;
            chainAudio.SfxSource = audioSource;

            return (chain, grapple);
        }

        private static GameSession CreateGameSession(AngelController angel, LevelBounds levelBounds)
        {
            // The one authority: it holds the world, seats the angel (seat 0) and steps everything.
            var go = new GameObject("GameSession");
            var session = go.AddComponent<GameSession>();
            session.Angel = angel;
            session.Level = levelBounds;
            return session;
        }

        private static void CreateEnemySpawner(GameSession session)
        {
            var go = new GameObject("EnemySpawner");
            var spawner = go.AddComponent<EnemySpawner>();
            spawner.Session = session;
            // Enemy.vb's 7-frame loop (gfx/enemy/Shadow/enemy1-7), a random one per spawn; UNI-T19.
            spawner.Frames = LoadSprites("Assets/Unity.Chain/Art/Enemies/", "enemy", 1, 7);
        }

        private const string BackgroundSpritePath = "Assets/Unity.Chain/Art/Background/beach.png";

        private static void CreateBackground()
        {
            // Background.vb: a bgWidth (1.2) x 2*bgHeight (0.83) quad translated to x -0.56, left
            // edge on X and vertically centred (Root.vb's setupVertex), beach.png by default
            // (UNI-T19). The sprite imports as a 1x1 unit square (ArtImportSettings), stretched here
            // to that quad. The B/N toggle, the brick-room fallback and the rest of the area list are
            // UNI-T40's: this is only the static backdrop so the scene is no longer empty behind it.
            var go = new GameObject("Background");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            renderer.sortingOrder = -100;
            go.transform.position = new Vector3(-0.56f, 0f, 1f);
            go.transform.localScale = new Vector3(1.2f, 0.83f, 1f);
        }

        private static SceneLight CreateSceneLight(GameSession session)
        {
            var go = new GameObject("SceneLight");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            var sceneLight = go.AddComponent<SceneLight>();
            sceneLight.Session = session;
            sceneLight.GlobalLight = light;
            return sceneLight;
        }

        private static GameHud CreateGameHud(GameSession session, GrappleController grapple, SceneLight sceneLight)
        {
            var go = new GameObject("GameHud");
            var hud = go.AddComponent<GameHud>();
            hud.Session = session;
            hud.Grapple = grapple;
            hud.SceneLight = sceneLight;
            return hud;
        }

        private static void CreateHitZoneOverlay(GameSession session, GameHud hud)
        {
            // Zone.vb's hit boxes (UNI-T56), lit by the scene's 2D light as the original's line strip took its ambient.
            var go = new GameObject("HitZones");
            var overlay = go.AddComponent<HitZoneOverlay>();
            overlay.Session = session;
            overlay.Hud = hud;
            overlay.LineMaterial = LoadOrCreateMaterial(ZoneMaterialPath, "ZoneLine", "Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        private const string HeartSpritePath = "Assets/Unity.Chain/Art/Items/heart.png";

        private static void CreateHeart(GameSession session)
        {
            var go = new GameObject("Heart");
            var renderer = go.AddComponent<SpriteRenderer>();
            // Item.vb's gfx/item/heart.png, magenta colour key baked to alpha, 16 px = itemSize 0.05.
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
            var heart = go.AddComponent<HeartPickup>();
            heart.Renderer = renderer;
            // The heart lives in this session's world; it joins it when play starts.
            heart.Session = session;
        }

        private static void CreateMusicPlayer(GameSession session)
        {
            var go = new GameObject("MusicPlayer");
            go.AddComponent<AudioSource>();
            go.AddComponent<MusicPlayer>().Session = session;
        }

        private static void CreateWindowAspectLock()
        {
            // UNI-T38: keeps the resizable window at the original's 4:3 shape.
            var go = new GameObject("WindowAspectLock");
            go.AddComponent<WindowAspectLockBehaviour>();
        }

        private static Material LoadOrCreateLineMaterial() =>
            LoadOrCreateMaterial(LineMaterialPath, "ChainLine", "Universal Render Pipeline/Unlit");

        private static Material LoadOrCreateMaterial(string path, string name, string shaderName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name };

            Directory.CreateDirectory(Path.GetDirectoryName(path) !);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var existing in scenes)
            {
                if (existing.path == ScenePath)
                {
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateFieldArray(Object target, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Loads "{folder}{prefix}{n}.png" for n in [first, last], skipping any that are missing.</summary>
        private static Sprite[] LoadSprites(string folder, string prefix, int first, int last)
        {
            var sprites = new System.Collections.Generic.List<Sprite>();
            for (var n = first; n <= last; n++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}{prefix}{n}.png");
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites.ToArray();
        }
    }
}
