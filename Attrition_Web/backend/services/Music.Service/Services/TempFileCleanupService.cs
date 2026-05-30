namespace Music.Service.Services;

/// <summary>
/// Periodically deletes leftover scan/upload temp files. ScanTrackAsync writes audio + cover
/// into music/temp; if the client never completes the upload, those files would otherwise leak.
/// Sweeps hourly, removing anything older than the configured TTL (default 6h).
/// </summary>
public class TempFileCleanupService : BackgroundService
{
    private readonly string _tempDir;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    private readonly ILogger<TempFileCleanupService> _logger;

    public TempFileCleanupService(IConfiguration config, ILogger<TempFileCleanupService> logger)
    {
        var uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        _tempDir = Path.Combine(uploadPath, "music", "temp");
        var hours = int.TryParse(config["FileUpload:TempTtlHours"], out var h) && h > 0 ? h : 6;
        _ttl = TimeSpan.FromHours(hours);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Sweep();
            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void Sweep()
    {
        try
        {
            if (!Directory.Exists(_tempDir)) return;
            var cutoff = DateTime.UtcNow - _ttl;
            foreach (var file in Directory.EnumerateFiles(_tempDir))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
                catch (IOException) { /* file in use; retry next sweep */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Temp-file sweep failed for {Dir}", _tempDir);
        }
    }
}
