using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.Simkl.Services
{
    /// <summary>
    /// Tracks placeholder stubs that need to be replaced when runtime becomes available.
    /// </summary>
    public class PlaceholderStubTracker
    {
        private readonly string _trackerFilePath;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderStubTracker"/> class.
        /// </summary>
        /// <param name="pluginDirectory">The plugin directory where the tracker file will be stored.</param>
        public PlaceholderStubTracker(string pluginDirectory)
        {
            _trackerFilePath = Path.Combine(pluginDirectory, "Placeholder_Stubs_List.txt");
        }

        /// <summary>
        /// Adds a placeholder stub to the tracking list.
        /// </summary>
        /// <param name="simklId">The Simkl ID of the item.</param>
        /// <param name="type">The type of item ("movie" or "animemovie").</param>
        /// <param name="filePath">The full path to the stub file.</param>
        public void AddPlaceholderStub(int simklId, string type, string filePath)
        {
            lock (_lock)
            {
                try
                {
                    var entries = ReadAllEntries();

                    // Check if already exists (shouldn't happen, but be safe)
                    if (!entries.Any(e => e.SimklId == simklId && e.Type == type))
                    {
                        entries.Add(new PlaceholderStubEntry
                        {
                            SimklId = simklId,
                            Type = type,
                            FilePath = filePath
                        });

                        WriteAllEntries(entries);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - tracking is not critical
                    Console.WriteLine($"Failed to add placeholder stub to tracker: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets all tracked placeholder stubs.
        /// </summary>
        /// <returns>List of placeholder stub entries.</returns>
        public List<PlaceholderStubEntry> GetAllPlaceholderStubs()
        {
            lock (_lock)
            {
                return ReadAllEntries();
            }
        }

        /// <summary>
        /// Removes a placeholder stub from the tracking list.
        /// </summary>
        /// <param name="simklId">The Simkl ID of the item.</param>
        /// <param name="type">The type of item.</param>
        public void RemovePlaceholderStub(int simklId, string type)
        {
            lock (_lock)
            {
                try
                {
                    var entries = ReadAllEntries();
                    entries.RemoveAll(e => e.SimklId == simklId && e.Type == type);
                    WriteAllEntries(entries);
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - tracking is not critical
                    Console.WriteLine($"Failed to remove placeholder stub from tracker: {ex.Message}");
                }
            }
        }

        private List<PlaceholderStubEntry> ReadAllEntries()
        {
            var entries = new List<PlaceholderStubEntry>();

            if (!File.Exists(_trackerFilePath))
            {
                return entries;
            }

            try
            {
                var lines = File.ReadAllLines(_trackerFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var parts = line.Split('|');
                    if (parts.Length == 3)
                    {
                        if (int.TryParse(parts[0], out var simklId))
                        {
                            entries.Add(new PlaceholderStubEntry
                            {
                                SimklId = simklId,
                                Type = parts[1],
                                FilePath = parts[2]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read placeholder stub tracker file: {ex.Message}");
            }

            return entries;
        }

        private void WriteAllEntries(List<PlaceholderStubEntry> entries)
        {
            try
            {
                var lines = entries.Select(e => $"{e.SimklId}|{e.Type}|{e.FilePath}");
                File.WriteAllLines(_trackerFilePath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write placeholder stub tracker file: {ex.Message}");
            }
        }

        /// <summary>
        /// Represents a placeholder stub entry in the tracker.
        /// </summary>
        public class PlaceholderStubEntry
        {
            /// <summary>
            /// Gets or sets the Simkl ID.
            /// </summary>
            public int SimklId { get; set; }

            /// <summary>
            /// Gets or sets the type ("movie" or "animemovie").
            /// </summary>
            public string Type { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the file path to the stub file.
            /// </summary>
            public string FilePath { get; set; } = string.Empty;
        }
    }
}