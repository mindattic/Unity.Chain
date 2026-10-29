using System.IO;
using MindAttic.Chain.Online;
using Unity.Multiplayer;
using Unity.Multiplayer.Editor;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// Networks the Main scene on the Unity.Multiplayer kit (U.CHN-T26), in code like the rest of the scene: it writes
    /// the two network prefabs (<see cref="ChainPlayer"/>, every connection's Player Prefab, and <see cref="ChainWorldSync"/>,
    /// keyed <see cref="ChainWorldSync.Key"/>) and their <see cref="NetPrefabList"/>, runs the kit's own Add Front End
    /// (a NetworkManager with Unity Transport, a <see cref="NetBootstrap"/> and the <c>NetFrontEnd</c>), and adds
    /// <see cref="ChainOnline"/>, which keeps local play first. The bootstrap never serves by itself in a headless
    /// process (<c>serveWhenHeadless</c> off) nor paces one at launch (<c>applyHeadlessSettings</c> off), so batch-mode runs of the scene play locally exactly as before; a
    /// dedicated server is launched with <c>-mode server</c>.
    /// </summary>
    public static class OnlineBuilder
    {
        public const string Folder = "Assets/Unity.Chain/Resources/Online";
        public const string PlayerPrefabPath = Folder + "/ChainPlayer.prefab";
        public const string WorldSyncPrefabPath = Folder + "/ChainWorldSync.prefab";
        public const string PrefabListPath = "Assets/Unity.Chain/Online/ChainNetPrefabs.asset";

        /// <summary>Resources paths the tests load the prefabs from.</summary>
        public const string PlayerResource = "Online/ChainPlayer";
        public const string WorldSyncResource = "Online/ChainWorldSync";

        private const string MarkerSpritePath = "Assets/Unity.Chain/Art/Angel/angel_right1.png";
        private const string MaceSpritePath = "Assets/Unity.Chain/Art/Chain/mace.png";

        /// <summary>The marker chain's width: the scene chain's (SceneBuilder.CreateChain).</summary>
        public const float MarkerChainWidth = 0.08f;

        /// <summary>Writes only the network prefabs and their list (the scene keeps its references to them by GUID).</summary>
        [MenuItem("MindAttic/Chain/Build Online Prefabs")]
        public static void BuildPrefabsOnly()
        {
            BuildPrefabs();
            Debug.Log("OnlineBuilder: wrote " + PlayerPrefabPath + " and " + WorldSyncPrefabPath + ".");
        }

        /// <summary>Writes the network prefabs and their list, and returns the list.</summary>
        public static NetPrefabList BuildPrefabs()
        {
            Directory.CreateDirectory(Folder);

            var player = new GameObject("ChainPlayer");
            player.AddComponent<NetworkObject>();
            var chainPlayer = player.AddComponent<ChainPlayer>();
            var marker = new GameObject("Marker");
            marker.transform.SetParent(player.transform, false);
            var markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = SceneBuilder.LoadSprite(MarkerSpritePath);
            markerRenderer.enabled = false;
            SceneBuilder.SetPrivateField(chainPlayer, "marker", markerRenderer);

            // U.CHN-T73: another seat's chain and mace beside its marker, drawn as the scene's own chain is.
            var chainGo = new GameObject("Chain");
            chainGo.transform.SetParent(player.transform, false);
            var chainLine = chainGo.AddComponent<LineRenderer>();
            chainLine.sharedMaterial = SceneBuilder.LoadOrCreateLineMaterial();
            chainLine.useWorldSpace = true;
            chainLine.widthMultiplier = MarkerChainWidth;
            chainLine.textureMode = LineTextureMode.Tile;
            chainLine.positionCount = 0;
            chainLine.enabled = false;
            SceneBuilder.SetPrivateField(chainPlayer, "chainLine", chainLine);

            var maceGo = new GameObject("Mace");
            maceGo.transform.SetParent(player.transform, false);
            // The mace sprite is one world unit across at scale 1; chain.json's maceScale times the line's width, as MaceController draws it.
            maceGo.transform.localScale = Vector3.one * (MarkerChainWidth * SettingsStore.Current.MaceScale);
            var maceRenderer = maceGo.AddComponent<SpriteRenderer>();
            maceRenderer.sprite = SceneBuilder.LoadSprite(MaceSpritePath);
            maceRenderer.sortingOrder = 10;
            maceRenderer.enabled = false;
            SceneBuilder.SetPrivateField(chainPlayer, "maceMarker", maceRenderer);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);

            var sync = new GameObject("ChainWorldSync");
            sync.AddComponent<NetworkObject>();
            sync.AddComponent<NetKey>();
            sync.AddComponent<ChainWorldSync>();
            var syncPrefab = PrefabUtility.SaveAsPrefabAsset(sync, WorldSyncPrefabPath);
            Object.DestroyImmediate(sync);

            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetPrefabList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }

            list.Add(playerPrefab);
            list.Add(syncPrefab);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        /// <summary>Networks <paramref name="scene"/> (loaded, being built) around <paramref name="session"/>.</summary>
        public static ChainOnline Create(Scene scene, GameSession session)
        {
            var list = BuildPrefabs();
            var result = NetFrontEndSetup.Run(scene, new NetSceneSetupOptions { PrefabList = list, RegisterProjectPrefabs = false });
            if (!result.Succeeded)
            {
                throw new System.InvalidOperationException("OnlineBuilder: the kit's Add Front End failed: " + result.Message);
            }

            var root = result.Scene.Root;
            result.Scene.Manager.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            EditorUtility.SetDirty(result.Scene.Manager);

            var bootstrap = new SerializedObject(result.Scene.Bootstrap);
            bootstrap.FindProperty("serveWhenHeadless").boolValue = false;
            bootstrap.FindProperty("autoStart").boolValue = false;
            // Not at launch: a headless local run (batch-mode tests) keeps its frame pacing; ChainOnline paces a
            // headless server once its session starts instead.
            bootstrap.FindProperty("applyHeadlessSettings").boolValue = false;
            bootstrap.ApplyModifiedPropertiesWithoutUndo();

            var online = root.AddComponent<ChainOnline>();
            online.FrontEnd = (Unity.Multiplayer.UI.NetFrontEnd)result.FrontEnd;
            online.Game = session;
            online.WorldSyncPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldSyncPrefabPath);
            return online;
        }
    }
}
