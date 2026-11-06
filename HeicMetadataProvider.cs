using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HeicPhotos;

public class HeicMetadataProvider : ICustomMetadataProvider<Photo>, IHasItemChangeMonitor, IHasOrder
{
    private readonly ILogger<HeicMetadataProvider> _logger;

    public HeicMetadataProvider(ILogger<HeicMetadataProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "HEIC Photos";

    public int Order => 100;

    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        if (!item.IsFileProtocol || !Heic.IsHeic(item.Path))
        {
            return false;
        }

        var file = directoryService.GetFile(item.Path);
        return file is not null && item.HasChanged(file.LastWriteTimeUtc);
    }

    public Task<ItemUpdateType> FetchAsync(Photo item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Heic.IsHeic(item.Path))
        {
            return Task.FromResult(FillIn(item));
        }

        item.SetImagePath(ImageType.Primary, item.Path);
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(item.Path);
            Apply(item, dirs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't read the details of {Path}", item.Path);
        }

        item.Orientation = ImageOrientation.TopLeft;
        return Task.FromResult(ItemUpdateType.ImageUpdate | ItemUpdateType.MetadataImport);
    }

    private static bool LooksUnread(Photo item)
        => item.PremiereDate is null && string.IsNullOrEmpty(item.CameraMake) && string.IsNullOrEmpty(item.CameraModel)
            && item.Latitude is null && item.ExposureTime is null && item.FocalLength is null;

    private ItemUpdateType FillIn(Photo item)
    {
        if (!item.IsFileProtocol || string.IsNullOrEmpty(item.Path) || !LooksUnread(item))
        {
            return ItemUpdateType.None;
        }

        IReadOnlyList<MetadataExtractor.Directory> dirs;
        try
        {
            dirs = ImageMetadataReader.ReadMetadata(item.Path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No second reading of {Path}", item.Path);
            return ItemUpdateType.None;
        }

        var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
        var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        if (ifd0 is null && exif is null && gps is null)
        {
            return ItemUpdateType.None;
        }

        Apply(item, dirs, heic: false);
        if (ifd0 is not null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var o) && o is >= 1 and <= 8)
        {
            item.Orientation = (ImageOrientation)o;
        }

        if (LooksUnread(item))
        {
            return ItemUpdateType.None;
        }

        return ItemUpdateType.MetadataImport;
    }

    private static void Apply(Photo item, IReadOnlyList<MetadataExtractor.Directory> dirs, bool heic = true)
    {
        if (heic && Heic.ReadSize(dirs) is { } size)
        {
            item.Width = size.Width;
            item.Height = size.Height;
        }

        var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
        var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        item.CameraMake = ifd0?.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
        item.CameraModel = ifd0?.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
        item.Software = ifd0?.GetDescription(ExifDirectoryBase.TagSoftware)?.Trim();

        var taken = Date(exif, ExifDirectoryBase.TagDateTimeOriginal) ?? Date(ifd0, ExifDirectoryBase.TagDateTime);
        if (taken is { } t)
        {
            item.DateCreated = t.ToUniversalTime();
            item.PremiereDate = t;
            item.ProductionYear = t.Year;
        }

        item.ExposureTime = Number(exif, ExifDirectoryBase.TagExposureTime);
        item.FocalLength = Number(exif, ExifDirectoryBase.TagFocalLength);
        item.Aperture = Number(exif, ExifDirectoryBase.TagAperture);
        item.ShutterSpeed = Number(exif, ExifDirectoryBase.TagShutterSpeed);
        item.IsoSpeedRating = exif is not null && exif.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso) ? iso : null;

        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        if (gps is not null && gps.TryGetGeoLocation(out var where) && !where.IsZero)
        {
            item.Latitude = where.Latitude;
            item.Longitude = where.Longitude;
            if (gps.TryGetRational(GpsDirectory.TagAltitude, out var alt))
            {
                var below = gps.TryGetInt32(GpsDirectory.TagAltitudeRef, out var aref) && aref == 1;
                item.Altitude = below ? -alt.ToDouble() : alt.ToDouble();
            }
        }
        else if (heic)
        {
            item.Latitude = item.Longitude = item.Altitude = null;
        }
    }

    private static double? Number(MetadataExtractor.Directory? d, int tag)
        => d is not null && d.TryGetRational(tag, out var r) && r.Denominator != 0 ? r.ToDouble() : null;

    private static DateTime? Date(MetadataExtractor.Directory? d, int tag)
    {
        var s = d?.GetString(tag);
        return s is not null && DateTime.TryParseExact(s.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var t)
            ? t
            : null;
    }
}

