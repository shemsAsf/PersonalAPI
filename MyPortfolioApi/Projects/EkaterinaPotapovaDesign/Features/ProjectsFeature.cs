using PersonalApi.Storage;
using System.Text;
using System.Text.Json;

namespace PersonalApi.Projects.EkaterinaPotapovaDesign.Features
{
    public class ProjectsFeature
    {
        private const string ProjectBucketPath = "ekaterinaDesign";
        private const string IndexPath = ProjectBucketPath + "/projects/index.json";
        private static string ProjectPath(int id) => $"{ProjectBucketPath}/projects/{id}/project.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static void Map(RouteGroupBuilder group, string scheme)
        {

            group.MapGet("/projects", async (R2Service r2) =>
            {
                var json = await r2.GetTextAsync(IndexPath);

                if (json is null)
                    return Results.NotFound(new { message = "Project index not found" });

                var index = JsonSerializer.Deserialize<ProjectIndex>(json, JsonOptions);

                if (index is null)
                    return Results.Problem("Failed to parse project index");

                var projects = index.Projects
                    .Where(p => p.Visible)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new ProjectSummary(
                        Id: p.Id,
                        Title: p.Title,
                        Subtitle: p.Subtitle,
                        Cover: r2.GetPublicUrl(p.Cover),
                        Tools: p.Tools.Select(r2.GetPublicUrl).ToList(),
                        Visible: p.Visible
                    ))
                    .ToList();

                return Results.Ok(new { projects });
            });

            group.MapGet("/projects/{id}", async (int id, R2Service r2) =>
            {
                var json = await r2.GetTextAsync(ProjectPath(id));

                if (json is null)
                    return Results.NotFound(new { message = "Project index not found" });

                return Results.Content(json, "application/json");
            });

            group.MapPut("/projects/{id}", async (HttpRequest req, int id, R2Service r2) =>
            {
                using var reader = new StreamReader(req.Body);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(json))
                    return Results.BadRequest("Empty body");

                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("body", out var bodyElement) ||
                    !doc.RootElement.TryGetProperty("index", out var indexElement))
                {
                    return Results.BadRequest("Missing body or index");
                }

                await PublishPageBody(r2, id, bodyElement);

                bool saved = await PublishIndex(r2, indexElement);
                if (!saved) return Results.BadRequest("Invalid index format");

                return Results.Ok();
            }).RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(scheme)
                .RequireAuthenticatedUser());

            group.MapGet("/projects-tools", async (R2Service r2) =>
            {
                var urls = r2.GetAllPublicUrl("ekaterinaDesign/globals/tools/");
                return Results.Ok(new { urls });
            });

            group.MapPost("/projects-tools", async (HttpRequest req, R2Service r2) =>
            {
                var (file, error) = FormFileHelper.GetFile(req);
                if (error is not null) return error;

                var url = await r2.PutFormFileAsync(file!, "ekaterinaDesign/globals/tools");

                return Results.Ok(new { url });
            }).RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(scheme)
                    .RequireAuthenticatedUser());

            group.MapGet("/projects-pigeons", async (R2Service r2) =>
            {
                var urls = r2.GetAllPublicUrl("ekaterinaDesign/globals/pigeons/");
                return Results.Ok(new { urls });
            });

            group.MapPost("/projects/{id}/images", async(HttpRequest req, int id, R2Service r2) =>
            {
                var (file, error) = FormFileHelper.GetFile(req);
                if (error is not null) return error;

                var key = await r2.PutFormFileAsync(file!, $"ekaterinaDesign/projects/{id}/images");
                string url = r2.GetPublicUrl(key);

                return Results.Ok(new { url });
            }).RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(scheme)
                    .RequireAuthenticatedUser());
        }

        /// <summary>
        /// Archive the already existing file at <paramref name="path"/> 
        /// save it as <c>file-old-00x.ext</c> to allow rollbacks
        /// </summary>
        /// <param name="r2"></param>
        /// <param name="path"></param>
        /// <param name="incomingContent"></param>
        /// <returns>The existing file content if the file was found in the bucket;
        /// <see langword="null"/> if no file existed</returns>
        public static async Task<string?> VersionFileIfExists(IR2Service r2, string path, string? incomingContent = null)
        {
            var existing = await r2.GetTextAsync(path);

            if (existing is null) return null;
            if (incomingContent is not null && existing.ToLower() == incomingContent.ToLower()) return existing;

            string? dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int count = 1;
            string newPath = "";
            string? taken = existing;

            while(taken is not null && count<=999)
            {
                newPath = $"{dir}/{fileName}-old-{count:D3}{ext}";

                taken = await r2.GetTextAsync(newPath);
                count++;
            }

            using var archiveStream = new MemoryStream(Encoding.UTF8.GetBytes(existing));
            await r2.PutFileAsync(newPath, archiveStream, "application/json");

            return existing;
        }

        public static async Task<string> MergeIndexEntry(IR2Service r2, ProjectSummary incomingIndex)
        {
            var existingJson = await r2.GetTextAsync(IndexPath);
            string finalJson;

            if (existingJson != null)
            {
                var existing = JsonSerializer.Deserialize<ProjectIndex>(existingJson, JsonOptions)
                               ?? new ProjectIndex(new List<ProjectSummary>());

                ProjectIndex projectList = new ProjectIndex(existing.Projects
                    .Where(p => p.Id != incomingIndex.Id)
                    .Append(incomingIndex)
                    .ToList());

                finalJson = JsonSerializer.Serialize(projectList);
            }
            else
            {
                finalJson = JsonSerializer.Serialize(new ProjectIndex([incomingIndex]));
            }

            return finalJson;
        }

        public static async Task PublishPageBody(IR2Service r2, int id, JsonElement body)
        {
            string bodyTxt = body.GetRawText();
            string? existingBody = await VersionFileIfExists(r2, ProjectPath(id), bodyTxt);
            if (existingBody is not null && existingBody.ToLower() == bodyTxt.ToLower()) return;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(bodyTxt));
            await r2.PutFileAsync(ProjectPath(id), stream, "application/json");
        }

        public static async Task<bool> PublishIndex(IR2Service r2, JsonElement index)
        {
            ProjectSummary? incomingIndex;

            try
            {
                incomingIndex = JsonSerializer.Deserialize<ProjectSummary>(
                    index.GetRawText(), JsonOptions
                );
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine("PublishIndex threz the following exception " + ex.Message);
                return false;
            }

            if (incomingIndex is null)
                return false;

            string finalJson = await MergeIndexEntry(r2, incomingIndex);
            string? existingIndex = await VersionFileIfExists(r2, IndexPath, finalJson);

            if (existingIndex is not null && existingIndex.ToLower() == finalJson.ToLower()) return true;

            var indexStream = new MemoryStream(Encoding.UTF8.GetBytes(finalJson));
            await r2.PutFileAsync(IndexPath, indexStream, "application/json");

            return true;
        }
    }
}
