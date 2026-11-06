using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// Intercepts LibraryController.GetDownload (/Items/{itemId}/Download) and GetFile (/Items/{itemId}/File)
/// for HEIC/HEIF photos, returning a 4K (3840px, quality 96) WebP transcode instead of the raw .heic file
/// so stock jellyfin-web fullscreen slideshows render properly in Chrome/Firefox without client patches.
/// </summary>
public sealed class HeicDownloadFilter : IAsyncActionFilter
{
    private readonly ILibraryManager _libraryManager;
    private readonly IImageProcessor _imageProcessor;

    public HeicDownloadFilter(ILibraryManager libraryManager, IImageProcessor imageProcessor)
    {
        _libraryManager = libraryManager;
        _imageProcessor = imageProcessor;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) ? action : null;
        if (string.Equals(actionName, "GetDownload", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionName, "GetFile", StringComparison.OrdinalIgnoreCase))
        {
            if (context.ActionArguments.TryGetValue("itemId", out var itemIdObj) && itemIdObj is Guid itemId)
            {
                var item = _libraryManager.GetItemById<BaseItem>(itemId);
                if (item is not null && Heic.IsHeic(item.Path))
                {
                    var imageInfo = item.GetImageInfo(ImageType.Primary, 0);
                    if (imageInfo is not null)
                    {
                        var options = new ImageProcessingOptions
                        {
                            Item = item,
                            Image = imageInfo,
                            MaxWidth = 3840,
                            Quality = 96,
                            SupportedOutputFormats = [ImageFormat.Webp],
                            RequiresAutoOrientation = true
                        };

                        var result = await _imageProcessor.ProcessImage(options).ConfigureAwait(false);
                        var filename = Path.ChangeExtension(Path.GetFileName(item.Path), ".webp");
                        context.Result = new PhysicalFileResult(result.Path, result.MimeType ?? "image/webp")
                        {
                            FileDownloadName = string.Equals(actionName, "GetDownload", StringComparison.OrdinalIgnoreCase) ? filename : string.Empty,
                            EnableRangeProcessing = true
                        };
                        return;
                    }
                }
            }
        }

        await next().ConfigureAwait(false);
    }
}

