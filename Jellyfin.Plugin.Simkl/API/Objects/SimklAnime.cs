using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Simkl anime.
    /// </summary>
    public class SimklAnime : SimklMediaObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimklAnime"/> class.
        /// </summary>
        public SimklAnime()
        {
            Title = string.Empty;
            Seasons = Array.Empty<Season>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimklAnime"/> class.
        /// </summary>
        /// <param name="mediaInfo">The media info.</param>
        public SimklAnime(BaseItemDto mediaInfo)
        {
            Title = mediaInfo.SeriesName;
            Ids = new SimklAnimeIds(mediaInfo.ProviderIds);
            Year = mediaInfo.ProductionYear;
            Seasons = new[]
            {
                new Season
                {
                    Number = mediaInfo.ParentIndexNumber,
                    Episodes = new[]
                    {
                        new ShowEpisode
                        {
                            Number = mediaInfo.IndexNumber
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Gets or sets title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets year.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the runtime in minutes.
        /// </summary>
        [JsonPropertyName("runtime")]
        public int? Runtime { get; set; }

        /// <summary>
        /// Gets or sets seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public IReadOnlyList<Season> Seasons { get; set; } = Array.Empty<Season>();
    }
}
