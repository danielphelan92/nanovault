using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NanoVault.Core.Models;

namespace NanoVault.App.ViewModels;

/// <summary>
/// Optional review screen: filter by artist, album, genre, playlist, or
/// search text, and choose which tracks to back up. Everything is selected
/// by default. The list itself is virtualised by the view.
/// </summary>
public sealed partial class TrackListViewModel : ObservableObject
{
    public const string AllFilter = "All";

    private List<TrackItemViewModel> _allTracks = new();
    private IReadOnlyList<IpodPlaylist> _playlists = Array.Empty<IpodPlaylist>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedArtist = AllFilter;

    [ObservableProperty]
    private string _selectedAlbum = AllFilter;

    [ObservableProperty]
    private string _selectedGenre = AllFilter;

    [ObservableProperty]
    private string _selectedPlaylist = AllFilter;

    [ObservableProperty]
    private int _selectedCount;

    public ObservableCollection<TrackItemViewModel> VisibleTracks { get; } = new();
    public ObservableCollection<string> Artists { get; } = new();
    public ObservableCollection<string> Albums { get; } = new();
    public ObservableCollection<string> Genres { get; } = new();
    public ObservableCollection<string> Playlists { get; } = new();

    public int TotalCount => _allTracks.Count;

    public void Load(ScanResult scan)
    {
        foreach (var old in _allTracks)
        {
            old.PropertyChanged -= OnTrackPropertyChanged;
        }

        _allTracks = scan.Tracks.Select(TrackItemViewModel.Create).ToList();
        _playlists = scan.Playlists;

        foreach (var track in _allTracks)
        {
            track.PropertyChanged += OnTrackPropertyChanged;
        }

        LoadFilterValues();
        SearchText = string.Empty;
        SelectedArtist = AllFilter;
        SelectedAlbum = AllFilter;
        SelectedGenre = AllFilter;
        SelectedPlaylist = AllFilter;

        ApplyFilter();
        UpdateSelectedCount();
        OnPropertyChanged(nameof(TotalCount));
    }

    /// <summary>Tracks currently ticked, regardless of the active filter.</summary>
    public IReadOnlyList<DiscoveredTrack> GetSelectedTracks() =>
        _allTracks.Where(t => t.IsSelected).Select(t => t.Track).ToList();

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var track in VisibleTracks)
        {
            track.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAllVisible()
    {
        foreach (var track in VisibleTracks)
        {
            track.IsSelected = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedArtistChanged(string value) => ApplyFilter();
    partial void OnSelectedAlbumChanged(string value) => ApplyFilter();
    partial void OnSelectedGenreChanged(string value) => ApplyFilter();
    partial void OnSelectedPlaylistChanged(string value) => ApplyFilter();

    private void OnTrackPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackItemViewModel.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount() => SelectedCount = _allTracks.Count(t => t.IsSelected);

    private void LoadFilterValues()
    {
        LoadValues(Artists, _allTracks.Select(t => t.Artist));
        LoadValues(Albums, _allTracks.Select(t => t.Album));
        LoadValues(Genres, _allTracks.Select(t => t.Genre).Where(g => g.Length > 0));
        LoadValues(Playlists, _playlists.Select(p => p.Name));
    }

    private static void LoadValues(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        target.Add(AllFilter);
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            target.Add(value);
        }
    }

    private void ApplyFilter()
    {
        HashSet<string>? playlistMembers = null;
        if (SelectedPlaylist != AllFilter)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Name == SelectedPlaylist);
            playlistMembers = playlist is null
                ? new HashSet<string>()
                : playlist.TrackRelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var search = SearchText.Trim().ToLowerInvariant();

        VisibleTracks.Clear();
        foreach (var track in _allTracks)
        {
            if (SelectedArtist != AllFilter && !string.Equals(track.Artist, SelectedArtist, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SelectedAlbum != AllFilter && !string.Equals(track.Album, SelectedAlbum, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SelectedGenre != AllFilter && !string.Equals(track.Genre, SelectedGenre, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (playlistMembers is not null && !playlistMembers.Contains(track.Track.RelativeSourcePath))
            {
                continue;
            }

            if (search.Length > 0 && !track.SearchText.Contains(search, StringComparison.Ordinal))
            {
                continue;
            }

            VisibleTracks.Add(track);
        }
    }
}
