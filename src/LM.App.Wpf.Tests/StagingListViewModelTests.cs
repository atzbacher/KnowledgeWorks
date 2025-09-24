using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using Xunit;

public class StagingListViewModelTests
{
    [Fact]
    public async Task StagePathsAsync_NormalizesInputAndStagesEachFileIndividually()
    {
        using var temp = new TempDir();
        var first = temp.CreateFile("b.pdf");
        var second = temp.CreateFile("a.pdf");
        var missing = Path.Combine(temp.Path, "missing.pdf");

        var pipeline = new RecordingPipeline();
        var viewModel = new StagingListViewModel(pipeline);

        await viewModel.StagePathsAsync(new[] { first, second, first, missing, string.Empty }, CancellationToken.None);

        Assert.Equal(2, pipeline.Calls.Count);
        Assert.All(pipeline.Calls, call => Assert.Single(call));
        Assert.Equal(new[] { second, first }, pipeline.Calls.Select(call => call.Single()));
        Assert.Equal(new[] { second, first }, viewModel.Items.Select(item => item.FilePath));
    }

    [Fact]
    public async Task StagePathsAsync_PublishesResultsInOrderEvenWhenPipelineCompletesOutOfOrder()
    {
        using var temp = new TempDir();
        var slow = temp.CreateFile("slow.pdf");
        var fast = temp.CreateFile("fast.pdf");
        var medium = temp.CreateFile("medium.pdf");

        var delays = new Dictionary<string, TimeSpan>
        {
            [slow] = TimeSpan.FromMilliseconds(120),
            [fast] = TimeSpan.FromMilliseconds(10),
            [medium] = TimeSpan.FromMilliseconds(60)
        };

        var pipeline = new DelayedPipeline(delays);
        var viewModel = new StagingListViewModel(pipeline);

        await viewModel.StagePathsAsync(new[] { slow, fast, medium }, CancellationToken.None);

        Assert.Equal(new[] { fast, medium, slow }, viewModel.Items.Select(item => item.FilePath));
    }

    private sealed class RecordingPipeline : IAddPipeline
    {
        private readonly object _gate = new();
        public List<List<string>> Calls { get; } = new();

        public Task<IReadOnlyList<StagingItem>> StagePathsAsync(IEnumerable<string> paths, CancellationToken ct)
        {
            var snapshot = paths.ToList();
            lock (_gate)
            {
                Calls.Add(snapshot);
            }

            var staged = snapshot.Select(path => new StagingItem { FilePath = path }).ToList();
            return Task.FromResult<IReadOnlyList<StagingItem>>(staged);
        }

        public Task<IReadOnlyList<StagingItem>> CommitAsync(IEnumerable<StagingItem> selectedRows, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StagingItem>>(Array.Empty<StagingItem>());
    }

    private sealed class DelayedPipeline : IAddPipeline
    {
        private readonly IReadOnlyDictionary<string, TimeSpan> _delays;

        public DelayedPipeline(IReadOnlyDictionary<string, TimeSpan> delays)
        {
            _delays = delays;
        }

        public async Task<IReadOnlyList<StagingItem>> StagePathsAsync(IEnumerable<string> paths, CancellationToken ct)
        {
            var path = paths.Single();
            if (_delays.TryGetValue(path, out var delay) && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            return new[] { new StagingItem { FilePath = path } };
        }

        public Task<IReadOnlyList<StagingItem>> CommitAsync(IEnumerable<StagingItem> selectedRows, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StagingItem>>(Array.Empty<StagingItem>());
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_staging_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateFile(string name)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, string.Empty);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
