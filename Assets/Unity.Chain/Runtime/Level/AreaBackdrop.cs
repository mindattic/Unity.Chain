using System;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Background.vb and Cube.vb (UNI-T40): the scenery behind the play field. <see cref="Backdrop"/> is renderBackground's
    /// quad (area.json's bgWidth x 2 bgHeight with its left edge at bgOffset), the current area's picture stretched over
    /// it as loadTexture's 512 x 512 stretched every area; <see cref="Room"/> is renderCube's brick room, drawn first,
    /// so wherever the backdrop does not reach (and if an area's picture were missing) the room's far wall shows. The
    /// room is placed every frame where the camera's eye sees that wall on the play plane (<see cref="Core.Backdrop.FarWall"/>,
    /// BIBLE V24), so it drifts behind the backdrop as the debug camera moves.
    ///
    /// Which area is shown is the screen's own state (<see cref="AreaSelection"/>): area.json's texBackground (beach) at
    /// the start, the Debug map's AreaB and AreaN (Form1.vb's B and N) show beach and utopia, read paused too, and no
    /// reset touches it. Every picture of gfx/area is listed in <see cref="Areas"/> by its area.json name; only those two
    /// are ever shown, as in the original. The component reads the settings and changes nothing in the world.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // after the CameraRig (200) has placed the view for the frame
    public sealed class AreaBackdrop : MonoBehaviour
    {
        /// <summary>The backdrop's sorting order: behind every sprite of the play field.</summary>
        public const int BackdropSortingOrder = -100;

        /// <summary>The brick room's: behind the backdrop, as renderCube draws before renderBackground.</summary>
        public const int RoomSortingOrder = -200;

        /// <summary>One area's picture, under its area.json name.</summary>
        [Serializable]
        public struct AreaArt
        {
            public AreaArt(string name, Sprite sprite)
            {
                Name = name;
                Sprite = sprite;
            }

            public string Name;
            public Sprite Sprite;
        }

        [Tooltip("Draws renderBackground's quad with the current area.")]
        [SerializeField] private SpriteRenderer backdrop;
        [Tooltip("Draws Cube.vb's brick room behind it.")]
        [SerializeField] private SpriteRenderer room;
        [Tooltip("Every area's picture, by its area.json name.")]
        [SerializeField] private AreaArt[] areas = Array.Empty<AreaArt>();
        [Tooltip("Whose eye sees the room. Falls back to the scene's CameraRig.")]
        [SerializeField] private CameraRig rig;

        private AreaSelection _selection;
        private ChainControls _controls;

        public SpriteRenderer Backdrop
        {
            get => backdrop;
            set
            {
                backdrop = value;
                Apply();
            }
        }

        public SpriteRenderer Room
        {
            get => room;
            set
            {
                room = value;
                PlaceRoom();
            }
        }

        public CameraRig Rig
        {
            get => rig;
            set => rig = value;
        }

        /// <summary>The area pictures, by name.</summary>
        public AreaArt[] Areas
        {
            get => areas;
            set
            {
                areas = value ?? Array.Empty<AreaArt>();
                Apply();
            }
        }

        /// <summary>Which area is shown (Textures.vb's texBackground).</summary>
        public AreaSelection Selection
        {
            get
            {
                if (_selection == null)
                {
                    _selection = new AreaSelection(Settings.Area);
                }

                return _selection;
            }
        }

        /// <summary>The area shown now.</summary>
        public string Current => Selection.Current;

        private static GameSettings Settings => SettingsStore.Current;

        /// <summary>The picture of area <paramref name="name"/>, or null if none is wired.</summary>
        public Sprite SpriteFor(string name)
        {
            foreach (var art in areas)
            {
                if (art.Name == name)
                {
                    return art.Sprite;
                }
            }

            return null;
        }

        /// <summary>Form1.vb's B or N: shows its area. Returns whether the area changed.</summary>
        public bool Press(AreaKey key)
        {
            var changed = Selection.Press(key);
            Apply();
            return changed;
        }

        /// <summary>Draws the current area on the backdrop's quad; with no picture for it the backdrop is hidden and the room shows.</summary>
        public void Apply()
        {
            if (backdrop == null)
            {
                return;
            }

            var sprite = SpriteFor(Selection.Current);
            backdrop.sprite = sprite;
            backdrop.enabled = sprite != null;
            backdrop.sortingOrder = BackdropSortingOrder;
            Fit(backdrop.transform, sprite, Settings.Backdrop.Quad);
        }

        /// <summary>Puts the room's far wall where the camera's eye sees it on the play plane. Called every frame; public so tests can drive it.</summary>
        public void PlaceRoom()
        {
            if (room == null)
            {
                return;
            }

            var eye = ResolveEye();
            var backdropSettings = Settings.Backdrop;
            var wall = Core.Backdrop.FarWall(new Vec2(eye.x, eye.y), eye.z, backdropSettings.RoomSize, backdropSettings.RoomDepth);
            room.sortingOrder = RoomSortingOrder;
            room.enabled = room.sprite != null && wall.Width > 0f && wall.Height > 0f;
            Fit(room.transform, room.sprite, wall);
        }

        /// <summary>The rig's eye, or Camera.vb's start (x 0, y 0, camera.json's eye z) when there is no rig.</summary>
        private Vector3 ResolveEye()
        {
            if (rig == null)
            {
                rig = FindAnyObjectByType<CameraRig>();
            }

            return rig != null ? rig.Eye : new Vector3(0f, 0f, Settings.DebugCameraSettings.EyeZ);
        }

        /// <summary>Scales and moves <paramref name="target"/> so <paramref name="sprite"/> covers <paramref name="rect"/> exactly, whatever its pivot and pixel size.</summary>
        private static void Fit(Transform target, Sprite sprite, Rect2 rect)
        {
            if (sprite == null)
            {
                return;
            }

            var bounds = sprite.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            var scale = new Vector2(rect.Width / bounds.size.x, rect.Height / bounds.size.y);
            target.localScale = new Vector3(scale.x, scale.y, 1f);
            var z = target.position.z;
            target.position = new Vector3(rect.XMin - bounds.min.x * scale.x, rect.YMin - bounds.min.y * scale.y, z);
        }

        private void Start() => Apply();

        private void OnEnable() => (_controls ?? (_controls = new ChainControls())).Debug.Enable();

        private void OnDisable() => _controls?.Debug.Disable();

        private void OnDestroy()
        {
            _controls?.Dispose();
            _controls = null;
        }

        // Form1.vb's key handler runs paused too, and renderBackground draws then, so B and N are read in real time.
        private void Update()
        {
            if (_controls == null)
            {
                return;
            }

            if (_controls.AreaB.WasPressedThisFrame())
            {
                Press(AreaKey.B);
            }

            if (_controls.AreaN.WasPressedThisFrame())
            {
                Press(AreaKey.N);
            }
        }

        private void LateUpdate() => PlaceRoom();
    }
}
