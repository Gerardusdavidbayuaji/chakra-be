# Scheduler (Pengingat Cicilan) Design

**Tanggal:** 2026-11-23
**Status:** Draft, menunggu persetujuan
**Scope:** Job harian yang mengirim pengingat cicilan lewat Telegram (berisi link bayar Snap), mengubah status cicilan (`Reminder1`, `Reminder2`, `Overdue`), mengirim alert ke admin untuk tunggakan panjang, serta fitur admin untuk menghentikan/melanjutkan pengingat per premi. Termasuk jejak audit setiap run (`JobRuns`).
**Bergantung pada:** `2026-11-22-midtrans-design.md` (`IPaymentLinkService`), `2026-11-21-installments-design.md` (aturan bayar berurutan, role admin), `2026-09-30-outbox-telegram-design.md` (outbox).

## Latar Belakang dan Tujuan

Cicilan dan pembayaran Midtrans sudah berjalan, tapi belum ada yang mengingatkan nasabah. Status `Reminder1`, `Reminder2`, `Overdue`, kolom `ReminderCount`, dan tipe outbox `TelegramReminder` sudah ada sejak awal tapi belum dipakai. Fitur ini membuat nasabah diingatkan sebelum, saat, dan sesudah jatuh tempo dengan link bayar langsung, dan admin diberi tahu saat tunggakan sudah lama.

Aplikasi akan di-deploy di **VPS (Hostinger)** yang selalu menyala, sehingga job berjalan sebagai `BackgroundService` di dalam aplikasi, sama seperti `OutboxProcessor` dan `PaymentExpiryProcessor`.

## Tujuan

1. Setiap hari pukul 09:00 WIB, nasabah dengan cicilan yang perlu diingatkan menerima satu pesan Telegram berisi link bayar Snap.
2. Jadwal pengingat per cicilan: H-3, H-1, hari H, lalu setiap 3 hari sejak hari H sampai dibayar.
3. Status cicilan mencerminkan posisinya: `Reminder1` (setelah H-3), `Reminder2` (setelah H-1), `Overdue` (setelah masa tenggang).
4. Pada hari ke-30 sejak H-3 (`DueDate + 27`), admin menerima alert satu kali per cicilan.
5. Admin bisa menghentikan dan melanjutkan pengingat per premi tanpa membatalkan premi.
6. Setiap run tercatat (kapan, berapa diproses, berapa terkirim, berapa gagal) dan tidak berjalan dobel untuk tanggal yang sama.
7. Run ulang (setelah gagal, restart, atau multi-instance) tidak mengirim pesan dobel.

## Non-goals

- Kanal selain Telegram (e-mail, SMS, WhatsApp).
- Denda keterlambatan, pembatalan premi otomatis, atau perubahan nominal.
- Pengingat untuk cicilan selain cicilan belum lunas yang paling awal di setiap premi.
- Kalender hari libur (job berjalan setiap hari, termasuk akhir pekan).
- Dashboard job; jejak audit cukup lewat tabel `JobRuns` dan log.
- Pengaturan jadwal per premi atau per nasabah.
- Memindahkan worker ke Cloud Scheduler / serverless.

## Anti-goals

- Nasabah menerima dua pesan untuk hal yang sama di hari yang sama.
- Pesan pengingat dikirim untuk cicilan yang sudah dibayar, premi yang dibatalkan, atau premi yang pengingatnya dihentikan admin.
- Pesan berisi link untuk cicilan yang belum boleh dibayar (melanggar aturan bayar berurutan).
- Aturan jadwal tersebar di worker; aturan harus di domain agar bisa diuji dan dipanggil dari pemicu lain nanti.
- Panggilan ke Midtrans (link bayar) di dalam domain event handler.

## Approaches

| # | Pendekatan | Trade-off |
|---|---|---|
| A | **`BackgroundService` + tabel `JobRuns`** (dipilih) | Konsisten dengan dua worker yang ada, tanpa dependency baru. Jejak audit ala batch EOD lewat satu tabel. Tanpa dashboard. |
| B | Hangfire | Dashboard, cron, retry bawaan. Dependency baru, ~10 tabel tambahan, pola berbeda dari worker yang ada. Berlebihan untuk satu job. |
| C | Pemicu eksternal (GCP Cloud Scheduler / n8n) memanggil endpoint internal | Cocok untuk serverless. Tidak dibutuhkan di VPS; menambah sistem dan endpoint yang harus diamankan. |

User memilih deploy di VPS, sehingga **A** dipakai. Logika ada di satu service aplikasi yang dipanggil worker, sehingga pemicu C bisa ditambahkan nanti tanpa menulis ulang logika.

## Keputusan

| Topik | Keputusan |
|---|---|
| Waktu run | Setiap hari 09:00 `Asia/Jakarta` (bukan jam server). "Hari ini" = tanggal di `Asia/Jakarta` |
| Yang diingatkan | Per premi `Active` yang pengingatnya tidak dihentikan: **hanya** cicilan belum lunas dengan nomor terkecil |
| Hari pengingat | H-3, H-1, hari H, lalu H+3, H+6, H+9, ... (setiap 3 hari sejak hari H) sampai dibayar |
| Masa tenggang | Hari H s.d. `DueDate + GracePeriodDays`: pesan bernada "masa tenggang". Setelahnya: "terlambat" |
| Status | H-3 → `Reminder1`; H-1 → `Reminder2`; lewat masa tenggang → `Overdue`. Hari H dan masa tenggang tidak mengubah status |
| Status cicilan lain | Cicilan belum lunas lain yang lewat masa tenggang ikut jadi `Overdue`, tanpa pesan |
| Catch-up | Jika satu atau lebih hari pengingat terlewat (server mati), kirim **satu** pesan sesuai kondisi hari ini; status langsung lompat ke status yang benar |
| Alert admin | Satu kali per cicilan, pada `DueDate + 27` (hari ke-30 sejak H-3) atau run pertama setelahnya, ke semua admin yang sudah link Telegram. Pengingat ke nasabah tetap jalan |
| Stop/resume | Per premi, oleh admin. Saat dihentikan: tidak ada pesan ke nasabah dan tidak ada alert admin; perubahan status tetap berjalan. Saat dilanjutkan: lanjut dari kondisi hari itu, tanpa mengirim pesan yang terlewat |
| Link bayar | Dari `IPaymentLinkService.GetOrCreateAsync` (link yang masih berlaku dipakai ulang) |
| Link gagal dibuat | Cicilan itu tidak ditandai sudah diingatkan; dicoba lagi di percobaan run berikutnya |
| Idempotensi | Per cicilan disimpan tanggal pengingat terakhir; satu run per tanggal bisnis dicatat di `JobRuns` (unik) |

*(Jam 09:00, siklus dari hari H, catch-up satu pesan, cicilan lain tanpa pesan, resume tanpa pesan terlewat, dan perilaku saat dihentikan adalah usulan Claude yang tidak dikoreksi user.)*

## 1. Aturan Jadwal (Domain)

Untuk cicilan target dengan jatuh tempo `D` dan masa tenggang `G`, himpunan **hari pengingat** adalah:

```
{ D-3, D-1, D, D+3, D+6, D+9, ... }
```

Pada tanggal `today`, ambil `L` = hari pengingat terbesar yang `<= today`.

- Tidak ada `L` (today < D-3): tidak ada pesan.
- `LastRemindedOn` cicilan `>= L`: sudah diingatkan untuk slot ini, tidak ada pesan.
- Selain itu: kirim **satu** pesan, jenisnya ditentukan oleh `today` (bukan `L`):

| Kondisi `today` | Jenis pesan |
|---|---|
| `D-3 <= today < D-1` | `Upcoming` (jatuh tempo D, sisa hari) |
| `D-1 <= today < D` | `DueTomorrow` |
| `today == D` | `DueToday` (sebut akhir masa tenggang `D+G`) |
| `D < today <= D+G` | `GracePeriod` (sebut akhir masa tenggang) |
| `today > D+G` | `Overdue` (sebut jumlah hari terlambat `today - D`) |

Saat pesan diputuskan dikirim dan berhasil diantrekan: `LastRemindedOn = today`, `ReminderCount++`.

Perubahan status (berlaku untuk cicilan target, tanpa bergantung pada pengiriman pesan dan pada stop/resume):

- `Pending` dan `today >= D-3` → `Reminder1`.
- `Pending`/`Reminder1` dan `today >= D-1` → `Reminder2`.
- Belum lunas, bukan `Cancelled`, dan `today > D+G` → `Overdue` (berlaku juga untuk cicilan belum lunas lain di premi yang sama).

Status tidak pernah mundur. Status `Pending`/`Reminder*`/`Overdue` tetap bisa dibayar (aturan bayar tidak berubah).

**Alert admin:** jika `today >= D+27`, cicilan target belum lunas, dan `OverdueAlertSentOn` masih null → alert dikirim dan `OverdueAlertSentOn = today`.

Contoh `D` = 15 Jan, `G` = 7: 12 Jan `Upcoming` (+`Reminder1`), 14 Jan `DueTomorrow` (+`Reminder2`), 15 Jan `DueToday`, 18 Jan dan 21 Jan `GracePeriod`, 23 Jan status `Overdue` tanpa pesan (bukan hari pengingat), 24 Jan `Overdue` (H+9), 27 Jan, ... ; 11 Feb (`D+27`) alert admin.

## 2. Domain: `Chakra.Domain`

### `Installment`

Kolom baru:

| Field | Tipe | Keterangan |
|---|---|---|
| `LastRemindedOn` | `DateOnly?` | tanggal (WIB) pesan pengingat terakhir diantrekan |
| `OverdueAlertSentOn` | `DateOnly?` | tanggal alert admin dikirim |

Method `internal` baru untuk transisi status, pencatatan pengingat (`LastRemindedOn`, `ReminderCount++`), dan pencatatan alert.

### `Premi`

Kolom baru:

| Field | Tipe | Keterangan |
|---|---|---|
| `RemindersPaused` | `bool` | default `false` |
| `RemindersPausedAt` | `DateTime?` | UTC |
| `RemindersPausedByUserId` | `UserId?` | admin yang menghentikan |

Method:

```csharp
public void PauseReminders(UserId adminId, DateTime now);   // ditolak jika sudah dihentikan atau premi tidak Active
public void ResumeReminders();                               // ditolak jika tidak sedang dihentikan

// Menerapkan perubahan status untuk hari ini dan mengembalikan keputusan (tanpa efek samping lain)
public ReminderDecision EvaluateReminders(DateOnly today, ReminderPolicy policy);

// Dipanggil setelah pesan berhasil diantrekan / alert dibuat
public void RecordReminderSent(InstallmentId installmentId, DateOnly today);
public void RecordOverdueAlertSent(InstallmentId installmentId, DateOnly today);
```

- `ReminderPolicy`: `DaysBeforeFirst = 3`, `DaysBeforeSecond = 1`, `RepeatEveryDays = 3`, `AdminAlertAfterDueDays = 27` (nilai dari settings).
- `ReminderDecision`: cicilan target (atau null), jenis pesan (atau null), `DaysLate`, `GracePeriodEndsOn`, dan apakah alert admin perlu dikirim. Jika `RemindersPaused`, jenis pesan dan alert selalu null.
- `EvaluateReminders` hanya untuk premi `Active`; premi lain menghasilkan keputusan kosong.
- Pelanggaran (pause/resume tidak valid) → `DomainException`.

`ReminderKind` (enum baru): `Upcoming`, `DueTomorrow`, `DueToday`, `GracePeriod`, `Overdue`.

### `JobRun : Entity<JobRunId>`

| Field | Tipe | Keterangan |
|---|---|---|
| `JobName` | `string` | `"DailyReminders"` |
| `BusinessDate` | `DateOnly` | tanggal WIB yang diproses |
| `Status` | `JobRunStatus` | `Running`, `Succeeded`, `Failed` |
| `Attempt` | `int` | 1.. |
| `StartedAt`, `FinishedAt` | `DateTime`, `DateTime?` | UTC |
| `PremisProcessed`, `RemindersQueued`, `AdminAlertsQueued`, `Skipped`, `Failures` | `int` | ringkasan |
| `LastError` | `string?` | dipotong 1000 karakter |

Unik pada `(JobName, BusinessDate)`.

## 3. Application: `Chakra.Application`

### `DailyReminderService` (dipanggil worker; nanti bisa dipanggil pemicu lain)

`Task<DailyReminderResult> RunAsync(DateOnly businessDate, CancellationToken ct)`

Untuk setiap premi `Active` yang punya cicilan belum lunas dengan `DueDate <= businessDate + 3` (dalam batch):

1. **Scope DI dan `DbContext` baru per premi**, agar kegagalan satu premi tidak mengotori yang lain (catatan dari review Midtrans).
2. Muat premi + cicilan + user. `decision = premi.EvaluateReminders(businessDate, policy)`.
3. Jika ada jenis pesan: `IPaymentLinkService.GetOrCreateAsync(target.Id)`.
   - Gagal → simpan perubahan status saja, hitung sebagai `Failures`, lanjut ke premi berikutnya (tanpa `RecordReminderSent`).
   - Berhasil → tambah `OutboxMessage.CreateTelegram(TelegramReminder, premi.UserId, teks)` lalu `premi.RecordReminderSent(...)`.
4. Jika alert admin perlu: tambah satu `OutboxMessage.CreateTelegram(AdminAlert, adminId, teks)` per admin yang sudah link Telegram, lalu `premi.RecordOverdueAlertSent(...)`.
5. `SaveChangesAsync`. `DbUpdateConcurrencyException` (misalnya pembayaran masuk bersamaan) → lewati premi ini, hitung `Failures`; run ulang akan mengevaluasinya lagi.

Link bayar dibuat **sebelum** pesan diantrekan dan di luar domain event handler, karena `GetOrCreateAsync` memanggil Midtrans.

### Lewati vs gagal *(keputusan Claude)*

| Kasus | Perlakuan | Dihitung `Failures`? |
|---|---|---|
| Nasabah belum link Telegram | Dicek **sebelum** membuat link: tidak membuat transaksi Midtrans, tidak `RecordReminderSent`, log information. Status tetap diperbarui. Begitu nasabah link Telegram, run berikutnya mengirim pesan untuk slot terakhir (catch-up) | Tidak (`Skipped`) |
| `GetOrCreateAsync` gagal karena aturan bayar (`CheckCanPay`/`NotFound`) | Log warning, lewati | Tidak (`Skipped`) |
| `GetOrCreateAsync` gagal karena gateway (`Payment gateway is unavailable` / `being created`) | Lewati premi ini | Ya |
| `DbUpdateConcurrencyException` | Lewati premi ini | Ya |
| Tidak ada admin yang sudah link Telegram saat alert jatuh tempo | `OverdueAlertSentOn` **tidak** diisi (alert dicoba lagi besok), log warning | Tidak |

`DailyReminderResult` memuat `PremisProcessed`, `RemindersQueued`, `AdminAlertsQueued`, `Skipped`, `Failures`. Hanya `Failures` yang membuat run `Failed` dan memicu percobaan ulang. Jika percobaan habis (mis. Midtrans mati lebih dari ~15 menit), run hari itu tetap `Failed`, admin menerima alert, dan cicilan yang gagal ditangani oleh catch-up run besok.

### Teks pesan (Telegram, Bahasa Indonesia)

| Jenis | Teks |
|---|---|
| `Upcoming` | `Pengingat: cicilan ke-{N} sebesar Rp{Amount} jatuh tempo pada {D:dd MMM yyyy} ({sisa} hari lagi).\nBayar sekarang: {link}` |
| `DueTomorrow` | `Cicilan ke-{N} sebesar Rp{Amount} jatuh tempo besok ({D:dd MMM yyyy}).\nBayar sekarang: {link}` |
| `DueToday` | `Cicilan ke-{N} sebesar Rp{Amount} jatuh tempo hari ini. Masa tenggang sampai {D+G:dd MMM yyyy}.\nBayar sekarang: {link}` |
| `GracePeriod` | `Cicilan ke-{N} sebesar Rp{Amount} sudah lewat jatuh tempo. Masa tenggang berakhir {D+G:dd MMM yyyy}.\nBayar sekarang: {link}` |
| `Overdue` | `Cicilan ke-{N} sebesar Rp{Amount} terlambat {DaysLate} hari.\nSegera bayar: {link}` |
| Alert admin | `Tunggakan 30 hari.\nNasabah: {Name} ({Email})\nCicilan ke-{N}, Rp{Amount}, jatuh tempo {D:dd MMM yyyy}.\nPengingat tetap dikirim. Hentikan lewat menu premi jika perlu.` |

Tanggal diformat dengan culture `id-ID`; nominal memakai `RupiahFormat`.

### Command admin

| Request | Interface | Isi |
|---|---|---|
| `PauseRemindersCommand(Guid PremiId)` | `IAdminRequest` | `premi.PauseReminders(adminId, now)` |
| `ResumeRemindersCommand(Guid PremiId)` | `IAdminRequest` | `premi.ResumeReminders()` |

Premi tidak ada → `NotFound`; `DomainException` → `Failure`. Respons: `PremiResponseDto`, yang ditambah `RemindersPaused`.

### Settings `Scheduler` (`Features/Scheduler/Settings/`)

| Key | Default |
|---|---|
| `RunAtLocalTime` | `"09:00"` |
| `TimeZone` | `"Asia/Jakarta"` |
| `CheckIntervalMinutes` | `5` |
| `MaxAttempts` | `3` |
| `BatchSize` | `100` |
| `DaysBeforeFirst` / `DaysBeforeSecond` / `RepeatEveryDays` / `AdminAlertAfterDueDays` | `3` / `1` / `3` / `27` |
| `TimeScale` | `"Days"` (`"Minutes"` hanya di Development) |
| `SimulationStartDate` | `null` (wajib diisi jika `TimeScale = Minutes`) |

## 4. Infrastructure: `Chakra.Infrastructure`

### `DailyReminderProcessor : BackgroundService`

Tiap `CheckIntervalMinutes`:

1. Hitung waktu lokal di `TimeZone`. Jika sebelum `RunAtLocalTime` → tidak ada aksi.
2. `businessDate` = tanggal lokal hari ini. Klaim run:
   - Belum ada `JobRun` untuk tanggal ini → insert `Running`, `Attempt = 1` (unique index mencegah instance lain ikut jalan; pelanggaran unik = instance lain sudah mengklaim).
   - Ada dan `Failed` dengan `Attempt < MaxAttempts` → klaim ulang (`Running`, `Attempt++`) dengan konkurensi optimistis.
   - Ada dan `Succeeded`, `Running`, atau `Failed` dengan percobaan habis → tidak ada aksi.
3. Panggil `DailyReminderService.RunAsync(businessDate)`.
4. Selesai (catat ringkasan `DailyReminderResult` di `JobRun`, termasuk kolom `Skipped`): `Succeeded` jika `Failures == 0`, selain itu `Failed` (dicoba lagi di siklus berikutnya; karena idempoten, hanya yang gagal yang terkirim). Exception tingkat run → `Failed` + `LastError`.
5. Saat `Failed` dengan percobaan habis: alert Telegram ke admin satu kali (`Job pengingat harian {tanggal} gagal setelah {N} percobaan: {LastError}`).

### Skala waktu Development (`TimeScale`)

Diminta user: di Development, interval hari diganti menit agar seluruh alur bisa diuji dalam beberapa menit. Di Production tetap hari.

| `TimeScale` | Perilaku |
|---|---|
| `Days` (default, Production) | Seperti di atas: sekali sehari setelah `RunAtLocalTime` di `TimeZone` |
| `Minutes` (Development) | Worker mengecek tiap 20 detik, mengabaikan `RunAtLocalTime`. `businessDate = SimulationStartDate + floor(menit sejak proses start)`; 1 menit nyata = 1 hari simulasi |

- Aturan domain (H-3, H-1, +3 hari, `D+27`) dan `JobRuns` tidak berubah; hanya sumber `businessDate` yang berbeda.
- Restart API mengulang tanggal simulasi dari `SimulationStartDate`; tanggal yang sudah punya `JobRun` dilewati. Untuk mengulang: ganti `SimulationStartDate` atau kosongkan `JobRuns`.
- `TimeScale = Minutes` di luar environment Development → aplikasi menolak start (log error + exception saat startup), agar tidak terbawa ke Production. *(Keputusan Claude.)*

Run yang `Running` tapi tertinggal (proses mati di tengah) dianggap gagal jika `StartedAt` lebih dari 1 jam lalu, lalu boleh diklaim ulang. *(Keputusan Claude.)*

Hanya hari ini yang diproses; tanggal yang terlewat karena server mati tidak dijalankan mundur, karena aturan catch-up di domain sudah menutupnya.

### Persistence

- `Installments`: kolom `LastRemindedOn` (`date`), `OverdueAlertSentOn` (`date`).
- `Premis`: `RemindersPaused` (default `false`), `RemindersPausedAt`, `RemindersPausedByUserId` (FK `Users`, restrict, nullable).
- Tabel `JobRuns` + unique index `(JobName, BusinessDate)`; enum disimpan string; `xmin` sebagai concurrency token.
- Index pendukung query kandidat: `Installments (Status, DueDate)`.
- Migration `AddScheduler`.

## 5. API: `Chakra.API`

| Endpoint | Akses | Hasil |
|---|---|---|
| `POST /api/premi/{id}/reminders/pause` | admin | 200 `PremiResponseDto` |
| `POST /api/premi/{id}/reminders/resume` | admin | 200 `PremiResponseDto` |
| `POST /api/jobs/daily-reminders/run?date=yyyy-MM-dd` | admin, **hanya Development** | Menjalankan `DailyReminderService.RunAsync(date)` langsung (tanpa `JobRuns`), mengembalikan ringkasan. Untuk menguji jadwal tanpa menunggu hari berganti |

Endpoint Development memiliki efek nyata: mengisi `LastRemindedOn`/`ReminderCount`/status, membuat transaksi Snap sungguhan (masa berlakunya dihitung dari waktu nyata, bukan `date`), dan mengantrekan pesan Telegram sungguhan. Tidak ada dry-run atau reset; untuk mengulang skenario, pakai premi baru atau reset manual lewat SQL (`UPDATE "Installments" SET "LastRemindedOn" = NULL, "OverdueAlertSentOn" = NULL, "ReminderCount" = 0, "Status" = 'Pending' WHERE ...`). *(Keputusan Claude.)*

`SchedulerSetup` (baru) mendaftarkan settings, `DailyReminderService`, dan `DailyReminderProcessor`. `appsettings.example.json` menambah section `Scheduler`.

## 6. Verifikasi

Tanpa project test. User menjalankan build dan migration, lalu memakai endpoint Development dengan `date` yang disimulasikan pada satu premi (`DueDate` 15 Jan 2027, `GracePeriodDays` 7, user sudah link Telegram):

| # | `date` | Harus |
|---|---|---|
| 1 | 2027-01-11 | Tidak ada pesan, status `Pending` |
| 2 | 2027-01-12 | Pesan `Upcoming` dengan link Snap, status `Reminder1`, `ReminderCount` 1 |
| 3 | 2027-01-12 lagi | Tidak ada pesan kedua |
| 4 | 2027-01-14, 15 | `DueTomorrow` (+`Reminder2`), lalu `DueToday` |
| 5 | 2027-01-18, 21, lalu 23 | 18 dan 21: pesan `GracePeriod`; 23: status `Overdue`, tidak ada pesan |
| 6 | 2027-01-24 | Pesan `Overdue` "terlambat 9 hari" |
| 7 | Premi baru, langsung `date` = H+10 (catch-up) | Satu pesan `Overdue`, status langsung `Overdue` |
| 8 | Pause premi, `date` = hari pengingat berikutnya | Tidak ada pesan; resume lalu jalankan lagi → pesan terkirim |
| 9 | 2027-02-11 | Alert admin sekali; jalankan lagi → tidak dobel |
| 10 | Bayar cicilan 1, `date` = H-3 cicilan 2 | Pesan untuk cicilan ke-2, link bisa dibayar |
| 11 | Cicilan 2 belum lunas, cicilan 3 lewat masa tenggang | Hanya cicilan 2 yang diingatkan; cicilan 3 `Overdue` tanpa pesan |
| 12 | Worker: set `RunAtLocalTime` ke beberapa menit ke depan | Satu baris `JobRuns` `Succeeded`; restart API → tidak jalan dua kali |

## Diserahkan ke Builder

- Bentuk persis `ReminderDecision` dan pembagian method domain, selama aturan di bagian 1 dipenuhi di domain.
- Query kandidat premi dan batching.
- Cara klaim `JobRun` (insert + tangkap pelanggaran unik, atau SQL), selama aman untuk multi-instance.
- Format tanggal di teks pesan (contoh: `15 Jan 2027`).

## Catatan di Luar Scope

- Jika nanti pindah ke serverless, tambahkan endpoint pemicu yang memanggil `DailyReminderService` dan klaim `JobRun` yang sama.
- Laporan harian ke admin (rekap tunggakan) bisa memakai `JobRuns` dan pola worker yang sama.
