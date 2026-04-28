using Amazon.S3;
using Amazon.S3.Model;

namespace PersonalApi.Storage
{

    public class R2Settings
    {
        public required string AccountId { get; init; }
        public required string AccessKey { get; init; }
        public required string SecretKey { get; init; }
        public required string BucketName { get; init; }
        public required string PublicBaseUrl { get; init; }
    }


    public class R2Service
    {
        private readonly AmazonS3Client _client;
        private readonly R2Settings _settings;

        public R2Service(R2Settings settings)
        {
            _settings = settings;
            _client = new AmazonS3Client(
                settings.AccessKey,
                settings.SecretKey,
                new AmazonS3Config
                {
                    ServiceURL = $"https://{settings.AccountId}.r2.cloudflarestorage.com",
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true
                }
            );
        }

        /// <summary>
        /// Read a file as a string (for JSON files)
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The JSON file's content</returns>
        public async Task<string?> GetTextAsync(string path)
        {
            try
            {
                Console.WriteLine("printing settings " + _settings.BucketName); 
                var response = await _client.GetObjectAsync(_settings.BucketName, path);
                using var reader = new StreamReader(response.ResponseStream);
                return await reader.ReadToEndAsync();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Upload a file (to use only in admin endpoints)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public async Task PutFileAsync(string path, Stream content, string contentType)
        {
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = path,
                InputStream = content,
                ContentType = contentType
            });
        }

        /// <summary>
        /// Delete a file (to use only in admin endpoints)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task DeleteFileAsync(string path)
        {
            await _client.DeleteObjectAsync(_settings.BucketName, path);
        }

        /// <summary>
        /// Build a public URL for a given path (to use for images for exemple)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string GetPublicUrl(string path) => $"{_settings.PublicBaseUrl}/{path}";
    }
}
