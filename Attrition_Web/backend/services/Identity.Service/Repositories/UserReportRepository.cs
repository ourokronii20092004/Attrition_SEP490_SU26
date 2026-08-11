using BuildingBlocks.Persistence;
using Identity.Service.Data;
using Identity.Service.Models;

namespace Identity.Service.Repositories;

public class UserReportRepository(IdentityDbContext context) : Repository<UserReport>(context), IUserReportRepository
{
}