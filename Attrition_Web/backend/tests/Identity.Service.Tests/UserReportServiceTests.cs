using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using NSubstitute;

namespace Identity.Service.Tests;

public class UserReportServiceTests
{
    private readonly IUserReportRepository reports = Substitute.For<IUserReportRepository>(); private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private UserReportService Sut => new(reports, users);

    [Fact]
    public async Task Report_UTCID03_ExistingUser_CreatesTrimmedPendingReport()
    {
        var target = IdentityTestFixture.User(); var reporter = Guid.NewGuid(); users.GetByIdAsync(target.Id).Returns(target); UserReport? added = null; reports.AddAsync(Arg.Do<UserReport>(x => added = x)).Returns(c => c.Arg<UserReport>()); var r = await Sut.ReportUserAsync(target.Id, "  Spam  ", reporter, "reporter"); Assert.True(r.Success); Assert.Equal("Spam", added!.Reason); Assert.Equal("Pending", added.Status);
    }

    [Fact]
    public async Task Report_UTCID04_UnknownUser_Fails()
    {
        Assert.False((await Sut.ReportUserAsync(Guid.NewGuid(), "Spam", Guid.NewGuid(), "r")).Success);
    }

    [Fact]
    public async Task Report_UTCID05_DeletedUser_IsHidden()
    {
        var target = IdentityTestFixture.User(); target.IsDeleted = true; users.GetByIdAsync(target.Id).Returns(target); Assert.False((await Sut.ReportUserAsync(target.Id, "Spam", Guid.NewGuid(), "r")).Success);
    }

    [Fact]
    public async Task Report_UTCID06_BlankReason_FailsBeforeLookup()
    {
        var r = await Sut.ReportUserAsync(Guid.NewGuid(), "   ", Guid.NewGuid(), "r"); Assert.False(r.Success); Assert.Equal("A reason is required.", r.Error); await users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Report_UTCID07_SelfReport_Fails()
    {
        var id = Guid.NewGuid(); var r = await Sut.ReportUserAsync(id, "Spam", id, "self"); Assert.False(r.Success); Assert.Contains("yourself", r.Error!);
    }

    [Fact]
    public async Task Moderate_UTCID12_ResolveAndBan_RevokesSessions()
    {
        var target = IdentityTestFixture.User(); var report = new UserReport { ReportedUserId = target.Id }; reports.GetByIdAsync(report.Id).Returns(report); users.GetByIdAsync(target.Id).Returns(target); var r = await Sut.ResolveAsync(report.Id, true, "  confirmed  ", "admin"); Assert.True(r.Success); Assert.True(target.IsBanned); Assert.Null(target.Refresh.Token); Assert.Equal("Banned", report.ActionTaken); Assert.Equal("confirmed", report.ModeratorNote);
    }

    [Fact]
    public async Task Moderate_UTCID13_ResolveWithoutBan_RecordsNoAction()
    {
        var report = new UserReport(); reports.GetByIdAsync(report.Id).Returns(report); Assert.True((await Sut.ResolveAsync(report.Id, false, null, "admin")).Success); Assert.Equal("Resolved", report.Status); Assert.Equal("None", report.ActionTaken);
    }

    [Fact]
    public async Task Moderate_UTCID14_UnknownReport_Fails()
    {
        var r = await Sut.ResolveAsync(Guid.NewGuid(), true, null, "admin"); Assert.False(r.Success); Assert.Equal("Report not found.", r.Error);
    }
}