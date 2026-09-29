# Domain Foundation Design

**Tanggal:** 2026-09-30
**Status:** Disetujui
**Scope:** Base class entity, strongly typed IDs, domain events, dan interceptor EF Core untuk `chakra-be`.

## Latar Belakang

Saat ini entity (`User`, `Premi`, `Installment`) adalah POCO dengan `Guid` Id, dan `Id` / `CreatedAt` / `UpdatedAt` di-set manual di handler dan middleware. Fitur berikutnya (Midtrans webhook, Outbox, AuditLog, Scheduler) butuh:

- Domain events yang di-dispatch **dalam transaksi yang sama** (ACID + Outbox Pattern di README).
- Timestamp yang konsisten tanpa diisi manual.
- Type safety untuk ID agar `PremiId` tidak tertukar dengan `InstallmentId` / `UserId`.

Referensi `chakra-app/Backend` hanya berisi nama file fondasi (kosong), jadi desain ini ditentukan sendiri.

## Keputusan

| Topik | Keputusan |
|---|---|
| Scope | Lengkap: base class + domain events + interceptor + strongly typed IDs |
| Typed IDs | `readonly record struct` manual + EF `ValueConverter`, tanpa library |
| Generasi ID | `Guid.CreateVersion7()` (urut waktu, ramah index) |
| Dispatch event | Sebelum commit (`SavingChangesAsync`), dalam transaksi yang sama |
| Kontrak API | Tidak berubah, route/DTO tetap `Guid` |

## 1. Domain: `Chakra.Domain`

### `Entities/Common/`

| File | Isi |
|---|---|
| `IEntity.cs` | `interface IEntity<TId> { TId Id { get; } }` |
| `Entity.cs` | `abstract class Entity<TId> : IEntity<TId>` dengan `Id { get; set; }`, dan equality (`Equals`, `GetHashCode`, `==`, `!=`) berdasarkan tipe + `Id` |
| `AuditableEntity.cs` | `interface IAuditableEntity { DateTime CreatedAt; DateTime UpdatedAt; }` dan `abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity` |
| `IDomainEvent.cs` | `interface IDomainEvent : MediatR.INotification` |
| `IAggregate.cs` | `interface IAggregate { IReadOnlyCollection<IDomainEvent> DomainEvents { get; } void ClearDomainEvents(); }` |
| `Aggregate.cs` | `abstract class Aggregate<TId> : AuditableEntity<TId>, IAggregate`: list privat, `protected void AddDomainEvent(IDomainEvent)`, `ClearDomainEvents()` |
| `StronglyTypedIds.cs` | `UserId`, `PremiId`, `InstallmentId` |

Bentuk typed ID:

```csharp
public readonly record struct PremiId(Guid Value)
{
    public static PremiId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
```

`DomainEvents` di-ignore oleh EF (bukan properti yang dipetakan, cukup expose sebagai property read-only dari field privat, di-`Ignore` di konfigurasi base atau convention).

**Dependency baru:** `Chakra.Domain` menambah paket `MediatR.Contracts` (hanya interface, tanpa MediatR penuh).

### Perubahan entity

| Entity | Base | Perubahan properti |
|---|---|---|
| `User` | `AuditableEntity<UserId>` | Hapus `Id`, `CreatedAt`, `UpdatedAt` lokal (pindah ke base) |
| `Premi` | `Aggregate<PremiId>` | `UserId : UserId`; hapus `Id`, `CreatedAt`, `UpdatedAt` lokal |
| `Installment` | `Aggregate<InstallmentId>` | `PremiId : PremiId`; hapus `Id`, `CreatedAt`, `UpdatedAt` lokal |

Komentar penjelasan di `Premi.cs` dipertahankan.

### `Events/InstallmentPaidEvent.cs`

```csharp
public sealed record InstallmentPaidEvent(InstallmentId InstallmentId, PremiId PremiId) : IDomainEvent;
```

Hanya definisi. Method `Installment.MarkAsPaid()` yang me-raise event ini dibuat di fitur Midtrans, bukan di scope ini.

## 2. Infrastructure: `Chakra.Infrastructure`

### `Interceptors/AuditableEntityInterceptor.cs`

- `SaveChangesInterceptor`, override `SavingChanges` dan `SavingChangesAsync`.
- Waktu diambil dari `TimeProvider.GetUtcNow().UtcDateTime`.
- `EntityState.Added`: set `CreatedAt` dan `UpdatedAt`.
- `EntityState.Modified`: set `UpdatedAt` saja.

### `Interceptors/DispatchDomainEventsInterceptor.cs`

- `SaveChangesInterceptor`, override `SavingChangesAsync` (dan `SavingChanges` sinkron yang memanggil versi async secara blocking, untuk kelengkapan).
- Ambil semua entry `IAggregate` yang punya event, salin event, `ClearDomainEvents()` **sebelum** publish (mencegah dispatch ganda), lalu `await publisher.Publish(event)` satu per satu.
- Dijalankan sebelum commit, jadi entity yang ditambahkan/diubah oleh handler event ikut tersimpan pada `SaveChanges` yang sama.
- Aturan untuk handler: **tidak boleh memanggil API eksternal** (Telegram, Midtrans). Efek keluar harus lewat OutboxMessage.
- Urutan registrasi: `DispatchDomainEventsInterceptor` lebih dulu, lalu `AuditableEntityInterceptor`, agar entity yang ditambahkan handler event juga mendapat timestamp.

### `ApplicationDbContext`

- Override `ConfigureConventions`:

```csharp
configurationBuilder.Properties<UserId>().HaveConversion<UserIdConverter>();
configurationBuilder.Properties<PremiId>().HaveConversion<PremiIdConverter>();
configurationBuilder.Properties<InstallmentId>().HaveConversion<InstallmentIdConverter>();
```

- Converter (`ValueConverter<XxxId, Guid>`) diletakkan di `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs`.
- Kolom tetap `uuid`: **tidak ada perubahan schema / migration**.
- Karena Id dibuat di aplikasi (`XxxId.New()`), konfigurasi key memakai `ValueGeneratedNever()`.

### `Chakra.API/Configurations/PersistenceSetup.cs`

- Register `TimeProvider.System` (singleton), `AuditableEntityInterceptor` dan `DispatchDomainEventsInterceptor` (scoped).
- `AddDbContext((sp, options) => options.AddInterceptors(...))`.

## 3. Application & API

- **Handler:** konversi `Guid` dari request ke typed ID (`new PremiId(request.Id)`), dan pakai `XxxId.New()` untuk entity baru.
- **Dihapus:** assignment manual `Id = Guid.NewGuid()`, `CreatedAt = ...`, `UpdatedAt = ...` di `CreatePremi`, `UpdatePremi`, `CancelPremi` dan `EnsureUserMiddleware`.
- **Mapster:** konfigurasi global `XxxId → Guid` (`src.Value`) agar `Adapt<>` ke DTO tetap bekerja. Konfigurasi diletakkan di `Chakra.Application/Mappers/MapsterConfig.cs` dan dipanggil saat startup.
- **`ICurrentUserService.DatabaseUserId`:** berubah menjadi `UserId?`.
- **DTO dan endpoint:** tetap memakai `Guid`, jadi kontrak API tidak berubah.
- **`User.ChatId`:** tidak diubah (ditangani di fitur Telegram).

### Keterbatasan

Filter/order Gridify pada kolom ID (mis. `filter=userId=...`) tidak didukung, karena EF tidak bisa menerjemahkan `.Value` pada properti dengan value converter. Filter kolom lain tetap berfungsi. Filter per-ID yang dibutuhkan (installment per premi) memakai `Where` biasa.

## 4. Verifikasi

1. `dotnet build` pada solution tanpa error.
2. `dotnet ef migrations has-pending-model-changes` tidak menemukan perubahan (bukti schema tidak berubah).
3. Smoke test manual: login → create premi → get premi & installment. `CreatedAt`/`UpdatedAt` terisi, response `id` tetap berbentuk Guid.

Project unit test tidak termasuk scope ini.

## Di Luar Scope

- `Installment.MarkAsPaid()` dan handler `InstallmentPaidEvent` (fitur Midtrans).
- Entity `OutboxMessage` dan `AuditLog` (fitur berikutnya).
- Perbaikan tipe `User.ChatId` (fitur Telegram).
