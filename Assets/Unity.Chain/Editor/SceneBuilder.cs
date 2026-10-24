using System.IO;
using MindAttic.Chain.Core;
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

        /// <summary>The scene the player opens on (UNI-T58): Form1.vb Startup's loading picture, which then loads Main.</summary>
        public const string SplashScenePath = "Assets/Scenes/Splash.unity";
        private const string LineMaterialPath = "Assets/Unity.Chain/Materials/ChainLine.mat";
        private const string ZoneMaterialPath = "Assets/Unity.Chain/Materials/ZoneLine.mat";
        private const string SfxPath = "Assets/Unity.Chain/Audio/Sfx/";
        private const string MusicClipPath = "Assets/Unity.Chain/Audio/Music/music.ogg";

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

            var levelBounds = CreateLevelBounds();
            var angel = CreateAngel(levelBounds);
            var rig = CreateCamera(levelBounds);
            CreateBackground(rig);
            var (chain, grapple) = CreateChain();
            angel.Chain = chain;
            var session = CreateGameSession(angel, levelBounds);
            // Root.vb's Reset recentres the camera and jumps the music: both follow the session's resets.
            rig.Session = session;
            var sceneLight = CreateSceneLight(session);
            var hud = CreateGameHud(session, grapple, sceneLight);
            CreateScripterConsole(session, hud, sceneLight, rig);
            CreateHitZoneOverlay(session, hud);
            CreateEnemySpawner(session);
            CreateHeart(session);
            CreateMusicPlayer(session);
            CreateWindowAspectLock();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) !);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // The splash goes with it: the player opens on it, and it loads the scene just written.
            BuildSplashSceneOnly();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"SceneBuilder: wrote {SplashScenePath} and {ScenePath} and registered them, in that order, in Build Settings.");
        }

        /// <summary>
        /// Builds the Splash scene alone (UNI-T58): a black camera and the <see cref="SplashScreen"/>, Form1.vb Startup's
        /// random loading picture, which loads Main once it is up. Registered first in Build Settings, so a build opens on it.
        /// </summary>
        [MenuItem("MindAttic/Chain/Build Splash Scene")]
        public static void BuildSplashScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildSplashSceneOnly();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"SceneBuilder: wrote {SplashScenePath} and registered it first in Build Settings.");
        }

        private static void BuildSplashSceneOnly()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Form1's BackColor: black wherever the picture is not (only before its panel is up).
            var cameraGo = new GameObject("Splash Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var splash = new GameObject("SplashScreen").AddComponent<SplashScreen>();
            splash.NextScene = Path.GetFileNameWithoutExtension(ScenePath);

            Directory.CreateDirectory(Path.GetDirectoryName(SplashScenePath) !);
            EditorSceneManager.SaveScene(scene, SplashScenePath);
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
            // Engine.vb's oneFrameRender clears the target to white (255, 255, 255, 255) before renderCube.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
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
            // LineRenderer.textureMode Tile repeats the texture once per world unit times the
            // material's x scale (not "scale.x total repeats", which is Stretch's behaviour), so a
            // square-looking, undistorted tile needs the repeat length to equal the line's own width.
            var linkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChainArtPath + "link.png");
            if (linkSprite != null)
            {
                line.textureMode = LineTextureMode.Tile;
                lineMaterial.mainTexture = linkSprite.texture;
                lineMaterial.mainTextureScale = new Vector2(1f / line.widthMultiplier, 1f);
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

            // UNI-T43: Chain.vb's theMaceMode as a skin, every mode's picture wired from chain.json's maceSkins.
            var maceSkinData = SettingsStore.Current.Chain.MaceSkins;
            var maceSkins = new MaceController.SkinArt[MaceModes.All.Count];
            for (var i = 0; i < maceSkins.Length; i++)
            {
                var mode = MaceModes.All[i];
                maceSkins[i] = new MaceController.SkinArt(mode, LoadSprite(ChainArtPath + maceSkinData.File(mode)));
            }

            mace.Skins = maceSkins;
            // U.CHN-T72: Input.vb's texMaceGrapple (mace2.png), shown over whichever mode is current while armed.
            mace.GrappleSprite = LoadSprite(ChainArtPath + maceSkinData.GrappleFile);
            maceRenderer.sprite = mace.SpriteFor(maceSkinData.Mode) ?? maceRenderer.sprite;

            // Form1.vb's U (UNI-T41): the links wearing a gem are drawn over the line, every gfx/chain link picture
            // wired under its chain.json name.
            var skinData = SettingsStore.Current.Chain.Skins;
            var skinArt = new LinkSkinView.SkinArt[skinData.Skins.Count];
            for (var i = 0; i < skinArt.Length; i++)
            {
                var name = skinData.Skins[i];
                skinArt[i] = new LinkSkinView.SkinArt(name, LoadSprite(ChainArtPath + skinData.File(name)));
            }

            go.AddComponent<LinkSkinView>().Skins = skinArt;

            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var chainAudio = go.AddComponent<ChainAudio>();
            chainAudio.Chain = chain;
            chainAudio.SfxSource = audioSource;

            // Sound.vb's DirectSound buffers (UNI-T20: sword_swing_1-7, sword_hit_1-3, death_1-4).
            chainAudio.SetClips(ChainSound.Swing, LoadClips(SfxPath, "sword_swing_", 1, 7));
            chainAudio.SetClips(ChainSound.Impact, LoadClips(SfxPath, "sword_hit_", 1, 3));
            chainAudio.SetClips(ChainSound.EnemyDeath, LoadClips(SfxPath, "death_", 1, 4));

            // GrappleCatch is defend.wav, as ChainSound's own doc comment says (UNI-T20). The original
            // has no throw sound to port, so GrappleThrow is throw.wav, a synthesized whoosh (BIBLE
            // V10, UNI-T71).
            chainAudio.SetClips(ChainSound.GrappleCatch, new[] { LoadClip(SfxPath + "defend.wav") });
            chainAudio.SetClips(ChainSound.GrappleThrow, new[] { LoadClip(SfxPath + "throw.wav") });

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

        private const string AreaArtPath = "Assets/Unity.Chain/Art/Background/";
        private const string RoomArtPath = "Assets/Unity.Chain/Art/Room/";

        private static void CreateBackground(CameraRig rig)
        {
            // Background.vb and Cube.vb (UNI-T40): renderBackground's quad with the current area and, behind it,
            // renderCube's brick room. AreaBackdrop sizes both from area.json at run time; every gfx/area picture is
            // wired under its area.json name (only beach and utopia are ever shown, by B and N, as in the original).
            var area = SettingsStore.Current.Area;
            var go = new GameObject("Background");
            var backdrop = go.AddComponent<AreaBackdrop>();
            backdrop.Rig = rig;

            var quadGo = new GameObject("Area");
            quadGo.transform.SetParent(go.transform, false);
            quadGo.transform.localPosition = new Vector3(0f, 0f, 1f);
            var quad = quadGo.AddComponent<SpriteRenderer>();
            quad.sortingOrder = AreaBackdrop.BackdropSortingOrder;

            var roomGo = new GameObject("BrickRoom");
            roomGo.transform.SetParent(go.transform, false);
            roomGo.transform.localPosition = new Vector3(0f, 0f, 2f);
            var room = roomGo.AddComponent<SpriteRenderer>();
            room.sortingOrder = AreaBackdrop.RoomSortingOrder;
            room.sprite = LoadSprite(RoomArtPath + area.CubeTexture);

            var art = new AreaBackdrop.AreaArt[area.Areas.Count];
            for (var i = 0; i < art.Length; i++)
            {
                var name = area.Areas[i];
                art[i] = new AreaBackdrop.AreaArt(name, LoadSprite(AreaArtPath + area.File(name)));
            }

            backdrop.Areas = art;
            backdrop.Backdrop = quad;
            backdrop.Room = room;
        }

        /// <summary>Loads the sprite at <paramref name="path"/>, throwing if it is missing: area.json and chain.json's skins name only art UNI-T19, UNI-T40 and UNI-T41 imported.</summary>
        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new FileNotFoundException($"SceneBuilder: expected a sprite at '{path}' (area.json or chain.json's skins).", path);
            }

            return sprite;
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

        private static void CreateScripterConsole(GameSession session, GameHud hud, SceneLight sceneLight, CameraRig rig)
        {
            // Form1.vb's scripter mode and Script.vb's showScripter (U.CHN-T61): its line is drawn on the HUD.
            var console = hud.gameObject.AddComponent<ScripterConsole>();
            console.Session = session;
            console.Hud = hud;
            console.SceneLight = sceneLight;
            console.Rig = rig;
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
            var music = go.AddComponent<MusicPlayer>();
            music.Session = session;

            // Sound.vb's playMusic track (UNI-T20); music.wma converted to .ogg, as Unity cannot
            // import WMA (BIBLE V11). Set through the serialized field, not the Music property,
            // which would try to Play() the source while the scene is only being built.
            SetPrivateField(music, "music", LoadClip(MusicClipPath));
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

        /// <summary>Splash first (the scene a build opens on), then Main, then anything else already listed.</summary>
        private static void RegisterInBuildSettings()
        {
            var updated = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SplashScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != SplashScenePath && existing.path != ScenePath)
                {
                    updated.Add(existing);
                }
            }

            EditorBuildSettings.scenes = updated.ToArray();
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

        /// <summary>
        /// Loads "{folder}{prefix}{n}.wav" for n in [first, last]. Unlike <see cref="LoadSprites"/>,
        /// every one of these is a clip UNI-T20 imported on purpose (see SfxPath's Assets folder):
        /// a missing file means a clip set silently plays fewer sounds than it should, so this throws
        /// instead of skipping it, failing the scene build loudly rather than shipping a partial import.
        /// </summary>
        private static AudioClip[] LoadClips(string folder, string prefix, int first, int last)
        {
            var clips = new AudioClip[last - first + 1];
            for (var n = first; n <= last; n++)
            {
                clips[n - first] = LoadClip($"{folder}{prefix}{n}.wav");
            }

            return clips;
        }

        /// <summary>Loads the AudioClip at <paramref name="path"/>, throwing if it is missing (see <see cref="LoadClips"/>).</summary>
        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new System.IO.FileNotFoundException($"SceneBuilder: expected an AudioClip at '{path}' (UNI-T20).", path);
            }

            return clip;
        }
    }
}
