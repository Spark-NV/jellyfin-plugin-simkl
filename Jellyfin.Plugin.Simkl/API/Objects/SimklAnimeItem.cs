using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Simkl anime item wrapper (contains metadata and anime object).
    /// </summary>
    public class SimklAnimeItem
    {
        /// <summary>
        /// Gets or sets the anime object.
        /// </summary>
        [JsonPropertyName("show")]
        public SimklAnime? Show { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets when it was added to watchlist.
        /// </summary>
        [JsonPropertyName("added_to_watchlist_at")]
        public DateTime? AddedToWatchlistAt { get; set; }

        /// <summary>
        /// Gets or sets when it was last watched.
        /// </summary>
        [JsonPropertyName("last_watched_at")]
        public DateTime? LastWatchedAt { get; set; }

        /// <summary>
        /// Gets or sets the anime type (tv or movie).
        /// </summary>
        [JsonPropertyName("anime_type")]
        public string? AnimeType { get; set; }
    }
}
