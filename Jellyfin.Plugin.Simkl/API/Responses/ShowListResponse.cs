using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Simkl.API.Objects;

namespace Jellyfin.Plugin.Simkl.API.Responses
{
    /// <summary>
    /// Show list response from Simkl API.
    /// </summary>
    public class ShowListResponse
    {
        /// <summary>
        /// Gets or sets the list of show items.
        /// </summary>
        [JsonPropertyName("shows")]
        public List<SimklShowItem> ShowItems { get; set; } = new List<SimklShowItem>();

        /// <summary>
        /// Gets the list of shows (extracted from items).
        /// </summary>
        public List<SimklShow> Shows => ShowItems
            .Where(item => item.Show != null)
            .Select(item => item.Show!)
            .ToList();
    }
}