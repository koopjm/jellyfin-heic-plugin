using System.Diagnostics;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HeicPhotos;

public sealed class HeicConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _services;
    private readonly IApplicationPaths _paths;
    private readonly ILogger<HeicConverter> _logger;
    private readonly SemaphoreSlim _slots = new(Math.Max(1, Environment.ProcessorCount / 2));

    public HeicConverter(IServiceProvider services, IApplicationPaths paths, ILogger<HeicConverter> logger)
    {
        _services = services;
        _paths = paths;
        _logger = logger;
    }

    private string Ffmpeg => _services.GetRequiredService<IMediaEncoder>().EncoderPath;

    public string ToTempJpeg(string input, int width = 0, int height = 0)
    {
        var dir = Path.Combine(_paths.TempDirectory, "heic-photos");
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".jpg");

        _slots.Wait();
        try
        {
            // 1. Try tiled stream group [0:g:0] first (handles iPhone/Android tile grids in FFmpeg 8)
            var groupFilter = (width > 0 || height > 0)
                ? ScaleGroup(width, height)
                : "[0:g:0]scale=iw:ih";

            if (Run(input, output, groupFilter))
            {
                return output;
            }

            // 2. Fallback for non-tiled single-stream HEIC files
            var singleFilter = (width > 0 || height > 0)
                ? ScaleSingle(width, height)
                : null;

            if (Run(input, output, singleFilter))
            {
                return output;
            }
        }
        finally
        {
            _slots.Release();
        }

        TryDelete(output);
        throw new InvalidOperationException($"ffmpeg couldn't convert {input}");
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ScaleGroup(int w, int h)
        => string.Create(CultureInfo.InvariantCulture, $"[0:g:0]scale=w=min(iw\\,max({w}\\,{h}*iw/ih)):h=-2");

    private static string ScaleSingle(int w, int h)
        => string.Create(CultureInfo.InvariantCulture, $"scale=w=min(iw\\,max({w}\\,{h}*iw/ih)):h=-2");

    private bool Run(string input, string output, string? filter)
    {
        var psi = new ProcessStartInfo(Ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-threads", "1", "-i", input, "-frames:v", "1" })
        {
            psi.ArgumentList.Add(a);
        }

        if (filter is not null)
        {
            psi.ArgumentList.Add("-filter_complex");
            psi.ArgumentList.Add(filter);
        }

        foreach (var a in new[] { "-q:v", "1", "-f", "image2", output })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi);
        if (p is null)
        {
            return false;
        }

        var err = p.StandardError.ReadToEndAsync();
        _ = p.StandardOutput.ReadToEndAsync();
        if (!p.WaitForExit(Timeout))
        {
            try
            {
                p.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            _logger.LogWarning("ffmpeg took too long converting {Path}", input);
            return false;
        }

        if (p.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 0)
        {
            return true;
        }

        _logger.LogDebug("ffmpeg couldn't convert {Path} (filter {Filter}): {Error}", input, filter ?? "none", err.Result.Trim());
        return false;
    }
}

