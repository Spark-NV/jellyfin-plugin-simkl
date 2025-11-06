using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Simkl.API;
using Jellyfin.Plugin.Simkl.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Simkl.Services
{
    /// <summary>
    /// Scheduled task that imports Simkl list items and creates/updates folders.
    /// </summary>
    public class ImportSimklListTask : IScheduledTask
    {
        private readonly ILogger<ImportSimklListTask> _logger;
        private readonly PlanToWatchImporter _planToWatchImporter;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportSimklListTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ImportSimklListTask}"/> interface.</param>
        /// <param name="planToWatchImporter">Instance of the <see cref="PlanToWatchImporter"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public ImportSimklListTask(
            ILogger<ImportSimklListTask> logger,
            PlanToWatchImporter planToWatchImporter,
            IUserManager userManager,
            ILibraryManager libraryManager)
        {
            _logger = logger;
            _planToWatchImporter = planToWatchImporter;
            _userManager = userManager;
            _libraryManager = libraryManager;
        }

        /// <inheritdoc />
        public string Key => "SimklImportListTask";

        /// <inheritdoc />
        public string Name => "Import Simkl List";

        /// <inheritdoc />
        public string Category => "Simkl";

        /// <inheritdoc />
        public string Description => "Fetches the latest items from each user's selected Simkl list and creates/updates folders in their library paths";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Return a trigger set to 6 hours
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(6).Ticks,
                    MaxRuntimeTicks = TimeSpan.FromMinutes(30).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (SimklPlugin.Instance == null)
            {
                _logger.LogWarning("SimklPlugin instance is null, cannot run import task");
                return;
            }

            var users = _userManager.Users.ToList();
            var userConfigs = SimklPlugin.Instance.Configuration.UserConfigs;

            if (users.Count == 0)
            {
                _logger.LogInformation("No users found");
                return;
            }

            // Filter users that have a valid token and configuration
            var usersWithConfig = users
                .Where(u =>
                {
                    var config = userConfigs?.FirstOrDefault(c => c.Id == u.Id);
                    return config != null
                        && !string.IsNullOrEmpty(config.UserToken)
                        && (config.MoviesLibraryId != Guid.Empty || config.TvShowsLibraryId != Guid.Empty
#pragma warning disable CS0618 // Type or member is obsolete
                            || !string.IsNullOrEmpty(config.MoviesLibraryPath) || !string.IsNullOrEmpty(config.TvShowsLibraryPath));
#pragma warning restore CS0618
                })
                .ToList();

            if (usersWithConfig.Count == 0)
            {
                _logger.LogInformation("No users with valid Simkl configuration found");
                return;
            }

            var percentPerUser = 100d / usersWithConfig.Count;
            double currentProgress = 0;

            foreach (var user in usersWithConfig)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var userConfig = userConfigs?.FirstOrDefault(c => c.Id == user.Id);
                    if (userConfig == null)
                    {
                        _logger.LogDebug("No Simkl configuration found for user {UserName}", user.Username);
                        currentProgress += percentPerUser;
                        progress.Report(currentProgress);
                        continue;
                    }

                    if (string.IsNullOrEmpty(userConfig.UserToken))
                    {
                        _logger.LogDebug("No Simkl token configured for user {UserName}", user.Username);
                        currentProgress += percentPerUser;
                        progress.Report(currentProgress);
                        continue;
                    }

#pragma warning disable CS0618 // Type or member is obsolete
                    if (userConfig.MoviesLibraryId == Guid.Empty && userConfig.TvShowsLibraryId == Guid.Empty
                        && string.IsNullOrEmpty(userConfig.MoviesLibraryPath) && string.IsNullOrEmpty(userConfig.TvShowsLibraryPath))
#pragma warning restore CS0618
                    {
                        _logger.LogWarning("No library paths configured for user {UserName}", user.Username);
                        currentProgress += percentPerUser;
                        progress.Report(currentProgress);
                        continue;
                    }

                    _logger.LogInformation("Importing Simkl list for user {UserName}", user.Username);

                    var result = await _planToWatchImporter.ImportPlanToWatch(userConfig).ConfigureAwait(false);

                    if (result.Success)
                    {
                        _logger.LogInformation(
                            "Successfully imported Simkl list for user {UserName}. Created {Movies} movie folders, {Shows} TV show folders",
                            user.Username,
                            result.MoviesCreated,
                            result.ShowsCreated);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to import Simkl list for user {UserName}: {Error}",
                            user.Username,
                            result.Error ?? "Unknown error");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing Simkl list for user {UserName}", user.Username);
                }
                finally
                {
                    currentProgress += percentPerUser;
                    progress.Report(currentProgress);
                }
            }

            _logger.LogInformation("Completed Simkl list import for all users");

            // Wait 60 seconds before triggering library scan
            _logger.LogInformation("Waiting 60 seconds before triggering library scan");
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);

            // Trigger library scan
            _logger.LogInformation("Triggering library scan");
            try
            {
                var scanProgress = new Progress<double>();
                await _libraryManager.ValidateMediaLibrary(scanProgress, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Library scan completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering library scan");
            }
        }
    }
}
