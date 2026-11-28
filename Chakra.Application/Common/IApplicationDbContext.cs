using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Chakra.Domain.Entities;

namespace Chakra.Application.Common;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Premi> Premis { get; }
    DbSet<Installment> Installments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<JobRun> JobRuns { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}