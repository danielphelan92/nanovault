using NanoVault.App.ViewModels;
using Xunit;

namespace NanoVault.App.Tests;

public class TrackListViewModelTests
{
    private static TrackListViewModel Loaded()
    {
        var device = TestData.Device();
        var vm = new TrackListViewModel();
        vm.Load(TestData.Scan(device,
            TestData.Track("A.mp3", "Alpha", "Abba", "Pop"),
            TestData.Track("B.mp3", "Beta", "Beethoven", "Classical"),
            TestData.Track("C.mp3", "Gamma", "Abba", "Pop")));
        return vm;
    }

    [Fact]
    public void Everything_is_selected_by_default()
    {
        var vm = Loaded();
        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal(3, vm.GetSelectedTracks().Count);
    }

    [Fact]
    public void Artist_filter_narrows_the_visible_list_only()
    {
        var vm = Loaded();
        vm.SelectedArtist = "Abba";

        Assert.Equal(2, vm.VisibleTracks.Count);
        Assert.Equal(3, vm.GetSelectedTracks().Count); // selection unaffected
    }

    [Fact]
    public void Search_matches_title_artist_and_filename()
    {
        var vm = Loaded();

        vm.SearchText = "beet";
        Assert.Single(vm.VisibleTracks);
        Assert.Equal("Beta", vm.VisibleTracks[0].Title);

        vm.SearchText = "a.mp3";
        Assert.Single(vm.VisibleTracks);
    }

    [Fact]
    public void Deselect_visible_only_touches_the_filtered_rows()
    {
        var vm = Loaded();
        vm.SelectedGenre = "Pop";
        vm.DeselectAllVisibleCommand.Execute(null);

        Assert.Equal(1, vm.SelectedCount);
        var remaining = Assert.Single(vm.GetSelectedTracks());
        Assert.Equal("Beta", remaining.Metadata.Title);

        vm.SelectAllVisibleCommand.Execute(null);
        Assert.Equal(3, vm.SelectedCount);
    }

    [Fact]
    public void Filter_lists_are_sorted_and_deduplicated()
    {
        var vm = Loaded();

        Assert.Equal([TrackListViewModel.AllFilter, "Abba", "Beethoven"], vm.Artists);
        Assert.Equal([TrackListViewModel.AllFilter, "Classical", "Pop"], vm.Genres);
    }

    [Fact]
    public void Reload_resets_filters_and_selection()
    {
        var vm = Loaded();
        vm.SelectedArtist = "Abba";
        vm.SearchText = "alpha";

        vm.Load(TestData.Scan(TestData.Device(), TestData.Track("Z.mp3", "Zed", "Zz Top")));

        Assert.Equal(TrackListViewModel.AllFilter, vm.SelectedArtist);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(1, vm.TotalCount);
        Assert.Equal(1, vm.SelectedCount);
    }
}
