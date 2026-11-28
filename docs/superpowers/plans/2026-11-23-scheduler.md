# Scheduler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Job harian 09:00 WIB yang mengirim pengingat cicilan via Telegram (berisi link Snap), memperbarui status cicilan, mengirim alert tunggakan ke admin, dengan stop/resume per premi dan jejak audit `JobRuns`.

**Architecture:** Aturan jadwal sepenuhnya di aggregate `Premi` (`EvaluateReminders` mengembalikan keputusan + menerapkan status). `DailyReminderService` (Application) memproses premi kandidat satu per satu dalam scope DI terpisah: evaluasi → buat link lewat `IPaymentLinkService` → antrekan outbox → catat. `DailyReminderProcessor` (BackgroundService) memicu service sekali per tanggal bisnis WIB dan mencatatnya di `JobRuns`.

**Tech Stack:** .NET 9, EF Core 9.0.1 + Npgsql 9.0.2, MediatR 14.1.0, FluentValidation 12.1.1, Mapster 10.0.0-pre02.

**Spec:** `docs/superpowers/specs/2026-11-23-scheduler-design.md`

## Global Constraints

- **Jangan menjalankan `git commit`, `git add`, `dotnet build`, `dotnet run`, `dotnet test`, maupun `dotnet ef`.** User menjalankannya sendiri. Tidak ada project test; pembuktian lewat build user dan skenario di Task 5.
- `CreatedAt`/`UpdatedAt` tidak di-set manual. Id baru memakai `XxxId.New()`. Waktu dari `TimeProvider`.
- "Hari ini" dan jam 09:00 dihitung di zona `TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone)` (default `"Asia/Jakarta"`), **tidak pernah** dari jam server.
- Domain event handler tidak boleh memanggil API eksternal. Link bayar dibuat di `DailyReminderService`, bukan di handler.
- Pesan error API berbahasa Inggris; teks Telegram berbahasa Indonesia, persis seperti tabel teks di spec bagian 3. Nominal memakai `RupiahFormat.Format`; tanggal `ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("id-ID"))`.
- Style kode mengikuti codebase: constructor klasik, `_camelCase`, file-scoped namespace, `Result<T>` + `ToErrorResult()`.
- Nilai default settings: `RunAtLocalTime "09:00"`, `TimeZone "Asia/Jakarta"`, `CheckIntervalMinutes 5`, `MaxAttempts 3`, `BatchSize 100`, `DaysBeforeFirst 3`, `DaysBeforeSecond 1`, `RepeatEveryDays 3`, `AdminAlertAfterDueDays 27`.
- Skala waktu: `TimeScale` `"Days"` (default) atau `"Minutes"`, plus `SimulationStartDate : DateOnly?`. Mode `Minutes`: cek tiap 20 detik, abaikan `RunAtLocalTime`, `businessDate = SimulationStartDate.AddDays((int)Math.Floor((now - processStartedAt).TotalMinutes))`. `Minutes` di luar Development atau tanpa `SimulationStartDate` → exception saat startup.
- `JobName` = `"DailyReminders"`. Run `Running` yang `StartedAt` lebih dari 1 jam lalu dianggap gagal.

## Review Focus

1. **Server di UTC, jam 00:00–07:00 UTC.** Pada 2027-01-12 01:00 UTC (08:00 WIB) job belum boleh jalan, dan pada 2027-01-12 02:30 UTC (09:30 WIB) tanggal bisnis adalah 12 Jan. Konversi di Task 4 memakai `TimeZoneInfo.ConvertTime(now, tz)`; dicek di Task 5 skenario 12.
2. **Catch-up tanpa pesan dobel.** Cicilan yang `LastRemindedOn` = 12 Jan, lalu run 13 Jan (bukan hari pengingat) tidak boleh mengirim pesan; run 15 Jan (H) mengirim satu. Aturan "slot terbesar `L <= today` dan `LastRemindedOn < L`" di Task 1; dicek skenario 3 dan 7.
3. **Pembayaran masuk di tengah run.** Cicilan dibayar setelah `EvaluateReminders` tapi sebelum link dibuat → `GetOrCreateAsync` menolak (aturan bayar) → `Skipped`, bukan `Failures`, dan tidak ada pesan. Task 3; dicek skenario 10.
4. **Dua instance / restart di hari yang sama.** Hanya satu `JobRun` per tanggal; restart setelah `Succeeded` tidak menjalankan ulang. Unique index + klaim di Task 4; dicek skenario 12.
5. **Nasabah belum link Telegram.** Tidak ada transaksi Midtrans dibuat dan `LastRemindedOn` tidak diisi, sehingga setelah link Telegram pesan terkirim di run berikutnya. Task 3; dicek skenario 13.

## File Structure

| File | Aksi | Tanggung jawab |
|---|---|---|
| `Chakra.Domain/Entities/Enums/ReminderKind.cs`, `JobRunStatus.cs` | Create | Enum |
| `Chakra.Domain/Entities/Reminders/ReminderPolicy.cs`, `ReminderDecision.cs` | Create | Value object aturan & hasil evaluasi |
| `Chakra.Domain/Entities/Installment.cs` | Modify | `LastRemindedOn`, `OverdueAlertSentOn`, method internal |
| `Chakra.Domain/Entities/Premi.cs` | Modify | Pause/resume, `EvaluateReminders`, `Record*` |
| `Chakra.Domain/Entities/JobRun.cs`, `Common/StronglyTypedIds.cs` | Create/Modify | Jejak audit run, `JobRunId` |
| `Chakra.Infrastructure/Configuration/JobRunConfiguration.cs` | Create | Mapping + unique index |
| `Chakra.Infrastructure/Configuration/InstallmentConfiguration.cs`, `PremiConfiguration.cs`, `Common/StronglyTypedIdConverters.cs`, `ApplicationDbContext.cs` | Modify | Kolom baru, converter, DbSet |
| `Chakra.Application/Common/IApplicationDbContext.cs` | Modify | `DbSet<JobRun>` |
| `Chakra.Application/Features/Premi/Dtos/PremiResponseDto.cs` | Modify | `RemindersPaused` |
| `Chakra.Application/Features/Premi/Command/PauseReminders.cs`, `ResumeReminders.cs` | Create | Command admin |
| `Chakra.API/Endpoints/PremiEndpoint.cs` | Modify | Endpoint pause/resume |
| `Chakra.Application/Features/Scheduler/Settings/SchedulerSettings.cs` | Create | Settings |
| `Chakra.Application/Features/Scheduler/ReminderMessages.cs` | Create | Teks Telegram |
| `Chakra.Application/Features/Scheduler/DailyReminderService.cs` | Create | Proses satu tanggal bisnis |
| `Chakra.Infrastructure/BackgroundJobs/DailyReminderProcessor.cs` | Create | Pemicu harian + `JobRuns` |
| `Chakra.API/Configurations/SchedulerSetup.cs`, `Program.cs`, `appsettings.example.json` | Create/Modify | Wiring |
| `Chakra.API/Endpoints/JobEndpoint.cs` | Create | Endpoint Development |
| `Chakra.API/Chakra.API.http` | Modify | Skenario verifikasi |

---

### Task 1: Domain aturan pengingat, `JobRun`, dan persistence

**Files:**
- Create: `Chakra.Domain/Entities/Enums/ReminderKind.cs`, `JobRunStatus.cs`
- Create: `Chakra.Domain/Entities/Reminders/ReminderPolicy.cs`, `ReminderDecision.cs`
- Modify: `Chakra.Domain/Entities/Installment.cs`, `Chakra.Domain/Entities/Premi.cs`
- Create: `Chakra.Domain/Entities/JobRun.cs`; Modify: `Chakra.Domain/Entities/Common/StronglyTypedIds.cs`
- Create: `Chakra.Infrastructure/Configuration/JobRunConfiguration.cs`
- Modify: `Chakra.Infrastructure/Configuration/InstallmentConfiguration.cs`, `PremiConfiguration.cs`, `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs`, `Chakra.Infrastructure/ApplicationDbContext.cs`, `Chakra.Application/Common/IApplicationDbContext.cs`
- Modify: `Chakra.Application/Features/Premi/Dtos/PremiResponseDto.cs`

**Interfaces:**
- Produces:
  - `enum ReminderKind { Upcoming, DueTomorrow, DueToday, GracePeriod, Overdue }`
  - `enum JobRunStatus { Running, Succeeded, Failed }`
  - `sealed record ReminderPolicy(int DaysBeforeFirst, int DaysBeforeSecond, int RepeatEveryDays, int AdminAlertAfterDueDays)`
  - `sealed record ReminderDecision(Installment? Target, ReminderKind? Kind, int DaysLate, DateOnly? GracePeriodEndsOn, bool AdminAlertDue)` + `static ReminderDecision None`
  - `Installment.LastRemindedOn : DateOnly?`, `Installment.OverdueAlertSentOn : DateOnly?`
  - `Premi.RemindersPaused : bool`, `RemindersPausedAt : DateTime?`, `RemindersPausedByUserId : UserId?`
  - `void Premi.PauseReminders(UserId adminId, DateTime now)`, `void Premi.ResumeReminders()`
  - `ReminderDecision Premi.EvaluateReminders(DateOnly today, ReminderPolicy policy)`
  - `void Premi.RecordReminderSent(InstallmentId installmentId, DateOnly today)`, `void Premi.RecordOverdueAlertSent(InstallmentId installmentId, DateOnly today)`
  - `readonly record struct JobRunId(Guid Value)`; `JobRun` dengan factory `static JobRun Start(string jobName, DateOnly businessDate, DateTime now)` dan method `Restart(DateTime now)` (Attempt++, Running, reset ringkasan), `Complete(DateTime now, int premisProcessed, int remindersQueued, int adminAlertsQueued, int skipped, int failures)` (Succeeded jika failures == 0, selain itu Failed), `Fail(DateTime now, string error)`; properti `bool IsStale(DateTime now)` (Running dan `StartedAt < now - 1 jam`)
  - `DbSet<JobRun> JobRuns` di `IApplicationDbContext`
  - `PremiResponseDto.RemindersPaused : bool`

- [ ] **Step 1: Enum, value object, kolom `Installment`**

Kolom baru `private set`. Method `internal` di `Installment`: `PromoteTo(InstallmentStatus status)` (hanya maju: `Pending` → `Reminder1` → `Reminder2` → `Overdue`; tidak pernah dari/ke `Paid`/`Cancelled`), `RecordReminder(DateOnly today)` (`LastRemindedOn = today`, `ReminderCount++`), `RecordOverdueAlert(DateOnly today)`.

- [ ] **Step 2: `Premi.EvaluateReminders`**

Algoritma (spec bagian 1), dengan `D` = `DueDate` target, `G` = `GracePeriodDays`:

```csharp
if (Status != PremiStatus.Active) return ReminderDecision.None;

// cicilan lain (dan target) yang lewat masa tenggang -> Overdue
foreach (var i in unpaid where today > i.DueDate.AddDays(G)) i.PromoteTo(Overdue);

var target = NextUnpaidInstallment();            // sudah ada
if (target == null) return None;
var D = target.DueDate;
if (today >= D.AddDays(-policy.DaysBeforeFirst))  target.PromoteTo(Reminder1);
if (today >= D.AddDays(-policy.DaysBeforeSecond)) target.PromoteTo(Reminder2);

// slot terbesar <= today dari {D-first, D-second, D, D+k*repeat}
DateOnly? slot = LatestSlot(D, today, policy);
bool alertDue = today >= D.AddDays(policy.AdminAlertAfterDueDays) && target.OverdueAlertSentOn == null;

if (RemindersPaused) return new(target, null, ..., AdminAlertDue: false);
ReminderKind? kind = slot != null && (target.LastRemindedOn == null || target.LastRemindedOn < slot)
    ? KindFor(today, D, G) : null;               // tabel jenis di spec bagian 1, ditentukan oleh today
return new(target, kind, DaysLate: Math.Max(0, today.DayNumber - D.DayNumber), D.AddDays(G), alertDue);
```

`RecordReminderSent` / `RecordOverdueAlertSent`: cari cicilan; tidak ada → `DomainException("Installment not found in this premi.")`.

- [ ] **Step 3: Pause/resume**

`PauseReminders`: `Status != Active` → `DomainException("Only active premi can pause reminders.")`; sudah dihentikan → `DomainException("Reminders are already paused.")`. `ResumeReminders`: tidak sedang dihentikan → `DomainException("Reminders are not paused.")`; resume mengosongkan `RemindersPausedAt` dan `RemindersPausedByUserId`.

- [ ] **Step 4: `JobRun` dan konfigurasi EF**

- `JobRunConfiguration`: tabel `JobRuns`; `JobName` max `Short`; `Status` string max `Short`; `LastError` max 1000 (dipotong di `Fail`); unique index `(JobName, BusinessDate)`; `RowVersion` sebagai `xmin` (pola `InstallmentConfiguration`); default timestamp seperti tabel lain.
- `InstallmentConfiguration`: dua kolom baru (`date`, nullable); index `(Status, DueDate)`.
- `PremiConfiguration`: `RemindersPaused` default `false`; FK `RemindersPausedByUserId` → `Users` restrict, tanpa navigation.
- Converter `JobRunId`, `DbSet` di context + interface.
- `PremiResponseDto.RemindersPaused` (Mapster mengisi otomatis dari nama yang sama).

- [ ] **Step 5: (User) build dan migration**

1. `dotnet build Chakra.sln` → `0 Error(s)`.
2. `.\add-migration.ps1 AddScheduler`. `Up()` hanya: dua kolom di `Installments`, tiga kolom di `Premis` + FK, index `Installments (Status, DueDate)`, tabel `JobRuns` + unique index.
3. `.\update-database.ps1`.

---

### Task 2: Stop/resume pengingat (admin)

**Files:**
- Create: `Chakra.Application/Features/Premi/Command/PauseReminders.cs`, `ResumeReminders.cs`
- Modify: `Chakra.API/Endpoints/PremiEndpoint.cs`

**Interfaces:**
- Consumes: `Premi.PauseReminders/ResumeReminders` (Task 1), `ICurrentUserService.DatabaseUserId`, `PremiResponseDto`, `premi.ToResponseDto()`.
- Produces:
  - `record PauseRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>`
  - `record ResumeRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>`
  - `POST /api/premi/{id:guid}/reminders/pause` (nama `"Pause Premi Reminders"`), `POST /api/premi/{id:guid}/reminders/resume` (nama `"Resume Premi Reminders"`)

- [ ] **Step 1: Handler**

Pola `CancelPremi.cs`: load premi `.Include(p => p.Installments)`; tidak ada → `NotFound("Premi not found.")`; `DatabaseUserId` null → `Unauthorized("Unauthorized")`; `DomainException` → `Failure(ex.Message)`; `DbUpdateConcurrencyException` → `Failure("Premi was modified by another request, please retry.")`; sukses → `premi.ToResponseDto()`.

- [ ] **Step 2: Endpoint**

Sukses → 200 `ApiResponse<object>.Success(data, 200, "Reminders paused successfully")` / `"Reminders resumed successfully"`; gagal → `ToErrorResult()`.

- [ ] **Step 3: (User) build** → `0 Error(s)`.

---

### Task 3: `DailyReminderService` dan teks pesan

**Files:**
- Create: `Chakra.Application/Features/Scheduler/Settings/SchedulerSettings.cs`
- Create: `Chakra.Application/Features/Scheduler/ReminderMessages.cs`
- Create: `Chakra.Application/Features/Scheduler/DailyReminderService.cs`
- Create: `Chakra.API/Configurations/SchedulerSetup.cs`; Modify: `Chakra.API/Program.cs`, `Chakra.API/appsettings.example.json`

**Interfaces:**
- Consumes: `Premi.EvaluateReminders`, `RecordReminderSent`, `RecordOverdueAlertSent`, `ReminderPolicy`, `ReminderDecision`, `ReminderKind` (Task 1); `IPaymentLinkService.GetOrCreateAsync(InstallmentId, CancellationToken) : Task<Result<PaymentLinkDto>>`; `OutboxMessage.CreateTelegram`, `OutboxMessageType.TelegramReminder`, `OutboxMessageType.AdminAlert`; `RupiahFormat.Format`.
- Produces:
  - `SchedulerSettings` (`const SectionName = "Scheduler"`, properti dan default sesuai Global Constraints termasuk `TimeScale` (enum `SchedulerTimeScale { Days, Minutes }`) dan `SimulationStartDate`; `ReminderPolicy ToPolicy()`)
  - `static class ReminderMessages`: `string ForCustomer(ReminderDecision decision, string paymentUrl)`, `string ForAdmin(User user, Installment installment)`
  - `sealed record DailyReminderResult(int PremisProcessed, int RemindersQueued, int AdminAlertsQueued, int Skipped, int Failures)`
  - `class DailyReminderService` dengan `Task<DailyReminderResult> RunAsync(DateOnly businessDate, CancellationToken ct)`; konstruktor menerima `IServiceScopeFactory`, `IOptions<SchedulerSettings>`, `ILogger<DailyReminderService>` (+ `IApplicationDbContext` dari scope luar hanya untuk query kandidat)
  - `SchedulerSetup.AddSchedulerSetup(this IServiceCollection, IConfiguration)`

- [ ] **Step 1: Settings dan teks**

Teks persis tabel spec bagian 3. `ForCustomer` memilih template dari `decision.Kind`, mengisi `{N}`, `{Amount}`, tanggal, `{sisa}` (= `D - today` hari, minimal 1), `{DaysLate}`, `{link}`.

- [ ] **Step 2: Kandidat**

Query id premi: `Status == Active` dan punya cicilan bukan `Paid`/`Cancelled` dengan `DueDate <= businessDate.AddDays(DaysBeforeFirst)`. Urut `Id`, diproses per halaman `BatchSize`.

- [ ] **Step 3: Proses per premi (scope baru per premi)**

Urutan dan klasifikasi persis tabel "Lewati vs gagal" di spec:

1. Load premi `.Include(Installments).Include(User)`; `decision = premi.EvaluateReminders(businessDate, policy)`.
2. `decision.Kind != null`:
   - `User.TelegramChatId == null` → log information, `Skipped++` (tanpa link, tanpa `RecordReminderSent`).
   - Selain itu `GetOrCreateAsync(target.Id)`. Gagal dengan pesan `"Payment gateway is unavailable, please try again."` atau `"Payment link is being created, please retry."` → `Failures++`; gagal lainnya → log warning, `Skipped++`. Berhasil → outbox `TelegramReminder` ke `premi.UserId`, `RecordReminderSent`, `RemindersQueued++`.
3. `decision.AdminAlertDue`: admin `Role == Admin && TelegramChatId != null`. Ada → satu outbox `AdminAlert` per admin, `RecordOverdueAlertSent`, `AdminAlertsQueued++`. Tidak ada → log warning.
4. `SaveChangesAsync` (selalu, agar perubahan status tersimpan). `DbUpdateConcurrencyException` → `Failures++`, log, lanjut.
5. Exception lain per premi (kecuali `OperationCanceledException` saat dibatalkan) → log error, `Failures++`, lanjut.

`GetOrCreateAsync` berbagi `DbContext` scope premi dan memanggil `SaveChanges` sendiri; itu diterima (status tersimpan lebih awal, link dipakai ulang saat run ulang).

- [ ] **Step 4: Wiring**

`SchedulerSetup`: `Configure<SchedulerSettings>`, `AddScoped<DailyReminderService>()` (hosted service ditambah di Task 4). `AddSchedulerSetup` menerima juga `IHostEnvironment` (ubah tanda tangan menjadi `AddSchedulerSetup(this IServiceCollection, IConfiguration, IHostEnvironment)`) dan melempar `InvalidOperationException("Scheduler TimeScale 'Minutes' is only allowed in Development and requires SimulationStartDate.")` jika `TimeScale == Minutes` dan (bukan Development atau `SimulationStartDate` null). `Program.cs`: `.AddSchedulerSetup(builder.Configuration)`. `appsettings.example.json`: section `Scheduler` dengan semua default.

- [ ] **Step 5: (User) build** → `0 Error(s)`.

---

### Task 4: `DailyReminderProcessor` dan `JobRuns`

**Files:**
- Create: `Chakra.Infrastructure/BackgroundJobs/DailyReminderProcessor.cs`
- Modify: `Chakra.API/Configurations/SchedulerSetup.cs`

**Interfaces:**
- Consumes: `DailyReminderService.RunAsync`, `DailyReminderResult` (Task 3); `JobRun.Start/Restart/Complete/Fail/IsStale` (Task 1); `SchedulerSettings`.
- Produces: `class DailyReminderProcessor : BackgroundService`.

- [ ] **Step 1: Loop**

Kerangka `OutboxProcessor` (`PeriodicTimer`, scope per siklus, exception siklus di-log). Interval: `Days` → `CheckIntervalMinutes`; `Minutes` → 20 detik. Catat `processStartedAt = timeProvider.GetUtcNow()` saat `ExecuteAsync` mulai. Per siklus, tentukan `businessDate` lewat method kecil `DateOnly? ResolveBusinessDate(DateTimeOffset now)`:

1. `Days`: `local = TimeZoneInfo.ConvertTime(now, tz)`; jika `TimeOnly.FromDateTime(local.DateTime) < TimeOnly.Parse(RunAtLocalTime)` → `null` (selesai); selain itu `DateOnly.FromDateTime(local.DateTime)`.
2. `Minutes`: `SimulationStartDate.Value.AddDays((int)Math.Floor((now - processStartedAt).TotalMinutes))`.
3. Klaim (spec bagian 4 langkah 2, ditambah run `IsStale` diperlakukan seperti `Failed`):
   - Tidak ada baris → `JobRuns.Add(JobRun.Start(...))`, `SaveChanges`; `DbUpdateException` (bukan konkurensi) = instance lain sudah klaim → selesai.
   - `Failed`/stale dan `Attempt < MaxAttempts` → `Restart(now)`, `SaveChanges`; `DbUpdateConcurrencyException` → selesai.
   - Lainnya → selesai.
4. `RunAsync(businessDate)` dalam scope baru → `Complete(...)`. Exception → `Fail(now, ex.Message)`.
5. Jika hasil akhirnya `Failed` dan `Attempt >= MaxAttempts` → outbox `AdminAlert` ke setiap admin ber-Telegram: `Job pengingat harian {businessDate:dd MMM yyyy} gagal setelah {Attempt} percobaan: {LastError ?? $"{Failures} premi gagal diproses"}`. Simpan.

- [ ] **Step 2: Registrasi** `AddHostedService<DailyReminderProcessor>()` di `SchedulerSetup`.

- [ ] **Step 3: (User) build** → `0 Error(s)`.

---

### Task 5: Endpoint Development dan verifikasi (dilakukan user)

**Files:**
- Create: `Chakra.API/Endpoints/JobEndpoint.cs`; Modify: `Chakra.API/Program.cs`
- Modify: `Chakra.API/Chakra.API.http` (section `### Scheduler`)

**Interfaces:**
- Consumes: `DailyReminderService.RunAsync` (Task 3), `ICurrentUserService.IsAdmin`.
- Produces: `POST /api/jobs/daily-reminders/run?date=yyyy-MM-dd`, grup `/api/jobs` `.RequireAuthorization()`, di-map hanya jika `app.Environment.IsDevelopment()` (pola `MapOutboxEndpoints`). Non-admin → 403 `ApiErrorResponse.Error("Forbidden", 403)`. Sukses → 200 `ApiResponse<object>.Success(result, 200, "Daily reminders executed")`.

- [ ] **Step 1: Endpoint** sesuai Interfaces (tanpa `JobRuns`).

- [ ] **Step 2: `.http`** — request untuk setiap skenario di bawah, dengan komentar hasil yang diharapkan dan SQL reset dari spec bagian 5.

- [ ] **Step 3: (User) skenario** — tabel spec bagian 6 (skenario 1–12), ditambah:

| # | Skenario | Harus |
|---|---|---|
| 13 | Nasabah belum link Telegram, `date` = H-3 | Tidak ada transaksi baru di `PaymentTransactions`, `LastRemindedOn` null, ringkasan `skipped` 1. Link Telegram lalu jalankan lagi → pesan terkirim |
| 14 | `POST .../reminders/pause` dua kali | Kedua: 400 `"Reminders are already paused."`; user biasa → 403 |
| 15 | Mode menit: `appsettings.Development.json` `Scheduler: { "TimeScale": "Minutes", "SimulationStartDate": "2027-01-10" }`, premi jatuh tempo 15 Jan 2027, masa tenggang 7, lalu start API | Menit ke-2 pesan Upcoming, ke-4 DueTomorrow, ke-5 DueToday, ke-8 dan ke-11 GracePeriod, ke-13 status `Overdue`, ke-14 pesan terlambat, ke-32 alert admin. Satu baris `JobRuns` per tanggal simulasi |
| 16 | `TimeScale: "Minutes"` dengan `ASPNETCORE_ENVIRONMENT=Production` | API gagal start dengan pesan TimeScale |
