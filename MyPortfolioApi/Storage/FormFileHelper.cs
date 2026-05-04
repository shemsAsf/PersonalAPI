namespace PersonalApi.Storage
{
    public class FormFileHelper
    {
        public static (IFormFile? file, IResult? error) GetFile(HttpRequest req, string fieldName = "file")
        {
            if (!req.HasFormContentType)
                return (null, Results.BadRequest("Expected multipart/form-data"));

            var file = req.ReadFormAsync().GetAwaiter().GetResult().Files.GetFile(fieldName);

            if (file is null || file.Length == 0)
                return (null, Results.BadRequest("No file provided"));

            return (file, null);
        }
    }
}
