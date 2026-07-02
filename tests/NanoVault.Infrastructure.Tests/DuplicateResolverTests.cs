using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.Backup;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Verification;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class DuplicateResolverTests : IDisposable
{
    private readonly TempWorkspace _source = new();
    private readonly TempWorkspace _destination = new();
    private readonly DuplicateResolver _resolver;

    public DuplicateResolverTests()
    {
        var fs = new PhysicalFileSystem();
        _resolver = new DuplicateResolver(fs, new Sha256FileVerificationService(fs), NullLogger<DuplicateResolver>.Instance);
    }

    public void Dispose()
    {
        _source.Dispose();
        _destination.Dispose();
    }

    private DiscoveredTrack SourceTrack(string content)
    {
        var path = _source.WriteFile($"F00/{Guid.NewGuid():N}.mp3", content);
        return Tracks.FromFile(_source.Root, path);
    }

    [Fact]
    public async Task No_conflict_when_destination_is_empty()
    {
        var decision = await _resolver.ResolveAsync(
            SourceTrack("abc"), _destination.Root, "Artist/song.mp3".Replace('/', Path.DirectorySeparatorChar),
            DuplicateBehavior.SkipExactDuplicates);

        Assert.Equal(DuplicateOutcome.NoConflict, decision.Outcome);
    }

    [Fact]
    public async Task Identical_content_is_an_exact_duplicate_under_skip_policy()
    {
        var track = SourceTrack("identical bytes");
        _destination.WriteFile("song.mp3", "identical bytes");

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.SkipExactDuplicates);

        Assert.Equal(DuplicateOutcome.ExactDuplicate, decision.Outcome);
    }

    [Fact]
    public async Task Same_size_different_content_keeps_both()
    {
        var track = SourceTrack("AAAABBBB");
        _destination.WriteFile("song.mp3", "AAAACCCC"); // same length, different hash

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.SkipExactDuplicates);

        Assert.Equal(DuplicateOutcome.UseAlternateName, decision.Outcome);
        Assert.Equal("song (2).mp3", decision.AlternateRelativePath);
    }

    [Fact]
    public async Task Different_size_short_circuits_without_hashing()
    {
        var track = SourceTrack("short");
        _destination.WriteFile("song.mp3", "much longer content here");

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.SkipExactDuplicates);

        Assert.Equal(DuplicateOutcome.UseAlternateName, decision.Outcome);
    }

    [Fact]
    public async Task Keep_both_policy_renames_even_exact_duplicates()
    {
        var track = SourceTrack("same");
        _destination.WriteFile("song.mp3", "same");

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.KeepBoth);

        Assert.Equal(DuplicateOutcome.UseAlternateName, decision.Outcome);
    }

    [Fact]
    public async Task Alternate_names_count_upward_past_existing_copies()
    {
        var track = SourceTrack("new content");
        _destination.WriteFile("song.mp3", "original");
        _destination.WriteFile("song (2).mp3", "second");
        _destination.WriteFile("song (3).mp3", "third");

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.SkipExactDuplicates);

        Assert.Equal("song (4).mp3", decision.AlternateRelativePath);
    }

    [Fact]
    public async Task Replace_policy_replaces_only_when_user_chose_it()
    {
        var track = SourceTrack("new");
        _destination.WriteFile("song.mp3", "old");

        var decision = await _resolver.ResolveAsync(
            track, _destination.Root, "song.mp3", DuplicateBehavior.ReplaceDestination);

        Assert.Equal(DuplicateOutcome.Replace, decision.Outcome);
    }
}
