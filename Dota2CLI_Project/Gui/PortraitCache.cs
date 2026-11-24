using System.Drawing;
using System.Security.Cryptography;
using System.Text;

namespace Dota2GUI;

internal sealed class PortraitCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _directory;

    public PortraitCache()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dota2CLI", "PortraitCache");
        Directory.CreateDirectory(_directory);
    }

    public string? Find(string url)
    {
        var path = GetPath(url);
        if (!File.Exists(path)) return null;

        try
        {
            using var image = Image.FromFile(path);
            _ = image.Width;
            return path;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
        {
            File.Delete(path);
            return null;
        }
    }

    public async Task<string> GetOrDownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        var path = GetPath(url);
        if (File.Exists(path)) return path;

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        // Перевіряємо, що сервер справді повернув зображення, перш ніж кешувати файл.
        using (var stream = new MemoryStream(bytes))
        using (var image = Image.FromStream(stream))
        {
            _ = image.Width;
        }

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
        try
        {
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        return path;
    }

    private string GetPath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(_directory, hash + ".img");
    }
}
