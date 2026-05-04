using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
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


    public class R2Service : IR2Service
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
        /// Upload a file from a form (admin endpoints only)
        /// </summary>
        /// <param name="file"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public async Task<string> PutFormFileAsync(IFormFile file, string folder)
        {
            var key = $"{folder.TrimEnd('/')}/{file.FileName}";
            using var stream = file.OpenReadStream();
            await PutFileAsync(key, stream, file.ContentType);
            return key;
        }

        /// <summary>
        /// Upload a file from a stream (admin endpoints only)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public async Task PutFileAsync(string path, Stream content, string contentType)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            ms.Position = 0;

            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = path,
                InputStream = ms,
                ContentType = contentType,
                DisablePayloadSigning = true,
                UseChunkEncoding = false,
                AutoCloseStream = false
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
        public string GetPublicUrl(string path) => path.StartsWith(_settings.PublicBaseUrl) ? 
            path :
            $"{_settings.PublicBaseUrl}/{path}";

        /// <summary>
        /// Get every public Url from  a specified folder
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<List<string>> GetAllPublicUrl(string path)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _settings.BucketName,
                Prefix = path
            };

            List<string> urls = new();
            ListObjectsV2Response response;
            do
            {
                response = await _client.ListObjectsV2Async(request);

                foreach (var obj in response.S3Objects)
                {
                    if (obj.Key.EndsWith("/")) continue;

                    var publicUrl = $"{_settings.PublicBaseUrl}/{obj.Key}";
                    urls.Add(publicUrl);
                }

                request.ContinuationToken = response.NextContinuationToken;

            } while (response.IsTruncated);

            return urls;
        }
    }
}
