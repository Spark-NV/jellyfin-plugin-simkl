using System;

namespace Jellyfin.Plugin.Simkl.API.Objects
{
    /// <summary>
    /// Library information.
    /// </summary>
    public class LibraryInfo
    {
        /// <summary>
        /// Gets or sets the library ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the library name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the library type.
        /// </summary>
        public string LibraryType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the library path.
        /// </summary>
        public string Path { get; set; } = string.Empty;
    }
}