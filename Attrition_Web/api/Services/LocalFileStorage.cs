using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Attrition.API.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _uploadPath;

    public LocalFileStorage(IConfiguration config)
    {
        _uploadPath = config["FileUpload:UploadPath"] ?? "./uploads";
    }

    public async Task<string> SaveAsync(string subfolder, string fileName, Stream stream)
    {
        var targetDir = Path.Combine(_uploadPath, subfolder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var filePath = Path.Combine(targetDir, fileName);
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream);
        }

        return $"/uploads/{subfolder}/{fileName}";
    }

    public Task<bool> DeleteAsync(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return Task.FromResult(false);

        var prefix = "/uploads/";
        if (relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath.Substring(prefix.Length);
        }

        // Clean up relative path formatting
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
