namespace Assets.Service.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string subfolder, string fileName, Stream stream);
    Task<bool> DeleteAsync(string relativePath);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _uploadPath;
    private readonly string _publicPrefix;

    public LocalFileStorage(IConfiguration config)
    {
        _uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        _publicPrefix = config["FileUpload:PublicPrefix"] ?? "/api/assets/media";
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

        var fullPath = Path.Combine(_uploadPath, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
