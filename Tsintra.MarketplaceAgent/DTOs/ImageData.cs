namespace Tsintra.MarketplaceAgent.DTOs;

/// <summary>
/// Represents image data passed in memory.
/// </summary>
/// <param name="CacheKey">A unique identifier for this image data, used as the cache key.</param>
/// <param name="Bytes">The raw byte content of the image.</param>
public record ImageData(string CacheKey, byte[] Bytes); 