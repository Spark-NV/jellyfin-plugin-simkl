using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Simkl.API;
using Jellyfin.Plugin.Simkl.API.Responses;
using Jellyfin.Plugin.Simkl.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Simkl.Services
{
    /// <summary>
    /// Service for importing plan to watch items and creating folders.
    /// </summary>
    public class PlanToWatchImporter
    {
        private readonly ILogger<PlanToWatchImporter> _logger;
        private readonly SimklApi _simklApi;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlanToWatchImporter"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{PlanToWatchImporter}"/> interface.</param>
        /// <param name="simklApi">Instance of the <see cref="SimklApi"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public PlanToWatchImporter(ILogger<PlanToWatchImporter> logger, SimklApi simklApi, ILibraryManager libraryManager)
        {
            _logger = logger;
            _simklApi = simklApi;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Import plan to watch items and create folders.
        /// </summary>
        /// <param name="userConfig">User configuration.</param>
        /// <returns>Import result.</returns>
        public async Task<ImportResult> ImportPlanToWatch(UserConfig userConfig)
        {
            var result = new ImportResult();

            if (string.IsNullOrEmpty(userConfig.UserToken))
            {
                result.Error = "User token is not set. Please log in first.";
                return result;
            }

            try
            {
                // Get list from Simkl based on selected status
                var listStatus = string.IsNullOrEmpty(userConfig.ImportListStatus) ? "plantowatch" : userConfig.ImportListStatus;
                _logger.LogInformation("Fetching {ListStatus} list from Simkl for user", listStatus);
                var planToWatch = await _simklApi.GetListByStatus(userConfig.UserToken, listStatus);
                if (planToWatch == null)
                {
                    result.Error = $"Failed to retrieve {listStatus} list from Simkl. The list may be empty or the API response was invalid.";
                    _logger.LogError("Failed to retrieve {ListStatus} list from Simkl - response was null", listStatus);
                    return result;
                }

                // Check if response has empty lists
                if ((planToWatch.Movies == null || planToWatch.Movies.Count == 0) &&
                    (planToWatch.Shows == null || planToWatch.Shows.Count == 0))
                {
                    _logger.LogInformation("Retrieved {ListStatus} list from Simkl but it is empty (no movies or shows)", listStatus);
                    result.Success = true;
                    result.Message = $"The {listStatus} list from Simkl is empty. No items to import.";
                    return result;
                }

                _logger.LogInformation(
                    "Retrieved {MovieCount} movies and {ShowCount} shows from Simkl",
                    planToWatch.Movies?.Count ?? 0,
                    planToWatch.Shows?.Count ?? 0);

                // Get stub file path - use configured path or fallback to default locations
                string? movieStubPath = null;

                // First, try user-configured path
                if (!string.IsNullOrEmpty(userConfig.MovieStubFilePath))
                {
                    if (File.Exists(userConfig.MovieStubFilePath))
                    {
                        movieStubPath = userConfig.MovieStubFilePath;
                        _logger.LogInformation("Using configured movie stub file: {Path}", movieStubPath);
                    }
                    else
                    {
                        _logger.LogWarning("Configured movie stub file not found: {Path}", userConfig.MovieStubFilePath);
                    }
                }

                // Fallback to default locations if not configured or not found
                if (movieStubPath == null)
                {
                    var pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    if (pluginPath != null)
                    {
                        // Try plugin directory
                        var path1 = Path.Combine(pluginPath, "movie_stub.mp4");
                        if (File.Exists(path1))
                        {
                            movieStubPath = path1;
                        }
                        else
                        {
                            // Try project root relative to plugin
                            var path2 = Path.Combine(pluginPath, "..", "..", "..", "..", "movie_stub.mp4");
                            path2 = Path.GetFullPath(path2);
                            if (File.Exists(path2))
                            {
                                movieStubPath = path2;
                            }
                        }
                    }

                    // Try current working directory
                    if (movieStubPath == null)
                    {
                        var path3 = Path.Combine(Directory.GetCurrentDirectory(), "movie_stub.mp4");
                        if (File.Exists(path3))
                        {
                            movieStubPath = path3;
                        }
                    }

                    if (movieStubPath == null || !File.Exists(movieStubPath))
                    {
                        _logger.LogWarning("Movie stub file not found. Searched in plugin directory and current working directory.");
                    }
                }

                // Get library paths from IDs
                string? moviesLibraryPath = null;
                if (userConfig.MoviesLibraryId != Guid.Empty)
                {
                    var moviesLibraryIdString = userConfig.MoviesLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();
                    _logger.LogDebug("Found {Count} virtual folders", allLibraries.Count);
                    foreach (var vf in allLibraries)
                    {
                        _logger.LogDebug("Virtual folder: Name={Name}, ItemId={ItemId}", vf.Name, vf.ItemId);
                    }

                    var moviesLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == moviesLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == userConfig.MoviesLibraryId));

                    if (moviesLibrary != null)
                    {
                        _logger.LogDebug(
                            "Found movies library: {Name}, LibraryOptions is null: {IsNull}",
                            moviesLibrary.Name,
                            moviesLibrary.LibraryOptions == null);

                        if (moviesLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = moviesLibrary.LibraryOptions.PathInfos.ToList();
                            _logger.LogDebug("Found {Count} path infos for movies library", pathInfos.Count);
                            foreach (var pathInfo in pathInfos)
                            {
                                _logger.LogDebug("Path info: {Path}", pathInfo.Path);
                            }

                            moviesLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Movies library with ID {LibraryId} not found in virtual folders", moviesLibraryIdString);
                    }

                    _logger.LogInformation(
                        "Movies library ID {LibraryId} resolved to path: {Path}",
                        moviesLibraryIdString,
                        moviesLibraryPath ?? "null");
                    if (string.IsNullOrEmpty(moviesLibraryPath))
                    {
                        _logger.LogWarning("Movies library ID {LibraryId} could not be resolved to a path", moviesLibraryIdString);
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(userConfig.MoviesLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    moviesLibraryPath = userConfig.MoviesLibraryPath;
                }
#pragma warning restore CS0618

                string? tvShowsLibraryPath = null;
                if (userConfig.TvShowsLibraryId != Guid.Empty)
                {
                    var tvShowsLibraryIdString = userConfig.TvShowsLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();

                    var tvShowsLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == tvShowsLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == userConfig.TvShowsLibraryId));

                    if (tvShowsLibrary != null)
                    {
                        _logger.LogDebug(
                            "Found TV shows library: {Name}, LibraryOptions is null: {IsNull}",
                            tvShowsLibrary.Name,
                            tvShowsLibrary.LibraryOptions == null);

                        if (tvShowsLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = tvShowsLibrary.LibraryOptions.PathInfos.ToList();
                            _logger.LogDebug("Found {Count} path infos for TV shows library", pathInfos.Count);
                            foreach (var pathInfo in pathInfos)
                            {
                                _logger.LogDebug("Path info: {Path}", pathInfo.Path);
                            }

                            tvShowsLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("TV Shows library with ID {LibraryId} not found in virtual folders", tvShowsLibraryIdString);
                    }

                    _logger.LogInformation(
                        "TV Shows library ID {LibraryId} resolved to path: {Path}",
                        tvShowsLibraryIdString,
                        tvShowsLibraryPath ?? "null");
                    if (string.IsNullOrEmpty(tvShowsLibraryPath))
                    {
                        _logger.LogWarning("TV Shows library ID {LibraryId} could not be resolved to a path", tvShowsLibraryIdString);
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(userConfig.TvShowsLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    tvShowsLibraryPath = userConfig.TvShowsLibraryPath;
                }
#pragma warning restore CS0618

                // Validate that at least one library path is configured
                if (string.IsNullOrEmpty(moviesLibraryPath) && string.IsNullOrEmpty(tvShowsLibraryPath))
                {
                    result.Error = "No library paths configured. Please select at least one library (Movies or TV Shows).";
                    _logger.LogError("Import failed: No library paths configured");
                    return result;
                }

                // Process movies
                if (!string.IsNullOrEmpty(moviesLibraryPath) && planToWatch.Movies != null && planToWatch.Movies.Count > 0)
                {
                    foreach (var movie in planToWatch.Movies)
                    {
                        try
                        {
                            var folderName = FormatMovieFolderName(movie);
                            var movieFolderPath = Path.Combine(moviesLibraryPath, folderName);

                            // Create folder if it doesn't exist
                            if (!Directory.Exists(movieFolderPath))
                            {
                                Directory.CreateDirectory(movieFolderPath);
                                result.MoviesCreated++;
                                _logger.LogInformation("Created movie folder: {Path}", movieFolderPath);
                            }

                            // Copy stub file if it exists and target file doesn't exist
                            if (File.Exists(movieStubPath))
                            {
                                var fileName = FormatMovieFileName(movie);
                                var targetFile = Path.Combine(movieFolderPath, fileName);
                                if (!File.Exists(targetFile))
                                {
                                    File.Copy(movieStubPath, targetFile);
                                    result.MoviesFilesCopied++;
                                    _logger.LogInformation("Copied stub file to: {Path}", targetFile);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing movie: {Title}", movie.Title);
                            result.MoviesErrors++;
                        }
                    }
                }

                // Process TV shows
                if (!string.IsNullOrEmpty(tvShowsLibraryPath) && planToWatch.Shows != null && planToWatch.Shows.Count > 0)
                {
                    foreach (var show in planToWatch.Shows)
                    {
                        try
                        {
                            var folderName = FormatShowFolderName(show);
                            var showFolderPath = Path.Combine(tvShowsLibraryPath, folderName);

                            // Create folder if it doesn't exist
                            if (!Directory.Exists(showFolderPath))
                            {
                                Directory.CreateDirectory(showFolderPath);
                                result.ShowsCreated++;
                                _logger.LogInformation("Created TV show folder: {Path}", showFolderPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing TV show: {Title}", show.Title);
                            result.ShowsErrors++;
                        }
                    }
                }

                if (result.MoviesCreated == 0 && result.ShowsCreated == 0 && result.MoviesErrors == 0 && result.ShowsErrors == 0)
                {
                    _logger.LogWarning(
                        "Import completed but no folders were created. Movies path: {MoviesPath}, TV Shows path: {TvShowsPath}",
                        moviesLibraryPath ?? "null",
                        tvShowsLibraryPath ?? "null");
                }

                result.Success = true;
                _logger.LogInformation(
                    "Import completed successfully. Created {Movies} movie folders, {Shows} TV show folders",
                    result.MoviesCreated,
                    result.ShowsCreated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing plan to watch list: {Message}", ex.Message);
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Format movie folder name: MOVIE_NAME (YEAR) [tmdbid-00000].
        /// </summary>
        /// <param name="movie">The movie object.</param>
        /// <returns>Formatted folder name.</returns>
        private static string FormatMovieFolderName(API.Objects.SimklMovie movie)
        {
            var name = SanitizeFolderName(movie.Title ?? "Unknown Movie");

            if (movie.Year.HasValue)
            {
                name = $"{name} ({movie.Year.Value})";
            }

            var tmdbId = movie.Ids?.Tmdb;
            if (!string.IsNullOrEmpty(tmdbId))
            {
                name = $"{name} [tmdbid-{tmdbId}]";
            }

            return name;
        }

        /// <summary>
        /// Format movie file name: MOVIE_NAME (YEAR) [tmdbid-00000].mkv.
        /// </summary>
        /// <param name="movie">The movie object.</param>
        /// <returns>Formatted file name.</returns>
        private static string FormatMovieFileName(API.Objects.SimklMovie movie)
        {
            var name = SanitizeFolderName(movie.Title ?? "Unknown Movie");

            if (movie.Year.HasValue)
            {
                name = $"{name} ({movie.Year.Value})";
            }

            var tmdbId = movie.Ids?.Tmdb;
            if (!string.IsNullOrEmpty(tmdbId))
            {
                name = $"{name} [tmdbid-{tmdbId}]";
            }

            return $"{name}.mkv";
        }

        /// <summary>
        /// Format TV show folder name: SHOW_NAME (YEAR) [tvdbid-00000].
        /// </summary>
        /// <param name="show">The show object.</param>
        /// <returns>Formatted folder name.</returns>
        private static string FormatShowFolderName(API.Objects.SimklShow show)
        {
            var name = SanitizeFolderName(show.Title ?? "Unknown Show");

            if (show.Year.HasValue)
            {
                name = $"{name} ({show.Year.Value})";
            }

            var tvdbId = show.Ids?.Tvdb;
            if (!string.IsNullOrEmpty(tvdbId))
            {
                name = $"{name} [tvdbid-{tvdbId}]";
            }

            return name;
        }

        /// <summary>
        /// Sanitize folder name by removing invalid characters.
        /// </summary>
        /// <param name="name">Original name.</param>
        /// <returns>Sanitized name.</returns>
        private static string SanitizeFolderName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        /// <summary>
        /// Import result.
        /// </summary>
        public class ImportResult
        {
            /// <summary>
            /// Gets or sets a value indicating whether the import was successful.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// Gets or sets error message.
            /// </summary>
            public string? Error { get; set; }

            /// <summary>
            /// Gets or sets informational message.
            /// </summary>
            public string? Message { get; set; }

            /// <summary>
            /// Gets or sets number of movie folders created.
            /// </summary>
            public int MoviesCreated { get; set; }

            /// <summary>
            /// Gets or sets number of movie stub files copied.
            /// </summary>
            public int MoviesFilesCopied { get; set; }

            /// <summary>
            /// Gets or sets number of movie errors.
            /// </summary>
            public int MoviesErrors { get; set; }

            /// <summary>
            /// Gets or sets number of TV show folders created.
            /// </summary>
            public int ShowsCreated { get; set; }

            /// <summary>
            /// Gets or sets number of TV show errors.
            /// </summary>
            public int ShowsErrors { get; set; }
        }
    }
}