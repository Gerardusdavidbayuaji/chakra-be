using Microsoft.EntityFrameworkCore;
using Chakra.Domain.Entities;

namespace Chakra.Application.Common;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}