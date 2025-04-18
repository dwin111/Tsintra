using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tsintra.Integrations.Interfaces
{
    /// <summary>
    /// Defines operations for interacting with S3 storage.
    /// </summary>
    public interface IS3StorageService
    {
        /// <summary>
        /// Uploads a file to S3.
        /// </summary>
        /// <param name="bucketName">The target S3 bucket name.</param>
        /// <param name="key">The desired key (path/filename) for the object in S3.</param>
        /// <param name="inputStream">The stream containing the file content.</param>
        /// <param name="contentType">The content type (MIME type) of the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The S3 object key if successful, otherwise null.</returns>
        Task<string?> UploadFileAsync(
            string bucketName,
            string key,
            Stream inputStream,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a pre-signed URL for accessing an S3 object.
        /// </summary>
        /// <param name="bucketName">The S3 bucket name.</param>
        /// <param name="key">The S3 object key.</param>
        /// <param name="expiresInSeconds">Duration for which the URL is valid (in seconds).</param>
        /// <returns>A pre-signed URL string.</returns>
        Task<string> GetPresignedUrlAsync(string bucketName, string key, int expiresInSeconds = 3600);

        /// <summary>
        /// Downloads a file from S3 as a stream.
        /// </summary>
        /// <param name="bucketName">The S3 bucket name.</param>
        /// <param name="key">The S3 object key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the file content, or null if the object doesn't exist.</returns>
        Task<Stream?> DownloadFileAsStreamAsync(string bucketName, string key, CancellationToken cancellationToken = default);

        // Add other methods as needed, e.g., DeleteFileAsync, GetPublicUrlAsync (if bucket allows public access)
    }
} 