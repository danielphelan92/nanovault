using CommunityToolkit.Mvvm.ComponentModel;
using NanoVault.Core.Models;
using NanoVault.Core.Policies;
using NanoVault.Core.Utilities;

namespace NanoVault.App.ViewModels;

/// <summary>One row in the track review list.</summary>
public sealed partial class TrackItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public TrackItemViewModel(DiscoveredTrack track)
    {
        Track = track;
    }

    public DiscoveredTrack Track { get; }

    public string Title => Track.Metadata.Title ?? Path.GetFileName(Track.SourcePath);
    public string Artist => Track.Metadata.EffectiveArtist ?? "Unknown Artist";
    public string Album => Track.Metadata.Album ?? "Unknown Album";
    public string Genre => Track.Metadata.Genre ?? string.Empty;
    public int? TrackNumber => Track.Metadata.TrackNumber;
    public string DurationText => Track.Metadata.Duration is { } d ? $"{(int)d.TotalMinutes}:{d.Seconds:00}" : "—";
    public string Format => Track.Metadata.Format ?? SupportedMedia.FormatLabel(Track.Extension);
    public string SizeText => ByteFormatter.FormatSize(Track.SizeBytes);

    public string StatusText => Track.Status switch
    {
        TrackReadStatus.Protected => "Protected — copied as-is",
        TrackReadStatus.Unreadable => "Could not be read",
        _ => "Ready",
    };

    public bool IsProtected => Track.Status == TrackReadStatus.Protected;
    public bool IsUnreadable => Track.Status == TrackReadStatus.Unreadable;

    /// <summary>Case-insensitive haystack for the search box.</summary>
    public string SearchText { get; init; } = string.Empty;

    public static TrackItemViewModel Create(DiscoveredTrack track) => new(track)
    {
        SearchText = string.Join(' ',
            track.Metadata.Title, track.Metadata.Artist, track.Metadata.AlbumArtist,
            track.Metadata.Album, track.Metadata.Genre, Path.GetFileName(track.SourcePath))
            .ToLowerInvariant(),
    };
}
