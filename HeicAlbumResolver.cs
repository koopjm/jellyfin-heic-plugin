using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;

namespace Jellyfin.Plugin.HeicPhotos;

public class HeicAlbumResolver : ItemResolver<PhotoAlbum>
{
    public override ResolverPriority Priority => ResolverPriority.Second;

    protected override PhotoAlbum? Resolve(ItemResolveArgs args)
    {
        if (!args.IsDirectory || !HeicPhotoResolver.PhotoLibrary(args.GetCollectionType(), args))
        {
            return null;
        }

        return args.FileSystemChildren.Any(f => !f.IsDirectory && HeicPhotoResolver.IsHeicPhoto(f.FullName))
            ? new PhotoAlbum { Path = args.Path }
            : null;
    }
}

