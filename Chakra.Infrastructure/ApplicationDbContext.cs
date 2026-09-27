using Microsoft.EntityFrameworkCore;
using Chakra.Application.Common;
using Chakra.Domain.Entities;

namespace Chakra.Infrastructure;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    } 
    
    public DbSet<User> Users {get; set;}
    public DbSet<Premi> Premis {get; set;}
    public DbSet<Installment> Installments {get; set;}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}