using System.Text;
using System.Text.Json;
using Moq;
using PersonalApi.Projects.EkaterinaPotapovaDesign.Features;
using PersonalApi.Projects.EkaterinaPotapovaDesign;
using PersonalApi.Storage;
using Xunit;

namespace PersonalApi.Tests.Projects.EkaterinaPotapovaDesign;

public class EkaterinaProjectsFeatureTests
{
    #region Helpers

    private static Mock<IR2Service> MockR2() => new Mock<IR2Service>();

    private static string Serialize<T>(T obj) =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    private static ProjectSummary MakeSummary(int id = 1) => new(
        Id: id,
        Title: $"Project {id}",
        Subtitle: $"Subtitle {id}",
        Cover: $"ekaterinaDesign/projects/{id}/cover.webp",
        Tools: new List<string> { "blender", "after-effects" },
        Visible: true
    );

    private static string MakeIndexJson(params ProjectSummary[] projects) =>
        Serialize(new ProjectIndex(projects.ToList()));

    #endregion

    #region ResolveToolUrls

    [Fact]
    public void ResolveToolUrls_MapsToolKeysToPublicUrls()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(key => $"https://cdn.example.com/{key}");

        var summary = MakeSummary() with { Tools = new List<string> { "blender", "after-effects" } };

        var result = ProjectsFeature.ResolveToolUrls(r2.Object, summary);

        Assert.Equal(2, result.Tools.Count);
        Assert.Contains("blender.png", result.Tools[0]);
        Assert.Contains("after-effects.png", result.Tools[1]);
        Assert.All<string>(result.Tools, item => Assert.StartsWith("https://cdn.example.com/", item));
    }

    [Fact]
    public void ResolveToolUrls_WithNullTools_ReturnsEmptyList()
    {
        var r2 = MockR2();
        var summary = MakeSummary() with { Tools = null! };

        var result = ProjectsFeature.ResolveToolUrls(r2.Object, summary);

        Assert.NotNull(result.Tools);
        Assert.Empty(result.Tools);
    }

    [Fact]
    public void ResolveToolUrls_RemovesDuplicateTools()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(key => $"https://cdn.example.com/{key}");

        var summary = MakeSummary() with { Tools = new List<string> { "blender", "blender" } };

        var result = ProjectsFeature.ResolveToolUrls(r2.Object, summary);

        Assert.Single(result.Tools);
    }

    #endregion

    #region MergeIndexEntry

    [Fact]
    public async Task MergeIndexEntry_WhenIndexEmpty_ReturnsJsonWithOneEntry()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var incoming = MakeSummary(id: 1);
        var json = await ProjectsFeature.MergeIndexEntry(r2.Object, incoming);

        var result = JsonSerializer.Deserialize<ProjectIndex>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Single(result!.Projects);
        Assert.Equal(1, result.Projects[0].Id);
    }

    [Fact]
    public async Task MergeIndexEntry_WhenProjectExists_ReplacesIt()
    {
        var r2 = MockR2();
        var existing = MakeSummary(id: 1) with { Title = "Old Title" };
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeIndexJson(existing));

        var updated = MakeSummary(id: 1) with { Title = "New Title" };
        var json = await ProjectsFeature.MergeIndexEntry(r2.Object, updated);

        var result = JsonSerializer.Deserialize<ProjectIndex>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Single(result!.Projects);
        Assert.Equal("New Title", result.Projects[0].Title);
    }

    [Fact]
    public async Task MergeIndexEntry_WhenOtherProjectsExist_KeepsThem()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeIndexJson(MakeSummary(1), MakeSummary(2)));

        var incoming = MakeSummary(id: 3);
        var json = await ProjectsFeature.MergeIndexEntry(r2.Object, incoming);

        var result = JsonSerializer.Deserialize<ProjectIndex>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(3, result!.Projects.Count);
    }

    #endregion

    #region VersionFileIfExists

    [Fact]
    public async Task VersionFileIfExists_WhenFileDoesNotExist_DoesNothing()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Should not throw, should not call PutFileAsync
        await ProjectsFeature.VersionFileIfExists(r2.Object, "some/file.json");

        r2.Verify(x => x.PutFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenFileExists_ArchivesToSlot001()
    {
        var r2 = MockR2();

        // Original exists, slot 001 does not
        r2.Setup(x => x.GetTextAsync("ekaterinaDesign/projects/1/project.json"))
            .ReturnsAsync("original content");
        r2.Setup(x => x.GetTextAsync("ekaterinaDesign/projects/1/project-old-001.json"))
            .ReturnsAsync((string?)null);

        string? archivedKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => archivedKey = key)
            .Returns(Task.CompletedTask);

        await ProjectsFeature.VersionFileIfExists(r2.Object, "ekaterinaDesign/projects/1/project.json");

        Assert.Equal("ekaterinaDesign/projects/1/project-old-001.json", archivedKey);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenSlot001Taken_UsesSlot002()
    {
        var r2 = MockR2();

        r2.Setup(x => x.GetTextAsync("ekaterinaDesign/projects/1/project.json"))
            .ReturnsAsync("original content");
        r2.Setup(x => x.GetTextAsync("ekaterinaDesign/projects/1/project-old-001.json"))
            .ReturnsAsync("already archived");
        r2.Setup(x => x.GetTextAsync("ekaterinaDesign/projects/1/project-old-002.json"))
            .ReturnsAsync((string?)null);

        string? archivedKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => archivedKey = key)
            .Returns(Task.CompletedTask);

        await ProjectsFeature.VersionFileIfExists(r2.Object, "ekaterinaDesign/projects/1/project.json");

        Assert.Equal("ekaterinaDesign/projects/1/project-old-002.json", archivedKey);
    }
    #endregion

    #region PublishPagePody

    [Fact]
    public async Task PublishPageBody_WritesJsonToCorrectKey()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null); // no existing file

        string? writtenKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => writtenKey = key)
            .Returns(Task.CompletedTask);

        var body = JsonDocument.Parse(@"{""blocks"":[]}").RootElement;
        await ProjectsFeature.PublishPageBody(r2.Object, 1, body);

        Assert.Equal("ekaterinaDesign/projects/1/project.json", writtenKey);
    }

    #endregion
}