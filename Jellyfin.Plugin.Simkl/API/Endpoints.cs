using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Simkl.API.Objects;
using Jellyfin.Plugin.Simkl.API.Responses;
using Jellyfin.Plugin.Simkl.Configuration;
using Jellyfin.Plugin.Simkl.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Simkl.API
{
    /// <summary>
    /// The simkl endpoints.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Simkl")]
    public class Endpoints : ControllerBase
    {
        private readonly SimklApi _simklApi;
        private readonly PlanToWatchImporter _planToWatchImporter;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="Endpoints"/> class.
        /// </summary>
        /// <param name="simklApi">Instance of the <see cref="SimklApi"/>.</param>
        /// <param name="planToWatchImporter">Instance of the <see cref="PlanToWatchImporter"/>.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/>.</param>
        public Endpoints(SimklApi simklApi, PlanToWatchImporter planToWatchImporter, ILibraryManager libraryManager)
        {
            _simklApi = simklApi;
            _planToWatchImporter = planToWatchImporter;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Gets the oauth pin.
        /// </summary>
        /// <returns>The oauth pin.</returns>
        [HttpGet("oauth/pin")]
        public async Task<ActionResult<CodeResponse?>> GetPin()
        {
            return await _simklApi.GetCode();
        }

        /// <summary>
        /// Gets the status for the code.
        /// </summary>
        /// <param name="userCode">The user auth code.</param>
        /// <returns>The code status response.</returns>
        [HttpGet("oauth/pin/{userCode}")]
        public async Task<ActionResult<CodeStatusResponse?>> GetPinStatus([FromRoute] string userCode)
        {
            return await _simklApi.GetCodeStatus(userCode);
        }

        /// <summary>
        /// Gets the settings for the user.
        /// </summary>
        /// <returns>The user settings.</returns>
        [HttpGet("users/settings")]
        public async Task<ActionResult<UserSettings?>> GetUserSettings()
        {
            var configuration = SimklPlugin.Instance?.Configuration;
            if (configuration == null || string.IsNullOrEmpty(configuration.UserToken))
            {
                return NotFound();
            }

            return await _simklApi.GetUserSettings(configuration.UserToken);
        }

        /// <summary>
        /// Gets the list of libraries.
        /// </summary>
        /// <returns>List of libraries.</returns>
        [HttpGet("libraries")]
        public ActionResult<List<LibraryInfo>> GetLibraries()
        {
            var libraries = _libraryManager.GetVirtualFolders()
                .Select(vf => new LibraryInfo
                {
                    Id = Guid.TryParse(vf.ItemId, out var guid) ? guid : Guid.Empty,
                    Name = vf.Name,
                    LibraryType = "Library",
                    Path = vf.LibraryOptions?.PathInfos?.FirstOrDefault()?.Path ?? string.Empty
                })
                .ToList();

            return libraries;
        }

        /// <summary>
        /// Import plan to watch list and create folders.
        /// </summary>
        /// <returns>The import result.</returns>
        [HttpPost("import/plan-to-watch")]
        public async Task<ActionResult<PlanToWatchImporter.ImportResult>> ImportPlanToWatch()
        {
            var configuration = SimklPlugin.Instance?.Configuration;
            if (configuration == null)
            {
                return NotFound();
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (configuration.MoviesLibraryId == Guid.Empty && configuration.TvShowsLibraryId == Guid.Empty
                && string.IsNullOrEmpty(configuration.MoviesLibraryPath) && string.IsNullOrEmpty(configuration.TvShowsLibraryPath))
#pragma warning restore CS0618
            {
                return BadRequest("Please configure at least one library (Movies or TV Shows) before importing.");
            }

            var result = await _planToWatchImporter.ImportPlanToWatch(configuration);

            if (!result.Success && !string.IsNullOrEmpty(result.Error))
            {
                return BadRequest(result);
            }

            return result;
        }
    }
}