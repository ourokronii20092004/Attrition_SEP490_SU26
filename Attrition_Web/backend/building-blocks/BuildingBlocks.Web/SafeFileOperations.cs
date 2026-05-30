using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web;

/// <summary>
/// Safe wrappers for file operations that log failures instead of throwing, so an IO error
/// degrades to a handled result rather than a generic 500 with orphaned files. Modeled after the
/// containment patterns already used in Assets.Service.
/// </summary>
public static class SafeFileOperations
{
    /// <summary>Delete a file. Returns true if deleted or already absent, false if the delete failed.</summary>
    public static async Task<bool> SafeDeleteAsync(string path, ILogger logger)
    {
        try
        {
            if (File.Exists(path))
                await Task.Run(() => File.Delete(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to delete file {Path}", path);
            return false;
        }
    }

    /// <summary>Move a file, creating the destination directory if needed. Returns false on IO failure.</summary>
    public static async Task<bool> SafeMoveAsync(string source, string destination, ILogger logger, bool overwrite = true)
    {
        try
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            await Task.Run(() => File.Move(source, destination, overwrite));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to move file {Source} -> {Dest}", source, destination);
            return false;
        }
    }

    /// <summary>Save a stream to a path, creating directories as needed. Returns false on IO failure.</summary>
    public static async Task<bool> SafeSaveAsync(string path, Stream content, ILogger logger)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var fileStream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await content.CopyToAsync(fileStream);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to save file to {Path}", path);
            return false;
        }
    }
}
