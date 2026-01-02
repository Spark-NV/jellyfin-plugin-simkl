<h1 align="center">Jellyfin SIMKL Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org/">Jellyfin Project</a></h3>


###

## Overview
The Jellyfin SIMKL plugin allows you to sync your watch history and import your SIMKL lists to automatically create organized libraries in Jellyfin.

## Key Features

### 📚 List Import & Library Creation
- **Import SIMKL Lists** - Automatically create Jellyfin folders from your configured SIMKL watchlist
- **Multiple list types** - Supported lists are: Plan to Watch, Currently Watching, Completed, On Hold, and Dropped lists. Note* that only Plan to Watch is actually tested with much time.
- **Automatic Separation** - This plugin will separate the SIMKL list into dedicated libraries, currently supports 4 libraries: 1 movie library, 1 TV show library, 1 anime TV show library, 1 anime movie library.
- **Stub File Support** - This plugin copies pre-generated stub video files for movies based on SIMKL runtime data, and creates folders for TV shows. This allows Jellyfin to display correct watch progress. Stub files should be placed in the "Pluginroot/STUBS/" directory and named with their runtime in minutes (e.g., "25min.mp4", "68min.mp3"). The plugin automatically selects the closest matching stub file (rounded down to the nearest minute). Movies without SIMKL runtime data automatically use a 1.5 hour fallback duration.
- **Library Scan Integration** - Optionally trigger library scans after importing. Useful to make sure the library scan happens every single time after stub files are created, highly recommend to keep enabled.


## Requirements

### 1. Custom TVDB plugin
This plugin only handles movie stubs if you want to be able to move episode stubs around it will require my modified [TVDB plugin](https://github.com/Spark-NV/jellyfin-plugin-tvdb/tree/master/compiled)

Optional but useful at least for me: [my Cloudflare DNS updater plugin](https://github.com/Spark-NV/Jellyfin.Plugin.CloudflareDNS) this plugin will allow Jellyfin to keep the Cloudflare DNS record up to date if you use Cloudflare for your domain DNS. This is for users who want a reverse DNS setup but don't want to use something like DuckDNS if you already have a Cloudflare hosted domain/DNS.

## Setup Instructions

### 1. Install the Plugin
Create a plugin directory in the SIMKL plugin folder for this plugin dll and move this plugin into that folder ex:`ProgramData\Jellyfin\Server\plugins\simkl\Jellyfin.Plugin.Simkl.dll`

### 2. Configure Libraries
Create the following libraries in Jellyfin if you don't have them already(note names of libraries can be whatever):
- **Movies Library** - For regular movies
- **TV Shows Library** - For regular TV series
- **Anime Library** - For anime TV series
- **Anime Movies Library** - For anime movies

### 3. Configure the Plugin
1. Select your user account
2. Click "Log In" and follow the authentication process
3. Configure library mappings for each content type
4. Set your preferred import list (Plan to Watch, etc.)

### 4. Import Lists
- Use the "Import Selected List" button or use the scheduled task to create folders from your SIMKL lists
- Ensure to set your scheduled task run frequency(I personally use a once a day schedule)

## NOTES

Scrobbling has been disabled. This is intentional as scrobbling could mess up the sync process with SIMKL, you are using let's say "Plan to Watch" as your watchlist and you let this plugin scrobble and mark it as watched with SIMKL, it gets removed from Plan to Watch and moves to "Completed" so we must disable scrobbling to prevent SIMKL from modifying our list at all.

Only jellyfin server version 10.11.1 has been tested.