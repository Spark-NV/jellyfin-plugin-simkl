using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Simkl.API.Objects;

namespace Jellyfin.Plugin.Simkl.API.Responses
{
    /// <summary>
    /// Anime list response from Simkl API.
    /// </summary>
    public class AnimeListResponse
    {
        /// <summary>
        /// Gets or sets the list of anime items.
        /// </summary>
        [JsonPropertyName("anime")]
        public List<SimklAnimeItem> AnimeItems { get; set; } = new List<SimklAnimeItem>();

        /// <summary>
        /// Gets the list of anime (extracted from items).
        /// </summary>
        public List<SimklAnime> Shows => AnimeItems
            .Where(item => item.Show != null)
            .Select(item => item.Show!)
            .ToList();
    }
}
