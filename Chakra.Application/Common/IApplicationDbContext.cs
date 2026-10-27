using Microsoft.EntityFrameworkCore;
using Chakra.Domain.Entities;

namespace Chakra.Application.Common;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Premi> Premis { get; }
    DbSet<Installment> Installments { get; }
    DbSet<OutboxMessage> OutboxMessage { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}