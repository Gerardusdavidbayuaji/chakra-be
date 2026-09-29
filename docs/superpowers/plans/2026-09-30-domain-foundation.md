# Domain Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menambahkan base class entity, strongly typed IDs, domain events, dan interceptor EF Core (timestamp + dispatch event) ke `chakra-be` tanpa mengubah kontrak API maupun schema database.

**Architecture:** Building block domain (`Entity<TId>`, `AuditableEntity<TId>`, `Aggregate<TId>`, `IDomainEvent`, typed IDs) di `Chakra.Domain/Entities/Common`. EF Core memetakan typed ID ke kolom `uuid` lewat `ValueConverter` yang didaftarkan di `ConfigureConventions`. Dua `SaveChangesInterceptor` di Infrastructure: satu mem-publish domain event lewat MediatR **sebelum** commit, satu mengisi `CreatedAt`/`UpdatedAt`. Application/API tetap memakai `Guid` di DTO/route dan mengonversi ke typed ID di handler.

**Tech Stack:** .NET 9, EF Core 9.0.1 + Npgsql 9.0.2, MediatR 14.1.0 (+ MediatR.Contracts 2.0.1), Mapster 10.0.0-pre02, Gridify 2.20.0.

**Spec:** `docs/superpowers/specs/2026-09-30-domain-foundation-design.md`

## Global Constraints

- Kontrak API tidak berubah: route, request DTO, dan response DTO tetap memakai `Guid`; response JSON `id` tetap string GUID (bukan object `{ "value": ... }`).
- Kolom ID tetap `uuid`; tidak boleh ada migration yang mengubah schema.
- Typed ID berbentuk `readonly record struct XxxId(Guid Value)` dengan `New()` memakai `Guid.CreateVersion7()`. Tanpa library typed ID.
- Domain event di-dispatch di `SavingChangesAsync` (sebelum commit). Handler event tidak boleh memanggil API eksternal.
- Urutan interceptor: `DispatchDomainEventsInterceptor` lalu `AuditableEntityInterceptor`.
- `User.ChatId` tidak diubah.
- Tidak ada project unit test (di luar scope); verifikasi via build, pengecekan migration, dan smoke test.
- Semua perintah dijalankan dari root repo `C:\David\3_Self_Development\chakra-be`.
- Style kode mengikuti codebase: constructor klasik (bukan primary constructor), field `_camelCase`, file-scoped namespace.
- Setiap commit diakhiri baris `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## File Structure

| File | Aksi | Tanggung jawab |
|---|---|---|
| `Chakra.Domain/Chakra.Domain.csproj` | Modify | Tambah `MediatR.Contracts` |
| `Chakra.Domain/Entities/Common/IEntity.cs` | Create | Kontrak entity ber-Id |
| `Chakra.Domain/Entities/Common/Entity.cs` | Create | Base entity + equality berdasarkan Id |
| `Chakra.Domain/Entities/Common/AuditableEntity.cs` | Create | `IAuditableEntity` + base dengan timestamp |
| `Chakra.Domain/Entities/Common/IDomainEvent.cs` | Create | Marker event (MediatR `INotification`) |
| `Chakra.Domain/Entities/Common/IAggregate.cs` | Create | Kontrak penampung event |
| `Chakra.Domain/Entities/Common/Aggregate.cs` | Create | Base aggregate + list event |
| `Chakra.Domain/Entities/Common/StronglyTypedIds.cs` | Create | `UserId`, `PremiId`, `InstallmentId` |
| `Chakra.Domain/Events/InstallmentPaidEvent.cs` | Create | Definisi event pembayaran |
| `Chakra.Domain/Entities/{User,Premi,Installment}.cs` | Modify | Pakai base class + typed ID |
| `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs` | Create | `ValueConverter` typed ID ↔ Guid |
| `Chakra.Infrastructure/ApplicationDbContext.cs` | Modify | `ConfigureConventions` |
| `Chakra.Infrastructure/Configuration/PremiConfiguration.cs` | Create | Key Premi `ValueGeneratedNever` |
| `Chakra.Infrastructure/Configuration/{User,Installment}Configuration.cs` | Modify | Key `ValueGeneratedNever` |
| `Chakra.Infrastructure/Interceptors/AuditableEntityInterceptor.cs` | Create | Isi timestamp |
| `Chakra.Infrastructure/Interceptors/DispatchDomainEventsInterceptor.cs` | Create | Publish domain event sebelum commit |
| `Chakra.Infrastructure/Services/CurrentUserService.cs` | Modify | `DatabaseUserId : UserId?` |
| `Chakra.Application/Mappers/MapsterConfig.cs` | Create | Mapping typed ID → Guid |
| `Chakra.Application/Common/ICurrentUserService.cs` | Modify | `DatabaseUserId : UserId?` |
| `Chakra.Application/Features/**` (7 handler) | Modify | Konversi Guid → typed ID, hapus timestamp manual |
| `Chakra.API/Configurations/{Persistence,Application}Setup.cs` | Modify | Registrasi interceptor, TimeProvider, Mapster |
| `Chakra.API/Endpoints/AuthEndpoint.cs` | Modify | Serialisasi `Id` sebagai Guid |
| `Chakra.API/Middlewares/EnsureUserMiddleware.cs` | Modify | `UserId.New()`, hapus timestamp manual |

---

### Task 1: Domain building blocks

Menambah seluruh building block di Domain. Entity yang ada belum disentuh, jadi solution tetap build.

**Files:**
- Modify: `Chakra.Domain/Chakra.Domain.csproj`
- Create: `Chakra.Domain/Entities/Common/IEntity.cs`, `Entity.cs`, `AuditableEntity.cs`, `IDomainEvent.cs`, `IAggregate.cs`, `Aggregate.cs`, `StronglyTypedIds.cs`
- Create: `Chakra.Domain/Events/InstallmentPaidEvent.cs`

**Interfaces:**
- Consumes: —
- Produces (namespace `Chakra.Domain.Entities.Common` kecuali disebut lain):
  - `interface IEntity<out TId> { TId Id { get; } }`
  - `abstract class Entity<TId> : IEntity<TId> where TId : struct` — `TId Id { get; set; }`
  - `interface IAuditableEntity { DateTime CreatedAt { get; set; } DateTime UpdatedAt { get; set; } }`
  - `abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity where TId : struct`
  - `interface IDomainEvent : MediatR.INotification`
  - `interface IAggregate { IReadOnlyCollection<IDomainEvent> DomainEvents { get; } void ClearDomainEvents(); }`
  - `abstract class Aggregate<TId> : AuditableEntity<TId>, IAggregate where TId : struct` — `protected void AddDomainEvent(IDomainEvent)`
  - `readonly record struct UserId(Guid Value)`, `PremiId(Guid Value)`, `InstallmentId(Guid Value)` — masing-masing `static New()`
  - `Chakra.Domain.Events.InstallmentPaidEvent(InstallmentId InstallmentId, PremiId PremiId) : IDomainEvent`

- [ ] **Step 1: Tambah paket MediatR.Contracts ke Domain**

Ganti isi `Chakra.Domain/Chakra.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="MediatR.Contracts" Version="2.0.1" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Versi 2.0.1 sama dengan dependency transitif MediatR 14.1.0 di `Chakra.Application`, jadi tidak ada konflik versi.

- [ ] **Step 2: Buat `IEntity.cs` dan `Entity.cs`**

`Chakra.Domain/Entities/Common/IEntity.cs`:

```csharp
namespace Chakra.Domain.Entities.Common;

public interface IEntity<out TId>
{
    TId Id { get; }
}
```

`Chakra.Domain/Entities/Common/Entity.cs`:

```csharp
namespace Chakra.Domain.Entities.Common;

public abstract class Entity<TId> : IEntity<TId>
    where TId : struct
{
    public TId Id { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        // Entity yang belum punya Id hanya sama dengan dirinya sendiri
        if (Id.Equals(default(TId)) || other.Id.Equals(default(TId))) return false;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
```

- [ ] **Step 3: Buat `AuditableEntity.cs`**

`Chakra.Domain/Entities/Common/AuditableEntity.cs`:

```csharp
namespace Chakra.Domain.Entities.Common;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : struct
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 4: Buat `IDomainEvent.cs`, `IAggregate.cs`, `Aggregate.cs`**

`Chakra.Domain/Entities/Common/IDomainEvent.cs`:

```csharp
using MediatR;

namespace Chakra.Domain.Entities.Common;

public interface IDomainEvent : INotification;
```

`Chakra.Domain/Entities/Common/IAggregate.cs`:

```csharp
namespace Chakra.Domain.Entities.Common;

public interface IAggregate
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
```

`Chakra.Domain/Entities/Common/Aggregate.cs`:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace Chakra.Domain.Entities.Common;

public abstract class Aggregate<TId> : AuditableEntity<TId>, IAggregate
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

- [ ] **Step 5: Buat `StronglyTypedIds.cs`**

`Chakra.Domain/Entities/Common/StronglyTypedIds.cs`:

```csharp
namespace Chakra.Domain.Entities.Common;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct PremiId(Guid Value)
{
    public static PremiId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct InstallmentId(Guid Value)
{
    public static InstallmentId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
```

- [ ] **Step 6: Buat `InstallmentPaidEvent.cs`**

`Chakra.Domain/Events/InstallmentPaidEvent.cs`:

```csharp
using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Events;

public sealed record InstallmentPaidEvent(InstallmentId InstallmentId, PremiId PremiId) : IDomainEvent;
```

- [ ] **Step 7: Build solution**

Run: `dotnet build Chakra.sln`
Expected: `Build succeeded` dengan `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add Chakra.Domain/Chakra.Domain.csproj Chakra.Domain/Entities/Common Chakra.Domain/Events
git commit -m "feat: add domain building blocks and typed ids

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Migrasi entity ke base class dan typed ID

Mengubah `User`, `Premi`, `Installment` ke base class. Karena tipe `Id` berubah, seluruh pemakai (EF, handler, API) disesuaikan dalam task yang sama agar solution kembali build. Timestamp manual **masih dipertahankan** di task ini (dihapus di Task 3 setelah interceptor ada).

**Files:**
- Modify: `Chakra.Domain/Entities/User.cs`, `Premi.cs`, `Installment.cs`
- Create: `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs`
- Create: `Chakra.Infrastructure/Configuration/PremiConfiguration.cs`
- Modify: `Chakra.Infrastructure/ApplicationDbContext.cs`
- Modify: `Chakra.Infrastructure/Configuration/UserConfiguration.cs`, `InstallmentConfiguration.cs`
- Modify: `Chakra.Infrastructure/Services/CurrentUserService.cs`
- Create: `Chakra.Application/Mappers/MapsterConfig.cs`
- Modify: `Chakra.Application/Common/ICurrentUserService.cs`
- Modify: `Chakra.Application/Features/Premi/Command/CreatePremi.cs`, `UpdatePremi.cs`, `CancelPremi.cs`
- Modify: `Chakra.Application/Features/Premi/Queries/GetPremiById.cs`
- Modify: `Chakra.Application/Features/Installments/Queries/GetInstallmentById.cs`, `GetAllInstallmentByPremiId.cs`
- Modify: `Chakra.Application/Features/Users/Queries/GetUsers.cs`
- Modify: `Chakra.API/Configurations/ApplicationSetup.cs`
- Modify: `Chakra.API/Endpoints/AuthEndpoint.cs`
- Modify: `Chakra.API/Middlewares/EnsureUserMiddleware.cs`

**Interfaces:**
- Consumes: semua tipe dari Task 1.
- Produces:
  - `User : AuditableEntity<UserId>`; `Premi : Aggregate<PremiId>` dengan `UserId UserId`; `Installment : Aggregate<InstallmentId>` dengan `PremiId PremiId`.
  - `Chakra.Infrastructure.Common.UserIdConverter`, `PremiIdConverter`, `InstallmentIdConverter` (`ValueConverter<XxxId, Guid>`).
  - `Chakra.Application.Mappers.MapsterConfig.RegisterMappings()` (static void).
  - `ICurrentUserService.DatabaseUserId : UserId?`.

- [ ] **Step 1: Ubah entity**

`Chakra.Domain/Entities/User.cs`:

```csharp
using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Entities;

public class User : AuditableEntity<UserId>
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? SupabaseAuthId { get; set; }
    public Guid ChatId { get; set; }
}
```

`Chakra.Domain/Entities/Premi.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

// Id, CreatedAt (waktu premi dibuat), UpdatedAt (waktu terakhir premi diupdate) ada di base class
public class Premi : Aggregate<PremiId>
{
    public UserId UserId { get; set; } // menunjukan premi ini untuk siapa
    public decimal TotalAmount { get; set; } // total keseluruhan pembayaran premi (misal: Rp 12.000.000)
    public decimal InstallmentAmount { get; set; } // jumlah per cicilan, dihitung otomatis TotalAmout / Tenor
    public int Tenor { get; set; } // jumlah bulan cicilan (misal: 12 bulan)
    public int DueDay { get; set; } // tanggal jatuh tempo setiap bulan (1–31). Misal 15 = setiap tanggal 15
    public int GracePeriodDays { get; set; } // masa tenggang setelah jatuh tempo sebelum dianggap overdue (misal: 7 hari)
    public DateOnly StartDate { get; set; } // tanggal mulai premi, cicilan pertama dihitung dari sini
    public PremiStatus Status { get; set; } // Status premi: Active (berjalan), Complated (lunas semua), Cancelled (dibatalkan)

    public User User { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}
```

`Chakra.Domain/Entities/Installment.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

public class Installment : Aggregate<InstallmentId>
{
    public PremiId PremiId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public InstallmentStatus Status { get; set; }
    public int ReminderCount { get; set; }
    public string? MidtransOrderId { get; set; }
    public DateTime? PaidAt { get; set; }
    public uint RowVersion { get; set; }

    public Premi Premi { get; set; } = null!;
}
```

- [ ] **Step 2: Buat value converter**

`Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chakra.Infrastructure.Common;

public sealed class UserIdConverter : ValueConverter<UserId, Guid>
{
    public UserIdConverter() : base(id => id.Value, value => new UserId(value))
    {
    }
}

public sealed class PremiIdConverter : ValueConverter<PremiId, Guid>
{
    public PremiIdConverter() : base(id => id.Value, value => new PremiId(value))
    {
    }
}

public sealed class InstallmentIdConverter : ValueConverter<InstallmentId, Guid>
{
    public InstallmentIdConverter() : base(id => id.Value, value => new InstallmentId(value))
    {
    }
}
```

- [ ] **Step 3: Daftarkan converter di `ApplicationDbContext`**

Ganti isi `Chakra.Infrastructure/ApplicationDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Infrastructure.Common;

namespace Chakra.Infrastructure;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    } 
    
    public DbSet<User> Users {get; set;}
    public DbSet<Premi> Premis {get; set;}
    public DbSet<Installment> Installments {get; set;}

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<UserId>().HaveConversion<UserIdConverter>();
        configurationBuilder.Properties<PremiId>().HaveConversion<PremiIdConverter>();
        configurationBuilder.Properties<InstallmentId>().HaveConversion<InstallmentIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Set `ValueGeneratedNever` pada key**

Di `Chakra.Infrastructure/Configuration/UserConfiguration.cs`, tepat setelah `builder.HasKey(x => x.Id);` tambahkan:

```csharp
        builder.Property(x => x.Id).ValueGeneratedNever();
```

Di `Chakra.Infrastructure/Configuration/InstallmentConfiguration.cs`, tepat setelah `builder.HasKey(x => x.Id);` tambahkan:

```csharp
        builder.Property(x => x.Id).ValueGeneratedNever();
```

Buat `Chakra.Infrastructure/Configuration/PremiConfiguration.cs` (hanya key — properti lain tetap mengikuti convention agar schema tidak berubah):

```csharp
using Chakra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class PremiConfiguration : IEntityTypeConfiguration<Premi>
{
    public void Configure(EntityTypeBuilder<Premi> builder)
    {
        builder.ToTable("Premis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}
```

- [ ] **Step 5: Buat konfigurasi Mapster dan panggil saat startup**

`Chakra.Application/Mappers/MapsterConfig.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using Mapster;

namespace Chakra.Application.Mappers;

public static class MapsterConfig
{
    public static void RegisterMappings()
    {
        TypeAdapterConfig<UserId, Guid>.NewConfig().MapWith(id => id.Value);
        TypeAdapterConfig<PremiId, Guid>.NewConfig().MapWith(id => id.Value);
        TypeAdapterConfig<InstallmentId, Guid>.NewConfig().MapWith(id => id.Value);
    }
}
```

Ganti isi `Chakra.API/Configurations/ApplicationSetup.cs`:

```csharp
// using Chakra.Application.Features.Midtrans.Settings;
// using Chakra.Application.Services;
// using Chakra.Application.Services;
using Chakra.Application.Mappers;

namespace Chakra.API.Configurations;

public static class ApplicationSetup
{
    public static IServiceCollection AddApplicationSetup(this IServiceCollection services,  IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        // services.AddScoped<PremiCompletionService>();
        // services.Configure<MidtransSettings>(configuration.GetSection(MidtransSettings.SectionsName));

        MapsterConfig.RegisterMappings();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
```

- [ ] **Step 6: Ubah `ICurrentUserService` dan `CurrentUserService`**

`Chakra.Application/Common/ICurrentUserService.cs`:

```csharp
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Common;

public interface ICurrentUserService
{
    string? SupabaseAuthId { get; }
    string? Email { get; }
    UserId? DatabaseUserId { get; }
    User? GetCurrentUser();
}
```

Di `Chakra.Infrastructure/Services/CurrentUserService.cs`, tambahkan `using Chakra.Domain.Entities.Common;` di blok using, lalu ganti baris:

```csharp
    public Guid? DatabaseUserId => GetCurrentUser()?.Id;
```

menjadi:

```csharp
    public UserId? DatabaseUserId => GetCurrentUser()?.Id;
```

`AuthorizationBehavior` tidak perlu diubah (`== null` tetap valid untuk `UserId?`).

- [ ] **Step 7: Ubah handler Premi command**

Di `Chakra.Application/Features/Premi/Command/CreatePremi.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti isi method `Handle` dengan:

```csharp
    public async Task<Result<PremiResponseDto>> Handle(CreatePremiInput request, CancellationToken cancellationToken)
    {
            var userId = new UserId(request.UserId);

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists) 
                return Result<PremiResponseDto>.Failure("User not found");

            var installmentAmount = Math.Round(request.TotalAmount / request.Tenor, 2);

            var premi = new PremiEntity
            {
                Id = PremiId.New(),
                UserId = userId,
                TotalAmount = request.TotalAmount,
                InstallmentAmount = installmentAmount,
                Tenor = request.Tenor,
                DueDay = request.DueDay,
                GracePeriodDays = request.GracePeriodDays,
                StartDate = request.StartDate,
                Status = PremiStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            

            //generate installments
            for (int i = 1; i <= request.Tenor; i++)
            {
                var dueDate = request.StartDate.AddMonths(i);
                // Sesuaikan hari jatuh tempo
                var maxDay = DateTime.DaysInMonth(dueDate.Year, dueDate.Month);
                var actualDueDay = Math.Min(request.DueDay, maxDay);
                dueDate = new DateOnly(dueDate.Year, dueDate.Month, actualDueDay);
                
                premi.Installments.Add(new Installment
                {
                    Id = InstallmentId.New(),
                    PremiId = premi.Id,
                    InstallmentNumber = i,
                    DueDate = dueDate,
                    Amount = installmentAmount,
                    Status = InstallmentStatus.Pending,
                    ReminderCount = 0,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            
            _context.Premis.Add(premi);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
```

Di `Chakra.Application/Features/Premi/Command/UpdatePremi.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti:

```csharp
        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
```

menjadi:

```csharp
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);
```

3. Di loop pembuatan installment, ganti `Id = Guid.NewGuid(),` menjadi `Id = InstallmentId.New(),`.

Di `Chakra.Application/Features/Premi/Command/CancelPremi.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti:

```csharp
        var premi = await _context.Premis
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
```

menjadi:

```csharp
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);
```

- [ ] **Step 8: Ubah query handler**

Di `Chakra.Application/Features/Premi/Queries/GetPremiById.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti:

```csharp
        var premi = await _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
```

menjadi:

```csharp
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);
```

Di `Chakra.Application/Features/Installments/Queries/GetInstallmentById.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti:

```csharp
        var installment = await _context.Installments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
```

menjadi:

```csharp
        var installmentId = new InstallmentId(request.Id);

        var installment = await _context.Installments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == installmentId, cancellationToken);
```

Di `Chakra.Application/Features/Installments/Queries/GetAllInstallmentByPremiId.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti:

```csharp
        var premiExists = await _context.Premis.AnyAsync(p => p.Id == request.PremiId, cancellationToken);
```

menjadi:

```csharp
        var premiId = new PremiId(request.PremiId);

        var premiExists = await _context.Premis.AnyAsync(p => p.Id == premiId, cancellationToken);
```

3. Ganti `.Where(i => i.PremiId == request.PremiId)` menjadi `.Where(i => i.PremiId == premiId)`.

Di `Chakra.Application/Features/Users/Queries/GetUsers.cs`, ganti isi method `Handle` dengan (mapping dilakukan di memori karena EF tidak bisa menerjemahkan `.Value` pada kolom ber-converter):

```csharp
    public async Task<Result<List<UserResponseDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var data = users
            .Select(u => new UserResponseDto
            {
                Id = u.Id.Value,
                Name = u.Name,
                Email = u.Email,
                ChatId = u.ChatId,
                CreatedAt = u.CreatedAt
            })
            .ToList();

        return Result<List<UserResponseDto>>.Success(data);
    }
```

- [ ] **Step 9: Ubah API (AuthEndpoint dan EnsureUserMiddleware)**

Di `Chakra.API/Endpoints/AuthEndpoint.cs`, ganti:

```csharp
                return Results.Ok(new
                {
                    user.Id,
```

menjadi (agar JSON `id` tetap string GUID, bukan `{ "value": ... }`):

```csharp
                return Results.Ok(new
                {
                    Id = user.Id.Value,
```

Di `Chakra.API/Middlewares/EnsureUserMiddleware.cs`:
1. Tambahkan `using Chakra.Domain.Entities.Common;` di blok using.
2. Ganti `Id = Guid.NewGuid(),` menjadi `Id = UserId.New(),`. (Baris `ChatId = Guid.NewGuid(),` **tidak** diubah.)

- [ ] **Step 10: Build solution**

Run: `dotnet build Chakra.sln`
Expected: `Build succeeded` dengan `0 Error(s)`. Jika ada error `cannot convert from 'System.Guid' to '...Id'` atau `Operator '==' cannot be applied`, berarti ada pemakai Id yang terlewat — cari dengan `grep -rn "\.Id ==\|Guid.NewGuid" --include=*.cs Chakra.Application Chakra.API` dan terapkan pola yang sama (bungkus Guid dengan `new XxxId(...)`).

- [ ] **Step 11: Pastikan schema tidak berubah**

Run: `dotnet ef migrations has-pending-model-changes --project Chakra.Infrastructure --startup-project Chakra.API`
Expected: `No changes have been made to the model since the last migration.`

Jika perintah melaporkan ada perubahan:
1. Buat migration untuk inspeksi: `dotnet ef migrations add SyncStronglyTypedIds --project Chakra.Infrastructure --startup-project Chakra.API`
2. Buka `Chakra.Infrastructure/Migrations/*_SyncStronglyTypedIds.cs`. Method `Up` dan `Down` **harus kosong** (hanya snapshot yang berubah). Jika kosong, pertahankan migration itu (menyinkronkan snapshot) dan ikutkan di commit.
3. Jika `Up` berisi operasi apa pun (`AlterColumn`, `DropColumn`, dsb.), **berhenti**: hapus migration dengan `dotnet ef migrations remove --project Chakra.Infrastructure --startup-project Chakra.API` dan laporkan isi operasinya ke user sebelum melanjutkan.

- [ ] **Step 12: Commit**

```bash
git add Chakra.Domain/Entities Chakra.Infrastructure/Common Chakra.Infrastructure/Configuration Chakra.Infrastructure/ApplicationDbContext.cs Chakra.Infrastructure/Services Chakra.Application Chakra.API/Configurations/ApplicationSetup.cs Chakra.API/Endpoints/AuthEndpoint.cs Chakra.API/Middlewares/EnsureUserMiddleware.cs
git commit -m "feat: migrate entities to base classes and typed ids

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Jika Step 11 menghasilkan migration `SyncStronglyTypedIds` yang kosong, tambahkan juga file `*_SyncStronglyTypedIds.cs`, `*_SyncStronglyTypedIds.Designer.cs`, dan `ApplicationDbContextModelSnapshot.cs` ke `git add`.)

---

### Task 3: Interceptor timestamp dan dispatch domain event

Menambah dua interceptor, mendaftarkannya, lalu menghapus pengisian timestamp manual.

**Files:**
- Create: `Chakra.Infrastructure/Interceptors/AuditableEntityInterceptor.cs`
- Create: `Chakra.Infrastructure/Interceptors/DispatchDomainEventsInterceptor.cs`
- Modify: `Chakra.API/Configurations/PersistenceSetup.cs`
- Modify: `Chakra.Application/Features/Premi/Command/CreatePremi.cs`, `UpdatePremi.cs`, `CancelPremi.cs`
- Modify: `Chakra.API/Middlewares/EnsureUserMiddleware.cs`

**Interfaces:**
- Consumes: `IAuditableEntity`, `IAggregate` (Task 1); `MediatR.IPublisher`; `System.TimeProvider`.
- Produces:
  - `Chakra.Infrastructure.Interceptors.AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor`
  - `Chakra.Infrastructure.Interceptors.DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor`
  - Setelah task ini, `CreatedAt`/`UpdatedAt` tidak boleh di-set manual di mana pun; fitur berikutnya cukup memanggil `AddDomainEvent(...)` di aggregate dan `SaveChangesAsync`.

- [ ] **Step 1: Buat `AuditableEntityInterceptor`**

`Chakra.Infrastructure/Interceptors/AuditableEntityInterceptor.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chakra.Infrastructure.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public AuditableEntityInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
```

- [ ] **Step 2: Buat `DispatchDomainEventsInterceptor`**

`Chakra.Infrastructure/Interceptors/DispatchDomainEventsInterceptor.cs`:

```csharp
using Chakra.Domain.Entities.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chakra.Infrastructure.Interceptors;

// Dispatch sebelum commit agar perubahan dari event handler (OutboxMessage, AuditLog)
// ikut tersimpan dalam transaksi yang sama. Handler tidak boleh memanggil API eksternal.
public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    public DispatchDomainEventsInterceptor(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEvents(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null) return;

        var aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Clear sebelum publish untuk mencegah event yang sama di-dispatch dua kali
        aggregates.ForEach(a => a.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);
    }
}
```

- [ ] **Step 3: Daftarkan interceptor dan TimeProvider**

Ganti isi `Chakra.API/Configurations/PersistenceSetup.cs`:

```csharp
using Chakra.Application.Common;
using Chakra.Infrastructure;
using Chakra.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Chakra.API.Configurations;

public static class PersistenceSetup
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DispatchDomainEventsInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.CommandTimeout(60);
            });

            // Urutan penting: dispatch dulu agar entity yang ditambahkan event handler ikut mendapat timestamp
            options.AddInterceptors(
                sp.GetRequiredService<DispatchDomainEventsInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
```

- [ ] **Step 4: Hapus timestamp manual di handler Premi**

(Trailing comma pada object initializer valid di C#, jadi koma di baris sebelumnya boleh dibiarkan.)

Di `Chakra.Application/Features/Premi/Command/CreatePremi.cs`, pada inisialisasi `premi` hapus dua baris ini:

```csharp
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
```

Pada inisialisasi `new Installment { ... }` hapus baris:

```csharp
                    CreatedAt = DateTime.UtcNow,
```

Di `Chakra.Application/Features/Premi/Command/UpdatePremi.cs` hapus baris:

```csharp
        premi.UpdatedAt = DateTime.UtcNow;
```

dan pada inisialisasi `new Installment { ... }` hapus baris `CreatedAt = DateTime.UtcNow`.

Di `Chakra.Application/Features/Premi/Command/CancelPremi.cs` hapus baris:

```csharp
        premi.UpdatedAt = DateTime.UtcNow;
```

- [ ] **Step 5: Hapus timestamp manual di `EnsureUserMiddleware`**

Di `Chakra.API/Middlewares/EnsureUserMiddleware.cs`, inisialisasi `new User { ... }` menjadi:

```csharp
                    user = new User
                    {
                        Id = UserId.New(),
                        Name = name,
                        Email = email,
                        SupabaseAuthId = supabaseAuthId,
                        ChatId = Guid.NewGuid()
                    };
```

dan hapus baris berikut di cabang link SupabaseAuthId:

```csharp
                    user.UpdatedAt = DateTime.UtcNow;
```

- [ ] **Step 6: Pastikan tidak ada timestamp manual tersisa**

Run: `grep -rn "CreatedAt =\|UpdatedAt =" --include=*.cs Chakra.Application/Features Chakra.API`
Expected: hanya `Chakra.Application/Features/Users/Queries/GetUsers.cs` (`CreatedAt = u.CreatedAt`, mapping DTO). Tidak ada assignment `DateTime.UtcNow` lain.

- [ ] **Step 7: Build dan cek schema**

Run: `dotnet build Chakra.sln`
Expected: `Build succeeded` dengan `0 Error(s)`.

Run: `dotnet ef migrations has-pending-model-changes --project Chakra.Infrastructure --startup-project Chakra.API`
Expected: `No changes have been made to the model since the last migration.`
(Perintah ini juga membuktikan DI interceptor bisa di-resolve, karena tool membangun `ApplicationDbContext` lewat host API.)

- [ ] **Step 8: Commit**

```bash
git add Chakra.Infrastructure/Interceptors Chakra.API/Configurations/PersistenceSetup.cs Chakra.Application/Features/Premi/Command Chakra.API/Middlewares/EnsureUserMiddleware.cs
git commit -m "feat: add audit and domain event interceptors

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Smoke test end-to-end

Memverifikasi kontrak API tidak berubah dan interceptor bekerja di aplikasi nyata. Tidak ada perubahan kode kecuali ditemukan bug.

**Files:** — (tidak ada, kecuali perbaikan bug)

**Interfaces:**
- Consumes: seluruh hasil Task 1–3.
- Produces: laporan hasil verifikasi.

- [ ] **Step 1: Jalankan API**

Run (background): `dotnet run --project Chakra.API --launch-profile http`
Jika nama profile berbeda, cek `Chakra.API/Properties/launchSettings.json`; profile Development memakai `http://localhost:5277`.
Expected: log `Now listening on: http://localhost:5277` tanpa exception startup.

- [ ] **Step 2: Cek endpoint tanpa auth**

Run: `curl -s http://localhost:5277/api/v1/users`
Expected: array user; setiap `id` berupa string GUID (mis. `"id":"3f2c..."`), **bukan** object `{"value":...}`.

Run: `curl -s "http://localhost:5277/api/premi?page=1&pageSize=5"`
Expected: response sukses; `data.data[].id` dan `userId` berupa string GUID.

Ambil salah satu `id` premi dari response di atas (jika ada), lalu:

Run: `curl -s http://localhost:5277/api/premi/<premiId>` dan `curl -s http://localhost:5277/api/premi/<premiId>/installments`
Expected: response sukses; `id`/`premiId` berupa string GUID.

Run: `curl -s "http://localhost:5277/api/premi?orderBy=createdAt desc"`
Expected: response sukses (ordering Gridify pada kolom non-ID tetap berfungsi).

- [ ] **Step 3: Cek create/update premi (butuh token Supabase — dilakukan user)**

Endpoint create/update/cancel memakai `IAuthorizedRequest`, jadi butuh Bearer token. Minta user menjalankan lewat Scalar (`http://localhost:5277/scalar`) atau `Chakra.API.http`:
1. `POST /api/premi` dengan `userId` user yang ada. Expected: 201; `id` baru adalah UUIDv7 (karakter pertama grup ke-3 adalah `7`, mis. `xxxxxxxx-xxxx-7xxx-...`); `createdAt` dan `updatedAt` terisi dan sama.
2. `PUT /api/premi/{id}` dengan body yang sama. Expected: 200; `updatedAt` lebih baru dari `createdAt`.
3. `PATCH /api/premi/{id}/cancel`. Expected: 200; `status` = `Cancelled`, `updatedAt` diperbarui.
4. `GET /api/auth/me`. Expected: `id` berupa string GUID.

- [ ] **Step 4: Hentikan API dan laporkan**

Hentikan proses `dotnet run`. Laporkan ke user hasil setiap langkah (termasuk langkah yang dilewati karena tidak ada token/data). Jika ada bug, perbaiki, ulangi Task 4, lalu commit perbaikan dengan pesan `fix: ...` + baris Co-Authored-By.
