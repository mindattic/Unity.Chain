using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Which picture each link of a chain is drawn with (UNI-T41). Chain.vb's <c>loadChain</c> gives every link
    /// <c>texChain</c> (gfx/chain/link, <see cref="Link"/>); Form1.vb's U sets <c>link(1)</c> to <c>link(5)</c> to
    /// <c>texAqua</c>, <c>texEmerald</c>, <c>texTeardrop</c>, <c>texTablet</c> and <c>texPearl</c>
    /// (<see cref="Gems"/>, link index to skin name), and nothing ever sets them back. The set is chain.json's
    /// <c>skins</c>; which chain wears it is the player's state (<see cref="PlayerEntity.HasGemSkins"/>).
    /// </summary>
    public sealed class LinkSkins
    {
        private static readonly IReadOnlyDictionary<int, string> NoGems = new Dictionary<int, string>();

        /// <summary>Every link plain, no gems: the set a player gets when the data gives none.</summary>
        public static readonly LinkSkins Plain = new LinkSkins("link", NoGems);

        public LinkSkins(string link, IReadOnlyDictionary<int, string> gems)
        {
            if (string.IsNullOrEmpty(link))
            {
                throw new ArgumentException("A link skin needs a name.", nameof(link));
            }

            Link = link;
            Gems = gems != null ? Copy(gems) : NoGems;
        }

        /// <summary>Textures.vb's <c>texChain</c> (link): what every link wears until U.</summary>
        public string Link { get; }

        /// <summary>What U puts on: link index to skin name (1 aqua, 2 emerald, 3 teardrop, 4 tablet, 5 pearl).</summary>
        public IReadOnlyDictionary<int, string> Gems { get; }

        /// <summary>The skin link <paramref name="link"/> wears: its gem once <paramref name="gems"/> is on and it has one, else <see cref="Link"/>.</summary>
        public string Skin(int link, bool gems) => gems && Gems.TryGetValue(link, out var gem) ? gem : Link;

        private static Dictionary<int, string> Copy(IReadOnlyDictionary<int, string> gems)
        {
            var copy = new Dictionary<int, string>();
            foreach (var pair in gems)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }

    /// <summary>
    /// chain.json's <c>skins</c>: Textures.vb's link pictures (gfx/chain's link, aqua, emerald, teardrop, tablet and
    /// pearl, each loaded at 16 x 16) by name with their files, the skin every link starts in (<c>link</c>) and the
    /// gems Form1.vb's U puts on (<c>keyU</c>, link index to skin name).
    /// </summary>
    public sealed class LinkSkinData
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _skins = new List<string>();
        private readonly Dictionary<int, string> _keyU = new Dictionary<int, string>();

        /// <summary>The skin every link wears from the start (Chain.vb's <c>loadChain</c>: <c>texChain</c>).</summary>
        public string Link { get; private set; }

        /// <summary>Form1.vb's U: link index to the skin it puts on that link.</summary>
        public IReadOnlyDictionary<int, string> KeyU => _keyU;

        /// <summary>Every skin's name, in the file's order.</summary>
        public IReadOnlyList<string> Skins => _skins;

        /// <summary>The picture file of skin <paramref name="name"/> (aqua.png), or null if there is no such skin.</summary>
        public string File(string name) => name != null && _files.TryGetValue(name, out var file) ? file : null;

        /// <summary>The port's set: <see cref="Link"/> and <see cref="KeyU"/>.</summary>
        public LinkSkins ToPort() => new LinkSkins(Link, _keyU);

        internal static LinkSkinData Read(SettingsReader r, int maxLinks)
        {
            var data = new LinkSkinData { Link = r.Text("link") };

            var files = r.Section("files");
            foreach (var name in files.Names())
            {
                data._skins.Add(name);
                data._files[name] = files.Text(name);
            }

            files.Check(data._skins.Count > 0, "files", "must name at least one link picture");
            files.Done();

            r.Check(data.File(data.Link) != null, "link", $"must name a skin in files (is \"{data.Link}\")");

            var keyU = r.Section("keyU");
            foreach (var key in keyU.Names())
            {
                var skin = keyU.Text(key);
                var isLink = int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var link);
                keyU.Check(isLink && link >= 1 && link <= maxLinks, key, $"must be a link index between 1 and maxLinks ({maxLinks})");
                keyU.Check(data.File(skin) != null, key, $"must name a skin in files (is \"{skin}\")");
                if (isLink)
                {
                    data._keyU[link] = skin;
                }
            }

            keyU.Done();
            return data;
        }
    }
}
