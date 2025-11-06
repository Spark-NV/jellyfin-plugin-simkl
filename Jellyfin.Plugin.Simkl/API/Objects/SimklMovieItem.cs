using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Simkl movie item wrapper (contains metadata and movie object).
    /// </summary>
    public class SimklMovieItem
    {
        /// <summary>
        /// Gets or sets the movie object.
        /// </summary>
        [JsonPropertyName("movie")]
        public SimklMovie? Movie { get; set; }

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