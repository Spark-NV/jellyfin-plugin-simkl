using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Simkl show item wrapper (contains metadata and show object).
    /// </summary>
    public class SimklShowItem
    {
        /// <summary>
        /// Gets or sets the show object.
        /// </summary>
        [JsonPropertyName("show")]
        public SimklShow? Show { get; set; }

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
    }
}