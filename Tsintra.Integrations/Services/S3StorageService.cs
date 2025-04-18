using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Integrations.Interfaces;

namespace Tsintra.Integrations.Services
{
    /// <summary>
    /// Implements S3 storage operations.
    /// </summary>
    public class S3StorageService : IS3StorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(IAmazonS3 s3Client, ILogger<S3StorageService> logger)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> UploadFileAsync(
            string bucketName,
            string key,
            Stream inputStream,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting upload to S3. Bucket: {BucketName}, Key: {Key}, ContentType: {ContentType}", 
                    bucketName, key, contentType);

                // Use TransferUtility for potentially large files and easier handling
                var fileTransferUtility = new TransferUtility(_s3Client);

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = inputStream,
                    ContentType = contentType,
                    // CannedACL = S3CannedACL.Private // Default is private, adjust if needed
                };

                _logger.LogDebug("Created upload request. Starting upload...");
                await fileTransferUtility.UploadAsync(uploadRequest, cancellationToken);
                _logger.LogInformation("Successfully uploaded {Key} to bucket {BucketName}", key, bucketName);
                return key;
            }
            catch (AmazonS3Exception e)
            {
                _logger.LogError(e, "S3 Error uploading {Key} to {BucketName}. Error Code: {ErrorCode}, Status Code: {StatusCode}, Message: {Message}", 
                    key, bucketName, e.ErrorCode, e.StatusCode, e.Message);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error uploading {Key} to {BucketName}: {Message}", key, bucketName, e.Message);
                return null;
            }
        }

        public Task<string> GetPresignedUrlAsync(string bucketName, string key, int expiresInSeconds = 3600)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                    Verb = HttpVerb.GET // Or PUT if needed for upload URLs
                };

                string url = _s3Client.GetPreSignedURL(request);
                return Task.FromResult(url);
            }
            catch (AmazonS3Exception e)
            {
                _logger.LogError(e, "S3 Error generating presigned URL for {Key} in {BucketName}: {Message}", key, bucketName, e.Message);
                throw; // Re-throw or return an empty string/null based on desired error handling
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error generating presigned URL for {Key} in {BucketName}: {Message}", key, bucketName, e.Message);
                throw; // Re-throw or return an empty string/null
            }
        }

        public async Task<Stream?> DownloadFileAsStreamAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };

                var response = await _s3Client.GetObjectAsync(request, cancellationToken);
                _logger.LogInformation("Successfully opened stream for {Key} from bucket {BucketName}", key, bucketName);
                // The caller is responsible for disposing the stream
                return response.ResponseStream;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("S3 object {Key} not found in bucket {BucketName}", key, bucketName);
                return null;
            }
            catch (AmazonS3Exception e)
            {
                _logger.LogError(e, "S3 Error downloading {Key} from {BucketName}: {Message}", key, bucketName, e.Message);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error downloading {Key} from {BucketName}: {Message}", key, bucketName, e.Message);
                return null;
            }
        }
    }
} 