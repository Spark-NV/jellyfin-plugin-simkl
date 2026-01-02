using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Simkl.API;
using Jellyfin.Plugin.Simkl.Configuration;
using Jellyfin.Plugin.Simkl.Logging;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.Simkl.Services
{
    /// <summary>
    /// Scheduled task that imports Simkl list items and creates/updates folders.
    /// </summary>
    public class ImportSimklListTask : IScheduledTask
    {
        private readonly SimklLogger _logger;
        private readonly PlanToWatchImporter _planToWatchImporter;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportSimklListTask"/> class.
        /// </summary>
        /// <param name="planToWatchImporter">Instance of the <see cref="PlanToWatchImporter"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public ImportSimklListTask(
            PlanToWatchImporter planToWatchImporter,
            ILibraryManager libraryManager)
        {
            _logger = SimklLoggerFactory.Instance;
            _planToWatchImporter = planToWatchImporter;
            _libraryManager = libraryManager;
        }

        /// <inheritdoc />
        public string Key => "SimklImportListTask";

        /// <inheritdoc />
        public string Name => "Import Simkl List";

        /// <inheritdoc />
        public string Category => "Simkl";

        /// <inheritdoc />
        public string Description => "Fetches the latest items from the selected Simkl list and creates/updates folders in the configured library paths";

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

            var configuration = SimklPlugin.Instance.Configuration;

            if (string.IsNullOrEmpty(configuration.UserToken))
            {
                _logger.LogInformation("No Simkl token configured. Please log in first.");
                progress.Report(100);
                return;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (configuration.MoviesLibraryId == Guid.Empty && configuration.TvShowsLibraryId == Guid.Empty && configuration.AnimeLibraryId == Guid.Empty
                && string.IsNullOrEmpty(configuration.MoviesLibraryPath) && string.IsNullOrEmpty(configuration.TvShowsLibraryPath) && string.IsNullOrEmpty(configuration.AnimeLibraryPath))
#pragma warning restore CS0618
            {
                _logger.LogWarning("No library paths configured. Please configure at least one library before importing.");
                progress.Report(100);
                return;
            }

            try
            {
                progress.Report(50);
                _logger.LogInformation("Importing Simkl list");

                var result = await _planToWatchImporter.ImportPlanToWatch(configuration).ConfigureAwait(false);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Successfully imported Simkl list. Created {0} movie folders, {1} TV show folders, {2} anime folders, {3} anime movie folders",
                        result.MoviesCreated,
                        result.ShowsCreated,
                        result.AnimeCreated,
                        result.AnimeMoviesCreated);
                }
                else
                {
                    _logger.LogWarning("Failed to import Simkl list: {0}", result.Error ?? "Unknown error");
                }

                progress.Report(90);

                // Check if library scanning is enabled after import
                if (configuration.TriggerLibraryScanAfterImport)
                {
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
                else
                {
                    _logger.LogInformation("Library scan after import is disabled, skipping");
                }

                progress.Report(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Simkl list");
                progress.Report(100);
            }

            _logger.LogInformation("Completed Simkl list import");
        }
    }
}
