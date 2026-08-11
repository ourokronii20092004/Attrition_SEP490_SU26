using Character.Service.Clients;
using Character.Service.DTOs;
using Character.Service.Models;
using Character.Service.Repositories.Interface;
using Character.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;

namespace Character.Service.Tests;

public class CharacterServiceTests
{
    private readonly ICharacterRepository repo = Substitute.For<ICharacterRepository>();
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();

    private CharacterService Sut => new
        (
        repo,
        new IdentityClient(new HttpClient(new Handler()) { BaseAddress = new Uri("http://test/") }, new ConfigurationBuilder().Build(), NullLogger<IdentityClient>.Instance),
        sessions,
        NullLogger<CharacterService>.Instance
        );

    private static CharacterEntity Character() => new() { OwnerId = Guid.NewGuid(), Name = "Hero", Archetype = "Vanguard", InventoryJson = "old-inv", EquipmentJson = "old-eq", QuestsJson = "old-q", Snapshots = new() { new() { Level = 2, Hp = 50, MaxHp = 100, CapturedAt = DateTime.UtcNow.AddMinutes(-2) }, new() { Level = 3, Hp = 70, MaxHp = 100, CapturedAt = DateTime.UtcNow } } };

    private static SnapshotIngestRequest Request(Guid owner, Guid? id = null, string name = "Hero", string? inv = "inv") => new(owner, id, name, "Rogue", 5, 80, 100, 20, true, "ROOM", "save", 100, inv, "eq", "quests");

    [Fact]
    public async Task Progress_UTCID01_OwnerList_MapsLatestSnapshot()
    {
        var c = Character();
        repo.GetByOwnerWithSnapshotsAsync(c.OwnerId).Returns(new List<CharacterEntity> { c });
        var r = await Sut.GetByOwnerAsync(c.OwnerId);
        Assert.Single(r);
        Assert.Equal(3, r[0].LatestSnapshot!.Level);
        Assert.Equal(2, r[0].SnapshotCount);
    }

    [Fact]
    public async Task Progress_UTCID02_OwnerWithoutCharacters_ReturnsEmpty()
    {
        repo.GetByOwnerWithSnapshotsAsync(Arg.Any<Guid>()).Returns(new List<CharacterEntity>()); Assert.Empty(await Sut.GetByOwnerAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Progress_UTCID03_Detail_OrdersHistoryNewestFirst()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); var r = await Sut.GetDetailAsync(c.Id); Assert.NotNull(r); Assert.Equal(3, r.Snapshots[0].Level);
    }

    [Fact]
    public async Task Progress_UTCID04_UnknownDetail_ReturnsNull()
    {
        Assert.Null(await Sut.GetDetailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Progress_UTCID05_NoSnapshots_HasNullLatest()
    {
        var c = Character(); c.Snapshots.Clear(); repo.GetByOwnerWithSnapshotsAsync(c.OwnerId).Returns(new List<CharacterEntity> { c }); Assert.Null((await Sut.GetByOwnerAsync(c.OwnerId))[0].LatestSnapshot);
    }

    [Fact]
    public async Task Sync_UTCID01_NewCharacter_IsCreatedWithSnapshot()
    {
        var owner = Guid.NewGuid(); CharacterEntity? added = null; repo.TryAddAsync(Arg.Do<CharacterEntity>(x => added = x)).Returns(true); var r = await Sut.IngestSnapshotAsync(Request(owner)); Assert.True(r.Success); Assert.Equal(owner, added!.OwnerId); Assert.Single(added.Snapshots);
    }

    [Fact]
    public async Task Sync_UTCID02_ExistingId_AppendsSnapshotAndUpdatesFields()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); var before = c.Snapshots.Count; var r = await Sut.IngestSnapshotAsync(Request(c.OwnerId, c.Id)); Assert.True(r.Success); Assert.Equal(before + 1, c.Snapshots.Count); Assert.Equal("Rogue", c.Archetype); await repo.Received(1).UpdateAsync(c);
    }

    [Fact]
    public async Task Sync_UTCID03_OwnerNameMatch_UpdatesWithoutCreating()
    {
        var c = Character(); repo.FindByOwnerAndNameAsync(c.OwnerId, c.Name).Returns(c); await Sut.IngestSnapshotAsync(Request(c.OwnerId)); await repo.Received(1).UpdateAsync(c); await repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!);
    }

    [Fact]
    public async Task Sync_UTCID04_NullJsonFields_PreserveExistingValues()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); var req = Request(c.OwnerId, c.Id, inv: null) with { EquipmentJson = null, QuestsJson = null }; await Sut.IngestSnapshotAsync(req); Assert.Equal("old-inv", c.InventoryJson); Assert.Equal("old-eq", c.EquipmentJson); Assert.Equal("old-q", c.QuestsJson);
    }

    [Fact]
    public async Task Sync_UTCID05_ConcurrentCreateWinner_IsRefetchedAndUpdated()
    {
        var c = Character();
        repo.TryAddAsync(Arg.Any<CharacterEntity>()).Returns(false);
        repo.FindByOwnerAndNameAsync(c.OwnerId, c.Name).Returns((CharacterEntity?)null, c);
        var r = await Sut.IngestSnapshotAsync(Request(c.OwnerId));
        Assert.True(r.Success);
        await repo.Received(1).UpdateAsync(c);
    }

    [Fact]
    public async Task Sync_UTCID06_LostRaceWithoutWinner_ReturnsRetryFailure()
    {
        var owner = Guid.NewGuid(); repo.TryAddAsync(Arg.Any<CharacterEntity>()).Returns(false); var r = await Sut.IngestSnapshotAsync(Request(owner)); Assert.False(r.Success); Assert.Contains("retry", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sync_UTCID07_EmptyOwner_IsRejectedBeforeRepository()
    {
        var r = await Sut.IngestSnapshotAsync(Request(Guid.Empty)); Assert.False(r.Success); await repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!);
    }

    [Fact]
    public async Task Sync_UTCID08_BlankName_IsRejected()
    {
        var r = await Sut.IngestSnapshotAsync(Request(Guid.NewGuid(), name: "   ")); Assert.False(r.Success); Assert.Contains("name", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_UTCID01_GetSaves_ValidOwner_MapsPagedHistory()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); sessions.GetSavesAsync(c.Id, 1, 20).Returns((new List<CharacterSaveEntity> { Save(c.Id, 2), Save(c.Id, 1) }, 2)); var r = await Sut.GetSavesAsync(c.Id, c.OwnerId, false, 1, 20); Assert.True(r.Success); Assert.Equal(2, r.Data!.Items.Count); Assert.True(r.Data.Items[0].IsCurrent);
    }

    [Fact]
    public async Task History_UTCID02_InvalidPaging_IsClamped()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); sessions.GetSavesAsync(c.Id, 1, 20).Returns((new List<CharacterSaveEntity>(), 0)); sessions.GetSavesAsync(c.Id, 1, 1).Returns((new List<CharacterSaveEntity>(), 0)); var r = await Sut.GetSavesAsync(c.Id, c.OwnerId, false, 0, 500); Assert.Equal(1, r.Data!.Page); Assert.Equal(20, r.Data.PageSize);
    }

    [Fact]
    public async Task History_UTCID03_NonOwner_CannotList()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); Assert.False((await Sut.GetSavesAsync(c.Id, Guid.NewGuid(), false, 1, 20)).Success);
    }

    [Fact]
    public async Task History_UTCID04_Admin_CanListOtherOwner()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); sessions.GetSavesAsync(c.Id, 1, 20).Returns((new List<CharacterSaveEntity>(), 0)); sessions.GetSavesAsync(c.Id, 1, 1).Returns((new List<CharacterSaveEntity>(), 0)); Assert.True((await Sut.GetSavesAsync(c.Id, Guid.NewGuid(), true, 1, 20)).Success);
    }

    [Fact]
    public async Task History_UTCID05_SaveBelongingToAnotherCharacter_IsHidden()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); sessions.GetSaveAsync(1).Returns(Save(Guid.NewGuid(), 1)); Assert.False((await Sut.GetSaveAsync(c.Id, 1, c.OwnerId, false)).Success);
    }

    [Fact]
    public async Task History_UTCID06_OnlySave_CannotBeDeleted()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); sessions.GetSaveAsync(1).Returns(Save(c.Id, 1)); sessions.CountSavesAsync(c.Id).Returns(1); var r = await Sut.DeleteSaveAsync(c.Id, 1, c.OwnerId, false, false); Assert.False(r.Success); Assert.Contains("only save", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_UTCID07_DeleteOlderSave_DoesNotRollbackLiveState()
    {
        var c = Character(); repo.GetOwnerAndNameAsync(c.Id).Returns((c.OwnerId, c.Name)); var newest = Save(c.Id, 2); var older = Save(c.Id, 1); sessions.GetSaveAsync(1).Returns(older); sessions.CountSavesAsync(c.Id).Returns(2); sessions.GetSavesAsync(c.Id, 1, 1).Returns((new List<CharacterSaveEntity> { newest }, 2)); sessions.DeleteSaveAndRollBackAsync(older, null).Returns(true); var r = await Sut.DeleteSaveAsync(c.Id, 1, c.OwnerId, false, false); Assert.True(r.Success); Assert.False(r.Data!.WasCurrent); await sessions.Received(1).DeleteSaveAndRollBackAsync(older, null);
    }

    [Fact]
    public async Task Progress_UTCID06_AdminList_MapsRowsWithUnresolvedUsername()
    {
        var c = Character(); repo.GetPagedWithSnapshotsAsync(1, 20).Returns((new List<CharacterEntity> { c }, 1)); var r = await Sut.GetAllAsync(1, 20); Assert.Single(r.Items); Assert.Null(r.Items[0].OwnerUsername); Assert.Equal(c.Id, r.Items[0].Id);
    }

    [Fact]
    public async Task Progress_UTCID08_Owner_CanDeleteCharacter()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); Assert.True((await Sut.DeleteAsync(c.Id, c.OwnerId, false)).Success); await repo.Received(1).DeleteAsync(c);
    }

    [Fact]
    public async Task Progress_UTCID09_Admin_CanDeleteAnotherUsersCharacter()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); Assert.True((await Sut.DeleteAsync(c.Id, Guid.NewGuid(), true)).Success); await repo.Received(1).DeleteAsync(c);
    }

    [Fact]
    public async Task Progress_UTCID10_OtherUser_CannotDeleteCharacter()
    {
        var c = Character(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); var r = await Sut.DeleteAsync(c.Id, Guid.NewGuid(), false); Assert.False(r.Success); Assert.Contains("permission", r.Error!, StringComparison.OrdinalIgnoreCase); await repo.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task SynchronizationHistory_UTCID05_DetailWithoutSnapshots_ReturnsEmptyHistory()
    {
        var c = Character(); c.Snapshots.Clear(); repo.GetWithSnapshotsAsync(c.Id).Returns(c); Assert.Empty((await Sut.GetDetailAsync(c.Id))!.Snapshots);
    }

    private static CharacterSaveEntity Save(Guid character, long id) => new() { Id = id, CharacterId = character, CurrentLevel = 3, IsAlive = true, CapturedAt = DateTime.UtcNow.AddMinutes(id), Vitals = new(), Combat = new(), Position = new() };
}

internal sealed class Handler : HttpMessageHandler

{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}") }); }