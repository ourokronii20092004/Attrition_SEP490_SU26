using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class RoomCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoomCleanupService> _logger;

    public RoomCleanupService(IServiceProvider serviceProvider, ILogger<RoomCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Room Cleanup Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupInactiveRoomsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up inactive rooms.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task CleanupInactiveRoomsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var roomRepo = scope.ServiceProvider.GetRequiredService<IRepository<GameRoom>>();

        // Find rooms with status "waiting" or "in_progress"
        var (rooms, _) = await roomRepo.GetPagedAsync(
            1, int.MaxValue,
            r => r.Status == RoomStatus.Waiting || r.Status == RoomStatus.InProgress,
            null,
            r => r.Players
        );

        var thresholdTime = DateTime.UtcNow.AddHours(-1); // Cleanup if older than 1 hour

        foreach (var room in rooms)
        {
            var hasPlayers = room.Players.Count > 0;
            var isExpired = room.CreatedAt < thresholdTime;

            if (!hasPlayers || isExpired)
            {
                _logger.LogInformation("Ending inactive room: {RoomCode} (Id: {RoomId})", room.RoomCode, room.RoomId);
                room.Status = RoomStatus.Ended;
                room.EndedAt = DateTime.UtcNow;
                await roomRepo.UpdateAsync(room);
            }
        }
    }
}
