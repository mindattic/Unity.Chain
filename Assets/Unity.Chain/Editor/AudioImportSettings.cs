using UnityEditor;
using UnityEngine;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// UNI-T20: Audio/Music/music.ogg is Sound.vb's <c>playMusic</c> track (<c>bin\sfx\music\music.wma</c>
    /// converted -- Unity cannot import WMA, BIBLE V11), a 75-second loop that
    /// <see cref="MusicPlayer"/> plays start to finish on its own <c>AudioSource</c>; it streams from
    /// disk instead of sitting fully decompressed in memory the way the short one-shot clips under
    /// Audio/Sfx do (those keep the AudioImporter's own defaults, already right for a few hundred
    /// milliseconds of mono PCM). Set here, not by hand in the Inspector, so a reimport never silently
    /// drops back to Decompress On Load (Unity.Chain's rebuild standard, point 5).
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/Unity.Chain/Audio/Music/";

        private void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(MusicFolder))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
