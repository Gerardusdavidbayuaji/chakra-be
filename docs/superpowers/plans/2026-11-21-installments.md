# Installments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin bisa menandai cicilan lunas (premi otomatis `Completed`), cancel premi membatalkan cicilan, jadwal cicilan dalam rupiah bulat dengan jatuh tempo baru, serta role Admin/User dan cek kepemilikan data.

**Architecture:** `Premi` jadi aggregate root yang memegang semua aturan: `Create`, `Update`, `Cancel`, `MarkInstallmentPaid`. Properti `Premi` dan `Installment` di-encapsulate. Pelanggaran aturan dilempar sebagai `DomainException`, lalu ditangkap handler menjadi `Result.Failure`. Otorisasi memakai pipeline MediatR: `IAuthorizedRequest` untuk login, `IAdminRequest` untuk admin. `Result<T>` membawa `ErrorType` supaya endpoint bisa membalas 401/403/404/400.

**Tech Stack:** .NET 9, EF Core 9.0.1 + Npgsql 9.0.2, MediatR 14.1.0, FluentValidation 12.1.1, Mapster 10.0.0-pre02.

**Spec:** `docs/superpowers/specs/2026-11-21-installments-design.md`

## Global Constraints

- **Jangan menjalankan `git commit`, `dotnet build`, `dotnet run`, `dotnet test`, maupun `dotnet ef`.** User menjalankannya sendiri. Langkah verifikasi di plan ini ditujukan untuk user.
- Repo belum punya project test. Pembuktian tiap task dilakukan lewat build dan skenario HTTP yang dijalankan user (Task 4), mengikuti plan-plan sebelumnya.
- Semua nominal dalam **rupiah bulat**: cicilan 1..N-1 = `Math.Floor(Total / Tenor)`, cicilan terakhir = `Total - base * (Tenor - 1)`. Kolom tetap `decimal(18,2)`.
- `CreatedAt`/`UpdatedAt` tidak di-set manual (interceptor). Id baru memakai `XxxId.New()`.
- Waktu diambil dari `TimeProvider` (sudah terdaftar singleton), bukan `DateTime.UtcNow`.
- Pesan error berbahasa Inggris, konsisten dengan pesan yang sudah ada.
- Style kode mengikuti codebase: constructor klasik, field `_camelCase`, file-scoped namespace, `Result<T>` + `ApiResponse`/`ApiErrorResponse` di endpoint.
- Route dan DTO request/response yang sudah ada tidak berubah bentuknya, kecuali penambahan endpoint `POST /api/installments/{id:guid}/pay`.

## Review Focus

1. **`DueDay` 31 melewati Februari.** Jadwal harus 31 Jan → 28/29 Feb → **31** Mar, bukan 28 Mar. Dicek di Task 4 skenario 2.
2. **`StartDate` tepat di `DueDay`.** `StartDate` 15 Jan dengan `DueDay` 15 harus menghasilkan cicilan pertama 15 Jan (tidak digeser). Pembandingnya `firstDue < StartDate`, bukan `<=`. Dicek di Task 4 skenario 1.
3. **`TotalAmount` lebih kecil dari `Tenor`.** Misalnya 5 rupiah dengan 12 bulan akan menghasilkan cicilan 0 rupiah. Kasus ini harus ditolak dengan pesan validasi (400). Aturannya ditambahkan di Task 1, dicek di Task 4 skenario 3.
4. **Admin menekan "bayar" dua kali** pada cicilan yang sama. Harus dapat 400 dengan pesan jelas, bukan 500, dan `PaidAt` tidak berubah. Dicek di Task 4 skenario 5.
5. **Dua request bayar bersamaan** pada cicilan yang sama. `DbUpdateConcurrencyException` harus jadi 400, bukan 500. Ditangani di Task 3, dicek di Task 4 skenario 6.

## File Structure

| File | Aksi | Tanggung jawab |
|---|---|---|
| `Chakra.Domain/Entities/Enums/InstallmentStatus.cs` | Modify | Tambah `Cancelled` |
| `Chakra.Domain/Entities/Enums/UserRole.cs` | Create | `User`, `Admin` |
| `Chakra.Domain/Exceptions/DomainException.cs` | Create | Pelanggaran aturan bisnis |
| `Chakra.Domain/Entities/User.cs` | Modify | `Role`, `IsAdmin` |
| `Chakra.Domain/Entities/Installment.cs` | Modify | Encapsulate, factory & method `internal` |
| `Chakra.Domain/Entities/Premi.cs` | Modify | Aggregate root: factory, method, jadwal, ringkasan |
| `Chakra.Infrastructure/Configuration/PremiConfiguration.cs` | Modify | Backing field `Installments`, ignore ringkasan |
| `Chakra.Infrastructure/Configuration/UserConfiguration.cs` | Modify | Kolom `Role` |
| `Chakra.Infrastructure/Migrations/*_AddUserRole.cs` | Create (user generate) | Kolom `Role` + backfill cicilan |
| `Chakra.Application/Features/Premi/Command/CreatePremi.cs` | Modify | Pakai `Premi.Create`, validator rupiah |
| `Chakra.Application/Features/Premi/Command/UpdatePremi.cs` | Modify | Pakai `premi.Update`, validator baru |
| `Chakra.Application/Features/Premi/Command/CancelPremi.cs` | Modify | Pakai `premi.Cancel` |
| `Chakra.Application/Features/Premi/Queries/GetPremiById.cs` | Modify | Ringkasan dari `Premi`, kepemilikan |
| `Chakra.Application/Features/Premi/Queries/getAllPremi.cs` | Modify | Include cicilan, filter pemilik |
| `Chakra.Application/Features/Installments/Queries/GetAllInstallmentByPremiId.cs` | Modify | Kepemilikan |
| `Chakra.Application/Features/Installments/Queries/GetInstallmentById.cs` | Modify | Kepemilikan |
| `Chakra.Application/Common/Response.cs` | Modify | `ErrorType` + factory |
| `Chakra.Application/Common/IAdminRequest.cs` | Create | Penanda request admin |
| `Chakra.Application/Common/Behaviors/AuthorizationBehavior.cs` | Modify | Balas `Unauthorized` |
| `Chakra.Application/Common/Behaviors/AdminAuthorizationBehavior.cs` | Create | Balas `Forbidden` |
| `Chakra.Application/Common/ICurrentUserService.cs` | Modify | `IsAdmin` |
| `Chakra.Infrastructure/Services/CurrentUserService.cs` | Modify | `IsAdmin` |
| `Chakra.API/Configurations/MediatRSetup.cs` | Modify | Daftarkan `AdminAuthorizationBehavior` |
| `Chakra.API/Endpoints/ResultHttpExtensions.cs` | Create | `Result` gagal → `IResult` |
| `Chakra.API/Endpoints/PremiEndpoint.cs` | Modify | `RequireAuthorization`, mapping error |
| `Chakra.API/Endpoints/InstallmentEndpoint.cs` | Modify | `RequireAuthorization`, mapping error, endpoint pay |
| `Chakra.Application/Features/Installments/Command/PayInstallment.cs` | Create | Command bayar cicilan |
| `Chakra.API/Chakra.API.http` | Modify | Skenario verifikasi |

---

### Task 1: Domain model kaya + refactor command Premi + migration

Task ini mencakup domain dan handler Premi sekaligus. Begitu setter dibuat `private`, handler lama tidak lagi bisa di-compile, jadi keduanya harus berubah bersama.

**Files:**
- Modify: `Chakra.Domain/Entities/Enums/InstallmentStatus.cs`
- Create: `Chakra.Domain/Entities/Enums/UserRole.cs`
- Create: `Chakra.Domain/Exceptions/DomainException.cs`
- Modify: `Chakra.Domain/Entities/User.cs`
- Modify: `Chakra.Domain/Entities/Installment.cs`
- Modify: `Chakra.Domain/Entities/Premi.cs`
- Modify: `Chakra.Infrastructure/Configuration/PremiConfiguration.cs`
- Modify: `Chakra.Infrastructure/Configuration/UserConfiguration.cs`
- Modify: `Chakra.Application/Features/Premi/Command/CreatePremi.cs`
- Modify: `Chakra.Application/Features/Premi/Command/UpdatePremi.cs`
- Modify: `Chakra.Application/Features/Premi/Command/CancelPremi.cs`
- Modify: `Chakra.Application/Features/Premi/Queries/GetPremiById.cs`

**Interfaces:**
- Consumes: `Aggregate<TId>.AddDomainEvent` (protected), `InstallmentPaidEvent(InstallmentId, PremiId)`, `PremiId.New()`, `InstallmentId.New()`.
- Produces:
  - `enum InstallmentStatus { Pending, Reminder1, Reminder2, Paid, Overdue, Cancelled }` (tambahan di akhir)
  - `enum UserRole { User, Admin }` di namespace `Chakra.Domain.Entities.Enums`
  - `class DomainException : Exception` dengan ctor `(string message)`, namespace `Chakra.Domain.Exceptions`
  - `User.Role : UserRole { get; private set; } = UserRole.User`, `User.IsAdmin : bool` (`=> Role == UserRole.Admin`)
  - `Premi.Create(UserId userId, decimal totalAmount, int tenor, int dueDay, int gracePeriodDays, DateOnly startDate) : Premi` (static)
  - `void Premi.Update(decimal totalAmount, int tenor, int dueDay, int gracePeriodDays, DateOnly startDate)`
  - `void Premi.Cancel()`
  - `Installment Premi.MarkInstallmentPaid(InstallmentId installmentId, DateTime paidAt, string? midtransOrderId = null)`
  - `Premi.Installments : IReadOnlyCollection<Installment>` (backing field `_installments`)
  - Properti hitung `Premi.InstallmentsPaid : int`, `Premi.RemainingAmount : decimal`, `Premi.NextDueDate : DateOnly?`. Namanya sama persis dengan field `PremiResponseDto`, jadi Mapster mengisinya otomatis.

- [ ] **Step 1: Enum, `DomainException`, `User.Role`**

Tambah `Cancelled` di akhir `InstallmentStatus`. Buat `UserRole` dan `DomainException` sesuai Interfaces. Di `User`, tambah `Role` dan `IsAdmin`.

- [ ] **Step 2: Encapsulate `Installment`**

Semua properti jadi `{ get; private set; }`, termasuk `RowVersion`. `Premi` navigation tetap `= null!`. Tambahkan:
- constructor privat tanpa parameter (untuk EF);
- `internal static Installment Create(PremiId premiId, int installmentNumber, DateOnly dueDate, decimal amount)`, dengan `Status = Pending` dan `ReminderCount = 0`;
- `internal void MarkPaid(DateTime paidAt, string? midtransOrderId)`, yang mengisi `Status = Paid` dan `PaidAt`, serta `MidtransOrderId` hanya bila argumennya tidak null;
- `internal void Cancel()`, yang mengisi `Status = Cancelled`.

- [ ] **Step 3: `Premi` sebagai aggregate root**

Semua properti skalar jadi `private set`. `Installments` diubah jadi `IReadOnlyCollection<Installment>` di atas `private readonly List<Installment> _installments = new()`. Komentar per properti dipertahankan. Tambahkan constructor privat untuk EF.

Validasi bersama di `Create` dan `Update` (lempar `DomainException`):
- `totalAmount % 1 != 0` → `"Total amount must be a whole rupiah amount."`
- `totalAmount < tenor` → `"Total amount must be at least equal to tenor."`

Aturan method, dengan urutan dan pesan:

| Method | Cek (urut) → pesan `DomainException` | Efek |
|---|---|---|
| `Create` | validasi bersama | `Id = PremiId.New()`, `Status = Active`, isi field, `InstallmentAmount = Math.Floor(total / tenor)`, generate jadwal |
| `Update` | `Status != Active` → `"Only active premi can be changed."`; ada cicilan `Paid` → `"Premi cannot be changed because installments have already been paid."`; validasi bersama | isi field, `_installments.Clear()`, generate jadwal ulang |
| `Cancel` | `Cancelled` → `"Premi is already cancelled."`; `Completed` → `"Completed premi cannot be cancelled."` | `Status = Cancelled`; tiap cicilan bukan `Paid` → `Cancel()` |
| `MarkInstallmentPaid` | lihat di bawah | |

`MarkInstallmentPaid`:
1. Cari cicilan dengan `installmentId`.
2. Jika ketemu, sudah `Paid`, `midtransOrderId != null`, dan `midtransOrderId == installment.MidtransOrderId`: `return installment` tanpa perubahan dan tanpa event.
3. `Status != Active` → `"Premi is not active."`
4. Tidak ketemu → `"Installment not found in this premi."`
5. Sudah `Paid` → `"Installment is already paid."`
6. Ada cicilan `InstallmentNumber` lebih kecil yang bukan `Paid` → `"Previous installments must be paid first."`
7. `installment.MarkPaid(paidAt, midtransOrderId)`, lalu `AddDomainEvent(new InstallmentPaidEvent(installment.Id, Id))`.
8. Semua cicilan `Paid` → `Status = Completed`. Return `installment`.

Generate jadwal (method privat `GenerateSchedule()`, dipakai `Create` dan `Update`):

```csharp
var firstDue = DueDateIn(StartDate.Year, StartDate.Month);
var baseMonth = firstDue < StartDate ? StartDate.AddMonths(1) : StartDate; // hanya Year/Month yang dipakai
var baseAmount = Math.Floor(TotalAmount / Tenor);
for (var i = 1; i <= Tenor; i++)
{
    var month = new DateOnly(baseMonth.Year, baseMonth.Month, 1).AddMonths(i - 1);
    var amount = i == Tenor ? TotalAmount - baseAmount * (Tenor - 1) : baseAmount;
    _installments.Add(Installment.Create(Id, i, DueDateIn(month.Year, month.Month), amount));
}
// DueDateIn(y, m) = new DateOnly(y, m, Math.Min(DueDay, DateTime.DaysInMonth(y, m)))
```

Properti ringkasan (cicilan "aktif" = bukan `Paid` dan bukan `Cancelled`):
- `InstallmentsPaid` = jumlah cicilan `Paid`;
- `RemainingAmount` = total `Amount` cicilan aktif;
- `NextDueDate` = `DueDate` terkecil cicilan aktif, atau `null`.

- [ ] **Step 4: Konfigurasi EF**

Di `PremiConfiguration`:
- `builder.Navigation(x => x.Installments).UsePropertyAccessMode(PropertyAccessMode.Field)`;
- `builder.Ignore(x => x.InstallmentsPaid)`, begitu juga untuk `RemainingAmount` dan `NextDueDate`.

Di `UserConfiguration`:
- `builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(EfConstants.Length.Short).HasDefaultValue(UserRole.User).IsRequired()`;
- `builder.Ignore(x => x.IsAdmin)`.

Pada `Update`, cicilan yang dikeluarkan dari `_installments` dihapus EF sebagai orphan (relasi required + cascade). `RemoveRange` manual di handler tidak diperlukan lagi.

- [ ] **Step 5: Refactor command dan `GetPremiById`**

- `CreatePremiRequestHandler`: setelah cek user ada, `var premi = PremiEntity.Create(...)`, `_context.Premis.Add(premi)`, lalu simpan.
- `CreatePremiValidator`: tambah dua aturan.
  - `TotalAmount`: `.Must(x => x % 1 == 0).WithMessage("Total amount must be a whole rupiah amount")`.
  - `.Must(x => x.TotalAmount >= x.Tenor).WithMessage("Total amount must be at least equal to tenor")` dengan `OverridePropertyName("TotalAmount")`.
- `UpdatePremi.cs`: buat `UpdatePremiValidator` dengan aturan yang sama seperti `CreatePremiValidator` minus `UserId`. Handler memuat premi `.Include(p => p.Installments)`, memanggil `premi.Update(...)`, lalu simpan.
- `CancelPremi.cs`: handler memuat premi `.Include(p => p.Installments)` (sekarang belum di-include), memanggil `premi.Cancel()`, lalu simpan.
- Ketiga handler membungkus panggilan domain dengan `try { ... } catch (DomainException ex) { return Result<PremiResponseDto>.Failure(ex.Message); }`.
- `GetPremiByIdQueryHandler`: hapus hitungan manual `InstallmentsPaid`, `RemainingAmount`, dan `NextDueDate`. Ganti dengan `premi.ToResponseDto()`, karena Mapster mengisi ketiganya dari properti `Premi`.

- [ ] **Step 6: (User) build, generate migration, tambah backfill**

User menjalankan:
1. `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.
2. `.\add-migration.ps1 AddUserRole`
3. Buka `Chakra.Infrastructure/Migrations/*_AddUserRole.cs`. `Up()` harus berisi **hanya** `AddColumn` `Role` (`character varying(50)`, not null, default `'User'`) di `Users`. Jika ada operasi lain pada `Premis` atau `Installments`, hapus migration (`dotnet ef migrations remove --project Chakra.Infrastructure --startup-project Chakra.API`) dan laporkan ke Claude.

Setelah migration ada, Claude menambahkan baris ini di akhir `Up()`:

```csharp
migrationBuilder.Sql("""
    UPDATE "Installments" AS i SET "Status" = 'Cancelled'
    FROM "Premis" AS p
    WHERE i."PremiId" = p."Id" AND p."Status" = 2 AND i."Status" <> 'Paid';
    """);
```

`Premis.Status` disimpan sebagai integer (`PremiStatus.Cancelled` = 2, lihat model snapshot). `Installments.Status` disimpan sebagai string. `Down()` tidak membalik backfill, karena status asli cicilan tidak tersimpan.

4. User menjalankan `.\update-database.ps1`.

---

### Task 2: Role, otorisasi, dan kepemilikan data

**Files:**
- Modify: `Chakra.Application/Common/Response.cs`
- Create: `Chakra.Application/Common/IAdminRequest.cs`
- Modify: `Chakra.Application/Common/Behaviors/AuthorizationBehavior.cs`
- Create: `Chakra.Application/Common/Behaviors/AdminAuthorizationBehavior.cs`
- Modify: `Chakra.Application/Common/ICurrentUserService.cs`
- Modify: `Chakra.Infrastructure/Services/CurrentUserService.cs`
- Modify: `Chakra.API/Configurations/MediatRSetup.cs`
- Create: `Chakra.API/Endpoints/ResultHttpExtensions.cs`
- Modify: `Chakra.API/Endpoints/PremiEndpoint.cs`
- Modify: `Chakra.API/Endpoints/InstallmentEndpoint.cs`
- Modify: `Chakra.Application/Features/Premi/Command/CreatePremi.cs`, `UpdatePremi.cs`, `CancelPremi.cs`
- Modify: `Chakra.Application/Features/Premi/Queries/GetPremiById.cs`, `getAllPremi.cs`
- Modify: `Chakra.Application/Features/Installments/Queries/GetAllInstallmentByPremiId.cs`, `GetInstallmentById.cs`

**Interfaces:**
- Consumes: `User.IsAdmin`, `Premi.UserId`, `Installment.Premi` (Task 1).
- Produces:
  - `enum ErrorType { Failure, NotFound, Unauthorized, Forbidden }` di `Response.cs`
  - `Result<T>.ErrorType : ErrorType` (default `Failure`; `ValidationFailure` tetap `Failure`)
  - `Result<T>.NotFound(string)`, `Result<T>.Unauthorized(string)`, `Result<T>.Forbidden(string)` (static)
  - `interface IAdminRequest<out TResponse> : IAuthorizedRequest<TResponse>`
  - `ICurrentUserService.IsAdmin : bool`
  - `IResult ResultHttpExtensions.ToErrorResult<T>(this Result<T> result)`: `Unauthorized` → 401, `Forbidden` → 403, `NotFound` → 404, `Errors != null` → 400 `ApiErrorResponse.Validation`, selain itu → 400 `ApiErrorResponse.Error`. Status code di body sama dengan HTTP status.

- [ ] **Step 1: `ErrorType` di `Result<T>`**

Tambah enum dan tiga factory sesuai Interfaces. `Success`, `Failure`, dan `ValidationFailure` tidak berubah.

- [ ] **Step 2: Behavior dan `IsAdmin`**

- `AuthorizationBehavior`: ganti refleksi `nameof(Result<object>.Failure)` menjadi `nameof(Result<object>.Unauthorized)`. Pesannya tetap `"Unauthorized"`.
- `AdminAuthorizationBehavior<TRequest, TResponse> where TRequest : IAdminRequest<TResponse>`: jika `!_currentUser.IsAdmin`, kembalikan lewat refleksi `Forbidden("Forbidden")` (pola sama dengan `AuthorizationBehavior`). Selain itu lanjut ke `next()`.
- `ICurrentUserService.IsAdmin`, diimplementasikan sebagai `GetCurrentUser()?.IsAdmin == true`.
- `MediatRSetup`: tambah `cfg.AddOpenBehavior(typeof(AdminAuthorizationBehavior<,>))` tepat **setelah** `AuthorizationBehavior` dan sebelum `ValidationResultPipelineBehavior`.

- [ ] **Step 3: Command Premi jadi admin-only**

`CreatePremiInput`, `UpdatePremiInput`, dan `CancelPremiRequest` sekarang memakai `IAdminRequest<Result<PremiResponseDto>>`. Ketiga handler mengembalikan `Result.NotFound(...)` (bukan `Failure`) untuk "Premi not found." dan "User not found".

- [ ] **Step 4: Query dengan kepemilikan**

Keempat query sekarang memakai `IAuthorizedRequest<...>`, dan handler-nya menerima `ICurrentUserService`. Aturannya: non-admin hanya boleh melihat data dengan `UserId == _currentUser.DatabaseUserId`. Kalau datanya tidak ada **atau** bukan miliknya, kembalikan `Result.NotFound` dengan pesan yang sama seperti "not found" yang sudah ada. Pesan `"Premi not  found."` yang dobel spasi sekalian dirapikan.

- `GetAllPremiQuery`: tambah `.Include(p => p.Installments)`. Untuk non-admin, tambah `.Where(p => p.UserId == userId)` sebelum `ApplyFilteringAndOrdering`.
- `GetPremiByIdQuery`: tambah cek pemilik setelah load.
- `GetAllInstallmentByPremiIdQuery`: ganti `AnyAsync` jadi `AnyAsync(p => p.Id == premiId && (isAdmin || p.UserId == userId))`.
- `GetInstallmentByIdQuery`: tambah `.Include(i => i.Premi)`, lalu cek `installment.Premi.UserId`.

- [ ] **Step 5: Endpoint**

- Buat `ResultHttpExtensions` sesuai Interfaces.
- Di `PremiEndpoint` dan `InstallmentEndpoint`, tambahkan `.RequireAuthorization()` pada `MapGroup(...)`. Semua cabang gagal diganti `result.ToErrorResult()`. Cabang sukses dan status 201 tidak berubah.

- [ ] **Step 6: (User) build**

User menjalankan `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`. Lalu set admin pertama:

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Email" = '<email admin>';
```

---

### Task 3: Bayar cicilan (admin)

**Files:**
- Create: `Chakra.Application/Features/Installments/Command/PayInstallment.cs`
- Modify: `Chakra.API/Endpoints/InstallmentEndpoint.cs`

**Interfaces:**
- Consumes: `Premi.MarkInstallmentPaid(...)`, `DomainException` (Task 1); `IAdminRequest`, `Result.NotFound`, `ToErrorResult()` (Task 2); `InstallmentResponseDto`, `installment.ToResponseDto()`.
- Produces:
  - `record PayInstallmentCommand(Guid InstallmentId) : IAdminRequest<Result<InstallmentResponseDto>>`
  - `POST /api/installments/{id:guid}/pay`, dengan nama endpoint `"Pay Installment"`

- [ ] **Step 1: Handler `PayInstallmentCommandHandler`**

Dependency: `IApplicationDbContext`, `TimeProvider`. Alurnya:
1. Ambil `PremiId` dari cicilan: `_context.Installments.Where(i => i.Id == id).Select(i => (PremiId?)i.PremiId).FirstOrDefaultAsync()`. Kalau `null`, kembalikan `NotFound("Installment not found.")`.
2. Load premi `.Include(p => p.Installments)`.
3. Panggil `premi.MarkInstallmentPaid(id, _timeProvider.GetUtcNow().UtcDateTime)`.
4. Panggil `SaveChangesAsync`, lalu kembalikan `Success(installment.ToResponseDto())`.

Penanganan error:
- `DomainException` → `Failure(ex.Message)`;
- `DbUpdateConcurrencyException` → `Failure("Installment was modified by another request, please retry.")`.

- [ ] **Step 2: Endpoint**

Tambahkan di `InstallmentEndpoint`. Kalau sukses, balas 200 `ApiResponse<object>.Success(result.Data!, 200, "Installment paid successfully")`. Kalau gagal, balas `result.ToErrorResult()`.

- [ ] **Step 3: (User) build**

User menjalankan `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.

---

### Task 4: Verifikasi end-to-end (dilakukan user)

**Files:**
- Modify: `Chakra.API/Chakra.API.http` (Claude menambahkan request berikut, dengan variabel `@adminToken`, `@userToken`, `@premiId`, `@installmentId`)

User menjalankan API, lalu mencoba setiap skenario. Kolom "Harus" adalah hasil yang diharapkan.

| # | Skenario | Harus |
|---|---|---|
| 1 | Admin `POST /api/premi`: total 12.000.000, tenor 7, dueDay 15, startDate 15 Jan 2027 | 201. `GET .../installments`: cicilan 1–6 = 1.714.285, cicilan 7 = 1.714.290, cicilan 1 jatuh tempo **15 Jan 2027**. `RemainingAmount` = 12.000.000 |
| 2 | Admin buat premi: dueDay 31, startDate 31 Jan 2027, tenor 3, total 300.000 | Jatuh tempo 31 Jan, 28 Feb, **31 Mar** 2027 |
| 3 | Admin buat premi: total 5, tenor 12. Lalu total 1.000.000,50 | 400 validasi pada `TotalAmount` di kedua kasus |
| 4 | Admin `POST /api/installments/{cicilan 2}/pay` sebelum cicilan 1 lunas | 400 `"Previous installments must be paid first."` |
| 5 | Bayar cicilan 1, lalu ulangi request yang sama | Pertama 200 dengan `PaidAt` terisi. Kedua 400 `"Installment is already paid."`, `PaidAt` tidak berubah |
| 6 | Kirim dua request bayar cicilan yang sama bersamaan (dua tab / `curl &`) | Satu 200, satu 400 (sudah dibayar atau konflik). Tidak ada 500 |
| 7 | Bayar semua cicilan sampai yang terakhir | `GET /api/premi/{id}`: `Status = Completed`, `RemainingAmount = 0`, `NextDueDate = null` |
| 8 | Cancel premi lain yang 1 cicilannya sudah lunas | 200. Cicilan lunas tetap `Paid`, sisanya `Cancelled`. Bayar cicilan berikutnya → 400 `"Premi is not active."` Cancel lagi → 400 `"Premi is already cancelled."` |
| 9 | Cancel premi `Completed` (skenario 7) | 400 `"Completed premi cannot be cancelled."` |
| 10 | User biasa: `POST /api/premi`, `PATCH .../cancel`, `POST .../pay` | 403 |
| 11 | User biasa: `GET /api/premi` | Hanya premi miliknya, dengan `RemainingAmount` terisi (bukan 0) |
| 12 | User biasa: `GET /api/premi/{id premi orang lain}` dan `GET /api/installments/{id cicilan orang lain}` | 404 |
| 13 | Tanpa token: `GET /api/premi` | 401 |
| 14 | Database: premi yang sudah `Cancelled` sebelum migration | Cicilan non-`Paid` sudah `Cancelled` |
