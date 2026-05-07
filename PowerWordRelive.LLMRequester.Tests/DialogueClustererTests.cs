using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Models;
using Xunit;

namespace PowerWordRelive.LLMRequester.Tests;

public class DialogueClustererTests
{
    [Fact]
    public void BuildClusters_EmptyList_ReturnsEmpty()
    {
        var clusters = DialogueClusterer.BuildClusters(new List<long>(), 2);
        Assert.Empty(clusters);
    }

    [Fact]
    public void BuildClusters_SingleId_ReturnsOneCluster()
    {
        var clusters = DialogueClusterer.BuildClusters([42], 2);
        Assert.Single(clusters);
        Assert.Equal([42], clusters[0]);
    }

    [Fact]
    public void BuildClusters_ConsecutiveIds_OneCluster()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 2, 3, 4, 5], 2);
        Assert.Single(clusters);
        Assert.Equal(5, clusters[0].Count);
    }

    [Fact]
    public void BuildClusters_GapExceedsWindow_SeparateClusters()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 2, 10, 11], 2);
        Assert.Equal(2, clusters.Count);
        Assert.Equal([1, 2], clusters[0]);
        Assert.Equal([10, 11], clusters[1]);
    }

    [Fact]
    public void BuildClusters_GapEqualsDoubleWindow_OneCluster()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 5], 2);
        Assert.Single(clusters);
    }

    [Fact]
    public void BuildClusters_GapExceedsDoubleWindow_NewCluster()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 6], 2);
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void BuildClusters_GapWithinDoubleWindow_OneCluster()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 2, 3, 4], 2);
        Assert.Single(clusters);
        Assert.Equal([1, 2, 3, 4], clusters[0]);
    }

    [Fact]
    public void BuildClusters_ThreeSegments_ThreeClusters()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 2, 10, 11, 20, 21], 2);
        Assert.Equal(3, clusters.Count);
        Assert.Equal([1, 2], clusters[0]);
        Assert.Equal([10, 11], clusters[1]);
        Assert.Equal([20, 21], clusters[2]);
    }

    [Fact]
    public void BuildClusters_WindowZero_EveryGapSplits()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 2, 3], 0);
        Assert.Equal(3, clusters.Count);
    }

    [Fact]
    public void BuildClusters_WindowZero_ContinuousIdsStillSplit()
    {
        var clusters = DialogueClusterer.BuildClusters([5, 6], 0);
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void BuildClusters_LargeWindow_MergesAll()
    {
        var clusters = DialogueClusterer.BuildClusters([1, 20, 50], 50);
        Assert.Single(clusters);
        Assert.Equal(3, clusters[0].Count);
    }

    [Fact]
    public void BuildClusters_SkipWithinWindow_Clusters()
    {
        var clusters = DialogueClusterer.BuildClusters([5, 6, 8, 9, 15, 16, 17, 20], 2);
        Assert.Equal(2, clusters.Count);
        Assert.Equal([5, 6, 8, 9], clusters[0]);
        Assert.Equal([15, 16, 17, 20], clusters[1]);
    }

    [Fact]
    public void BuildClusters_UserScenario_MixedGaps()
    {
        var ids = new List<long> { 5, 8, 9, 15, 20 };
        var clusters = DialogueClusterer.BuildClusters(ids, 2);
        Assert.Equal(3, clusters.Count);
        Assert.Equal([5, 8, 9], clusters[0]);
        Assert.Equal([15], clusters[1]);
        Assert.Equal([20], clusters[2]);
    }

    [Fact]
    public void BuildClusters_LargeIdList_Performance()
    {
        var ids = Enumerable.Range(1, 10000).Select(i => (long)i).ToList();
        var clusters = DialogueClusterer.BuildClusters(ids, 2);
        Assert.Single(clusters);
        Assert.Equal(10000, clusters[0].Count);
    }
}

public class FormatContextTests
{
    private static readonly Dictionary<string, string> NameMap = new()
    {
        ["speaker_01"] = "DM",
        ["speaker_02"] = "卡尔",
        ["speaker_03"] = "__UNASSIGNED__",
        ["speaker_04"] = "__UNKNOWN__"
    };

    [Fact]
    public void FormatContext_TargetSpeaker_GetsArrowMarker()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_01", "欢迎来到酒馆")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);
        Assert.StartsWith(">>>", result);
    }

    [Fact]
    public void FormatContext_ContextSpeaker_GetsIndent()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_02", "我今天带来了新货")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);
        Assert.StartsWith("   ", result);
    }

    [Fact]
    public void FormatContext_KnownName_ShowsResolved()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_01", "text")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_02", NameMap);
        Assert.Contains("[DM]", result);
    }

    [Fact]
    public void FormatContext_Unassigned_ShowsRawId()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_03", "text")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);
        Assert.Contains("[speaker_03]", result);
        Assert.DoesNotContain("__UNASSIGNED__", result);
    }

    [Fact]
    public void FormatContext_Unknown_ShowsRawId()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_04", "text")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);
        Assert.Contains("[speaker_04]", result);
        Assert.DoesNotContain("__UNKNOWN__", result);
    }

    [Fact]
    public void FormatContext_MixedSpeakers_ProperMarkers()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_01", "谁想接这个任务？"),
            new(2, "speaker_02", "我来！"),
            new(3, "speaker_01", "很好，这是报酬"),
            new(4, "speaker_03", "算我一个")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);

        var lines = result.Split('\n');
        Assert.StartsWith(">>> [DM]:", lines[0]);
        Assert.StartsWith("    [卡尔]:", lines[1]);
        Assert.StartsWith(">>> [DM]:", lines[2]);
        Assert.StartsWith("    [speaker_03]:", lines[3]);
    }

    [Fact]
    public void FormatContext_EmptyEntries_ReturnsEmpty()
    {
        var entries = new List<DialogueEntry>();
        var result = DialogueClusterer.FormatContext(entries, "speaker_01", NameMap);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatContext_SpeakerNotInMap_ShowsRawId()
    {
        var entries = new List<DialogueEntry>
        {
            new(1, "speaker_99", "你好")
        };

        var result = DialogueClusterer.FormatContext(entries, "speaker_01", new Dictionary<string, string>());
        Assert.Contains("[speaker_99]", result);
    }
}