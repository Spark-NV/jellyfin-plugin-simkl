using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Simkl.API.Objects;

namespace Jellyfin.Plugin.Simkl.API.Responses
{
    /// <summary>
    /// Movie list response from Simkl API.
    /// </summary>
    public class MovieListResponse
    {
        /// <summary>
        /// Gets or sets the list of movie items.
        /// </summary>
        [JsonPropertyName("movies")]
        public List<SimklMovieItem> MovieItems { get; set; } = new List<SimklMovieItem>();

        /// <summary>
        /// Gets the list of movies (extracted from items).
        /// </summary>
        public List<SimklMovie> Movies => MovieItems
            .Where(item => item.Movie != null)
            .Select(item => item.Movie!)
            .ToList();
    }
}