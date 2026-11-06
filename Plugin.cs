using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// HEIC Photos plugin: indexes HEIC/HEIF photos, extracts EXIF/GPS metadata, decodes tiled HEIC grids
/// via FFmpeg 8 stream groups, and intercepts full-resolution slideshow downloads to serve 4K WebP.
/// </summary>
public class Plugin : BasePlugin<BasePluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    public override string Name => "HEIC Photos";

    public override Guid Id => Guid.Parse("5b0f7c1e-3a6d-4f2b-9e84-2c71d0a9b6e3");

    public override string Description => "Indexes HEIC/HEIF photos, reads EXIF/GPS metadata, decodes tiled iPhone/Android HEICs via FFmpeg, and transcodes fullscreen slideshows to 4K WebP.";
}

