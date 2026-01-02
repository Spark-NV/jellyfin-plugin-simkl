using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Simkl.API.Objects;

namespace Jellyfin.Plugin.Simkl.API.Responses
{
    /// <summary>
    /// Plan to watch response from Simkl API.
    /// </summary>
    public class PlanToWatchResponse
    {
        /// <summary>
        /// Gets or sets the list of movies.
        /// </summary>
        [JsonPropertyName("movies")]
        public List<SimklMovie> Movies { get; set; } = new List<SimklMovie>();

        /// <summary>
        /// Gets or sets the list of shows.
        /// </summary>
        [JsonPropertyName("shows")]
        public List<SimklShow> Shows { get; set; } = new List<SimklShow>();

        /// <summary>
        /// Gets or sets the list of anime items (including type information).
        /// </summary>
        [JsonPropertyName("anime")]
        public List<SimklAnimeItem> Anime { get; set; } = new List<SimklAnimeItem>();
    }
}