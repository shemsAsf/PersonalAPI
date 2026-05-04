namespace PersonalApi.Storage
{
    public interface IR2Service
    {
        Task<string?> GetTextAsync(string key);
        Task PutFileAsync(string key, Stream content, string contentType);
        Task<string> PutFormFileAsync(IFormFile file, string folder);
        string GetPublicUrl(string key);
    }
}
