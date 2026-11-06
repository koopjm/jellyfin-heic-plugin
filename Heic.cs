using MetadataExtractor;
using MetadataExtractor.Formats.Heif;

namespace Jellyfin.Plugin.HeicPhotos;

internal static class Heic
{
    public static readonly string[] Formats = ["heic", "heif", "hif"];

    public static bool IsHeic(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path.AsSpan()).TrimStart('.');
        foreach (var f in Formats)
        {
            if (ext.Equals(f, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static (int Width, int Height)? ReadSize(IReadOnlyList<MetadataExtractor.Directory> dirs)
    {
        foreach (var d in dirs.OfType<HeicImagePropertiesDirectory>())
        {
            if (d.TryGetInt32(HeicImagePropertiesDirectory.TagImageWidth, out var w)
                && d.TryGetInt32(HeicImagePropertiesDirectory.TagImageHeight, out var h)
                && w > 0 && h > 0)
            {
                var turned = d.TryGetInt32(HeicImagePropertiesDirectory.TagRotation, out var r) && (r == 90 || r == 270);
                return turned ? (h, w) : (w, h);
            }
        }

        return null;
    }

    public static (int Width, int Height)? ReadSize(string path)
    {
        try
        {
            return ReadSize(ImageMetadataReader.ReadMetadata(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}

