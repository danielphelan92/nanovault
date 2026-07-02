using NanoVault.Core.Models;

namespace NanoVault.Core.Policies;

/// <summary>
/// Builds the destination-relative path for a track from its merged metadata
/// and the chosen organisation template. Pure and deterministic, so plans are
/// reproducible and easy to test.
/// </summary>
public static class OutputPathBuilder
{
    public const string UnknownArtist = "Unknown Artist";
    public const string UnknownAlbum = "Unknown Album";
    public const string UnknownTrackPrefix = "Unknown Track";
    public const string AllMusicFolder = "All Music";

    /// <summary>Practical ceiling for generated relative paths.</summary>
    public const int MaxRelativePathLength = 180;

    public static string BuildRelativePath(DiscoveredTrack track, BackupOptions options)
    {
        var metadata = track.Metadata;
        var extension = PathSanitizer.NormalizeExtension(track.Extension);
        var originalStem = Path.GetFileNameWithoutExtension(track.SourcePath);

        string[] folders;
        string fileName;

        if (options.Template == OrganizationTemplate.FlatAllMusic)
        {
            folders = new[] { AllMusicFolder };
            fileName = metadata.HasTitle
                ? PathSanitizer.SanitizeFileName(
                    $"{FirstNonEmpty(metadata.EffectiveArtist, UnknownArtist)} - {metadata.Title}",
                    extension,
                    FallbackStem(originalStem))
                : PathSanitizer.SanitizeFileName(null, extension, FallbackStem(originalStem));
        }
        else
        {
            var artistFolder = options.Template == OrganizationTemplate.AlbumArtistYearAlbum
                ? PathSanitizer.SanitizeComponent(metadata.EffectiveAlbumArtist, UnknownArtist)
                : PathSanitizer.SanitizeComponent(metadata.EffectiveArtist, UnknownArtist);

            var albumName = PathSanitizer.SanitizeComponent(metadata.Album, UnknownAlbum);
            var albumFolder = options.Template == OrganizationTemplate.AlbumArtistYearAlbum && metadata.Year is int year and > 0
                ? PathSanitizer.SanitizeComponent($"{year} - {metadata.Album ?? UnknownAlbum}", albumName)
                : albumName;

            var isMultiDisc = (metadata.DiscCount ?? 0) > 1 || (metadata.DiscNumber ?? 0) > 1;
            var discNumber = Math.Max(1, metadata.DiscNumber ?? 1);

            if (isMultiDisc && options.UseDiscSubfolders)
            {
                folders = new[] { artistFolder, albumFolder, $"Disc {discNumber}" };
            }
            else
            {
                folders = new[] { artistFolder, albumFolder };
            }

            var discPrefix = isMultiDisc && !options.UseDiscSubfolders ? $"{discNumber}-" : string.Empty;
            var numberPrefix = metadata.TrackNumber is int n and > 0 ? $"{discPrefix}{n:00} - " : discPrefix;

            fileName = metadata.HasTitle
                ? PathSanitizer.SanitizeFileName($"{numberPrefix}{metadata.Title}", extension, FallbackStem(originalStem))
                : PathSanitizer.SanitizeFileName(null, extension, FallbackStem(originalStem));
        }

        return ShortenIfNeeded(folders, fileName, extension, MaxRelativePathLength);
    }

    private static string FallbackStem(string originalStem)
    {
        var safeOriginal = PathSanitizer.SanitizeComponent(originalStem, "Track", 40);
        return $"{UnknownTrackPrefix} - {safeOriginal}";
    }

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>
    /// Keeps the whole relative path within budget by shrinking the largest
    /// components first, preserving the extension and track prefix readability.
    /// </summary>
    private static string ShortenIfNeeded(string[] folders, string fileName, string extension, int maxLength)
    {
        var path = Combine(folders, fileName);
        if (path.Length <= maxLength)
        {
            return path;
        }

        // First shrink folder components to at most 48 characters each.
        for (var i = 0; i < folders.Length && path.Length > maxLength; i++)
        {
            if (folders[i].Length > 48)
            {
                folders[i] = PathSanitizer.SanitizeComponent(folders[i], folders[i][..8], 48);
                path = Combine(folders, fileName);
            }
        }

        // Then shrink the file stem to whatever room remains.
        if (path.Length > maxLength)
        {
            var foldersLength = path.Length - fileName.Length;
            var room = Math.Max(12 + extension.Length, maxLength - foldersLength);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            fileName = PathSanitizer.SanitizeFileName(stem, extension, "Track", room);
            path = Combine(folders, fileName);
        }

        return path;
    }

    private static string Combine(string[] folders, string fileName)
    {
        var parts = new string[folders.Length + 1];
        Array.Copy(folders, parts, folders.Length);
        parts[^1] = fileName;
        return string.Join(Path.DirectorySeparatorChar, parts);
    }
}
