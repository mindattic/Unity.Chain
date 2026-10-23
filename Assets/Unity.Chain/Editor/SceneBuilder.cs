using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var levelBounds = CreateLevelBounds();
            var (angel, handSocket) = CreateAngel(levelBounds);
            CreateCamera(levelBounds);
            var (grapple, hitDetector) = CreateChain(handSocket, levelBounds);
            var session = CreateGameSession(angel, hitDetector);
            CreateGameHud(session, grapple);
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

        private static (AngelController angel, Transform handSocket) CreateAngel(LevelBounds levelBounds)
        {
            var go = new GameObject("Angel");
            go.AddComponent<SpriteRenderer>();

            var handSocketGo = new GameObject("HandSocket");
            handSocketGo.transform.SetParent(go.transform, false);

            var angel = go.AddComponent<AngelController>();
            angel.LevelBounds = levelBounds;
            // No public setter: AngelController moves this transform to the hand socket each
            // frame, and it is what ChainController pins link 0 to below.
            SetPrivateField(angel, "handSocketTransform", handSocketGo.transform);

            return (angel, handSocketGo.transform);
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

        private static (GrappleController grapple, MaceHitDetector hitDetector) CreateChain(
            Transform handSocket, LevelBounds levelBounds)
        {
            var go = new GameObject("Chain");

            var line = go.AddComponent<LineRenderer>();
            line.material = LoadOrCreateLineMaterial();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;

            var chain = go.AddComponent<ChainController>();
            chain.HandSocket = handSocket;
            chain.LevelBounds = levelBounds;

            go.AddComponent<MaceController>();

            var grapple = go.AddComponent<GrappleController>();
            grapple.Chain = chain;

            var hitDetector = go.AddComponent<MaceHitDetector>();
            hitDetector.Chain = chain;
            hitDetector.Grapple = grapple;

            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var chainAudio = go.AddComponent<ChainAudio>();
            chainAudio.Chain = chain;
            chainAudio.Grapple = grapple;
            chainAudio.HitDetector = hitDetector;
            chainAudio.SfxSource = audioSource;

            return (grapple, hitDetector);
        }

        private static GameSession CreateGameSession(AngelController angel, MaceHitDetector hitDetector)
        {
            var go = new GameObject("GameSession");
            var session = go.AddComponent<GameSession>();
            session.Angel = angel;
            session.MaceHits = hitDetector;
            return session;
        }

        private static void CreateGameHud(GameSession session, GrappleController grapple)
        {
            var go = new GameObject("GameHud");
            var hud = go.AddComponent<GameHud>();
            hud.Session = session;
            hud.Grapple = grapple;
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
