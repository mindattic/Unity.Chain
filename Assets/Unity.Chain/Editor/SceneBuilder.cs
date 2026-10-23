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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var levelBounds = CreateLevelBounds();
            var angel = CreateAngel(levelBounds);
            CreateCamera(levelBounds);
            var (chain, grapple) = CreateChain();
            angel.Chain = chain;
            var session = CreateGameSession(angel, levelBounds);
            var sceneLight = CreateSceneLight(session);
            CreateGameHud(session, grapple, sceneLight);
            CreateEnemySpawner(session);
            CreateHeart();
            CreateMusicPlayer();

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
            // hand the world hangs link 0 from; there is no sprite art in the repo yet
            // (scaffold stage), so this renderer has no sprite or frames to show until that lands.
            SetPrivateField(angel, "spriteRenderer", spriteRenderer);
            SetPrivateField(angel, "handSocketTransform", handSocketGo.transform);

            return angel;
        }

        private static void CreateCamera(LevelBounds levelBounds)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<CameraRig>();
            rig.Level = levelBounds;
        }

        private static (ChainController chain, GrappleController grapple) CreateChain()
        {
            var go = new GameObject("Chain");

            var line = go.AddComponent<LineRenderer>();
            line.material = LoadOrCreateLineMaterial();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;

            // The chain's player is seated by the GameSession, which binds these to the world.
            var chain = go.AddComponent<ChainController>();
            go.AddComponent<MaceController>();
            var grapple = go.AddComponent<GrappleController>();

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

        private static void CreateGameHud(GameSession session, GrappleController grapple, SceneLight sceneLight)
        {
            var go = new GameObject("GameHud");
            var hud = go.AddComponent<GameHud>();
            hud.Session = session;
            hud.Grapple = grapple;
            hud.SceneLight = sceneLight;
        }

        private const string HeartSpritePath = "Assets/Unity.Chain/Art/Items/heart.png";

        private static void CreateHeart()
        {
            var go = new GameObject("Heart");
            var renderer = go.AddComponent<SpriteRenderer>();
            // Item.vb's gfx/item/heart.png, magenta colour key baked to alpha, 16 px = itemSize 0.05.
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
            var heart = go.AddComponent<HeartPickup>();
            heart.Renderer = renderer;
        }

        private static void CreateMusicPlayer()
        {
            var go = new GameObject("MusicPlayer");
            go.AddComponent<AudioSource>();
            go.AddComponent<MusicPlayer>();
        }

        private static Material LoadOrCreateLineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "ChainLine" };

            Directory.CreateDirectory(Path.GetDirectoryName(LineMaterialPath) !);
            AssetDatabase.CreateAsset(material, LineMaterialPath);
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
    }
}
