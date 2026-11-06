using MediaBrowser.Controller;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HeicPhotos;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HeicConverter>();

        // 1. Decorate IImageEncoder with HeicImageEncoder
        var originalEncoder = serviceCollection.LastOrDefault(d => d.ServiceType == typeof(IImageEncoder));
        if (originalEncoder is not null)
        {
            serviceCollection.Remove(originalEncoder);
            serviceCollection.AddSingleton<IImageEncoder>(sp =>
                new HeicImageEncoder(Make<IImageEncoder>(sp, originalEncoder), sp.GetRequiredService<HeicConverter>()));
        }

        // 2. Decorate IImageProcessor with HeicImageProcessor so default-option requests never bypass transcoding
        var originalProcessor = serviceCollection.LastOrDefault(d => d.ServiceType == typeof(IImageProcessor));
        if (originalProcessor is not null)
        {
            serviceCollection.Remove(originalProcessor);
            serviceCollection.AddSingleton<IImageProcessor>(sp =>
                new HeicImageProcessor(Make<IImageProcessor>(sp, originalProcessor)));
        }

        // 3. Register HeicDownloadFilter so /Items/{itemId}/Download and /Items/{itemId}/File serve 4K WebP for HEIC
        serviceCollection.AddScoped<HeicDownloadFilter>();
        serviceCollection.Configure<MvcOptions>(options =>
        {
            options.Filters.AddService<HeicDownloadFilter>();
        });
    }

    private static T Make<T>(IServiceProvider sp, ServiceDescriptor d)
        => (T)(d.ImplementationInstance
            ?? d.ImplementationFactory?.Invoke(sp)
            ?? ActivatorUtilities.CreateInstance(sp, d.ImplementationType!));
}
