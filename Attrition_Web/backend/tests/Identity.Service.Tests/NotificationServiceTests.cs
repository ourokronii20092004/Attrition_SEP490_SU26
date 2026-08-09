using Identity.Service.DTOs;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using NSubstitute;

namespace Identity.Service.Tests;

public class NotificationServiceTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    private NotificationService Sut => new(_repository);

    [Fact]
    public async Task UTCID01_ValidListLimit_IsForwarded()
    {
        var expected = new List<NotificationDto>(); _repository.ListAsync(_userId, 20).Returns(expected);
        Assert.Same(expected, await Sut.ListAsync(_userId, 20));
    }

    [Theory]
    [InlineData(0, 1, "UTCID02")]
    [InlineData(500, 50, "UTCID03")]
    public async Task ListLimit_IsClamped(int input, int expected, string _)
    {
        await Sut.ListAsync(_userId, input);
        await _repository.Received(1).ListAsync(_userId, expected);
    }

    [Fact]
    public async Task UTCID04_InvalidPage_IsClampedToOne()
    {
        await Sut.ListPagedAsync(_userId, 0, 20, false);
        await _repository.Received(1).ListPagedAsync(_userId, 1, 20, false);
    }

    [Fact]
    public async Task UTCID05_OversizedPageSize_IsClampedToOneHundred()
    {
        await Sut.ListPagedAsync(_userId, 1, 500, false);
        await _repository.Received(1).ListPagedAsync(_userId, 1, 100, false);
    }

    [Fact]
    public async Task UTCID06_UnreadFilter_IsForwarded()
    {
        await Sut.ListPagedAsync(_userId, 1, 20, true);
        await _repository.Received(1).ListPagedAsync(_userId, 1, 20, true);
    }

    [Fact]
    public async Task UTCID07_MarkRead_UsesBothUserAndNotificationIds()
    {
        var notificationId = Guid.NewGuid(); await Sut.MarkReadAsync(_userId, notificationId);
        await _repository.Received(1).MarkReadAsync(_userId, notificationId);
    }

    [Fact]
    public async Task UTCID08_MarkAllRead_IsScopedToUser()
    {
        await Sut.MarkAllReadAsync(_userId); await _repository.Received(1).MarkAllReadAsync(_userId);
    }

    [Fact]
    public async Task UTCID09_UnreadCount_IsReturnedFromRepository()
    {
        _repository.UnreadCountAsync(_userId).Returns(3);
        Assert.Equal(3, await Sut.UnreadCountAsync(_userId));
    }
}