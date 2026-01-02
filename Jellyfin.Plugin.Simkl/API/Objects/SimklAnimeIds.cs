using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Simkl anime ids.
    /// </summary>
    public class SimklAnimeIds : SimklIds
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimklAnimeIds"/> class.
        /// </summary>
        public SimklAnimeIds()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimklAnimeIds"/> class.
        /// </summary>
        /// <param name="providerAnimeIds">The provider anime ids.</param>
        public SimklAnimeIds(Dictionary<string, string> providerAnimeIds)
            : base(providerAnimeIds)
        {
        }

        /// <summary>
        /// Gets or sets mal.
        /// </summary>
        [JsonPropertyName("mal")]
        public int? Mal { get; set; }

        /// <summary>
        /// Gets or sets anilist.
        /// </summary>
        [JsonPropertyName("anilist")]
        public int? Anilist { get; set; }

        /// <summary>
        /// Gets or sets kitsu.
        /// </summary>
        [JsonPropertyName("kitsu")]
        public int? Kitsu { get; set; }

        /// <summary>
        /// Gets or sets crunchyroll.
        /// </summary>
        [JsonPropertyName("crunchyroll")]
        public int? Crunchyroll { get; set; }
    }
}
