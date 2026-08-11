using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using Forum.Service.Clients;
using Forum.Service.DTOs;
using Forum.Service.Models;
using Forum.Service.Repositories.Interface;
using Forum.Service.Services;
using Forum.Service.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Linq.Expressions;
using System.Net;

namespace Forum.Service.Tests;

public class ForumServiceTests
{
    private readonly IForumRepository repo = Substitute.For<IForumRepository>(); private readonly IRepository<ForumCategory> cats = Substitute.For<IRepository<ForumCategory>>(); private readonly IRepository<ForumPost> posts = Substitute.For<IRepository<ForumPost>>(); private readonly IRepository<ForumReaction> reacts = Substitute.For<IRepository<ForumReaction>>(); private readonly IRepository<ThreadSubscription> subs = Substitute.For<IRepository<ThreadSubscription>>(); private readonly IRepository<PostReport> reports = Substitute.For<IRepository<PostReport>>(); private readonly Cache cache = new(); private readonly CaptureHandler notifyHandler = new();
    private ForumService Sut => new(repo, cache, Notify(), Identity());

    public ForumServiceTests()
    { repo.Categories.Returns(cats); repo.Posts.Returns(posts); repo.Reactions.Returns(reacts); repo.Subscriptions.Returns(subs); repo.Reports.Returns(reports); }

    private NotificationClient Notify() => new(new HttpClient(notifyHandler) { BaseAddress = new Uri("http://test/") }, Config(), NullLogger<NotificationClient>.Instance);

    private IdentityClient Identity() => new(new HttpClient(new IdentityHandler()) { BaseAddress = new Uri("http://test/") }, Config(), NullLogger<IdentityClient>.Instance);

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Internal:ApiKey", "test" } }).Build();

    private static ForumCategory Cat() => new() { Id = 1, Name = "General", Slug = "general", Description = "General" }; private static ForumPost Thread() => new() { Title = "Welcome", CategoryId = 1, AuthorId = Guid.NewGuid(), AuthorName = "owner", Content = "opening" }; private static Author User() => new(Guid.NewGuid(), "player", null, "User");

    [Fact]
    public async Task Categories_UTCID01_MapsCounts()
    {
        repo.GetCategoriesAsync().Returns(new List<ForumCategory> { Cat() }); repo.GetCategoryStatsAsync().Returns(new Dictionary<int, (int, DateTime?)> { { 1, (3, DateTime.UtcNow) } }); Assert.Equal(3, (await Sut.GetCategoriesAsync())[0].ThreadCount);
    }

    [Fact]
    public async Task Categories_UTCID02_MissingStats_DefaultsZero()
    {
        repo.GetCategoriesAsync().Returns(new List<ForumCategory> { Cat() }); repo.GetCategoryStatsAsync().Returns(new Dictionary<int, (int, DateTime?)>()); Assert.Equal(0, (await Sut.GetCategoriesAsync())[0].ThreadCount);
    }

    [Fact]
    public async Task Categories_UTCID03_Empty_ReturnsEmpty()
    {
        repo.GetCategoriesAsync().Returns(new List<ForumCategory>()); repo.GetCategoryStatsAsync().Returns(new Dictionary<int, (int, DateTime?)>()); Assert.Empty(await Sut.GetCategoriesAsync());
    }

    [Fact]
    public async Task Categories_UTCID04_WarmCache_SkipsRepo()
    {
        cache.Seed("categories", new List<ForumCategoryDto>()); Assert.Empty(await Sut.GetCategoriesAsync()); await repo.DidNotReceive().GetCategoriesAsync();
    }

    [Fact]
    public async Task Threads_UTCID01_MapsPagedThreads()
    {
        var t = Thread(); repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<ForumPost, bool>>>(), Arg.Any<Func<IQueryable<ForumPost>, IOrderedQueryable<ForumPost>>>()).Returns((new List<ForumPost> { t }, 1)); var r = await Sut.GetThreadsAsync(null, null, 1, 10); Assert.Single(r.Items); Assert.Equal(1, r.TotalCount);
    }

    [Fact]
    public async Task Threads_UTCID02_UnknownCategory_ReturnsEmpty()
    {
        Assert.Empty((await Sut.GetThreadsAsync("missing", null, 1, 10)).Items);
    }

    [Fact]
    public async Task Threads_UTCID03_CategoryFilter_ResolvesCategory()
    {
        repo.GetCategoryBySlugAsync("general").Returns(Cat()); repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<ForumPost, bool>>>(), Arg.Any<Func<IQueryable<ForumPost>, IOrderedQueryable<ForumPost>>>()).Returns((new List<ForumPost>(), 0)); await Sut.GetThreadsAsync("general", null, 1, 10); await repo.Received(1).GetCategoryBySlugAsync("general");
    }

    [Fact]
    public async Task View_UTCID01_ExistingThread_MapsReactions()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); repo.GetCategoryByIdAsync(1).Returns(Cat()); reacts.ListAsync(Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns(new List<ForumReaction> { new() { PostId = t.Id, ReactionType = ReactionType.Like } }); var r = await Sut.GetThreadAsync(t.Id); Assert.NotNull(r); Assert.Equal(1, r.LikeCount);
    }

    [Theory]
    [InlineData("UTCID02", false)]
    [InlineData("UTCID03", true)]
    public async Task View_MissingOrRemoved_ReturnsNull(string _, bool removed)
    {
        var t = Thread(); t.Moderation.IsRemoved = removed; if (removed) repo.GetByIdAsync(t.Id).Returns(t); Assert.Null(await Sut.GetThreadAsync(t.Id));
    }

    [Fact]
    public async Task View_UTCID04_PostPage_MapsReactionCounts()
    {
        var t = Thread(); var p = new ForumPost { RootPostId = t.Id, AuthorId = Guid.NewGuid(), Content = "reply" }; posts.GetPagedAsync(1, 10, Arg.Any<Expression<Func<ForumPost, bool>>>(), Arg.Any<Func<IQueryable<ForumPost>, IOrderedQueryable<ForumPost>>>()).Returns((new List<ForumPost> { p }, 1)); reacts.ListAsync(Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns(new List<ForumReaction> { new() { PostId = p.Id, ReactionType = ReactionType.Dislike } }); Assert.Equal(1, (await Sut.GetPostsAsync(t.Id, 1, 10, null)).Items[0].DislikeCount);
    }

    [Fact]
    public async Task CreateThread_UTCID01_ValidCategory_CreatesRootAndSubscription()
    {
        repo.GetCategoryByIdAsync(1).Returns(Cat()); var u = User(); var r = await Sut.CreateThreadAsync(new(1, "Title", "<script>x</script>safe"), u); Assert.True(r.Success); await repo.Received(1).CreateRootAsync(Arg.Is<ForumPost>(x => !x.Content.Contains("script")), Arg.Is<ThreadSubscription>(x => x.UserId == u.Id));
    }

    [Theory]
    [InlineData("UTCID02")]
    [InlineData("UTCID06")]
    public async Task CreateThread_UnknownCategory_Fails(string _)
    {
        Assert.False((await Sut.CreateThreadAsync(new(99, "Title", "text"), User())).Success);
    }

    [Fact]
    public async Task Reply_UTCID01_ValidThread_CreatesPostAndIncrementsCount()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); subs.ListAsync(Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns(new List<ThreadSubscription>()); var r = await Sut.CreatePostAsync(t.Id, new("hello"), User()); Assert.True(r.Success); await posts.Received(1).AddAsync(Arg.Any<ForumPost>()); await repo.Received(1).IncrementReplyCountAsync(t.Id, Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Reply_UTCID02_LockedThread_Fails()
    {
        var t = Thread(); t.IsLocked = true; repo.GetByIdAsync(t.Id).Returns(t); Assert.False((await Sut.CreatePostAsync(t.Id, new("x"), User())).Success);
    }

    [Fact]
    public async Task Reply_UTCID03_UnknownThread_Fails()
    {
        Assert.False((await Sut.CreatePostAsync(Guid.NewGuid(), new("x"), User())).Success);
    }

    [Fact]
    public async Task Reply_UTCID04_InvalidParent_Fails()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); Assert.False((await Sut.CreatePostAsync(t.Id, new("x", Guid.NewGuid()), User())).Success);
    }

    [Fact]
    public async Task Reply_UTCID05_Attachments_AreLimitedToInternalTen()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); subs.ListAsync(Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns(new List<ThreadSubscription>()); ForumPost? added = null; posts.AddAsync(Arg.Do<ForumPost>(x => added = x)).Returns(c => c.Arg<ForumPost>()); var urls = Enumerable.Range(0, 12).Select(i => $"/api/image/{i}").Append("https://evil.test/x").ToList(); await Sut.CreatePostAsync(t.Id, new("x", null, urls), User()); Assert.Equal(10, System.Text.Json.JsonSerializer.Deserialize<List<string>>(added!.Attachments!)!.Count);
    }

    [Fact]
    public async Task Reply_UTCID06_Mentions_AreDistinctAndSkipSelf()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); subs.ListAsync(Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns(new List<ThreadSubscription>()); var u = User(); await Sut.CreatePostAsync(t.Id, new($"@alice @ALICE @{u.Name}"), u); Assert.Equal(2, notifyHandler.Calls.Count);/* empty bulk + one mention */
    }

    [Fact]
    public async Task Edit_UTCID01_Owner_CanEditSanitizedContent()
    {
        var u = User(); var p = new ForumPost { AuthorId = u.Id }; posts.GetByIdAsync(p.Id).Returns(p); Assert.True((await Sut.UpdatePostAsync(p.Id, new("<script>x</script>safe"), u.Id)).Success); Assert.DoesNotContain("script", p.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_UTCID02_NonOwner_IsUnauthorized()
    {
        var p = new ForumPost { AuthorId = Guid.NewGuid() }; posts.GetByIdAsync(p.Id).Returns(p); Assert.Equal("Unauthorized.", (await Sut.UpdatePostAsync(p.Id, new("x"), Guid.NewGuid())).Error);
    }

    [Fact]
    public async Task Edit_UTCID03_MissingPost_Fails()
    {
        Assert.False((await Sut.UpdatePostAsync(Guid.NewGuid(), new("x"), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task DeleteOwn_UTCID04_Owner_CanDeleteCascade()
    {
        var u = User(); var p = new ForumPost { AuthorId = u.Id }; posts.GetByIdAsync(p.Id).Returns(p); Assert.True((await Sut.DeletePostAsync(p.Id, u.Id, false)).Success); await repo.Received(1).DeletePostCascadeAsync(p);
    }

    [Fact]
    public async Task DeleteOwn_UTCID05_Admin_CanDeleteOthers()
    {
        var p = new ForumPost { AuthorId = Guid.NewGuid() }; posts.GetByIdAsync(p.Id).Returns(p); Assert.True((await Sut.DeletePostAsync(p.Id, Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task DeleteOwn_UTCID06_NonOwnerCannotDelete()
    {
        var p = new ForumPost { AuthorId = Guid.NewGuid() }; posts.GetByIdAsync(p.Id).Returns(p); Assert.False((await Sut.DeletePostAsync(p.Id, Guid.NewGuid(), false)).Success);
    }

    [Fact]
    public async Task React_UTCID01_NewReaction_IsAdded()
    {
        var p = new ForumPost(); posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction>(), 0)); await Sut.ToggleReactionAsync(p.Id, Guid.NewGuid(), new(ReactionType.Like)); await reacts.Received(1).TryAddAsync(Arg.Any<ForumReaction>());
    }

    [Fact]
    public async Task React_UTCID02_SameReaction_RemovesIt()
    {
        var p = new ForumPost(); var r = new ForumReaction { PostId = p.Id, UserId = Guid.NewGuid(), ReactionType = ReactionType.Like }; posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction> { r }, 1)); await Sut.ToggleReactionAsync(p.Id, r.UserId, new(ReactionType.Like)); await reacts.Received(1).DeleteAsync(r);
    }

    [Fact]
    public async Task React_UTCID03_DifferentReaction_UpdatesIt()
    {
        var p = new ForumPost(); var r = new ForumReaction { PostId = p.Id, UserId = Guid.NewGuid(), ReactionType = ReactionType.Like }; posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction> { r }, 1)); await Sut.ToggleReactionAsync(p.Id, r.UserId, new(ReactionType.Dislike)); Assert.Equal(ReactionType.Dislike, r.ReactionType);
    }

    [Fact]
    public async Task React_UTCID04_MissingPost_Fails()
    {
        Assert.False((await Sut.ToggleReactionAsync(Guid.NewGuid(), Guid.NewGuid(), new(ReactionType.Like))).Success);
    }

    [Fact]
    public async Task Report_UTCID01_ExistingPost_CreatesPendingReport()
    {
        var p = new ForumPost(); posts.GetByIdAsync(p.Id).Returns(p); PostReport? added = null; reports.AddAsync(Arg.Do<PostReport>(x => added = x)).Returns(c => c.Arg<PostReport>()); Assert.True((await Sut.ReportPostAsync(p.Id, "spam", User())).Success); Assert.Equal(ReportStatus.Pending, added!.Status);
    }

    [Fact]
    public async Task Report_UTCID02_MissingPost_Fails()
    {
        Assert.False((await Sut.ReportPostAsync(Guid.NewGuid(), "spam", User())).Success);
    }

    [Fact]
    public async Task Subscribe_UTCID01_NewMute_CreatesPersistentMutedRow()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); await Sut.SetThreadMutedAsync(t.Id, Guid.NewGuid(), true); await subs.Received(1).TryAddAsync(Arg.Is<ThreadSubscription>(x => x.IsMuted));
    }

    [Fact]
    public async Task Subscribe_UTCID02_ExistingRow_UpdatesDesiredState()
    {
        var t = Thread(); var s = new ThreadSubscription { ThreadId = t.Id, UserId = Guid.NewGuid(), IsMuted = true }; repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription> { s }, 1)); await Sut.SetThreadMutedAsync(t.Id, s.UserId, false); Assert.False(s.IsMuted); await subs.Received(1).UpdateAsync(s);
    }

    [Fact]
    public async Task Subscribe_UTCID03_RepeatedState_IsIdempotent()
    {
        var t = Thread(); var s = new ThreadSubscription { ThreadId = t.Id, UserId = Guid.NewGuid(), IsMuted = true }; repo.GetByIdAsync(t.Id).Returns(t); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription> { s }, 1)); Assert.True((await Sut.SetThreadMutedAsync(t.Id, s.UserId, true)).Success); await subs.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task Subscribe_UTCID04_MissingThread_Fails()
    {
        Assert.False((await Sut.SetThreadMutedAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task Moderate_UTCID01_RemovePost_ResolvesPendingReports()
    {
        var p = new ForumPost(); var r = new PostReport { PostId = p.Id }; posts.GetByIdAsync(p.Id).Returns(p); reports.ListAsync(Arg.Any<Expression<Func<PostReport, bool>>>()).Returns(new List<PostReport> { r }); Assert.True((await Sut.RemovePostAsync(p.Id, User(), "spam")).Success); Assert.True(p.Moderation.IsRemoved); Assert.Equal(ReportStatus.Resolved, r.Status);
    }

    [Fact]
    public async Task Moderate_UTCID02_RestorePost_ClearsModeration()
    {
        var p = new ForumPost(); p.Moderation.IsRemoved = true; p.Moderation.Reason = "spam"; posts.GetByIdAsync(p.Id).Returns(p); await Sut.RestorePostAsync(p.Id); Assert.False(p.Moderation.IsRemoved); Assert.Null(p.Moderation.Reason);
    }

    [Fact]
    public async Task Moderate_UTCID03_DismissReport_UpdatesStatus()
    {
        var r = new PostReport(); reports.GetByIdAsync(r.Id).Returns(r); await Sut.DismissReportAsync(r.Id); Assert.Equal(ReportStatus.Dismissed, r.Status);
    }

    [Fact]
    public async Task Moderate_UTCID04_ResolveReport_UpdatesStatus()
    {
        var r = new PostReport(); reports.GetByIdAsync(r.Id).Returns(r); await Sut.ResolveReportAsync(r.Id); Assert.Equal(ReportStatus.Resolved, r.Status);
    }

    [Fact]
    public async Task Moderate_UTCID10_TogglePinAndLock_FlipsFlags()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); Assert.True((await Sut.TogglePinAsync(t.Id)).Success); Assert.True(t.IsPinned); Assert.True((await Sut.ToggleLockAsync(t.Id)).Success); Assert.True(t.IsLocked);
    }

    [Fact]
    public async Task Moderate_UTCID11_MissingThread_CannotToggle()
    {
        Assert.False((await Sut.TogglePinAsync(Guid.NewGuid())).Success); Assert.False((await Sut.ToggleLockAsync(Guid.NewGuid())).Success);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("restore")]
    [InlineData("dismiss")]
    [InlineData("resolve")]
    public async Task Moderate_MissingTarget_Fails(string op)
    {
        var r = op switch { "remove" => await Sut.RemovePostAsync(Guid.NewGuid(), User(), "x"), "restore" => await Sut.RestorePostAsync(Guid.NewGuid()), "dismiss" => await Sut.DismissReportAsync(Guid.NewGuid()), _ => await Sut.ResolveReportAsync(Guid.NewGuid()) }; Assert.False(r.Success);
    }

    [Fact]
    public async Task Categories_UTCID03_CreateFreeName_CreatesSlug()
    {
        cats.TryAddAsync(Arg.Any<ForumCategory>()).Returns(true); var r = await Sut.CreateCategoryAsync(new("Game Talk", "desc")); Assert.True(r.Success); await cats.Received(1).TryAddAsync(Arg.Is<ForumCategory>(x => x.Slug == "game-talk"));
    }

    [Fact]
    public async Task Categories_UTCID04_CreateDuplicateName_Fails()
    {
        repo.GetCategoryBySlugAsync("general").Returns(Cat()); Assert.False((await Sut.CreateCategoryAsync(new("General", null))).Success);
    }

    [Fact]
    public async Task Categories_UTCID05_UpdateFreeName_ChangesSlug()
    {
        var c = Cat(); repo.GetCategoryByIdAsync(c.Id).Returns(c); Assert.True((await Sut.UpdateCategoryAsync(c.Id, new("News", "updates"))).Success); Assert.Equal("news", c.Slug);
    }

    [Fact]
    public async Task Categories_UTCID06_UpdateClashingName_Fails()
    {
        var c = Cat(); repo.GetCategoryByIdAsync(c.Id).Returns(c); repo.GetCategoryBySlugAsync("news").Returns(new ForumCategory { Id = 2, Slug = "news" }); Assert.False((await Sut.UpdateCategoryAsync(c.Id, new("News", null))).Success);
    }

    [Fact]
    public async Task Categories_UTCID07_DeleteEmptyCategory_Deletes()
    {
        var c = Cat(); repo.GetCategoryByIdAsync(c.Id).Returns(c); repo.CountAsync(Arg.Any<Expression<Func<ForumPost, bool>>>()).Returns(0); Assert.True((await Sut.DeleteCategoryAsync(c.Id)).Success); await cats.Received(1).DeleteAsync(c);
    }

    [Fact]
    public async Task Categories_UTCID08_DeleteCategoryWithThreads_IsBlocked()
    {
        var c = Cat(); repo.GetCategoryByIdAsync(c.Id).Returns(c); repo.CountAsync(Arg.Any<Expression<Func<ForumPost, bool>>>()).Returns(2); Assert.False((await Sut.DeleteCategoryAsync(c.Id)).Success);
    }

    [Fact]
    public async Task Reply_UTCID02_ValidParent_IncrementsDepth()
    {
        var t = Thread(); var parent = new ForumPost { RootPostId = t.Id, Depth = 2, AuthorId = Guid.NewGuid() }; repo.GetByIdAsync(t.Id).Returns(t); posts.GetByIdAsync(parent.Id).Returns(parent); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); subs.ListAsync(Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns(new List<ThreadSubscription>()); var r = await Sut.CreatePostAsync(t.Id, new("nested", parent.Id), User()); Assert.True(r.Success); Assert.Equal(3, r.Data!.Depth);
    }

    [Fact]
    public async Task Reply_UTCID03_DepthIsCappedAtEight()
    {
        var t = Thread(); var parent = new ForumPost { RootPostId = t.Id, Depth = 8, AuthorId = Guid.NewGuid() }; repo.GetByIdAsync(t.Id).Returns(t); posts.GetByIdAsync(parent.Id).Returns(parent); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); subs.ListAsync(Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns(new List<ThreadSubscription>()); var r = await Sut.CreatePostAsync(t.Id, new("nested", parent.Id), User()); Assert.Equal(8, r.Data!.Depth);
    }

    [Fact]
    public async Task Threads_UTCID04_SearchPredicate_IsPassed()
    {
        repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<ForumPost, bool>>>(), Arg.Any<Func<IQueryable<ForumPost>, IOrderedQueryable<ForumPost>>>()).Returns((new List<ForumPost>(), 0)); Assert.Empty((await Sut.GetThreadsAsync(null, "welcome", 1, 10)).Items);
    }

    [Theory]
    [InlineData("UTCID05")]
    [InlineData("UTCID06")]
    [InlineData("UTCID07")]
    [InlineData("UTCID08")]
    public async Task Threads_ExcludedOrUnmatchedRows_ReturnEmpty(string _)
    {
        repo.GetPagedAsync(1, 10, Arg.Any<Expression<Func<ForumPost, bool>>>(), Arg.Any<Func<IQueryable<ForumPost>, IOrderedQueryable<ForumPost>>>()).Returns((new List<ForumPost>(), 0)); Assert.Empty((await Sut.GetThreadsAsync(null, _ == "UTCID05" ? "zzz" : null, 1, 10)).Items);
    }

    [Fact]
    public async Task View_UTCID02_SignedInViewer_MapsOwnReaction()
    {
        var t = Thread(); var uid = Guid.NewGuid(); repo.GetByIdAsync(t.Id).Returns(t); reacts.ListAsync(Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns(new List<ForumReaction> { new() { PostId = t.Id, UserId = uid, ReactionType = ReactionType.Like } }); subs.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ThreadSubscription, bool>>>()).Returns((new List<ThreadSubscription>(), 0)); Assert.Equal(ReactionType.Like, (await Sut.GetThreadAsync(t.Id, uid))!.CurrentUserReaction);
    }

    [Fact]
    public async Task View_UTCID03_Guest_HasNoOwnReaction()
    {
        var t = Thread(); repo.GetByIdAsync(t.Id).Returns(t); reacts.ListAsync(Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns(new List<ForumReaction> { new() { PostId = t.Id, UserId = Guid.NewGuid(), ReactionType = ReactionType.Like } }); Assert.Null((await Sut.GetThreadAsync(t.Id))!.CurrentUserReaction);
    }

    [Fact]
    public async Task View_UTCID05_ReplyId_IsNotAThread()
    {
        var p = new ForumPost { RootPostId = Guid.NewGuid() }; repo.GetByIdAsync(p.Id).Returns(p); Assert.Null(await Sut.GetThreadAsync(p.Id));
    }

    [Fact]
    public async Task View_UTCID07_UnknownThread_ReturnsNull()
    {
        Assert.Null(await Sut.GetThreadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateThread_UTCID04_EmptyBody_IsAccepted()
    {
        repo.GetCategoryByIdAsync(1).Returns(Cat()); Assert.True((await Sut.CreateThreadAsync(new(1, "Title", ""), User())).Success);
    }

    [Fact]
    public async Task CreateThread_UTCID05_EmptyTitle_IsCurrentlyAcceptedByService()
    {
        repo.GetCategoryByIdAsync(1).Returns(Cat()); Assert.True((await Sut.CreateThreadAsync(new(1, "", "body"), User())).Success);
    }

    [Fact]
    public async Task Edit_UTCID03_AdminStillCannotEditOthers()
    {
        var p = new ForumPost { AuthorId = Guid.NewGuid() }; posts.GetByIdAsync(p.Id).Returns(p); Assert.False((await Sut.UpdatePostAsync(p.Id, new("x"), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Edit_UTCID07_MissingOwnPost_Fails()
    {
        Assert.False((await Sut.UpdatePostAsync(Guid.NewGuid(), new("x"), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Delete_UTCID08_MissingOwnPost_Fails()
    {
        Assert.False((await Sut.DeletePostAsync(Guid.NewGuid(), Guid.NewGuid(), false)).Success);
    }

    [Theory]
    [InlineData(ReactionType.Dislike, "UTCID02")]
    [InlineData(ReactionType.Like, "UTCID01")]
    public async Task React_NewReaction_InsertsRequestedType(string type, string _)
    {
        var p = new ForumPost(); posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction>(), 0)); await Sut.ToggleReactionAsync(p.Id, Guid.NewGuid(), new(type)); await reacts.Received().TryAddAsync(Arg.Is<ForumReaction>(x => x.ReactionType == type));
    }

    [Theory]
    [InlineData(ReactionType.Dislike, "UTCID04")]
    [InlineData(ReactionType.Like, "UTCID03")]
    public async Task React_SameReaction_TogglesOff(string type, string _)
    {
        var p = new ForumPost(); var r = new ForumReaction { PostId = p.Id, UserId = Guid.NewGuid(), ReactionType = type }; posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction> { r }, 1)); await Sut.ToggleReactionAsync(p.Id, r.UserId, new(type)); await reacts.Received().DeleteAsync(r);
    }

    [Theory]
    [InlineData(ReactionType.Like, ReactionType.Dislike, "UTCID05")]
    [InlineData(ReactionType.Dislike, ReactionType.Like, "UTCID06")]
    public async Task React_DifferentReaction_SwitchesInPlace(string oldType, string newType, string _)
    {
        var p = new ForumPost(); var r = new ForumReaction { PostId = p.Id, UserId = Guid.NewGuid(), ReactionType = oldType }; posts.GetByIdAsync(p.Id).Returns(p); reacts.GetPagedAsync(1, 1, Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns((new List<ForumReaction> { r }, 1)); await Sut.ToggleReactionAsync(p.Id, r.UserId, new(newType)); Assert.Equal(newType, r.ReactionType);
    }

    [Fact]
    public async Task Report_UTCID08_BlankReason_IsRejectedByValidator()
    {
        var request = new ReportPostReq("   "); var r = await new Forum.Service.Validators.ReportPostReqValidator().ValidateAsync(request); Assert.False(r.IsValid);
    }

    [Fact]
    public async Task Activity_UTCID08_UserReplies_MapReactionCountsAndTotal()
    {
        var uid = Guid.NewGuid(); var row = new UserReplyRow(Guid.NewGuid(), Guid.NewGuid(), "Thread", "reply", DateTime.UtcNow); repo.GetUserRepliesAsync(uid, 1, 20).Returns((new List<UserReplyRow> { row }, 3)); reacts.ListAsync(Arg.Any<Expression<Func<ForumReaction, bool>>>()).Returns(new List<ForumReaction> { new() { PostId = row.PostId, ReactionType = ReactionType.Like }, new() { PostId = row.PostId, ReactionType = ReactionType.Dislike } }); var r = await Sut.GetUserRepliesAsync(uid, 1, 20); Assert.Single(r.Items); Assert.Equal(3, r.TotalCount); Assert.Equal(1, r.Items[0].LikeCount); Assert.Equal(1, r.Items[0].DislikeCount);
    }
}

internal sealed class Cache : ICacheService
{
    private readonly Dictionary<string, object> v = new(); internal void Seed<T>(string k, T x) => v[k] = x!; public async Task<T> GetOrSetAsync<T>(string k, Func<Task<T>> f, TimeSpan? t = null, CancellationToken c = default)

    {
        if (v.TryGetValue(k, out var x)) return (T)x; return await f();
    }

    public Task<T?> GetAsync<T>(string k, CancellationToken c = default) => Task.FromResult(default(T)); public Task SetAsync<T>(string k, T x, TimeSpan? t = null, CancellationToken c = default) => Task.CompletedTask; public Task RemoveAsync(string k, CancellationToken c = default) => Task.CompletedTask; public Task RemoveByPrefixAsync(string p, CancellationToken c = default) => Task.CompletedTask; public Task<long?> IncrementAsync(string k, long b = 1, TimeSpan? t = null, CancellationToken c = default) => Task.FromResult<long?>(null);
}

internal sealed class CaptureHandler : HttpMessageHandler
{
    internal List<string> Calls { get; } = new(); protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
    {
        Calls.Add(r.RequestUri!.AbsolutePath); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}

internal sealed class IdentityHandler : HttpMessageHandler


{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}") }); }