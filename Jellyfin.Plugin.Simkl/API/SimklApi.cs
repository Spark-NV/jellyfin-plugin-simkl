using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.Simkl.API.Exceptions;
using Jellyfin.Plugin.Simkl.API.Objects;
using Jellyfin.Plugin.Simkl.API.Responses;
using Jellyfin.Plugin.Simkl.Logging;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.Simkl.API
{
    /// <summary>
    /// Simkl Api.
    /// </summary>
    public class SimklApi
    {
        /* INTERFACES */
        private readonly SimklLogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly JsonSerializerOptions _caseInsensitiveJsonSerializerOptions;

        /* BASIC API THINGS */

        /// <summary>
        /// Base url.
        /// </summary>
        public const string Baseurl = @"https://api.simkl.com";

        /// <summary>
        /// Redirect uri.
        /// </summary>
        public const string RedirectUri = @"https://simkl.com/apps/jellyfin/connected/";

        /// <summary>
        /// Api key.
        /// </summary>
        public const string Apikey = @"c721b22482097722a84a20ccc579cf9d232be85b9befe7b7805484d0ddbc6781";

        /// <summary>
        /// Secret.
        /// </summary>
        public const string Secret = @"87893fc73cdbd2e51a7c63975c6f941ac1c6155c0e20ffa76b83202dd10a507e";

        /// <summary>
        /// Initializes a new instance of the <see cref="SimklApi"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        public SimklApi(IHttpClientFactory httpClientFactory)
        {
            _logger = SimklLoggerFactory.Instance;
            _httpClientFactory = httpClientFactory;
            _jsonSerializerOptions = JsonDefaults.Options;
            _caseInsensitiveJsonSerializerOptions = new JsonSerializerOptions(_jsonSerializerOptions)
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Get code.
        /// </summary>
        /// <returns>Code response.</returns>
        public async Task<CodeResponse?> GetCode()
        {
            var uri = $"/oauth/pin?client_id={Apikey}&redirect={RedirectUri}";
            return await Get<CodeResponse>(uri);
        }

        /// <summary>
        /// Get code status.
        /// </summary>
        /// <param name="userCode">User code.</param>
        /// <returns>Code status.</returns>
        public async Task<CodeStatusResponse?> GetCodeStatus(string userCode)
        {
            var uri = $"/oauth/pin/{userCode}?client_id={Apikey}";
            return await Get<CodeStatusResponse>(uri);
        }

        /// <summary>
        /// Get user settings.
        /// </summary>
        /// <param name="userToken">User token.</param>
        /// <returns>User settings.</returns>
        public async Task<UserSettings?> GetUserSettings(string userToken)
        {
            try
            {
                return await Post<UserSettings, object>("/users/settings/", userToken);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Wontfix: Custom status codes
                // "You don't get to pick your response code" - Luke (System Architect of Emby)
                // https://emby.media/community/index.php?/topic/61889-wiki-issue-resultfactorythrowerror/
                return new UserSettings { Error = "user_token_failed" };
            }
        }

        /// <summary>
        /// Get list items by status.
        /// </summary>
        /// <param name="userToken">User token.</param>
        /// <param name="status">Status to filter by (e.g., plantowatch, watching, completed, hold, dropped).</param>
        /// <returns>List response.</returns>
        public async Task<PlanToWatchResponse?> GetListByStatus(string userToken, string status = "plantowatch")
        {
            try
            {
                // Validate status value
                var validStatuses = new[] { "plantowatch", "watching", "completed", "hold", "dropped" };
                if (!validStatuses.Contains(status.ToLowerInvariant()))
                {
                    _logger.LogWarning("Invalid status {0}, defaulting to plantowatch", status);
                    status = "plantowatch";
                }

                var statusLower = status.ToLowerInvariant();
                var result = new PlanToWatchResponse { Movies = new List<SimklMovie>(), Shows = new List<SimklShow>(), Anime = new List<SimklAnimeItem>() };

                // Fetch movies
                var movieUri = $"/sync/all-items/movie/{statusLower}?extended=full";
                var movieListResponse = await Get<MovieListResponse>(movieUri, userToken);
                if (movieListResponse != null && movieListResponse.Movies != null)
                {
                    result.Movies = movieListResponse.Movies;
                    _logger.LogDebug("Retrieved {0} movies from Simkl", movieListResponse.Movies.Count);
                }
                else
                {
                    _logger.LogDebug("No movies returned from Simkl API");
                }

                // Fetch TV shows
                var tvUri = $"/sync/all-items/tv/{statusLower}?extended=full";
                var showListResponse = await Get<ShowListResponse>(tvUri, userToken);
                if (showListResponse != null && showListResponse.Shows != null)
                {
                    result.Shows = showListResponse.Shows;
                    _logger.LogDebug("Retrieved {0} TV shows from Simkl", showListResponse.Shows.Count);
                }
                else
                {
                    _logger.LogDebug("No TV shows returned from Simkl API");
                }

                // Fetch anime
                var animeUri = $"/sync/all-items/anime/{statusLower}?extended=full";
                var animeListResponse = await Get<AnimeListResponse>(animeUri, userToken);
                if (animeListResponse != null && animeListResponse.AnimeItems != null)
                {
                    result.Anime = animeListResponse.AnimeItems;
                }

                _logger.LogDebug("Simkl API response: {0} movies, {1} shows, {2} anime", result.Movies?.Count ?? 0, result.Shows?.Count ?? 0, result.Anime?.Count ?? 0);
                return result;
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(e, "Invalid user token {0}", userToken);
                if (SimklPlugin.Instance?.Configuration != null && SimklPlugin.Instance.Configuration.UserToken == userToken)
                {
                    SimklPlugin.Instance.Configuration.UserToken = string.Empty;
                    SimklPlugin.Instance.SaveConfiguration();
                }

                throw new InvalidTokenException("Invalid user token " + userToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving list from Simkl API for status: {0}", status);
                throw;
            }
        }

        /// <summary>
        /// Get plan to watch list.
        /// </summary>
        /// <param name="userToken">User token.</param>
        /// <returns>Plan to watch response.</returns>
        [Obsolete("Use GetListByStatus instead")]
        public async Task<PlanToWatchResponse?> GetPlanToWatch(string userToken)
        {
            return await GetListByStatus(userToken, "plantowatch");
        }

        /// <summary>
        /// Mark as watched.
        /// </summary>
        /// <param name="item">Item.</param>
        /// <param name="userToken">User token.</param>
        /// <returns>Status.</returns>
        public async Task<(bool Success, BaseItemDto Item)> MarkAsWatched(BaseItemDto item, string userToken)
        {
            var history = CreateHistoryFromItem(item);
            var r = await SyncHistoryAsync(history, userToken);
            _logger.LogDebug("BaseItem: {0}", item);
            _logger.LogDebug("History: {0}", history);
            _logger.LogDebug("Response: {0}", r?.ToString() ?? "null");
            if (r != null && history.Movies.Count == r.Added.Movies
                && history.Shows.Count == r.Added.Shows
                && history.Episodes.Count == r.Added.Episodes)
            {
                return (true, item);
            }

            // If we are here, is because the item has not been found
            // let's try scrobbling from full path
            try
            {
                (history, item) = await GetHistoryFromFileName(item);
            }
            catch (InvalidDataException)
            {
                // Let's try again but this time using only the FILE name
                _logger.LogDebug("Couldn't scrobble using full path, trying using only filename");
                (history, item) = await GetHistoryFromFileName(item, false);
            }

            r = await SyncHistoryAsync(history, userToken);
            return r == null
                ? (false, item)
                : (history.Movies.Count == r.Added.Movies && history.Shows.Count == r.Added.Shows, item);
        }

        /// <summary>
        /// Get from file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns>Search file response.</returns>
        private async Task<SearchFileResponse?> GetFromFile(string filename)
        {
            var f = new SimklFile { File = filename };
            _logger.LogInformation("Posting: {0}", f);
            return await Post<SearchFileResponse, SimklFile>("/search/file/", null, f);
        }

        /// <summary>
        /// Get history from file name.
        /// </summary>
        /// <param name="item">Item.</param>
        /// <param name="fullpath">Full path.</param>
        /// <returns>Srobble history.</returns>
        private async Task<(SimklHistory history, BaseItemDto item)> GetHistoryFromFileName(BaseItemDto item, bool fullpath = true)
        {
            var fname = fullpath ? item.Path : Path.GetFileName(item.Path);
            var mo = await GetFromFile(fname);
            if (mo == null)
            {
                throw new InvalidDataException("Search file response is null");
            }

            var history = new SimklHistory();
            if (mo.Movie != null &&
                (item.IsMovie == true || item.Type == BaseItemKind.Movie))
            {
                if (!string.Equals(mo.Type, "movie", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("type != movie (" + mo.Type + ")");
                }

                item.Name = mo.Movie.Title;
                item.ProductionYear = mo.Movie.Year;
                history.Movies.Add(mo.Movie);
            }
            else if (mo.Episode != null
                     && mo.Show != null
                     && (item.IsSeries == true || item.Type == BaseItemKind.Episode))
            {
                if (!string.Equals(mo.Type, "episode", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("type != episode (" + mo.Type + ")");
                }

                item.Name = mo.Episode.Title;
                item.SeriesName = mo.Show.Title;
                item.IndexNumber = mo.Episode.Episode;
                item.ParentIndexNumber = mo.Episode.Season;
                item.ProductionYear = mo.Show.Year;
                history.Episodes.Add(mo.Episode);
            }

            return (history, item);
        }

        private static HttpRequestMessage GetOptions(string? userToken = null)
        {
            var requestMessage = new HttpRequestMessage();
            requestMessage.Headers.TryAddWithoutValidation("simkl-api-key", Apikey);
            if (!string.IsNullOrEmpty(userToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            }

            return requestMessage;
        }

        private static SimklHistory CreateHistoryFromItem(BaseItemDto item)
        {
            var history = new SimklHistory();

            if (item.IsMovie == true || item.Type == BaseItemKind.Movie)
            {
                history.Movies.Add(new SimklMovie(item));
            }
            else if (item.IsSeries == true || (item.Type == BaseItemKind.Series))
            {
                // Check if this is anime based on provider IDs or other metadata
                // For now, we'll treat it as a regular show, but this could be enhanced
                // to detect anime based on metadata
                history.Shows.Add(new SimklShow(item));
            }
            else if (item.Type == BaseItemKind.Episode)
            {
                history.Episodes.Add(new SimklEpisode(item));
            }

            return history;
        }

        /// <summary>
        /// Implements /sync/history method from simkl.
        /// </summary>
        /// <param name="history">History object.</param>
        /// <param name="userToken">User token.</param>
        /// <returns>The sync history response.</returns>
        private async Task<SyncHistoryResponse?> SyncHistoryAsync(SimklHistory history, string userToken)
        {
            try
            {
                _logger.LogInformation("Syncing History");
                return await Post<SyncHistoryResponse, SimklHistory>("/sync/history", userToken, history);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(e, "Invalid user token {0}, deleting", userToken);
                if (SimklPlugin.Instance?.Configuration != null && SimklPlugin.Instance.Configuration.UserToken == userToken)
                {
                    SimklPlugin.Instance.Configuration.UserToken = string.Empty;
                    SimklPlugin.Instance.SaveConfiguration();
                }

                throw new InvalidTokenException("Invalid user token " + userToken);
            }
        }

        /// <summary>
        /// API's private get method, given RELATIVE url and headers.
        /// </summary>
        /// <param name="url">Relative url.</param>
        /// <param name="userToken">Authentication token.</param>
        /// <returns>HTTP(s) Stream to be used.</returns>
        private async Task<T?> Get<T>(string url, string? userToken = null)
        {
            // Todo: If string is not null neither empty
            using var options = GetOptions(userToken);
            options.RequestUri = new Uri(Baseurl + url);
            options.Method = HttpMethod.Get;
            var responseMessage = await _httpClientFactory.CreateClient(NamedClient.Default)
                .SendAsync(options);

            if (!responseMessage.IsSuccessStatusCode)
            {
                _logger.LogError("API request failed with status {0}: {1}", responseMessage.StatusCode, url);
                return default;
            }

            var content = await responseMessage.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("API response is empty for: {0}", url);
                return default;
            }

            _logger.LogDebug("API response content (first 500 chars): {0}", content.Length > 500 ? content.Substring(0, 500) : content);

            try
            {
                var result = await responseMessage.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions);
                if (result == null)
                {
                    _logger.LogWarning("JSON deserialization returned null for: {0}. Content: {1}", url, content.Length > 500 ? content.Substring(0, 500) : content);
                }

                return result;
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON response for: {0}. Content: {1}", url, content.Length > 500 ? content.Substring(0, 500) : content);
                throw;
            }
        }

        /// <summary>
        /// API's private post method.
        /// </summary>
        /// <param name="url">Relative post url.</param>
        /// <param name="userToken">Authentication token.</param>
        /// <param name="data">Object to serialize.</param>
        private async Task<T1?> Post<T1, T2>(string url, string? userToken = null, T2? data = null)
         where T2 : class
        {
            using var options = GetOptions(userToken);
            options.RequestUri = new Uri(Baseurl + url);
            options.Method = HttpMethod.Post;

            if (data != null)
            {
                options.Content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonSerializerOptions),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);
            }

            var responseMessage = await _httpClientFactory.CreateClient(NamedClient.Default)
                .SendAsync(options);

            return await responseMessage.Content.ReadFromJsonAsync<T1>(_caseInsensitiveJsonSerializerOptions);
        }
    }
}