using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using NSubstitute;
using Wiki.Service.DTOs;
using Wiki.Service.Models;
using Wiki.Service.Repositories.Interface;
using Wiki.Service.Services;

namespace Wiki.Service.Tests;

public class WikiServiceTests
{
    private readonly IWikiRepository _repo = Substitute.For<IWikiRepository>();
    private readonly IRepository<WikiCategory> _categories = Substitute.For<IRepository<WikiCategory>>();
    private readonly IRepository<WikiRevision> _revisions = Substitute.For<IRepository<WikiRevision>>();
    private readonly IRepository<WikiContribution> _contributions = Substitute.For<IRepository<WikiContribution>>();
    private readonly Cache _cache = new();
    private WikiService Sut => new(_repo, _cache);

    public WikiServiceTests()
    {
        _repo.Categories.Returns(_categories); _repo.Revisions.Returns(_revisions); _repo.Contributions.Returns(_contributions);
        _repo.ExecuteInTransactionAsync(Arg.Any<Func<Task>>()).Returns(c => { c.Arg<Func<Task>>()(); return Task.CompletedTask; });
        _repo.ExecuteInTransactionAsync(Arg.Any<Func<Task<BuildingBlocks.Contracts.ApiResponse<string>>>>())
            .Returns(c => c.Arg<Func<Task<BuildingBlocks.Contracts.ApiResponse<string>>>>()());
        _repo.ExecuteInTransactionAsync(Arg.Any<Func<Task<BuildingBlocks.Contracts.ApiResponse>>>())
            .Returns(c => c.Arg<Func<Task<BuildingBlocks.Contracts.ApiResponse>>>()());
    }

    private static WikiCategory Category(int id = 1) => new() { Id = id, Name = "Lore", Slug = "lore", Description = "World lore" };

    private static WikiArticle Article(string status = ArticleStatus.Published) => new() { Title = "The First Flame", Slug = "first-flame", CategoryId = 1, Content = "content", Status = status, CreatedByName = "admin" };

    private static WikiContribution Contribution(string status = ContributionStatus.Pending) => new() { ArticleId = Guid.NewGuid(), ContributorId = Guid.NewGuid(), ContributorName = "player", SuggestedContent = "new content", Status = status };

    [Fact]
    public async Task Categories_UTCID01_ReturnsCategoriesWithPublishedCounts()
    {
        _repo.GetCategoriesAsync().Returns(new List<WikiCategory> { Category() }); _repo.CountPublishedArticlesByCategoryAsync().Returns(new Dictionary<int, int> { { 1, 3 } }); var r = await Sut.GetCategoriesAsync(); Assert.Single(r); Assert.Equal(3, r[0].ArticleCount);
    }

    [Fact]
    public async Task Categories_UTCID02_MissingCount_DefaultsToZero()
    {
        _repo.GetCategoriesAsync().Returns(new List<WikiCategory> { Category() }); _repo.CountPublishedArticlesByCategoryAsync().Returns(new Dictionary<int, int>()); Assert.Equal(0, (await Sut.GetCategoriesAsync())[0].ArticleCount);
    }

    [Fact]
    public async Task Categories_UTCID03_EmptyDatabase_ReturnsEmpty()
    {
        _repo.GetCategoriesAsync().Returns(new List<WikiCategory>()); _repo.CountPublishedArticlesByCategoryAsync().Returns(new Dictionary<int, int>()); Assert.Empty(await Sut.GetCategoriesAsync());
    }

    [Fact]
    public async Task Categories_UTCID04_WarmCache_SkipsRepository()
    {
        _cache.Seed("categories", new List<WikiCategoryDto> { new(1, "Lore", "lore", "", null, 2) }); Assert.Single(await Sut.GetCategoriesAsync()); await _repo.DidNotReceive().GetCategoriesAsync();
    }

    [Fact]
    public async Task Article_UTCID01_PublishedSlug_ReturnsSanitizedStoredArticle()
    {
        var a = Article(); _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { a }, 1)); _repo.GetCategoryByIdAsync(1).Returns(Category()); var r = await Sut.GetArticleBySlugAsync(a.Slug); Assert.NotNull(r); Assert.Equal("lore", r.CategorySlug);
    }

    [Fact]
    public async Task Article_UTCID02_UnknownSlug_ReturnsNull()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); Assert.Null(await Sut.GetArticleBySlugAsync("unknown"));
    }

    [Fact]
    public async Task Article_UTCID03_AdminCanIncludeDraft()
    {
        var a = Article(ArticleStatus.Draft); _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { a }, 1)); Assert.NotNull(await Sut.GetArticleBySlugAsync(a.Slug, true));
    }

    [Fact]
    public async Task Revision_UTCID01_History_IsMappedNewestFirst()
    {
        var a = Article(); var r = new WikiRevision { ArticleId = a.Id, Content = "old" }; _revisions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiRevision, bool>>>(), Arg.Any<Func<IQueryable<WikiRevision>, IOrderedQueryable<WikiRevision>>>()).Returns(new List<WikiRevision> { r }); Assert.Equal(r.Id, (await Sut.GetRevisionsAsync(a.Id))[0].Id);
    }

    [Fact]
    public async Task Revision_UTCID02_MatchingRevision_ReturnsIt()
    {
        var a = Article(); var r = new WikiRevision { ArticleId = a.Id }; _revisions.GetByIdAsync(r.Id).Returns(r); Assert.NotNull(await Sut.GetRevisionByIdAsync(a.Id, r.Id));
    }

    [Fact]
    public async Task Revision_UTCID03_WrongArticle_ReturnsNull()
    {
        var r = new WikiRevision { ArticleId = Guid.NewGuid() }; _revisions.GetByIdAsync(r.Id).Returns(r); Assert.Null(await Sut.GetRevisionByIdAsync(Guid.NewGuid(), r.Id));
    }

    [Fact]
    public async Task Revision_UTCID04_UnknownRevision_ReturnsNull()
    {
        Assert.Null(await Sut.GetRevisionByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Submit_UTCID01_ExistingArticle_AddsSanitizedContribution()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); WikiContribution? added = null; _contributions.AddAsync(Arg.Do<WikiContribution>(x => added = x)).Returns(c => c.Arg<WikiContribution>()); var r = await Sut.SubmitContributionAsync(a.Id, new("<script>x</script><b>safe</b>", "note"), Guid.NewGuid(), "player"); Assert.True(r.Success); Assert.DoesNotContain("script", added!.SuggestedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UTCID02")]
    [InlineData("UTCID06")]
    public async Task Submit_UnknownArticle_Fails(string _)
    {
        var r = await Sut.SubmitContributionAsync(Guid.NewGuid(), new("text", null), Guid.NewGuid(), "player"); Assert.False(r.Success); Assert.Equal("Article not found.", r.Error);
    }

    [Fact]
    public async Task Contributions_UTCID01_FiltersByStatusAndMapsArticle()
    {
        var a = Article(); var c = Contribution(); c.ArticleId = a.Id; _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>(), Arg.Any<Func<IQueryable<WikiContribution>, IOrderedQueryable<WikiContribution>>>()).Returns(new List<WikiContribution> { c }); _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle> { a }); var r = await Sut.GetContributionsAsync(ContributionStatus.Pending); Assert.Single(r); Assert.Equal(a.Title, r[0].ArticleTitle);
    }

    [Fact]
    public async Task Contributions_UTCID02_DeletedArticle_MapsEmptyArticleFields()
    {
        var c = Contribution(); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>(), Arg.Any<Func<IQueryable<WikiContribution>, IOrderedQueryable<WikiContribution>>>()).Returns(new List<WikiContribution> { c }); _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); Assert.Equal("", (await Sut.GetContributionsAsync(ContributionStatus.Pending))[0].ArticleTitle);
    }

    [Fact]
    public async Task Contributions_UTCID03_NoRows_ReturnsEmpty()
    {
        _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>(), Arg.Any<Func<IQueryable<WikiContribution>, IOrderedQueryable<WikiContribution>>>()).Returns(new List<WikiContribution>()); _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); Assert.Empty(await Sut.GetContributionsAsync(ContributionStatus.Pending));
    }

    [Fact]
    public async Task Approve_UTCID01_AppliesContentAndCreatesRevision()
    {
        var a = Article(); var c = Contribution(); c.ArticleId = a.Id; _contributions.GetByIdAsync(c.Id).Returns(c); _repo.GetByIdAsync(a.Id).Returns(a); var r = await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Approved), Guid.NewGuid()); Assert.True(r.Success); Assert.Equal("new content", a.Content); Assert.Equal(ContributionStatus.Approved, c.Status); await _revisions.Received(1).AddAsync(Arg.Any<WikiRevision>());
    }

    [Fact]
    public async Task Approve_UTCID02_DeletedTarget_FailsWithoutReview()
    {
        var c = Contribution(); _contributions.GetByIdAsync(c.Id).Returns(c); var r = await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Approved), Guid.NewGuid()); Assert.False(r.Success); Assert.Equal(ContributionStatus.Pending, c.Status);
    }

    [Theory]
    [InlineData(ContributionStatus.Approved, "UTCID03")]
    [InlineData(ContributionStatus.Rejected, "UTCID04")]
    public async Task Review_AlreadyReviewed_Fails(string status, string _)
    {
        var c = Contribution(status); _contributions.GetByIdAsync(c.Id).Returns(c); Assert.False((await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Approved), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Reject_UTCID01_MarksRejectedWithoutChangingArticle()
    {
        var a = Article(); var c = Contribution(); c.ArticleId = a.Id; _contributions.GetByIdAsync(c.Id).Returns(c); var r = await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Rejected), Guid.NewGuid()); Assert.True(r.Success); Assert.Equal(ContributionStatus.Rejected, c.Status); Assert.Equal("content", a.Content); await _revisions.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Reject_UTCID02_InvalidStatus_Fails()
    {
        var r = await Sut.ReviewContributionAsync(Guid.NewGuid(), new("Maybe"), Guid.NewGuid()); Assert.False(r.Success); Assert.Equal("Invalid status.", r.Error);
    }

    [Fact]
    public async Task CategoryManage_UTCID01_Create_GeneratesSlugAndInvalidates()
    {
        _categories.TryAddAsync(Arg.Any<WikiCategory>()).Returns(true); var r = await Sut.CreateCategoryAsync(new("Game Lore", null, null)); Assert.True(r.Success); await _categories.Received(1).TryAddAsync(Arg.Is<WikiCategory>(x => x.Slug == "game-lore")); Assert.Contains("categories", _cache.Removed);
    }

    [Theory]
    [InlineData(true, "UTCID02")]
    [InlineData(false, "UTCID03")]
    public async Task CategoryManage_DuplicateOrRace_Fails(bool preexisting, string _)
    {
        if (preexisting) _repo.GetCategoryBySlugAsync("game-lore").Returns(Category()); else _categories.TryAddAsync(Arg.Any<WikiCategory>()).Returns(false); Assert.False((await Sut.CreateCategoryAsync(new("Game Lore", null, null))).Success);
    }

    [Fact]
    public async Task CategoryManage_UTCID04_Update_ChangesNameAndSlug()
    {
        var c = Category(); _repo.GetCategoryByIdAsync(c.Id).Returns(c); Assert.True((await Sut.UpdateCategoryAsync(c.Id, new("History", null, null))).Success); Assert.Equal("history", c.Slug);
    }

    [Fact]
    public async Task CategoryManage_UTCID05_UpdateUnknown_Fails()
    {
        Assert.False((await Sut.UpdateCategoryAsync(99, new("Name", null, null))).Success);
    }

    [Fact]
    public async Task CategoryManage_UTCID06_DeleteEmpty_Deletes()
    {
        var c = Category(); _repo.GetCategoryByIdAsync(c.Id).Returns(c); _repo.CountArticlesInCategoryAsync(c.Id).Returns(0); Assert.Equal((true, false), await Sut.DeleteCategoryAsync(c.Id)); await _categories.Received(1).DeleteAsync(c);
    }

    [Fact]
    public async Task CategoryManage_UTCID07_DeleteUsed_IsBlocked()
    {
        var c = Category(); _repo.GetCategoryByIdAsync(c.Id).Returns(c); _repo.CountArticlesInCategoryAsync(c.Id).Returns(2); Assert.Equal((true, true), await Sut.DeleteCategoryAsync(c.Id));
    }

    [Fact] public async Task CategoryManage_UTCID08_DeleteUnknown_ReturnsNotFound() => Assert.Equal((false, false), await Sut.DeleteCategoryAsync(99));

    [Fact]
    public async Task ArticleManage_UTCID01_Create_AddsArticleAndInitialRevision()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); _categories.GetByIdAsync(1).Returns(Category()); _repo.AddAsync(Arg.Any<WikiArticle>()).Returns(c => c.Arg<WikiArticle>()); _revisions.AddAsync(Arg.Any<WikiRevision>()).Returns(c => c.Arg<WikiRevision>()); var r = await Sut.CreateArticleAsync(new("New Article", 1, "<script>x</script>safe", ArticleStatus.Published), Guid.NewGuid(), "admin"); Assert.True(r.Success); Assert.Equal("new-article", r.Data); await _revisions.Received(1).AddAsync(Arg.Any<WikiRevision>());
    }

    [Fact]
    public async Task ArticleManage_UTCID02_DuplicateSlug_Fails()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { Article() }, 1)); Assert.False((await Sut.CreateArticleAsync(new("The First Flame", 1, "x", ArticleStatus.Published), Guid.NewGuid(), "admin")).Success);
    }

    [Fact]
    public async Task ArticleManage_UTCID03_UnknownCategory_Fails()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); Assert.False((await Sut.CreateArticleAsync(new("New", 99, "x", ArticleStatus.Published), Guid.NewGuid(), "admin")).Success);
    }

    [Fact]
    public async Task ArticleManage_UTCID04_Update_SavesPreviousRevision()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); var r = await Sut.UpdateArticleAsync(a.Id, new(null, "new", null, "note"), Guid.NewGuid(), "editor"); Assert.True(r.Success); Assert.Equal("new", a.Content); await _revisions.Received(1).AddAsync(Arg.Is<WikiRevision>(x => x.Content == "content"));
    }

    [Fact]
    public async Task ArticleManage_UTCID05_UpdateUnknown_Fails()
    {
        Assert.False((await Sut.UpdateArticleAsync(Guid.NewGuid(), new("x", null, null, null), Guid.NewGuid(), "admin")).Success);
    }

    [Fact]
    public async Task ArticleManage_UTCID06_DeleteExisting_DeletesAndInvalidates()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); Assert.True((await Sut.DeleteArticleAsync(a.Id)).Success); await _repo.Received(1).DeleteAsync(a);
    }

    [Fact]
    public async Task ArticleManage_UTCID07_DeleteUnknown_Fails()
    {
        Assert.False((await Sut.DeleteArticleAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Activity_UTCID01_MergesAuthoredAndApprovedNewestFirst()
    {
        var uid = Guid.NewGuid(); var a = Article(); a.CreatedById = uid; a.CreatedAt = DateTime.UtcNow.AddDays(-1); var c = Contribution(ContributionStatus.Approved); c.ContributorId = uid; c.ArticleId = a.Id; c.ReviewedAt = DateTime.UtcNow; _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle> { a }); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(new List<WikiContribution> { c }); var r = await Sut.GetUserContributionsAsync(uid); Assert.Equal(2, r.Count); Assert.Equal("Edited", r[0].Kind);
    }

    [Fact]
    public async Task Submit_UTCID03_ScriptMarkup_IsRemoved()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); WikiContribution? added = null; _contributions.AddAsync(Arg.Do<WikiContribution>(x => added = x)).Returns(c => c.Arg<WikiContribution>()); await Sut.SubmitContributionAsync(a.Id, new("<script>bad</script>safe", "note"), Guid.NewGuid(), "player"); Assert.DoesNotContain("script", added!.SuggestedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_UTCID04_EmptySuggestion_IsStored()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); WikiContribution? added = null; _contributions.AddAsync(Arg.Do<WikiContribution>(x => added = x)).Returns(c => c.Arg<WikiContribution>()); Assert.True((await Sut.SubmitContributionAsync(a.Id, new("", "note"), Guid.NewGuid(), "player")).Success); Assert.Equal("", added!.SuggestedContent);
    }

    [Fact]
    public async Task Submit_UTCID05_NullChangeNote_IsPreserved()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); WikiContribution? added = null; _contributions.AddAsync(Arg.Do<WikiContribution>(x => added = x)).Returns(c => c.Arg<WikiContribution>()); await Sut.SubmitContributionAsync(a.Id, new("text", null), Guid.NewGuid(), "player"); Assert.Null(added!.ChangeNote);
    }

    [Theory]
    [InlineData(ContributionStatus.Approved, "UTCID04")]
    [InlineData(ContributionStatus.Rejected, "UTCID05")]
    [InlineData("Unknown", "UTCID06")]
    public async Task Contributions_StatusFilter_IsPassedThrough(string status, string _)
    {
        _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>(), Arg.Any<Func<IQueryable<WikiContribution>, IOrderedQueryable<WikiContribution>>>()).Returns(new List<WikiContribution>()); _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); Assert.Empty(await Sut.GetContributionsAsync(status));
    }

    [Fact]
    public async Task Contributions_UTCID07_ZeroRows_ReturnsEmpty()
    {
        _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>(), Arg.Any<Func<IQueryable<WikiContribution>, IOrderedQueryable<WikiContribution>>>()).Returns(new List<WikiContribution>()); _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); Assert.Empty(await Sut.GetContributionsAsync(ContributionStatus.Pending));
    }

    [Fact]
    public async Task Contributions_UTCID08_PendingCount_IsForwarded()
    {
        _contributions.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(4); Assert.Equal(4, await Sut.CountPendingContributionsAsync());
    }

    [Fact]
    public async Task Approve_UTCID04_UnknownContribution_Fails()
    {
        Assert.False((await Sut.ReviewContributionAsync(Guid.NewGuid(), new(ContributionStatus.Approved), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Approve_UTCID06_InvalidStatus_FailsBeforeLookup()
    {
        var r = await Sut.ReviewContributionAsync(Guid.NewGuid(), new("Invalid"), Guid.NewGuid()); Assert.False(r.Success); Assert.Equal("Invalid status.", r.Error); await _contributions.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Approve_UTCID07_InvalidStatusWinsOverReviewedState()
    {
        var c = Contribution(ContributionStatus.Approved); _contributions.GetByIdAsync(c.Id).Returns(c); var r = await Sut.ReviewContributionAsync(c.Id, new("Invalid"), Guid.NewGuid()); Assert.Equal("Invalid status.", r.Error);
    }

    [Fact]
    public async Task Reject_UTCID02_OmittedReason_IsAcceptedBecauseRequestHasNoReasonField()
    {
        var c = Contribution(); _contributions.GetByIdAsync(c.Id).Returns(c); Assert.True((await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Rejected), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Reject_UTCID03_AlreadyRejected_Fails()
    {
        var c = Contribution(ContributionStatus.Rejected); _contributions.GetByIdAsync(c.Id).Returns(c); Assert.False((await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Rejected), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Reject_UTCID04_UnknownContribution_Fails()
    {
        Assert.False((await Sut.ReviewContributionAsync(Guid.NewGuid(), new(ContributionStatus.Rejected), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Reject_UTCID05_DeletedArticle_DoesNotBlockRejection()
    {
        var c = Contribution(); _contributions.GetByIdAsync(c.Id).Returns(c); Assert.True((await Sut.ReviewContributionAsync(c.Id, new(ContributionStatus.Rejected), Guid.NewGuid())).Success); Assert.Equal(ContributionStatus.Rejected, c.Status);
    }

    [Fact]
    public async Task Categories_UTCID06_ArticleWrite_InvalidatesWarmCategoryCache()
    {
        _cache.Seed("categories", new List<WikiCategoryDto>()); var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); await Sut.UpdateArticleAsync(a.Id, new(null, "new", null, null), Guid.NewGuid(), "admin"); Assert.Contains("categories", _cache.Removed);
    }

    [Fact]
    public async Task Categories_UTCID07_CategoryRename_InvalidatesWarmCache()
    {
        _cache.Seed("categories", new List<WikiCategoryDto>()); var c = Category(); _repo.GetCategoryByIdAsync(c.Id).Returns(c); await Sut.UpdateCategoryAsync(c.Id, new("Renamed", null, null)); Assert.Contains("categories", _cache.Removed);
    }

    [Fact]
    public async Task Article_UTCID04_AdminRouteAlsoReturnsPublishedArticle()
    {
        var a = Article(); _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { a }, 1)); Assert.NotNull(await Sut.GetArticleBySlugAsync(a.Slug, true));
    }

    [Fact]
    public async Task Article_UTCID05_UnknownSlug_ReturnsNull()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); Assert.Null(await Sut.GetArticleBySlugAsync("unknown"));
    }

    [Fact]
    public async Task Article_UTCID06_EmptySlug_ReturnsNull()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); Assert.Null(await Sut.GetArticleBySlugAsync(""));
    }

    [Fact]
    public async Task Article_UTCID07_DeletedCategory_MapsEmptySlug()
    {
        var a = Article(); _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { a }, 1)); Assert.Equal("", (await Sut.GetArticleBySlugAsync(a.Slug))!.CategorySlug);
    }

    [Fact]
    public async Task Revision_UTCID02_InitialCreationRevision_IsReturned()
    {
        var a = Article(); var r = new WikiRevision { ArticleId = a.Id, Content = a.Content, ChangeNote = "Initial creation" }; _revisions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiRevision, bool>>>(), Arg.Any<Func<IQueryable<WikiRevision>, IOrderedQueryable<WikiRevision>>>()).Returns(new List<WikiRevision> { r }); Assert.Equal("Initial creation", (await Sut.GetRevisionsAsync(a.Id))[0].ChangeNote);
    }

    [Fact]
    public async Task Revision_UTCID06_SoleRevisionFromOtherArticle_ReturnsNull()
    {
        var r = new WikiRevision { ArticleId = Guid.NewGuid() }; _revisions.GetByIdAsync(r.Id).Returns(r); Assert.Null(await Sut.GetRevisionByIdAsync(Guid.NewGuid(), r.Id));
    }

    [Fact]
    public async Task CategoryManage_UTCID06_RenameToExistingSlug_Fails()
    {
        var c = Category(); _repo.GetCategoryByIdAsync(c.Id).Returns(c); _repo.GetCategoryBySlugAsync("history").Returns(new WikiCategory { Id = 2, Slug = "history" }); Assert.False((await Sut.UpdateCategoryAsync(c.Id, new("History", null, null))).Success);
    }

    [Fact]
    public async Task ArticleManage_UTCID04_CreateSanitizesUnsafeContent()
    {
        _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle>(), 0)); _categories.GetByIdAsync(1).Returns(Category()); WikiArticle? added = null; _repo.AddAsync(Arg.Do<WikiArticle>(x => added = x)).Returns(c => c.Arg<WikiArticle>()); _revisions.AddAsync(Arg.Any<WikiRevision>()).Returns(c => c.Arg<WikiRevision>()); await Sut.CreateArticleAsync(new("New", 1, "<script>bad</script>safe", ArticleStatus.Published), Guid.NewGuid(), "admin"); Assert.DoesNotContain("script", added!.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArticleManage_UTCID06_NullTitle_KeepsSlug()
    {
        var a = Article(); var slug = a.Slug; _repo.GetByIdAsync(a.Id).Returns(a); await Sut.UpdateArticleAsync(a.Id, new(null, "new", null, null), Guid.NewGuid(), "admin"); Assert.Equal(slug, a.Slug);
    }

    [Fact]
    public async Task ArticleManage_UTCID07_UpdateToClashingSlug_FailsBeforeRevision()
    {
        var a = Article(); _repo.GetByIdAsync(a.Id).Returns(a); _repo.GetPagedAsync(1, 1, Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns((new List<WikiArticle> { Article() }, 1)); var r = await Sut.UpdateArticleAsync(a.Id, new("Taken", null, null, null), Guid.NewGuid(), "admin"); Assert.False(r.Success); await _revisions.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Activity_UTCID02_OnlyAuthoredArticles_AreReturned()
    {
        var uid = Guid.NewGuid(); var a = Article(); a.CreatedById = uid; _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle> { a }); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(new List<WikiContribution>()); var r = await Sut.GetUserContributionsAsync(uid); Assert.Single(r); Assert.Equal("Authored", r[0].Kind);
    }

    [Fact]
    public async Task Activity_UTCID03_NoActivity_ReturnsEmpty()
    {
        _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(new List<WikiContribution>()); Assert.Empty(await Sut.GetUserContributionsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Activity_UTCID04_DraftOnly_IsExcludedByRepositoryFilter()
    {
        _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(new List<WikiContribution>()); Assert.Empty(await Sut.GetUserContributionsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Activity_UTCID05_ApprovedEditForDeletedArticle_IsSkipped()
    {
        var uid = Guid.NewGuid(); var c = Contribution(ContributionStatus.Approved); c.ContributorId = uid; _repo.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(new List<WikiArticle>()); _contributions.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(new List<WikiContribution> { c }); Assert.Empty(await Sut.GetUserContributionsAsync(uid));
    }

    [Fact]
    public async Task Activity_UTCID06_Count_AddsAuthoredAndApproved()
    {
        _repo.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(2); _contributions.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(1); Assert.Equal(3, await Sut.CountUserContributionsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Activity_UTCID07_DraftOnlyCount_IsZero()
    {
        _repo.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiArticle, bool>>>()).Returns(0); _contributions.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WikiContribution, bool>>>()).Returns(0); Assert.Equal(0, await Sut.CountUserContributionsAsync(Guid.NewGuid()));
    }
}

internal sealed class Cache : ICacheService
{
    private readonly Dictionary<string, object> v = new(); internal List<string> Removed { get; } = new(); internal void Seed<T>(string k, T x) => v[k] = x!;

    public async Task<T> GetOrSetAsync<T>(string k, Func<Task<T>> f, TimeSpan? t = null, CancellationToken c = default)
    { if (v.TryGetValue(k, out var x)) return (T)x; var r = await f(); v[k] = r!; return r; }

    public Task<T?> GetAsync<T>(string k, CancellationToken c = default) => Task.FromResult(v.TryGetValue(k, out var x) ? (T?)x : default); public Task SetAsync<T>(string k, T x, TimeSpan? t = null, CancellationToken c = default)

    { v[k] = x!; return Task.CompletedTask; }

    public Task RemoveAsync(string k, CancellationToken c = default)
    { Removed.Add(k); v.Remove(k); return Task.CompletedTask; }

    public Task RemoveByPrefixAsync(string p, CancellationToken c = default) => Task.CompletedTask; public Task<long?> IncrementAsync(string k, long b = 1, TimeSpan? t = null, CancellationToken c = default) => Task.FromResult<long?>(null);
}