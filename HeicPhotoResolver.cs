using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;

namespace Jellyfin.Plugin.HeicPhotos;

public class HeicPhotoResolver : ItemResolver<Photo>
{
    private static readonly string[] _artwork = ["folder", "thumb", "landscape", "fanart", "backdrop", "poster", "cover", "logo", "default"];

    internal static bool PhotoLibrary(CollectionType? type, ItemResolveArgs args)
        => type == CollectionType.photos || (type == CollectionType.homevideos && args.LibraryOptions.EnablePhotos);

    internal static bool IsHeicPhoto(string path)
        => Heic.IsHeic(path) && !_artwork.Any(a => Path.GetFileNameWithoutExtension(path).StartsWith(a, StringComparison.OrdinalIgnoreCase));

    protected override Photo? Resolve(ItemResolveArgs args)
    {
        if (args.IsDirectory || !PhotoLibrary(args.CollectionType, args) || !IsHeicPhoto(args.Path))
        {
            return null;
        }

        return new Photo { Path = args.Path };
    }
}

