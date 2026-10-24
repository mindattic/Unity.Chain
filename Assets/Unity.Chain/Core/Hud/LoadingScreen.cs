using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// hud.json's <c>loading</c>: Form1.vb Startup's loading picture (UNI-T58). Before it builds the <c>Engine</c>,
    /// Startup picks <c>n = getRndNum(1, 12)</c> (Root.vb: <c>rndNum.Next(1, 12 + 1)</c>, so 1 to 12 inclusive, each
    /// as likely, from a <see cref="Random"/> seeded off the clock) and sets the form's <c>BackgroundImage</c> to
    /// <c>gfx/gui/load/load640_&lt;n&gt;.jpg</c>, 640 x 480 on the 640 x 480 form, so the picture fills the window; it
    /// clears it (<c>Me.BackgroundImage = Nothing</c>) just before the main loop's first <c>oneFrameRender</c>.
    /// </summary>
    public sealed class LoadingScreen
    {
        /// <summary>The pictures' shared file stem: <c>load640_</c> then the picture's number.</summary>
        public const string NameStem = "load640_";

        private LoadingScreen(int pictures)
        {
            Pictures = pictures;
        }

        /// <summary>How many loading pictures there are, numbered from 1 (<c>getRndNum(1, 12)</c>: 12).</summary>
        public int Pictures { get; }

        /// <summary>Startup's <c>getRndNum(1, Pictures)</c>: a picture number from 1 to <see cref="Pictures"/>, inclusive.</summary>
        public int Pick(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return random.Next(1, Pictures + 1);
        }

        /// <summary>Picture <paramref name="number"/>'s file name without its extension: <c>load640_&lt;n&gt;</c>.</summary>
        public string PictureName(int number)
        {
            if (number < 1 || number > Pictures)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, $"must be 1 to {Pictures}");
            }

            return NameStem + number;
        }

        /// <summary>A loading screen picking from <paramref name="pictures"/> pictures (tests and tools; the game reads hud.json).</summary>
        public static LoadingScreen Of(int pictures)
        {
            if (pictures < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pictures), pictures, "must be at least 1");
            }

            return new LoadingScreen(pictures);
        }

        internal static LoadingScreen Read(SettingsReader parent)
        {
            var r = parent.Section("loading");
            var screen = new LoadingScreen(r.Integer("pictures", 1, 999));
            r.Done();
            return screen;
        }
    }
}
