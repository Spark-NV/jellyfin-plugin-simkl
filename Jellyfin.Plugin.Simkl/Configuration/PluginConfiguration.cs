using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Simkl.Configuration
{
    /// <summary>
    /// Class needed to create a Plugin and configure it.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            ScrobbleMovies = false;
            ScrobbleShows = false;
            ScrobblePercentage = 70;
            ScrobbleNowWatchingPercentage = 5;
            MinLength = 5;
            UserToken = string.Empty;
            ScrobbleTimeout = 30;
            TriggerLibraryScanAfterImport = true;
            ImportListStatus = "plantowatch";
        }

        /// <summary>
        /// Gets or sets a value indicating whether scrobble movies.
        /// </summary>
        public bool ScrobbleMovies { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether scrobble shows.
        /// </summary>
        public bool ScrobbleShows { get; set; }

        /// <summary>
        /// Gets or sets scrobble percentage.
        /// </summary>
        public int ScrobblePercentage { get; set; }

        /// <summary>
        /// Gets or sets scrobble now watching percentage.
        /// </summary>
        public int ScrobbleNowWatchingPercentage { get; set; }

        /// <summary>
        /// Gets or sets min length.
        /// </summary>
        /// <remarks>
        /// Minimum length for scrobbling (in minutes).
        /// </remarks>
        public int MinLength { get; set; }

        /// <summary>
        /// Gets or sets user token.
        /// </summary>
        public string UserToken { get; set; }

        /// <summary>
        /// Gets or sets scrobble timeout.
        /// </summary>
        /// <remarks>
        /// Time between scrobbling tries.
        /// </remarks>
        public int ScrobbleTimeout { get; set; }

        /// <summary>
        /// Gets or sets the movies library ID.
        /// </summary>
        public Guid MoviesLibraryId { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the TV shows library ID.
        /// </summary>
        public Guid TvShowsLibraryId { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the anime library ID.
        /// </summary>
        public Guid AnimeLibraryId { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the anime movies library ID.
        /// </summary>
        public Guid AnimeMoviesLibraryId { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the movies library path (deprecated, use MoviesLibraryId).
        /// </summary>
        [Obsolete("Use MoviesLibraryId instead")]
        public string MoviesLibraryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the TV shows library path (deprecated, use TvShowsLibraryId).
        /// </summary>
        [Obsolete("Use TvShowsLibraryId instead")]
        public string TvShowsLibraryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the anime library path (deprecated, use AnimeLibraryId).
        /// </summary>
        [Obsolete("Use AnimeLibraryId instead")]
        public string AnimeLibraryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the anime movies library path (deprecated, use AnimeMoviesLibraryId).
        /// </summary>
        [Obsolete("Use AnimeMoviesLibraryId instead")]
        public string AnimeMoviesLibraryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Simkl list status to import from.
        /// </summary>
        /// <remarks>
        /// Valid values: plantowatch, watching, completed, hold, dropped.
        /// </remarks>
        public string ImportListStatus { get; set; } = "plantowatch";

        /// <summary>
        /// Gets or sets a value indicating whether to trigger a library scan after importing lists.
        /// </summary>
        public bool TriggerLibraryScanAfterImport { get; set; }
    }
}