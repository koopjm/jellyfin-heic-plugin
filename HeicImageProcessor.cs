using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// Decorates <see cref="IImageProcessor"/> so that requests for HEIC images with default options
/// (no explicit width/height) force MaxWidth=3840 and Quality=96 instead of bypassing transcoding
/// in stock ImageProcessor.ProcessImage.
/// </summary>
public sealed class HeicImageProcessor : IImageProcessor
{
    private readonly IImageProcessor _inner;

    public HeicImageProcessor(IImageProcessor inner)
    {
        _inner = inner;
    }

    public IReadOnlyCollection<string> SupportedInputFormats => _inner.SupportedInputFormats;

    public bool SupportsImageCollageCreation => _inner.SupportsImageCollageCreation;

    public Task<(string Path, string? MimeType, DateTime DateModified)> ProcessImage(ImageProcessingOptions options)
    {
        if (options.Image is not null && Heic.IsHeic(options.Image.Path))
        {
            if (options.Width is null && options.Height is null && options.MaxWidth is null && options.MaxHeight is null)
            {
                options.MaxWidth = 3840;
            }

            if (options.Quality == 0 || options.Quality == 100)
            {
                options.Quality = 96;
            }
        }

        return _inner.ProcessImage(options);
    }

    public ImageDimensions GetImageDimensions(BaseItem item, ItemImageInfo info)
        => _inner.GetImageDimensions(item, info);

    public ImageDimensions GetImageDimensions(string path)
        => _inner.GetImageDimensions(path);

    public string GetImageBlurHash(string path)
        => _inner.GetImageBlurHash(path);

    public string GetImageBlurHash(string path, ImageDimensions imageDimensions)
        => _inner.GetImageBlurHash(path, imageDimensions);

    public string? GetImageCacheTag(string baseItemPath, DateTime imageDateModified)
        => _inner.GetImageCacheTag(baseItemPath, imageDateModified);

    public string? GetImageCacheTag(BaseItemDto item, ChapterInfo image)
        => _inner.GetImageCacheTag(item, image);

    public string GetImageCacheTag(BaseItem item, ItemImageInfo image)
        => _inner.GetImageCacheTag(item, image);

    public string GetImageCacheTag(BaseItemDto item, ItemImageInfo image)
        => _inner.GetImageCacheTag(item, image);

    public string? GetImageCacheTag(BaseItem item, ChapterInfo chapter)
        => _inner.GetImageCacheTag(item, chapter);

    public string? GetImageCacheTag(User user)
        => _inner.GetImageCacheTag(user);

    public IReadOnlyCollection<ImageFormat> GetSupportedImageOutputFormats()
        => _inner.GetSupportedImageOutputFormats();

    public void CreateImageCollage(ImageCollageOptions options, string? libraryName)
        => _inner.CreateImageCollage(options, libraryName);
}
