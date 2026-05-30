namespace Identity.Service.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _uploadPath;
    private readonly string _publicPrefix;

    public LocalFileStorage(IConfiguration config)
    {
        _uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        // Served by this service under the gateway-routable prefix.
        _publicPrefix = config["FileUpload:PublicPrefix"] ?? "/api/account/media";
    }

    public async Task<string> SaveAsync(string subfolder, string fileName, Stream stream)
    {
        var targetDir = Path.Combine(_uploadPath, subfolder);
        Directory.CreateDirectory(targetDir);

        var filePath = Path.Combine(targetDir, fileName);
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        return $"{_publicPrefix}/{subfolder}/{fileName}";
    }

    public Task<bool> DeleteAsync(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return Task.FromResult(false);

        var prefix = _publicPrefix + "/";
        if (relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath[prefix.Length..];

        relativePath = relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                                   .Replace("\\", Path.DirectorySeparatorChar.ToString());

        var baseFull = Path.GetFullPath(_uploadPath);
        var fullPath = Path.GetFullPath(Path.Combine(baseFull, relativePath));

        // Containment guard: never delete outside the upload root, even if relativePath
        // contains "../" segments (defense-in-depth against a poisoned stored path).
        var baseWithSep = baseFull.EndsWith(Path.DirectorySeparatorChar)
            ? baseFull
            : baseFull + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSep, StringComparison.Ordinal))
            return Task.FromResult(false);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
