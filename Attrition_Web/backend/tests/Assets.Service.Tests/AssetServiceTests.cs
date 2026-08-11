using Assets.Service.Models;
using Assets.Service.Repositories.Interface;
using Assets.Service.Services;
using Assets.Service.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;

namespace Assets.Service.Tests;

public class AssetServiceTests
{
    private readonly IAssetRepository repo = Substitute.For<IAssetRepository>(); private readonly IFileStorage storage = Substitute.For<IFileStorage>();

    private AssetService Sut(long mb = 1) => new(repo, storage, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "FileUpload:MaxImageSizeMB", mb.ToString() } }).Build(), NullLogger<AssetService>.Instance);

    private static Asset Asset() => new() { FileName = "image.png", FilePath = "/uploads/image.png", AssetType = "concept-art", MimeType = "image/png", FileSize = 8, UploadedByName = "player" };

    private static IFormFile File(string name = "image.png", byte[]? bytes = null) => new FormFile(new MemoryStream(bytes ?? new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }), 0, (bytes ?? new byte[8]).Length, "file", name);

    [Fact]
    public async Task Gallery_UTCID01_GetExisting_ReturnsDto()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); Assert.Equal(a.Id, (await Sut().GetAssetAsync(a.Id))!.Id);
    }

    [Fact]
    public async Task Gallery_UTCID02_GetUnknown_ReturnsNull()
    {
        Assert.Null(await Sut().GetAssetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Gallery_UTCID03_List_ReturnsPagedRows()
    {
        repo.GetPagedAsync(1, 20, Arg.Any<Expression<Func<Asset, bool>>?>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset> { Asset() }, 1)); var r = await Sut().ListAssetsAsync(1, 20, null, null); Assert.Single(r.Items); Assert.Equal(1, r.TotalCount);
    }

    [Fact]
    public async Task Gallery_UTCID04_TypeFilter_IsPassedToRepository()
    {
        repo.GetPagedAsync(1, 20, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset>(), 0)); await Sut().ListAssetsAsync(1, 20, "sprite", null); await repo.Received(1).GetPagedAsync(1, 20, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>());
    }

    [Fact]
    public async Task Gallery_UTCID05_Search_IsCaseNormalized()
    {
        repo.GetPagedAsync(1, 20, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset>(), 0)); await Sut().ListAssetsAsync(1, 20, null, "IMAGE");
    }

    [Fact]
    public async Task Upload_UTCID01_ValidPng_SavesFileAndRow()
    {
        storage.SaveAsync("assets", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/stored.png"); Asset? added = null; repo.AddAsync(Arg.Do<Asset>(x => added = x)).Returns(c => c.Arg<Asset>()); var r = await Sut().UploadAssetAsync(File(), "concept-art", "Title", "Description", "tag", Guid.NewGuid(), "player"); Assert.True(r.Success); Assert.Equal("image/png", added!.MimeType); Assert.Equal("/stored.png", added.FilePath);
    }

    [Fact]
    public async Task Upload_UTCID02_EmptyFile_FailsBeforeStorage()
    {
        var r = await Sut().UploadAssetAsync(File(bytes: Array.Empty<byte>()), "concept-art", null, null, null, Guid.NewGuid(), "p"); Assert.False(r.Success); await storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Upload_UTCID03_OversizedFile_Fails()
    {
        var r = await Sut().UploadAssetAsync(File(bytes: new byte[1024 * 1024 + 1]), "concept-art", null, null, null, Guid.NewGuid(), "p"); Assert.False(r.Success); Assert.Contains("maximum", r.Error!);
    }

    [Fact]
    public async Task Upload_UTCID04_InvalidImageExtension_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File("x.exe"), "concept-art", null, null, null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID05_SpoofedPngMagicBytes_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File(bytes: new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "concept-art", null, null, null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID06_DocumentAcceptsPdf()
    {
        storage.SaveAsync("documents", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/doc.pdf"); repo.AddAsync(Arg.Any<Asset>()).Returns(c => c.Arg<Asset>()); Assert.True((await Sut().UploadAssetAsync(File("x.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }), "document", null, null, null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID07_EmptyAssetType_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File(), "", null, null, null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID08_TooLongTitle_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File(), "concept-art", new string('x', 201), null, null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID09_TooLongDescription_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File(), "concept-art", null, new string('x', 2001), null, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID10_TooLongTags_Fails()
    {
        Assert.False((await Sut().UploadAssetAsync(File(), "concept-art", null, null, new string('x', 501), Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Upload_UTCID08_DbFailure_DeletesOrphanedFile()
    {
        storage.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>()).Returns("/orphan.png"); repo.AddAsync(Arg.Any<Asset>()).ThrowsAsync(new InvalidOperationException()); await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().UploadAssetAsync(File(), "sprite", null, null, null, Guid.NewGuid(), "p")); await storage.Received(1).DeleteAsync("/orphan.png");
    }

    [Fact]
    public async Task Upload_UTCID09_StorageFailure_DoesNotInsertRow()
    {
        storage.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>()).ThrowsAsync(new IOException()); await Assert.ThrowsAsync<IOException>(() => Sut().UploadAssetAsync(File(), "sprite", null, null, null, Guid.NewGuid(), "p")); await repo.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Edit_UTCID01_PatchesOnlySuppliedMetadata()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); await Sut().UpdateAssetAsync(a.Id, new("new", null, null, null)); Assert.Equal("new", a.Title); Assert.Equal("concept-art", a.AssetType); await repo.Received(1).UpdateAsync(a);
    }

    [Fact]
    public async Task Edit_UTCID02_AllFields_AreUpdated()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); await Sut().UpdateAssetAsync(a.Id, new("new", "desc", "tags", "sprite")); Assert.Equal("sprite", a.AssetType); Assert.NotNull(a.UpdatedAt);
    }

    [Fact]
    public async Task Edit_UTCID03_UnknownAsset_Fails()
    {
        Assert.False((await Sut().UpdateAssetAsync(Guid.NewGuid(), new("x", null, null, null))).Success);
    }

    [Fact]
    public async Task Replace_UTCID01_IdenticalUnityHash_ReturnsExistingWithoutSave()
    {
        var a = Asset(); a.ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })).ToLowerInvariant(); repo.GetBySourceAsync("unity-item", "iron_helm").Returns(a); Assert.True((await Sut().UploadUnitySourceAsync(File(), "item", "iron_helm", Guid.NewGuid(), "p")).Success); await storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Replace_UTCID02_NewUnitySource_CreatesTrackedAsset()
    {
        storage.SaveAsync("assets", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/new.png"); var r = await Sut().UploadUnitySourceAsync(File(), "enemy", "dragon", Guid.NewGuid(), "p"); Assert.True(r.Success); await repo.Received(1).AddTrackedAsync(Arg.Is<Asset>(x => x.SourceType == "unity-enemy")); await repo.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Replace_UTCID04_DifferentHash_UpdatesExistingAsset()
    {
        var a = Asset(); a.SourceType = "unity-item"; a.SourceId = "iron_helm"; a.ContentHash = "old"; repo.GetBySourceAsync("unity-item", "iron_helm").Returns(a); storage.SaveAsync("assets", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/replacement.png"); var r = await Sut().UploadUnitySourceAsync(File(), "item", "iron_helm", Guid.NewGuid(), "p"); Assert.True(r.Success); Assert.Equal("/replacement.png", a.FilePath); Assert.NotEqual("old", a.ContentHash); Assert.NotNull(a.UpdatedAt); await repo.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Replace_UTCID08_OverlongSourceId_Fails()
    {
        Assert.False((await Sut().UploadUnitySourceAsync(File(), "item", new string('a', 65), Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Replace_UTCID09_ConcurrentInsert_ReturnsWinnerAndDeletesLosingFile()
    {
        var winner = Asset(); winner.SourceType = "unity-item"; winner.SourceId = "iron_helm"; repo.GetBySourceAsync("unity-item", "iron_helm").Returns((Asset?)null, winner); storage.SaveAsync("assets", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/loser.png"); repo.SaveAsync().ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException()); var r = await Sut().UploadUnitySourceAsync(File(), "item", "iron_helm", Guid.NewGuid(), "p"); Assert.True(r.Success); Assert.Equal(winner.Id, r.Data!.Id); await storage.Received(1).DeleteAsync("/loser.png");
    }

    [Fact]
    public async Task Replace_UTCID03_InvalidSourceType_Fails()
    {
        Assert.False((await Sut().UploadUnitySourceAsync(File(), "wiki", "id", Guid.NewGuid(), "p")).Success);
    }

    [Theory]
    [InlineData("Bad Id")]
    [InlineData("")]
    public async Task Replace_InvalidSourceId_Fails(string id)
    {
        Assert.False((await Sut().UploadUnitySourceAsync(File(), "item", id, Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Delete_UTCID01_ExistingAsset_DeletesStorageThenRow()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); Assert.True((await Sut().DeleteAssetAsync(a.Id)).Success); Received.InOrder(() => { storage.DeleteAsync(a.FilePath); repo.DeleteAsync(a); });
    }

    [Fact]
    public async Task Delete_UTCID02_UnknownAsset_FailsWithoutStorage()
    {
        Assert.False((await Sut().DeleteAssetAsync(Guid.NewGuid())).Success); await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task Delete_StorageException_PreventsDbDeletion()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); storage.DeleteAsync(a.FilePath).ThrowsAsync(new IOException()); await Assert.ThrowsAsync<IOException>(() => Sut().DeleteAssetAsync(a.Id)); await repo.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task Gallery_UTCID05_TypeAndSearchFilters_AreCombined()
    {
        repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset> { Asset() }, 1)); var r = await Sut().ListAssetsAsync(1, 10, "concept-art", "image"); Assert.Single(r.Items);
    }

    [Fact]
    public async Task Gallery_UTCID06_NoSearchMatches_ReturnsEmptyWithTotal()
    {
        repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset>(), 0)); var r = await Sut().ListAssetsAsync(1, 10, null, "zzz"); Assert.Empty(r.Items); Assert.Equal(0, r.TotalCount);
    }

    [Fact]
    public async Task Gallery_UTCID07_UnknownType_ReturnsEmpty()
    {
        repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<Asset, bool>>>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset>(), 0)); Assert.Empty((await Sut().ListAssetsAsync(1, 10, "unknown", null)).Items);
    }

    [Fact]
    public async Task Gallery_UTCID08_PageBeyondLast_PreservesTotal()
    {
        repo.GetPagedAsync(99, 10, Arg.Any<Expression<Func<Asset, bool>>?>(), Arg.Any<Func<IQueryable<Asset>, IOrderedQueryable<Asset>>>()).Returns((new List<Asset>(), 25)); var r = await Sut().ListAssetsAsync(99, 10, null, null); Assert.Empty(r.Items); Assert.Equal(25, r.TotalCount);
    }

    [Fact]
    public async Task Library_UTCID01_LinkedAsset_MapsSourceAssociation()
    {
        var a = Asset(); a.SourceType = "unity-item"; a.SourceId = "iron_helm"; repo.GetByIdAsync(a.Id).Returns(a); var r = await Sut().GetAssetAsync(a.Id); Assert.Equal("unity-item", r!.SourceType); Assert.Equal("iron_helm", r.SourceId);
    }

    [Fact]
    public async Task Library_UTCID02_UnlinkedAsset_MapsNullAssociation()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); var r = await Sut().GetAssetAsync(a.Id); Assert.Null(r!.SourceType); Assert.Null(r.SourceId);
    }

    [Theory]
    [InlineData("UTCID04")]
    [InlineData("UTCID05")]
    public async Task Library_Count_ReturnsTotal(string _)
    {
        repo.CountAsync().Returns(12); Assert.Equal(12, await Sut().CountAsync());
    }

    [Fact]
    public async Task Edit_UTCID03_DescriptionOnly_PreservesOtherFields()
    {
        var a = Asset(); a.Title = "old"; repo.GetByIdAsync(a.Id).Returns(a); await Sut().UpdateAssetAsync(a.Id, new(null, "new desc", null, null)); Assert.Equal("old", a.Title); Assert.Equal("new desc", a.Description);
    }

    [Fact]
    public async Task Edit_UTCID04_AllFieldsOmitted_OnlyRefreshesTimestamp()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); await Sut().UpdateAssetAsync(a.Id, new(null, null, null, null)); Assert.NotNull(a.UpdatedAt); Assert.Equal("concept-art", a.AssetType);
    }

    [Fact]
    public async Task Edit_UTCID06_UnknownAssetWithEmptyPatch_Fails()
    {
        Assert.False((await Sut().UpdateAssetAsync(Guid.NewGuid(), new(null, null, null, null))).Success);
    }

    [Fact]
    public async Task Replace_UTCID02_NewSkillSource_CreatesSkillAssociation()
    {
        storage.SaveAsync("assets", Arg.Any<string>(), Arg.Any<Stream>()).Returns("/skill.png"); Assert.True((await Sut().UploadUnitySourceAsync(File(), "skill", "fireball", Guid.NewGuid(), "p")).Success); await repo.Received(1).AddTrackedAsync(Arg.Is<Asset>(x => x.SourceType == "unity-skill"));
    }

    [Fact]
    public async Task Replace_UTCID06_NonSnakeCaseSourceId_Fails()
    {
        Assert.False((await Sut().UploadUnitySourceAsync(File(), "item", "IronHelm", Guid.NewGuid(), "p")).Success);
    }

    [Fact]
    public async Task Delete_UTCID02_LinkedAsset_IsAlsoDeleted()
    {
        var a = Asset(); a.SourceType = "unity-enemy"; a.SourceId = "dragon"; repo.GetByIdAsync(a.Id).Returns(a); storage.DeleteAsync(a.FilePath).Returns(true); Assert.True((await Sut().DeleteAssetAsync(a.Id)).Success); await repo.Received(1).DeleteAsync(a);
    }

    [Fact]
    public async Task Delete_UTCID03_AlreadyMissingFile_StillDeletesRow()
    {
        var a = Asset(); repo.GetByIdAsync(a.Id).Returns(a); storage.DeleteAsync(a.FilePath).Returns(false); Assert.True((await Sut().DeleteAssetAsync(a.Id)).Success); await repo.Received(1).DeleteAsync(a);
    }
}