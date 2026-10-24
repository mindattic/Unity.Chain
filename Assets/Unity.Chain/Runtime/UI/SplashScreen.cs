using System.Collections;
using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MindAttic.Chain
{
    /// <summary>
    /// Form1.vb Startup's loading picture (UNI-T58), the only thing in the Splash scene the player opens on. Startup
    /// picks <c>getRndNum(1, 12)</c>, sets the form's <c>BackgroundImage</c> to <c>gfx/gui/load/load640_&lt;n&gt;.jpg</c>
    /// (640 x 480 on the 640 x 480 form, so it fills the window), builds the <c>Engine</c> and clears the picture just
    /// before the first <c>oneFrameRender</c>. Here the picture (<see cref="LoadingScreen.Pick"/>, from hud.json's
    /// <c>loading</c>, out of <c>Resources/UI/Load</c>) is a UI Toolkit panel stretched over the whole window, made in
    /// code like the HUD's; it is drawn for at least one frame, then <see cref="NextScene"/> (Main) loads under it, and
    /// the picture goes the moment Main has loaded, so Main's first frame is the first one drawn without it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplashScreen : MonoBehaviour
    {
        /// <summary>The pictures' folder under Resources (the original's gfx/gui/load).</summary>
        public const string PictureFolder = "UI/Load/";

        /// <summary>Above the HUD (sorting order 0), so nothing of Main shows through while it loads.</summary>
        public const float SortingOrder = 1000f;

        [Tooltip("The scene the splash loads once its picture is up.")]
        [SerializeField] private string nextScene = "Main";

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _picture;

        /// <summary>The scene loaded under the picture (Main).</summary>
        public string NextScene
        {
            get => nextScene;
            set => nextScene = value;
        }

        /// <summary>The number of the picture chosen (1 to hud.json's pictures), 0 before it is.</summary>
        public int PictureNumber { get; private set; }

        /// <summary>The chosen picture.</summary>
        public Texture2D Picture { get; private set; }

        /// <summary>The full-window element the picture is drawn on.</summary>
        public VisualElement PictureElement => _picture;

        /// <summary>The panel's document.</summary>
        public UIDocument Document => _document;

        /// <summary>Whether <see cref="NextScene"/> has loaded and the picture is gone.</summary>
        public bool Finished { get; private set; }

        /// <summary>Picture <paramref name="number"/> of <paramref name="screen"/>, from Resources.</summary>
        public static Texture2D LoadPicture(LoadingScreen screen, int number) =>
            Resources.Load<Texture2D>(PictureFolder + screen.PictureName(number));

        private void Awake()
        {
            // Root.vb's rndNum: a Random seeded off the clock, so every start can show any picture.
            var screen = SettingsStore.Current.Hud.Loading;
            PictureNumber = screen.Pick(new System.Random(unchecked((int)System.DateTime.Now.Ticks)));
            Picture = LoadPicture(screen, PictureNumber);
            if (Picture == null)
            {
                Debug.LogWarning($"[Chain] The loading picture {PictureFolder}{screen.PictureName(PictureNumber)} is missing.");
            }

            BuildPanel();
        }

        private IEnumerator Start()
        {
            // The splash outlives its scene until Main is in, as Startup's form kept its picture while the Engine was built.
            DontDestroyOnLoad(gameObject);

            // Me.Show(): the picture is drawn (this frame renders) before the loading starts next frame. A frame, not
            // WaitForEndOfFrame, which never comes in a batch-mode run without graphics.
            yield return null;

            if (!string.IsNullOrEmpty(nextScene))
            {
                yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
            }

            // Me.BackgroundImage = Nothing, just before the first oneFrameRender.
            Finished = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
            }
        }

        private void BuildPanel()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettings.name = "ChainSplashPanel";
                _panelSettings.hideFlags = HideFlags.DontSave;
                _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/ChainTheme");
                _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                _panelSettings.sortingOrder = SortingOrder;
                _panelSettings.clearColor = false;
                _document.panelSettings = _panelSettings;
            }

            // The form's BackColor is black; the picture is stretched over all of it (the window keeps 4:3, as the art).
            _picture = new VisualElement { name = "splash-picture", pickingMode = PickingMode.Ignore };
            _picture.style.position = Position.Absolute;
            _picture.style.left = 0f;
            _picture.style.top = 0f;
            _picture.style.right = 0f;
            _picture.style.bottom = 0f;
            _picture.style.backgroundColor = Color.black;
            if (Picture != null)
            {
                _picture.style.backgroundImage = new StyleBackground(Picture);
                _picture.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }

            Attach();
        }

        // The document's root exists once its panel does, which can be after Awake; as GameHud, attach when it is there.
        private void LateUpdate() => Attach();

        private void Attach()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root != null && _picture != null && _picture.parent != root)
            {
                root.Add(_picture);
            }
        }
    }
}
