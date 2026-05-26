using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Attrition.API.Services;
using Moq;
using Xunit;

namespace Attrition.API.Tests;

public class RoomServiceTests
{
    private readonly Mock<IRepository<GameRoom>> _roomRepoMock;
    private readonly Mock<IRepository<RoomPlayer>> _roomPlayerRepoMock;
    private readonly RoomService _roomService;

    public RoomServiceTests()
    {
        _roomRepoMock = new Mock<IRepository<GameRoom>>();
        _roomPlayerRepoMock = new Mock<IRepository<RoomPlayer>>();
        _roomService = new RoomService(_roomRepoMock.Object, _roomPlayerRepoMock.Object);
    }

    [Fact]
    public async Task CreateRoomAsync_ShouldCreateRoomAndAddHost()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        _roomRepoMock.Setup(r => r.AddAsync(It.IsAny<GameRoom>())).ReturnsAsync((GameRoom r) => r);
        _roomPlayerRepoMock.Setup(r => r.AddAsync(It.IsAny<RoomPlayer>())).ReturnsAsync((RoomPlayer p) => p);
        _roomRepoMock.Setup(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<Expression<Func<GameRoom, bool>>>(), 
            It.IsAny<Func<IQueryable<GameRoom>, IOrderedQueryable<GameRoom>>>(),
            It.IsAny<Expression<Func<GameRoom, object>>[]>()
        )).ReturnsAsync((new List<GameRoom>(), 0));

        // Act
        var room = await _roomService.CreateRoomAsync(hostId, "My Room", false);

        // Assert
        Assert.NotNull(room);
        Assert.Equal(hostId, room.HostUserId);
        Assert.Equal("My Room", room.RoomName);
        Assert.False(room.IsPrivate);
        Assert.NotEmpty(room.RoomCode);
        
        _roomRepoMock.Verify(r => r.AddAsync(It.Is<GameRoom>(gr => gr.HostUserId == hostId)), Times.Once);
        _roomPlayerRepoMock.Verify(r => r.AddAsync(It.Is<RoomPlayer>(rp => rp.UserId == hostId && rp.PlayerRole == 1)), Times.Once);
    }

    [Fact]
    public async Task JoinRoomAsync_ShouldSucceed_WhenUnderCapacity()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var charId = Guid.NewGuid();
        
        var room = new GameRoom { RoomId = roomId, MaxPlayers = 4 };

        // Mock existing players check
        _roomPlayerRepoMock.Setup(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<Expression<Func<RoomPlayer, bool>>>(), 
            It.IsAny<Func<IQueryable<RoomPlayer>, IOrderedQueryable<RoomPlayer>>>(),
            It.IsAny<Expression<Func<RoomPlayer, object>>[]>()
        )).ReturnsAsync((new List<RoomPlayer>(), 0));

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepoMock.Setup(r => r.CountAsync(It.IsAny<Expression<Func<RoomPlayer, bool>>>())).ReturnsAsync(1); // 1 player currently

        // Act
        var result = await _roomService.JoinRoomAsync(roomId, userId, charId);

        // Assert
        Assert.True(result);
        _roomPlayerRepoMock.Verify(r => r.AddAsync(It.Is<RoomPlayer>(rp => rp.RoomId == roomId && rp.UserId == userId && rp.CharacterId == charId)), Times.Once);
    }

    [Fact]
    public async Task JoinRoomAsync_ShouldFail_WhenOverCapacity()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var charId = Guid.NewGuid();
        
        var room = new GameRoom { RoomId = roomId, MaxPlayers = 4 };

        _roomPlayerRepoMock.Setup(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<Expression<Func<RoomPlayer, bool>>>(), 
            It.IsAny<Func<IQueryable<RoomPlayer>, IOrderedQueryable<RoomPlayer>>>(),
            It.IsAny<Expression<Func<RoomPlayer, object>>[]>()
        )).ReturnsAsync((new List<RoomPlayer>(), 0));

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepoMock.Setup(r => r.CountAsync(It.IsAny<Expression<Func<RoomPlayer, bool>>>())).ReturnsAsync(4); // already full

        // Act
        var result = await _roomService.JoinRoomAsync(roomId, userId, charId);

        // Assert
        Assert.False(result);
        _roomPlayerRepoMock.Verify(r => r.AddAsync(It.IsAny<RoomPlayer>()), Times.Never);
    }

    [Fact]
    public async Task LeaveRoomAsync_ShouldMigrateHost_ToOldestPlayer()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var otherPlayerId1 = Guid.NewGuid();
        var otherPlayerId2 = Guid.NewGuid();

        var leavingHost = new RoomPlayer { RoomId = roomId, UserId = hostId, PlayerRole = 1, JoinedAt = DateTime.UtcNow.AddMinutes(-5) };
        var remainingPlayer1 = new RoomPlayer { RoomId = roomId, UserId = otherPlayerId1, PlayerRole = 0, JoinedAt = DateTime.UtcNow.AddMinutes(-3) };
        var remainingPlayer2 = new RoomPlayer { RoomId = roomId, UserId = otherPlayerId2, PlayerRole = 0, JoinedAt = DateTime.UtcNow.AddMinutes(-1) };

        var room = new GameRoom { RoomId = roomId, HostUserId = hostId };

        // Setup GetPagedAsync calls in sequence
        _roomPlayerRepoMock.SetupSequence(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<Expression<Func<RoomPlayer, bool>>>(), 
            It.IsAny<Func<IQueryable<RoomPlayer>, IOrderedQueryable<RoomPlayer>>>(),
            It.IsAny<Expression<Func<RoomPlayer, object>>[]>()
        ))
        .ReturnsAsync((new List<RoomPlayer> { leavingHost }, 1))
        .ReturnsAsync((new List<RoomPlayer> { remainingPlayer1, remainingPlayer2 }, 2));

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.LeaveRoomAsync(roomId, hostId);

        // Assert
        Assert.True(result);
        _roomPlayerRepoMock.Verify(r => r.DeleteAsync(leavingHost), Times.Once);
        Assert.Equal(1, remainingPlayer1.PlayerRole); // promoted to host
        Assert.Equal(0, remainingPlayer2.PlayerRole); // remains guest
        Assert.Equal(otherPlayerId1, room.HostUserId); // room host ID updated
        _roomPlayerRepoMock.Verify(r => r.UpdateAsync(remainingPlayer1), Times.Once);
        _roomRepoMock.Verify(r => r.UpdateAsync(room), Times.Once);
    }

    [Fact]
    public async Task LeaveRoomAsync_ShouldEndRoom_WhenNoPlayersRemain()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var leavingHost = new RoomPlayer { RoomId = roomId, UserId = hostId, PlayerRole = 1 };
        var room = new GameRoom { RoomId = roomId, HostUserId = hostId, Status = "waiting" };

        _roomPlayerRepoMock.SetupSequence(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<Expression<Func<RoomPlayer, bool>>>(), 
            It.IsAny<Func<IQueryable<RoomPlayer>, IOrderedQueryable<RoomPlayer>>>(),
            It.IsAny<Expression<Func<RoomPlayer, object>>[]>()
        ))
        .ReturnsAsync((new List<RoomPlayer> { leavingHost }, 1))
        .ReturnsAsync((new List<RoomPlayer>(), 0));

        _roomRepoMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.LeaveRoomAsync(roomId, hostId);

        // Assert
        Assert.True(result);
        Assert.Equal("ended", room.Status);
        Assert.NotNull(room.EndedAt);
        _roomRepoMock.Verify(r => r.UpdateAsync(room), Times.Once);
    }
}
