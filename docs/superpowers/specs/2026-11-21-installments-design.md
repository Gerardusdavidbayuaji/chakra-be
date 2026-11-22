# Installments Design

**Tanggal:** 2026-11-21
**Status:** Draft, menunggu persetujuan
**Scope:** Pembayaran cicilan (manual oleh admin), premi lunas otomatis, efek cancel ke cicilan, aturan bayar berurutan, perbaikan pembulatan dan jatuh tempo, role admin, dan cek kepemilikan data.

## Latar Belakang dan Tujuan

Saat ini cicilan hanya dibuat (di `CreatePremi` / `UpdatePremi`) dan bisa dibaca, tapi tidak ada cara menandainya lunas. Midtrans belum dikerjakan, dan ketika dikerjakan nanti webhook-nya harus **memanggil logika pembayaran yang sama**, bukan menulis ulang aturannya.

Pembayaran manual oleh admin adalah **fitur sungguhan** (bukan endpoint dev), misalnya untuk pembayaran di luar sistem.

Masalah lain yang ditemukan dan ikut dibereskan:

- Tidak ada role. `AuthorizationBehavior` hanya cek login, dan query Premi/Installment tidak dicek sama sekali, jadi siapa pun bisa membaca, mengubah, atau cancel premi milik orang lain.
- Logika generate jadwal cicilan diduplikasi di `CreatePremi` dan `UpdatePremi`.
- `Math.Round(Total / Tenor, 2)` membuat jumlah cicilan bisa tidak sama dengan `TotalAmount`.
- Cicilan pertama jatuh tempo sebulan setelah `StartDate`, padahal yang diinginkan bulan yang sama.
- Semua properti `Premi` / `Installment` `public set`, sehingga status bisa diubah dari mana saja.

## Tujuan

1. Admin bisa menandai satu cicilan lunas lewat API.
2. Saat cicilan terakhir lunas, premi otomatis `Completed` dalam transaksi yang sama.
3. Cancel premi membatalkan cicilan yang belum dibayar.
4. Cicilan harus dibayar berurutan.
5. Semua nominal dalam rupiah bulat, dan jumlah semua cicilan selalu tepat sama dengan `TotalAmount`.
6. Cicilan pertama jatuh tempo di bulan `StartDate` (dengan aturan geser di bawah).
7. Role `Admin` / `User` dengan hak akses di bawah, dan user biasa hanya melihat datanya sendiri.
8. Satu method domain yang nanti dipanggil webhook Midtrans tanpa perubahan aturan.

## Non-goals

- Integrasi Midtrans (order, webhook). Hanya disiapkan titik panggilnya.
- Scheduler, reminder (`Reminder1`/`Reminder2`, `ReminderCount`), dan perubahan ke `Overdue`.
- Handler untuk `InstallmentPaidEvent` (notifikasi Telegram via outbox). Event di-raise, belum ada yang menangkap.
- Refund untuk cicilan yang sudah dibayar pada premi yang di-cancel.
- Membatalkan pembayaran (unpay) cicilan.
- UI / endpoint untuk mengelola role. Admin pertama diset manual lewat SQL.
- Migrasi data jadwal cicilan lama. Premi yang sudah ada tetap dengan jadwal lamanya; aturan baru berlaku untuk premi yang dibuat atau di-update setelah ini.

## Anti-goals

- Aturan pembayaran tersebar di handler. Webhook Midtrans harus bisa melewati aturan yang sama tanpa duplikasi.
- Premi berubah `Completed` di transaksi terpisah dari pembayaran cicilan terakhirnya.
- User biasa bisa melihat atau mengubah premi orang lain.

## Pendekatan

| # | Pendekatan | Trade-off |
|---|---|---|
| 1 | **Rich domain di aggregate `Premi`** (dipilih) | Aturan di satu tempat, webhook Midtrans tidak bisa melewatinya. Harus refactor `CreatePremi`, `UpdatePremi`, `CancelPremi`. |
| 2 | `InstallmentPaymentService` di Application, entity tetap anemic | Perubahan lebih kecil, tapi setter tetap terbuka dan aturan generate/cancel tetap tersebar di handler. |

User memilih **pendekatan 1**, sejalan dengan pola `User.LinkTelegram()` yang sudah ada.

## Keputusan

| Topik | Keputusan |
|---|---|
| Lunas otomatis | Premi jadi `Completed` saat semua cicilannya `Paid`, di method yang sama dengan pembayaran |
| Titik pembayaran | `Premi.MarkInstallmentPaid(...)`; dipanggil endpoint admin sekarang dan webhook Midtrans nanti |
| Role | Kolom `Role` di tabel `Users` (enum `User` / `Admin`, disimpan string, default `User`) |
| Hak akses | Lihat tabel Hak Akses |
| Bayar berurutan | Cicilan ke-N hanya bisa dibayar jika cicilan 1..N-1 sudah `Paid` |
| Cancel premi | Cicilan belum `Paid` → status baru `Cancelled`; yang `Paid` tetap |
| Pembulatan | Rupiah bulat (Midtrans hanya menerima IDR bulat). Cicilan 1..N-1 = `Math.Floor(Total / Tenor)`; cicilan terakhir = `Total - sum(cicilan sebelumnya)`. `TotalAmount` wajib bilangan bulat |
| Jatuh tempo pertama | Bulan yang sama dengan `StartDate`; jika tanggal itu sudah lewat dari `StartDate`, geser ke bulan berikutnya |

## Hak Akses

| Aksi | Admin | User |
|---|---|---|
| Buat / ubah / cancel premi | ✅ | ❌ (403) |
| Tandai cicilan lunas | ✅ | ❌ (403) |
| Lihat daftar premi | semua | hanya miliknya |
| Lihat detail premi / daftar cicilan / detail cicilan | semua | hanya miliknya; milik orang lain → 404 |
| Tidak login | 401 untuk semua endpoint Premi & Installment | |

Data milik orang lain dijawab 404 (bukan 403) agar keberadaan data tidak bocor. *(Keputusan Claude.)*

## 1. Domain: `Chakra.Domain`

### Enum

- `InstallmentStatus`: tambah `Cancelled`.
- Baru `UserRole { User, Admin }` di `Entities/Enums/`.

### `User`

- Tambah `public UserRole Role { get; private set; } = UserRole.User;`
- `public bool IsAdmin => Role == UserRole.Admin;` (tidak dipetakan ke kolom).

### `Premi` (aggregate root untuk semua perubahan cicilan)

Semua properti jadi `private set`. Koleksi cicilan diekspos read-only (`IReadOnlyCollection<Installment>`) dengan backing field privat. Constructor tanpa parameter tetap ada (privat/protected) untuk EF.

Method:

```csharp
public static Premi Create(UserId userId, decimal totalAmount, int tenor, int dueDay,
                           int gracePeriodDays, DateOnly startDate);

public void Update(decimal totalAmount, int tenor, int dueDay,
                   int gracePeriodDays, DateOnly startDate);

public void Cancel();

public Installment MarkInstallmentPaid(InstallmentId installmentId, DateTime paidAt,
                                       string? midtransOrderId = null);
```

**`Create`**: set field, `Status = Active`, generate jadwal (lihat Generate Jadwal).

**`Update`**: ditolak jika `Status != Active` atau ada cicilan `Paid`. Mengganti semua cicilan dengan jadwal baru (cicilan lama dihapus, seperti perilaku sekarang).

**`Cancel`**: ditolak jika `Status` sudah `Cancelled` atau `Completed`. Set `Status = Cancelled`, dan setiap cicilan yang bukan `Paid` jadi `Cancelled`.

**`MarkInstallmentPaid`**, urutan cek:

0. **Idempotensi:** jika cicilan sudah `Paid` dan `midtransOrderId` tidak null serta sama dengan `MidtransOrderId` yang tersimpan, kembalikan cicilan itu tanpa perubahan dan tanpa raise event (notifikasi ulang Midtrans tidak boleh gagal). *(Keputusan Claude, sejalan dengan "Idempotency" di README.)*
1. Premi harus `Active` (bukan `Cancelled` / `Completed`). Pembayaran yang masuk untuk premi yang sudah di-cancel tetap ditolak di sini; penanganannya (misal dicatat untuk refund) diputuskan di fitur Midtrans.
2. Cicilan dengan `installmentId` harus ada di premi ini.
3. Cicilan belum `Paid` (pembayaran manual kedua, atau order Midtrans berbeda, ditolak).
4. Semua cicilan dengan `InstallmentNumber` lebih kecil sudah `Paid`.
5. Cicilan: `Status = Paid`, `PaidAt = paidAt`, `MidtransOrderId = midtransOrderId` (jika tidak null).
6. Raise `InstallmentPaidEvent(installmentId, Id)` lewat `AddDomainEvent` di `Premi`.
7. Jika semua cicilan `Paid` → `Status = Completed`.

Status cicilan `Pending`, `Reminder1`, `Reminder2`, `Overdue` semuanya boleh dibayar.

### `Installment`

Semua properti `private set` (kecuali yang dibutuhkan EF). Dibuat hanya dari `Premi` (constructor/factory `internal`). Method `internal`: `MarkPaid(DateTime paidAt, string? midtransOrderId)` dan `Cancel()`. `Installment` tetap `Aggregate<InstallmentId>` (tidak diubah di scope ini).

### Generate Jadwal

Untuk `i = 1..Tenor`:

- **Bulan dasar**: bulan dari `StartDate`. Hitung `firstDue = new DateOnly(StartDate.Year, StartDate.Month, min(DueDay, hariDalamBulan))`. Jika `firstDue < StartDate`, bulan dasar maju satu bulan.
- **DueDate cicilan ke-i** = bulan dasar + (i-1) bulan, tanggal `min(DueDay, hariDalamBulanItu)`. Dihitung dari bulan dasar setiap kali (bukan berantai dari due date sebelumnya) agar `DueDay = 31` tidak "menyusut" setelah Februari.
- **Amount**: `baseAmount = Math.Floor(Total / Tenor)` untuk cicilan 1..Tenor-1; cicilan terakhir = `Total - baseAmount * (Tenor - 1)`. Contoh: 12.000.000 / 7 → 6 × 1.714.285 + 1 × 1.714.290.
- `Premi.InstallmentAmount` tetap diisi `baseAmount`.
- `Premi.Create` / `Update` menolak `TotalAmount` yang bukan bilangan bulat (`DomainException`), dan validator `CreatePremiInput` / `UpdatePremiInput` ditambah aturan yang sama agar client dapat pesan validasi per field. Kolom tetap `decimal(18,2)`, tanpa perubahan skema.

Contoh: `StartDate` 10 Jan, `DueDay` 15 → 15 Jan, 15 Feb, ... `StartDate` 20 Jan, `DueDay` 15 → 15 Feb, 15 Mar, ... `StartDate` 31 Jan, `DueDay` 31 → 31 Jan, 28/29 Feb, 31 Mar, ...

### Error domain

Pelanggaran aturan dilempar sebagai `DomainException` (baru, `Chakra.Domain/Exceptions/DomainException.cs`) dengan pesan yang bisa ditampilkan ke client. Handler menangkapnya dan mengembalikan `Result.Failure(message)` → 400. *(Keputusan Claude: domain tidak punya akses ke `Result`, yang ada di Application.)*

## 2. Application: `Chakra.Application`

### Otorisasi

- `Result<T>` ditambah `ErrorType` (`Failure` default, `NotFound`, `Unauthorized`, `Forbidden`) dan factory `NotFound(...)`, `Unauthorized(...)`, `Forbidden(...)`. `Failure` / `ValidationFailure` yang sudah ada tidak berubah perilakunya. *(Keputusan Claude: dibutuhkan agar endpoint bisa membedakan 401/403/404 dari 400.)*
- `AuthorizationBehavior` mengembalikan `ErrorType.Unauthorized`.
- Interface penanda baru `IAdminRequest<TResponse> : IAuthorizedRequest<TResponse>` + `AdminAuthorizationBehavior` yang mengembalikan `Forbidden` jika `!currentUser.IsAdmin`. Didaftarkan di `MediatRSetup` setelah `AuthorizationBehavior`.
- `ICurrentUserService`: tambah `bool IsAdmin` (dari `GetCurrentUser()?.Role`).

### Command

| Command | Interface | Isi |
|---|---|---|
| `CreatePremiInput` | `IAdminRequest` | Cek user tujuan ada, `Premi.Create(...)`, simpan |
| `UpdatePremiInput` | `IAdminRequest` | Load premi + cicilan, `premi.Update(...)`, simpan |
| `CancelPremiRequest` | `IAdminRequest` | Load premi + cicilan, `premi.Cancel()`, simpan |
| `PayInstallmentCommand(Guid InstallmentId)` **baru** | `IAdminRequest` | Cari `PremiId` dari cicilan, load premi + semua cicilannya, `premi.MarkInstallmentPaid(id, timeProvider.GetUtcNow().UtcDateTime)`, simpan, kembalikan `InstallmentResponseDto` |

`PayInstallmentCommand` ada di `Features/Installments/Command/PayInstallment.cs`.

### Query

Semua query Premi dan Installment jadi `IAuthorizedRequest`:

- `GetAllPremiQuery`: non-admin difilter `UserId == currentUser`.
- `GetPremiByIdQuery`, `GetAllInstallmentByPremiIdQuery`, `GetInstallmentByIdQuery`: non-admin dan bukan pemilik → `NotFound`.

Field ringkasan di `PremiResponseDto` dihitung di satu tempat, yaitu properti/method read-only di `Premi` (karena cicilan terakhir sekarang bisa beda nominal, dan ada status `Cancelled`):

- `InstallmentsPaid` = jumlah cicilan `Paid`.
- `RemainingAmount` = jumlah `Amount` cicilan yang bukan `Paid` dan bukan `Cancelled`.
- `NextDueDate` = `DueDate` terkecil dari cicilan yang bukan `Paid` dan bukan `Cancelled`.

Semua response premi mengisi field ini: `GetPremiById`, `GetAllPremi` (query memuat cicilan), dan response Create/Update/Cancel. Sekarang hanya `GetPremiById` yang mengisinya; yang lain mengembalikan 0/null. *(Keputusan Claude: daftar premi akan jadi layar utama user biasa.)*

## 3. Infrastructure: `Chakra.Infrastructure`

- `UserConfiguration`: `Role` → `HasConversion<string>()`, max length `EfConstants.Length.Short`, default `"User"`, required.
- `PremiConfiguration`: konfigurasi navigation `Installments` ke backing field.
- Migration baru `AddUserRole` (kolom `Role`, default `User` untuk baris lama). `InstallmentStatus` disimpan string, jadi `Cancelled` tidak butuh perubahan skema.
- Migration yang sama berisi backfill data: cicilan yang belum `Paid` milik premi yang sudah `Cancelled` di-set `Cancelled` (`CancelPremi` lama tidak menyentuh cicilan). Tanpa ini, premi cancel lama tetap menampilkan `RemainingAmount` dan nanti diambil Scheduler. *(Keputusan Claude.)*
- Konkurensi: tetap memakai `xmin` di `Installments`. Dua pembayaran untuk cicilan yang sama, atau bayar vs cancel / update yang bersamaan, sama-sama mengubah baris cicilan sehingga salah satunya gagal dengan `DbUpdateConcurrencyException`. Handler mengubahnya jadi `Failure("Installment was modified by another request, please retry.")`.

## 4. API: `Chakra.API`

- Endpoint baru: `POST /api/installments/{id:guid}/pay` → 200 dengan `InstallmentResponseDto`.
- Grup endpoint Premi dan Installment: `.RequireAuthorization()`.
- Mapping `ErrorType` → `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, lainnya 400. Dibuat helper bersama agar tidak ditulis di setiap endpoint.

## Setup Admin

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Email" = '<email admin>';
```

## Diserahkan ke Builder

- Nama dan bentuk helper mapping `Result` → `IResult`.
- Detail EF untuk backing field koleksi dan constructor privat.
- Pesan error persis (bahasa Inggris, konsisten dengan pesan yang ada).
- Apakah `CreatePremiInput` / `UpdatePremiInput` perlu `IAdminRequest` di samping validator FluentValidation yang sudah ada (urutan behavior: Authorization → Admin → Validation).

## Catatan di Luar Scope

- `GET /api/v1/users` saat ini terbuka tanpa login dan mengembalikan semua user. Tidak diubah di sini, tapi sebaiknya dijadikan admin-only di fitur berikutnya.
