using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Simkl.API;
using Jellyfin.Plugin.Simkl.API.Responses;
using Jellyfin.Plugin.Simkl.Configuration;
using Jellyfin.Plugin.Simkl.Logging;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Simkl.Services
{
    /// <summary>
    /// Service for importing plan to watch items and creating folders.
    /// </summary>
    public class PlanToWatchImporter
    {
        private readonly SimklLogger _logger;
        private readonly SimklApi _simklApi;
        private readonly ILibraryManager _libraryManager;
        private readonly PlaceholderStubTracker _placeholderTracker;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlanToWatchImporter"/> class.
        /// </summary>
        /// <param name="simklApi">Instance of the <see cref="SimklApi"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public PlanToWatchImporter(SimklApi simklApi, ILibraryManager libraryManager)
        {
            _logger = SimklLoggerFactory.Instance;
            _simklApi = simklApi;
            _libraryManager = libraryManager;

            var pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (pluginPath != null)
            {
                _placeholderTracker = new PlaceholderStubTracker(pluginPath);
            }
            else
            {
                throw new InvalidOperationException("Cannot determine plugin directory for placeholder stub tracking.");
            }
        }

        /// <summary>
        /// Import plan to watch items and create folders.
        /// </summary>
        /// <param name="config">Plugin configuration.</param>
        /// <returns>Import result.</returns>
        public async Task<ImportResult> ImportPlanToWatch(PluginConfiguration config)
        {
            var result = new ImportResult();

            if (string.IsNullOrEmpty(config.UserToken))
            {
                result.Error = "User token is not set. Please log in first.";
                return result;
            }

            try
            {
                // Get list from Simkl based on selected status
                var listStatus = string.IsNullOrEmpty(config.ImportListStatus) ? "plantowatch" : config.ImportListStatus;
                var planToWatch = await _simklApi.GetListByStatus(config.UserToken, listStatus);
                if (planToWatch == null)
                {
                    result.Error = $"Failed to retrieve {listStatus} list from Simkl. The list may be empty or the API response was invalid.";
                    return result;
                }

                // Check and update placeholder stubs that now have runtime
                await CheckAndUpdatePlaceholderStubsAsync(planToWatch, config);

                // Check if response has empty lists
                if ((planToWatch.Movies == null || planToWatch.Movies.Count == 0) &&
                    (planToWatch.Shows == null || planToWatch.Shows.Count == 0) &&
                    (planToWatch.Anime == null || planToWatch.Anime.Count == 0))
                {
                    result.Success = true;
                    result.Message = $"The {listStatus} list from Simkl is empty. No items to import.";
                    return result;
                }

                // Get library paths from IDs
                string? moviesLibraryPath = null;
                if (config.MoviesLibraryId != Guid.Empty)
                {
                    var moviesLibraryIdString = config.MoviesLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();

                    var moviesLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == moviesLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == config.MoviesLibraryId));

                    if (moviesLibrary != null)
                    {
                        if (moviesLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = moviesLibrary.LibraryOptions.PathInfos.ToList();
                            moviesLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(config.MoviesLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    moviesLibraryPath = config.MoviesLibraryPath;
                }
#pragma warning restore CS0618

                string? tvShowsLibraryPath = null;
                if (config.TvShowsLibraryId != Guid.Empty)
                {
                    var tvShowsLibraryIdString = config.TvShowsLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();

                    var tvShowsLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == tvShowsLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == config.TvShowsLibraryId));

                    if (tvShowsLibrary != null)
                    {
                        if (tvShowsLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = tvShowsLibrary.LibraryOptions.PathInfos.ToList();
                            tvShowsLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(config.TvShowsLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    tvShowsLibraryPath = config.TvShowsLibraryPath;
                }
#pragma warning restore CS0618

                string? animeLibraryPath = null;
                if (config.AnimeLibraryId != Guid.Empty)
                {
                    var animeLibraryIdString = config.AnimeLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();

                    var animeLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == animeLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == config.AnimeLibraryId));

                    if (animeLibrary != null)
                    {
                        _logger.LogDebug("Found anime library: {0}, LibraryOptions is null: {1}", animeLibrary.Name, animeLibrary.LibraryOptions == null);

                        if (animeLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = animeLibrary.LibraryOptions.PathInfos.ToList();
                            _logger.LogDebug("Found {0} path infos for anime library", pathInfos.Count);
                            foreach (var pathInfo in pathInfos)
                            {
                                _logger.LogDebug("Path info: {0}", pathInfo.Path);
                            }

                            animeLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Anime library with ID {0} not found in virtual folders", animeLibraryIdString);
                    }

                    _logger.LogInformation("Anime library ID {0} resolved to path: {1}", animeLibraryIdString, animeLibraryPath ?? "null");
                    if (string.IsNullOrEmpty(animeLibraryPath))
                    {
                        _logger.LogWarning("Anime library ID {0} could not be resolved to a path", animeLibraryIdString);
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(config.AnimeLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    animeLibraryPath = config.AnimeLibraryPath;
                }
#pragma warning restore CS0618

                string? animeMoviesLibraryPath = null;
                if (config.AnimeMoviesLibraryId != Guid.Empty)
                {
                    var animeMoviesLibraryIdString = config.AnimeMoviesLibraryId.ToString();
                    var allLibraries = _libraryManager.GetVirtualFolders().ToList();

                    var animeMoviesLibrary = allLibraries
                        .FirstOrDefault(
                            vf => vf.ItemId == animeMoviesLibraryIdString
                                || (Guid.TryParse(vf.ItemId, out var guid) && guid == config.AnimeMoviesLibraryId));

                    if (animeMoviesLibrary != null)
                    {
                        _logger.LogDebug("Found anime movies library: {0}, LibraryOptions is null: {1}", animeMoviesLibrary.Name, animeMoviesLibrary.LibraryOptions == null);

                        if (animeMoviesLibrary.LibraryOptions?.PathInfos != null)
                        {
                            var pathInfos = animeMoviesLibrary.LibraryOptions.PathInfos.ToList();
                            _logger.LogDebug("Found {0} path infos for anime movies library", pathInfos.Count);
                            foreach (var pathInfo in pathInfos)
                            {
                                _logger.LogDebug("Path info: {0}", pathInfo.Path);
                            }

                            animeMoviesLibraryPath = pathInfos.FirstOrDefault()?.Path;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Anime movies library with ID {0} not found in virtual folders", animeMoviesLibraryIdString);
                    }

                    _logger.LogInformation("Anime movies library ID {0} resolved to path: {1}", animeMoviesLibraryIdString, animeMoviesLibraryPath ?? "null");
                    if (string.IsNullOrEmpty(animeMoviesLibraryPath))
                    {
                        _logger.LogWarning("Anime movies library ID {0} could not be resolved to a path", animeMoviesLibraryIdString);
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (!string.IsNullOrEmpty(config.AnimeMoviesLibraryPath))
                {
                    // Fallback to deprecated path for backward compatibility
                    animeMoviesLibraryPath = config.AnimeMoviesLibraryPath;
                }
#pragma warning restore CS0618

                // Validate that at least one library path is configured
                if (string.IsNullOrEmpty(moviesLibraryPath) && string.IsNullOrEmpty(tvShowsLibraryPath) && string.IsNullOrEmpty(animeLibraryPath) && string.IsNullOrEmpty(animeMoviesLibraryPath))
                {
                    result.Error = "No library paths configured. Please select at least one library (Movies, TV Shows, Anime, or Anime Movies).";
                    _logger.LogError("Import failed: No library paths configured");
                    return result;
                }

                // Process movies
                if (!string.IsNullOrEmpty(moviesLibraryPath) && planToWatch.Movies != null && planToWatch.Movies.Count > 0)
                {
                    // First pass: Create all folders
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
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating folder for movie: {0}", movie.Title ?? "Unknown");
                            result.MoviesErrors++;
                        }
                    }

                    // Second pass: Copy stub files (folders are now guaranteed to exist)
                    foreach (var movie in planToWatch.Movies)
                    {
                        try
                        {
                            var folderName = FormatMovieFolderName(movie);
                            var movieFolderPath = Path.Combine(moviesLibraryPath, folderName);
                            var fileName = FormatMovieFileName(movie);
                            var targetFile = Path.Combine(movieFolderPath, fileName);

                            if (!IsStubFileValid(targetFile))
                            {
                                // Check if file exists but is malformed (too small)
                                var fileExistsButInvalid = File.Exists(targetFile) && !IsStubFileValid(targetFile);
                                if (fileExistsButInvalid)
                                {
                                    _logger.LogWarning("Detected malformed stub file (too small) for movie '{0}', will replace: {1}", movie.Title ?? "Unknown", targetFile);

                                    // Add malformed stub to placeholder tracker for replacement
                                    if (movie.Ids?.Simkl.HasValue == true)
                                    {
                                        _placeholderTracker.AddPlaceholderStub(movie.Ids.Simkl.Value, "movie", targetFile);
                                    }
                                }

                                // Use runtime from SIMKL if available, otherwise fallback to 1.5 hours (90 minutes)
                                var runtimeMinutes = movie.Runtime.HasValue && movie.Runtime.Value > 0
                                    ? movie.Runtime.Value
                                    : 90; // 1.5 hours fallback

                                var isPlaceholder = !movie.Runtime.HasValue || movie.Runtime.Value <= 0;
                                if (isPlaceholder)
                                {
                                    _logger.LogDebug("Using fallback duration (1.5 hours) for movie '{0}': no runtime data available from SIMKL", movie.Title ?? "Unknown");

                                    // Track placeholder stub if we have a Simkl ID (only if not already added above)
                                    if (movie.Ids?.Simkl.HasValue == true && !fileExistsButInvalid)
                                    {
                                        _placeholderTracker.AddPlaceholderStub(movie.Ids.Simkl.Value, "movie", targetFile);
                                    }
                                }

                                // Find the closest stub file and copy it
                                var stubFilePath = FindClosestStubFile(runtimeMinutes);
                                if (stubFilePath != null)
                                {
                                    try
                                    {
                                        File.Copy(stubFilePath, targetFile, true); // Overwrite if exists
                                        result.MoviesFilesCopied++;
                                        _logger.LogInformation("Copied stub file for movie '{0}': {1} -> {2}", movie.Title ?? "Unknown", Path.GetFileName(stubFilePath), targetFile);
                                    }
                                    catch (Exception copyEx)
                                    {
                                        _logger.LogError(copyEx, "Failed to copy stub file for movie '{0}': {1} -> {2}", movie.Title ?? "Unknown", stubFilePath, targetFile);
                                        result.MoviesErrors++;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("No suitable stub file found for movie '{0}' with runtime {1} minutes", movie.Title ?? "Unknown", runtimeMinutes);
                                    result.MoviesErrors++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing stub file for movie: {0}", movie.Title ?? "Unknown");
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
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing TV show: {0}", show.Title ?? "Unknown");
                            result.ShowsErrors++;
                        }
                    }
                }

                // Process anime TV shows
                if (!string.IsNullOrEmpty(animeLibraryPath) && planToWatch.Anime != null && planToWatch.Anime.Count > 0)
                {
                    var tvAnime = planToWatch.Anime.Where(a => a.Show != null && string.Equals(a.AnimeType, "tv", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var animeItem in tvAnime)
                    {
                        try
                        {
                            var folderName = FormatAnimeFolderName(animeItem.Show!);
                            var animeFolderPath = Path.Combine(animeLibraryPath, folderName);

                            // Create folder if it doesn't exist
                            if (!Directory.Exists(animeFolderPath))
                            {
                                Directory.CreateDirectory(animeFolderPath);
                                result.AnimeCreated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing anime TV show: {0}", animeItem.Show?.Title ?? "Unknown");
                            result.AnimeErrors++;
                        }
                    }
                }

                // Process anime movies
                if (!string.IsNullOrEmpty(animeMoviesLibraryPath) && planToWatch.Anime != null && planToWatch.Anime.Count > 0)
                {
                    var movieAnime = planToWatch.Anime.Where(a => a.Show != null && string.Equals(a.AnimeType, "movie", StringComparison.OrdinalIgnoreCase)).ToList();

                    // First pass: Create all folders
                    foreach (var animeItem in movieAnime)
                    {
                        try
                        {
                            var folderName = FormatAnimeMovieFolderName(animeItem.Show!);
                            var animeFolderPath = Path.Combine(animeMoviesLibraryPath, folderName);

                            // Create folder if it doesn't exist
                            if (!Directory.Exists(animeFolderPath))
                            {
                                Directory.CreateDirectory(animeFolderPath);
                                result.AnimeMoviesCreated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating folder for anime movie: {0}", animeItem.Show?.Title ?? "Unknown");
                            result.AnimeMoviesErrors++;
                        }
                    }

                    // Second pass: Copy stub files (folders are now guaranteed to exist)
                    foreach (var animeItem in movieAnime)
                    {
                        try
                        {
                            var folderName = FormatAnimeMovieFolderName(animeItem.Show!);
                            var animeFolderPath = Path.Combine(animeMoviesLibraryPath, folderName);
                            var fileName = FormatAnimeMovieFileName(animeItem.Show!);
                            var targetFile = Path.Combine(animeFolderPath, fileName);

                            if (!IsStubFileValid(targetFile))
                            {
                                // Check if file exists but is malformed (too small)
                                var fileExistsButInvalid = File.Exists(targetFile) && !IsStubFileValid(targetFile);
                                if (fileExistsButInvalid)
                                {
                                    _logger.LogWarning("Detected malformed stub file (too small) for anime movie '{0}', will replace: {1}", animeItem.Show?.Title ?? "Unknown", targetFile);

                                    // Add malformed stub to placeholder tracker for replacement
                                    if (animeItem.Show?.Ids?.Simkl.HasValue == true)
                                    {
                                        _placeholderTracker.AddPlaceholderStub(animeItem.Show.Ids.Simkl.Value, "animemovie", targetFile);
                                    }
                                }

                                // Use runtime from SIMKL if available, otherwise fallback to 50 minutes for anime movies
                                var runtimeMinutes = animeItem.Show?.Runtime.HasValue == true && animeItem.Show.Runtime.Value > 0
                                    ? animeItem.Show.Runtime.Value
                                    : 50; // 50 minutes fallback for anime movies

                                var isPlaceholder = !(animeItem.Show?.Runtime.HasValue == true && animeItem.Show.Runtime.Value > 0);
                                if (isPlaceholder)
                                {
                                    _logger.LogDebug("Using fallback duration (50 minutes) for anime movie '{0}': no runtime data available from SIMKL", animeItem.Show?.Title ?? "Unknown");

                                    // Track placeholder stub if we have a Simkl ID (only if not already added above)
                                    if (animeItem.Show?.Ids?.Simkl.HasValue == true && !fileExistsButInvalid)
                                    {
                                        _placeholderTracker.AddPlaceholderStub(animeItem.Show.Ids.Simkl.Value, "animemovie", targetFile);
                                    }
                                }

                                // Find the closest stub file and copy it
                                var stubFilePath = FindClosestStubFile(runtimeMinutes);
                                if (stubFilePath != null)
                                {
                                    try
                                    {
                                        File.Copy(stubFilePath, targetFile, true); // Overwrite if exists
                                        result.AnimeMoviesFilesCopied++;
                                        _logger.LogInformation("Copied stub file for anime movie '{0}': {1} -> {2}", animeItem.Show?.Title ?? "Unknown", Path.GetFileName(stubFilePath), targetFile);
                                    }
                                    catch (Exception copyEx)
                                    {
                                        _logger.LogError(copyEx, "Failed to copy stub file for anime movie '{0}': {1} -> {2}", animeItem.Show?.Title ?? "Unknown", stubFilePath, targetFile);
                                        result.AnimeMoviesErrors++;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("No suitable stub file found for anime movie '{0}' with runtime {1} minutes", animeItem.Show?.Title ?? "Unknown", runtimeMinutes);
                                    result.AnimeMoviesErrors++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing stub file for anime movie: {0}", animeItem.Show?.Title ?? "Unknown");
                            result.AnimeMoviesErrors++;
                        }
                    }
                }

                if (result.MoviesCreated == 0 && result.ShowsCreated == 0 && result.AnimeCreated == 0 && result.AnimeMoviesCreated == 0 &&
                    result.MoviesErrors == 0 && result.ShowsErrors == 0 && result.AnimeErrors == 0 && result.AnimeMoviesErrors == 0)
                {
                _logger.LogWarning("Import completed but no folders were created. Anime path: {0}, Anime Movies path: {1}", animeLibraryPath ?? "null", animeMoviesLibraryPath ?? "null");
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing plan to watch list: {0}", ex.Message);
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
        /// Format anime folder name: ANIME_NAME (YEAR) [malid-00000].
        /// </summary>
        /// <param name="anime">The anime object.</param>
        /// <returns>Formatted folder name.</returns>
        private static string FormatAnimeFolderName(API.Objects.SimklAnime anime)
        {
            var name = SanitizeFolderName(anime.Title ?? "Unknown Anime");

            if (anime.Year.HasValue)
            {
                name = $"{name} ({anime.Year.Value})";
            }

            var tvdbId = anime.Ids?.Tvdb;
            if (!string.IsNullOrEmpty(tvdbId))
            {
                name = $"{name} [tvdbid-{tvdbId}]";
            }

            return name;
        }

        /// <summary>
        /// Format anime movie folder name: ANIME_NAME (YEAR) [tmdbid-00000].
        /// </summary>
        /// <param name="anime">The anime object.</param>
        /// <returns>Formatted folder name.</returns>
        private static string FormatAnimeMovieFolderName(API.Objects.SimklAnime anime)
        {
            var name = SanitizeFolderName(anime.Title ?? "Unknown Anime Movie");

            if (anime.Year.HasValue)
            {
                name = $"{name} ({anime.Year.Value})";
            }

            var tmdbId = anime.Ids?.Tmdb;
            if (!string.IsNullOrEmpty(tmdbId))
            {
                name = $"{name} [tmdbid-{tmdbId}]";
            }

            return name;
        }

        /// <summary>
        /// Format anime movie file name: ANIME_NAME (YEAR) [tmdbid-00000].mkv.
        /// </summary>
        /// <param name="anime">The anime object.</param>
        /// <returns>Formatted file name.</returns>
        private static string FormatAnimeMovieFileName(API.Objects.SimklAnime anime)
        {
            var name = SanitizeFolderName(anime.Title ?? "Unknown Anime Movie");

            if (anime.Year.HasValue)
            {
                name = $"{name} ({anime.Year.Value})";
            }

            var tmdbId = anime.Ids?.Tmdb;
            if (!string.IsNullOrEmpty(tmdbId))
            {
                name = $"{name} [tmdbid-{tmdbId}]";
            }

            return $"{name}.mkv";
        }

        /// <summary>
        /// Checks if a stub file is valid (exists and has reasonable size > 20KB).
        /// </summary>
        /// <param name="filePath">Path to the stub file.</param>
        /// <returns>True if the file exists and is large enough, false otherwise.</returns>
        private bool IsStubFileValid(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                // Consider files smaller than 20KB as malformed/incomplete
                return fileInfo.Length >= 20 * 1024; // 20KB minimum
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking stub file size for: {0}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Checks placeholder stubs and updates them if runtime is now available.
        /// </summary>
        /// <param name="planToWatch">The plan to watch response from Simkl.</param>
        /// <param name="config">Plugin configuration.</param>
        private async Task CheckAndUpdatePlaceholderStubsAsync(PlanToWatchResponse planToWatch, PluginConfiguration config)
        {
            try
            {
                var placeholderStubs = _placeholderTracker.GetAllPlaceholderStubs();
                if (placeholderStubs.Count == 0)
                {
                    return;
                }

                _logger.LogDebug("Checking {0} placeholder stubs for runtime updates", placeholderStubs.Count);

                var updatedCount = 0;

                // Check movies
                if (planToWatch.Movies != null)
                {
                    foreach (var placeholder in placeholderStubs.Where(p => p.Type == "movie"))
                    {
                        var movie = planToWatch.Movies.FirstOrDefault(m => m.Ids?.Simkl == placeholder.SimklId);
                        if (movie != null && movie.Runtime.HasValue && movie.Runtime.Value > 0)
                        {
                            // Runtime is now available, update the stub
                            if (File.Exists(placeholder.FilePath))
                            {
                                try
                                {
                                    // Delete old placeholder stub
                                    File.Delete(placeholder.FilePath);
                                    _logger.LogInformation("Deleted placeholder stub for movie (Simkl ID: {0}) to replace with correct runtime", placeholder.SimklId);

                                    // Copy appropriate stub file with correct runtime
                                    var stubFilePath = FindClosestStubFile(movie.Runtime.Value);
                                    if (stubFilePath != null)
                                    {
                                        File.Copy(stubFilePath, placeholder.FilePath, true); // Overwrite if exists
                                        _placeholderTracker.RemovePlaceholderStub(placeholder.SimklId, placeholder.Type);
                                        updatedCount++;
                                        _logger.LogInformation("Successfully updated placeholder stub for movie (Simkl ID: {0}) with runtime {1} minutes", placeholder.SimklId, movie.Runtime.Value);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to find stub file for movie (Simkl ID: {0}) with runtime {1} minutes", placeholder.SimklId, movie.Runtime.Value);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error updating placeholder stub for movie (Simkl ID: {0})", placeholder.SimklId);
                                }
                            }
                            else
                            {
                                // File doesn't exist, remove from tracker
                                _placeholderTracker.RemovePlaceholderStub(placeholder.SimklId, placeholder.Type);
                            }
                        }
                    }
                }

                // Check anime movies
                if (planToWatch.Anime != null)
                {
                    var movieAnime = planToWatch.Anime.Where(a => a.Show != null && string.Equals(a.AnimeType, "movie", StringComparison.OrdinalIgnoreCase));
                    foreach (var placeholder in placeholderStubs.Where(p => p.Type == "animemovie"))
                    {
                        var animeItem = movieAnime.FirstOrDefault(a => a.Show?.Ids?.Simkl == placeholder.SimklId);
                        if (animeItem?.Show != null && animeItem.Show.Runtime.HasValue && animeItem.Show.Runtime.Value > 0)
                        {
                            // Runtime is now available, update the stub
                            if (File.Exists(placeholder.FilePath))
                            {
                                try
                                {
                                    // Delete old placeholder stub
                                    File.Delete(placeholder.FilePath);
                                    _logger.LogInformation("Deleted placeholder stub for anime movie (Simkl ID: {0}) to replace with correct runtime", placeholder.SimklId);

                                    // Copy appropriate stub file with correct runtime
                                    var stubFilePath = FindClosestStubFile(animeItem.Show.Runtime.Value);
                                    if (stubFilePath != null)
                                    {
                                        File.Copy(stubFilePath, placeholder.FilePath, true); // Overwrite if exists
                                        _placeholderTracker.RemovePlaceholderStub(placeholder.SimklId, placeholder.Type);
                                        updatedCount++;
                                        _logger.LogInformation("Successfully updated placeholder stub for anime movie (Simkl ID: {0}) with runtime {1} minutes", placeholder.SimklId, animeItem.Show.Runtime.Value);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to find stub file for anime movie (Simkl ID: {0}) with runtime {1} minutes", placeholder.SimklId, animeItem.Show.Runtime.Value);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error updating placeholder stub for anime movie (Simkl ID: {0})", placeholder.SimklId);
                                }
                            }
                            else
                            {
                                // File doesn't exist, remove from tracker
                                _placeholderTracker.RemovePlaceholderStub(placeholder.SimklId, placeholder.Type);
                            }
                        }
                    }
                }

                if (updatedCount > 0)
                {
                    _logger.LogInformation("Updated {0} placeholder stubs with correct runtime", updatedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and updating placeholder stubs");
            }
        }

        /// <summary>
        /// Find the closest stub file for the given runtime.
        /// Runtimes shorter than 10 minutes use the 10min stub.
        /// Runtimes longer than 4 hours (240 minutes) use the 240min stub.
        /// </summary>
        /// <param name="runtimeMinutes">Runtime in minutes.</param>
        /// <returns>Path to the closest stub file, or null if none found.</returns>
        private string? FindClosestStubFile(int runtimeMinutes)
        {
            try
            {
                var pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (pluginPath == null)
                {
                    _logger.LogError("Cannot determine plugin directory for stub file lookup.");
                    return null;
                }

                var stubsPath = Path.Combine(pluginPath, "STUBS");
                if (!Directory.Exists(stubsPath))
                {
                    _logger.LogError("STUBS directory not found at: {0}", stubsPath);
                    return null;
                }

                var stubFiles = Directory.GetFiles(stubsPath, "*.mp4").Concat(Directory.GetFiles(stubsPath, "*.mkv")).ToList();
                if (stubFiles.Count == 0)
                {
                    _logger.LogError("No stub files found in STUBS directory: {0}", stubsPath);
                    return null;
                }

                // Parse stub file names to extract minute values
                var stubMinutes = new List<(int minutes, string filePath)>();
                foreach (var file in stubFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Look for pattern like "25min", "68min", etc.
                    var minIndex = fileName.IndexOf("min", StringComparison.OrdinalIgnoreCase);
                    if (minIndex > 0)
                    {
                        var minutePart = fileName.Substring(0, minIndex);
                        if (int.TryParse(minutePart, out var minutes))
                        {
                            stubMinutes.Add((minutes, file));
                        }
                    }
                }

                if (stubMinutes.Count == 0)
                {
                    _logger.LogError("No valid stub files found with 'min' naming pattern in STUBS directory");
                    return null;
                }

                // Apply runtime limits: minimum 10 minutes, maximum 240 minutes (4 hours)
                var targetMinutes = runtimeMinutes;
                if (targetMinutes < 10)
                {
                    targetMinutes = 10;
                    _logger.LogDebug("Runtime {0} minutes is below minimum, using 10min stub", runtimeMinutes);
                }
                else if (targetMinutes > 240)
                {
                    targetMinutes = 240;
                    _logger.LogDebug("Runtime {0} minutes is above maximum, using 240min stub", runtimeMinutes);
                }

                var closestStub = stubMinutes
                    .OrderBy(s => Math.Abs(s.minutes - targetMinutes))
                    .ThenBy(s => s.minutes) // If tie, prefer smaller (already rounded down)
                    .First();

                _logger.LogDebug("Found stub file for {0} minutes (original runtime: {1} minutes): {2} (stub has {3} minutes)", targetMinutes, runtimeMinutes, Path.GetFileName(closestStub.filePath), closestStub.minutes);

                return closestStub.filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding closest stub file for runtime {0} minutes", runtimeMinutes);
                return null;
            }
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

            /// <summary>
            /// Gets or sets number of anime folders created.
            /// </summary>
            public int AnimeCreated { get; set; }

            /// <summary>
            /// Gets or sets number of anime errors.
            /// </summary>
            public int AnimeErrors { get; set; }

            /// <summary>
            /// Gets or sets number of anime movies folders created.
            /// </summary>
            public int AnimeMoviesCreated { get; set; }

            /// <summary>
            /// Gets or sets number of anime movies stub files copied.
            /// </summary>
            public int AnimeMoviesFilesCopied { get; set; }

            /// <summary>
            /// Gets or sets number of anime movies errors.
            /// </summary>
            public int AnimeMoviesErrors { get; set; }
        }
    }
}