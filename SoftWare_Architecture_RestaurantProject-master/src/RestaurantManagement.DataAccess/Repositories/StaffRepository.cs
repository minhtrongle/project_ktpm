using Microsoft.EntityFrameworkCore;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Repositories;

public interface IStaffRepository : IRepository<Staff>
{
    Task<Staff?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Staff?> GetByCodeAsync(string staffCode, CancellationToken cancellationToken = default);
}

public class StaffRepository : Repository<Staff>, IStaffRepository
{
    public StaffRepository(RestaurantDbContext context) : base(context)
    {
    }

    public async Task<Staff?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Staffs
            .SingleOrDefaultAsync(s => s.Username == username, cancellationToken);
    }

    public async Task<Staff?> GetByCodeAsync(string staffCode, CancellationToken cancellationToken = default)
    {
        return await _context.Staffs
            .SingleOrDefaultAsync(s => s.StaffCode == staffCode, cancellationToken);
    }
}
