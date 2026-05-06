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
        await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/file.json");

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
        r2.Setup(x => x.GetTextAsync("/path/project.json"))
            .ReturnsAsync("original content");
        r2.Setup(x => x.GetTextAsync("/path/project-old-001.json"))
            .ReturnsAsync((string?)null);

        string? archivedKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => archivedKey = key)
            .Returns(Task.CompletedTask);

        await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/project.json");

        Assert.Equal("/path/project-old-001.json", archivedKey);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenSlot001Taken_UsesSlot002()
    {
        var r2 = MockR2();

        r2.Setup(x => x.GetTextAsync("/path/project.json"))
            .ReturnsAsync("original content");
        r2.Setup(x => x.GetTextAsync("/path/project-old-001.json"))
            .ReturnsAsync("already archived");
        r2.Setup(x => x.GetTextAsync("/path/project-old-002.json"))
            .ReturnsAsync((string?)null);

        string? archivedKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => archivedKey = key)
            .Returns(Task.CompletedTask);

        await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/project.json");

        Assert.Equal("/path/project-old-002.json", archivedKey);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenIncomingContentMatchesExisting_DoesNotArchive()
    {
        var r2 = MockR2();
        const string sameContent = "{\"projects\":[]}";

        r2.Setup(x => x.GetTextAsync("/path/index.json"))
            .ReturnsAsync(sameContent);

        await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/index.json", sameContent);

        r2.Verify(x => x.PutFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenFileExists_ReturnsExistingContent()
    {
        var r2 = MockR2();
        const string content = "{\"projects\":[]}";

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => path.Contains("-old-") ? null : content);

        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/index.json");

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenFileDoesNotExist_ReturnsNull()
    {
        var r2 = MockR2();

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var result = await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/index.json");

        Assert.Null(result);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenIncomingContentMatches_ReturnsExistingContent()
    {
        var r2 = MockR2();
        const string content = "{\"projects\":[]}";

        r2.Setup(x => x.GetTextAsync("/path/index.json"))
            .ReturnsAsync(content);

        var result = await ProjectsFeature.VersionFileIfExists(r2.Object, "/path/index.json", content);

        Assert.Equal(content, result);
        r2.Verify(x => x.PutFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VersionFileIfExists_WhenAllSlotsAreTaken_DoesNotInfiniteLoop()
    {
        var r2 = MockR2();

        // Every path returns content, including all -old-XXX slots
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync("some content");

        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Should terminate at count = 999 and not throw
        var ex = await Record.ExceptionAsync(() =>
            ProjectsFeature.VersionFileIfExists(r2.Object, "/path/index.json"));

        Assert.Null(ex);
        // Confirms exactly 999 slot reads were attempted (1 original + 999 slots)
        r2.Verify(x => x.GetTextAsync(It.IsAny<string>()), Times.Exactly(1000));
    }

    #endregion

    #region PublishPagePody

    [Fact]
    public async Task PublishPageBody_WritesJsonToCorrectKey()
    {
        var r2 = MockR2();
        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        string? writtenKey = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => writtenKey = key)
            .Returns(Task.CompletedTask);

        var body = JsonDocument.Parse(@"{""blocks"":[]}").RootElement;
        await ProjectsFeature.PublishPageBody(r2.Object, 1, body);

        Assert.Equal("ekaterinaDesign/projects/1/project.json", writtenKey);
    }

    #endregion

    #region PublishIndex

    [Fact]
    public async Task PublishIndex_WhenIndexUnchanged_DoesNotUpload()
    {
        var r2 = MockR2();

        var existing = MakeSummary(id: 1);
        var existingIndexJson = MakeIndexJson(existing);

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync(existingIndexJson);

        var incoming = JsonDocument.Parse(Serialize(existing)).RootElement;
        await ProjectsFeature.PublishIndex(r2.Object, incoming);

        r2.Verify(x => x.PutFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PublishIndex_WhenIndexChanged_UploadsNewContent()
    {
        var r2 = MockR2();

        var existing = MakeSummary(id: 1) with { Title = "Old Title" };
        var updated = MakeSummary(id: 1) with { Title = "New Title" };

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeIndexJson(existing));

        string? uploadedContent = null;
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((_, stream, __) =>
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                uploadedContent = reader.ReadToEnd();
            })
            .Returns(Task.CompletedTask);

        var incoming = JsonDocument.Parse(Serialize(updated)).RootElement;
        await ProjectsFeature.PublishIndex(r2.Object, incoming);

        Assert.NotNull(uploadedContent);
        var result = JsonSerializer.Deserialize<ProjectIndex>(uploadedContent!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Single(result!.Projects);
        Assert.Equal("New Title", result.Projects[0].Title);
    }

    [Fact]
    public async Task PublishIndex_WhenIndexChanged_ArchivesOldFile()
    {
        var r2 = MockR2();

        var existing = MakeSummary(id: 1) with { Title = "Old Title" };
        var updated = MakeSummary(id: 1) with { Title = "New Title" };
        var existingJson = MakeIndexJson(existing);

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => path.Contains("-old-") ? null : existingJson);

        var uploadedKeys = new List<string>();
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => uploadedKeys.Add(key))
            .Returns(Task.CompletedTask);

        var incoming = JsonDocument.Parse(Serialize(updated)).RootElement;
        await ProjectsFeature.PublishIndex(r2.Object, incoming);

        Assert.Contains(uploadedKeys, k => k.Contains("-old-001"));
    }

    [Fact]
    public async Task PublishIndex_WhenIndexChangedAndOldExisting_ArchivesOldFile()
    {
        var r2 = MockR2();

        var archived = MakeSummary(id: 1) with { Title = "Old Old Title" };
        var existing = MakeSummary(id: 1) with { Title = "Old Title" };
        var updated = MakeSummary(id: 1) with { Title = "New Title" };
        var archivedJson = MakeIndexJson(archived);
        var existingJson = MakeIndexJson(existing);

        r2.Setup(x => x.GetTextAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => path.Contains("-old-") ? 
            (path.Contains("-old-001") ? archivedJson : null) : 
            existingJson);

        var uploadedKeys = new List<string>();
        r2.Setup(x => x.PutFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((key, _, __) => uploadedKeys.Add(key))
            .Returns(Task.CompletedTask);

        var incoming = JsonDocument.Parse(Serialize(updated)).RootElement;
        await ProjectsFeature.PublishIndex(r2.Object, incoming);

        //Should leave 001 alone
        Assert.DoesNotContain(uploadedKeys, k => k.Contains("-old-001"));
        Assert.Contains(uploadedKeys, k => k.Contains("-old-002"));
        Assert.Contains(uploadedKeys, k => !k.Contains("-old-"));
    }

    [Fact]
    public async Task PublishIndex_WhenDeserializationFails_ReturnsFalse()
    {
        var r2 = MockR2();

        var invalid = JsonDocument.Parse(@"""not-a-project-summary""").RootElement;
        var result = await ProjectsFeature.PublishIndex(r2.Object, invalid);

        Assert.False(result);
    }

    [Fact]
    public async Task PublishIndex_WhenDeserializationFails_NeverUploads()
    {
        var r2 = MockR2();

        var invalid = JsonDocument.Parse(@"""not-a-project-summary""").RootElement;
        await ProjectsFeature.PublishIndex(r2.Object, invalid);

        r2.Verify(x => x.PutFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Never);
    }

    #endregion
}