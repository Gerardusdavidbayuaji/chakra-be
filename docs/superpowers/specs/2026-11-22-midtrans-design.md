# Midtrans Design

**Tanggal:** 2026-11-22
**Status:** Draft, menunggu persetujuan
**Scope:** Notifikasi pembayaran sukses (Tahap 0), pembayaran cicilan lewat Midtrans Snap, webhook notifikasi, pencegahan pembayaran ganda, dan penampungan dana yang tidak bisa diterapkan (alokasi / refund manual oleh admin).
**Bergantung pada:** `2026-11-21-installments-design.md` (`Premi.MarkInstallmentPaid`, role admin, `ErrorType`), `2026-09-30-outbox-telegram-design.md` (outbox).

## Latar Belakang

Pembayaran manual oleh admin sudah ada lewat `Premi.MarkInstallmentPaid(...)`, yang sejak awal disiapkan agar webhook Midtrans memakai aturan yang sama (idempoten per `midtransOrderId`). `InstallmentPaidEvent` sudah di-raise tapi belum ada yang menangkap.

Fitur ini menambahkan jalur bayar mandiri oleh user lewat Snap. Ada kasus uang masuk tapi tidak boleh diterapkan ke cicilan yang dituju: premi sudah di-cancel, cicilan sudah dibayar manual oleh admin, atau nominal tidak cocok. Mengikuti praktik umum bank/asuransi, dana seperti ini **tidak di-refund otomatis**. Dana dicatat sebagai *unapplied* (penampungan), lalu admin memutuskan: dialokasikan ke cicilan berikutnya atau di-refund manual.

## Tujuan

1. User (pemilik premi) bisa mendapatkan link pembayaran Snap untuk cicilan yang boleh dibayar.
2. Webhook Midtrans menandai cicilan lunas lewat `Premi.MarkInstallmentPaid`, tanpa menduplikasi aturan.
3. Webhook aman: signature diverifikasi, status dikonfirmasi ulang ke Midtrans, dan notifikasi ganda tidak berefek.
4. User menerima notifikasi Telegram setiap kali cicilan lunas, baik dibayar manual maupun lewat Midtrans (Tahap 0).
5. Transaksi Snap yang masih pending di-expire saat premi di-cancel atau cicilan dibayar manual (lapis 1).
6. Dana yang tidak bisa diterapkan dicatat sebagai *unapplied*, admin diberi tahu, lalu admin bisa mengalokasikan (premi masih aktif) atau mencatat refund manual.
7. Pembuatan link bayar bisa dipakai ulang oleh Scheduler (reminder berisi `redirect_url`).

## Non-goals

- Scheduler / reminder (spec terpisah, memakai `IPaymentLinkService` dari spec ini).
- Refund otomatis lewat Refund API Midtrans.
- Pembayaran sebagian (partial) atau satu pembayaran untuk beberapa cicilan.
- Core API (charge langsung tanpa halaman Snap), tokenisasi kartu, recurring.
- Halaman frontend `finish`/`unfinish`/`error` Snap.
- Rekonsiliasi harian otomatis dengan laporan settlement Midtrans.

## Anti-goals

- Uang masuk tapi tidak tercatat di mana pun (transaksi Midtrans tanpa baris di database).
- Webhook menulis ulang aturan bayar (berurutan, premi aktif, lunas otomatis).
- Uang keluar (refund) tanpa keputusan admin.
- Midtrans berhenti mengirim ulang notifikasi padahal datanya gagal disimpan.

## Keputusan

| Topik | Keputusan |
|---|---|
| Produk Midtrans | **Snap** (halaman bayar dari Midtrans, semua metode bayar) |
| Satu cicilan, banyak percobaan | Entity baru `PaymentTransaction`, satu baris per `order_id` |
| Transaksi pending per cicilan | Maksimal satu (`unique` partial index); link yang masih berlaku dipakai ulang |
| Masa berlaku link | `Midtrans:PaymentExpiryHours`, default 24 jam |
| Sumber kebenaran status | Notifikasi diverifikasi signature-nya, lalu status diambil ulang via `GET /v2/{order_id}/status` |
| Lapis 1 (cegah) | Premi di-cancel / cicilan dibayar manual → transaksi pending ditandai `ExpireRequested`, worker memanggil `POST /v2/{order_id}/expire` |
| Lapis 2 (tampung) | Settlement yang ditolak domain atau nominalnya beda → `PaymentTransaction.Status = Unapplied` + notifikasi admin |
| Penyelesaian unapplied | Admin: **alokasi** ke cicilan belum lunas berikutnya (hanya jika premi `Active` dan nominal sama), atau **refund manual** (wajib untuk premi yang tidak aktif) |
| Refund via dashboard Midtrans | Notifikasi `refund` dari Midtrans otomatis menandai transaksi `Unapplied` jadi `Refunded` |
| Response webhook | 200 untuk semua hasil bisnis (termasuk unapplied dan order tidak dikenal); 5xx untuk error sementara agar Midtrans mengirim ulang |

## 1. Tahap 0: Notifikasi Pembayaran Sukses

`InstallmentPaidEvent` diperluas agar handler tidak perlu query tambahan:

```csharp
public sealed record InstallmentPaidEvent(
    InstallmentId InstallmentId, PremiId PremiId, UserId UserId,
    int InstallmentNumber, decimal Amount, bool PremiCompleted,
    string? MidtransOrderId) : IDomainEvent;
```

`Premi.MarkInstallmentPaid` me-raise event **setelah** status premi dihitung, agar `PremiCompleted` benar.

`InstallmentPaidEventHandler` (Application, `Features/Installments/EventHandlers/`) menambahkan `OutboxMessage.CreateTelegram(PaymentSuccess, userId, text)`. Contoh teks:

- `Pembayaran cicilan ke-3 sebesar Rp1.714.285 berhasil.`
- Jika `PremiCompleted`: tambah `Seluruh cicilan premi Anda sudah lunas.`

Karena `DispatchDomainEventsInterceptor` berjalan sebelum commit, pesan outbox ikut tersimpan dalam transaksi pembayaran. Berlaku untuk pembayaran manual dan Midtrans sekaligus.

## 2. Domain: `Chakra.Domain`

### Typed ID dan enum

- `PaymentTransactionId` di `StronglyTypedIds.cs`.
- `PaymentTransactionStatus`: `Pending`, `Paid`, `Failed`, `Expired`, `Unapplied`, `Allocated`, `Refunded`.
- `PaymentRejectionReason`: `PremiNotActive`, `InstallmentNotFound`, `InstallmentAlreadyPaid`, `PreviousInstallmentUnpaid`, `AmountMismatch`.
- `OutboxMessageType`: tambah `AdminAlert`.

### `Premi`

```csharp
// Cek tanpa mengubah apa pun. null = boleh dibayar.
public PaymentRejectionReason? CheckCanPay(InstallmentId installmentId);

// Cicilan belum lunas dengan InstallmentNumber terkecil (target alokasi)
public Installment? NextUnpaidInstallment();
```

`MarkInstallmentPaid` tetap seperti sekarang (melempar `DomainException`), tapi urutan cek 1–4 di dalamnya memakai `CheckCanPay` agar aturannya tetap satu tempat. Idempotensi (cek 0) tidak berubah.

`Cancel()` me-raise `PremiCancelledEvent(PremiId)` (baru).

### `PaymentTransaction : Aggregate<PaymentTransactionId>`

| Field | Tipe | Keterangan |
|---|---|---|
| `OrderId` | `string` | `CHK-` + `Guid.CreateVersion7():N` (36 karakter, batas Midtrans 50), unik |
| `InstallmentId` | `InstallmentId` | cicilan yang dituju saat link dibuat |
| `PremiId` | `PremiId` | denormalisasi untuk query |
| `Amount` | `decimal` | = `Installment.Amount` saat dibuat (`gross_amount`) |
| `Status` | `PaymentTransactionStatus` | string |
| `SnapToken`, `RedirectUrl` | `string?` | diisi setelah Snap merespons |
| `ExpiresAt` | `DateTime` | UTC |
| `ExpireRequested` | `bool` | lapis 1 |
| `PaymentType` | `string?` | dari Midtrans (`bank_transfer`, `gopay`, `qris`, ...) |
| `MidtransTransactionId` | `string?` | |
| `SettledAt` | `DateTime?` | |
| `RejectionReason` | `PaymentRejectionReason?` | diisi saat `Unapplied` |
| `AllocatedInstallmentId` | `InstallmentId?` | diisi saat `Allocated` |
| `RefundReference`, `ResolutionNote` | `string?` | dari admin, atau `"midtrans"` jika refund lewat dashboard |
| `ResolvedByUserId`, `ResolvedAt` | `UserId?`, `DateTime?` | |
| `RowVersion` | `uint` | `xmin` |

Method (transisi status di satu tempat; transisi yang tidak valid → `DomainException`):

| Method | Dari | Ke |
|---|---|---|
| `static Create(installment, now, expiryHours)` | — | `Pending` |
| `AttachSnap(token, redirectUrl)` | `Pending` | `Pending` |
| `RequestExpire()` | `Pending` | `Pending` + `ExpireRequested` |
| `MarkPaid(settledAt, paymentType, midtransTxId)` | `Pending`, `Expired`, `Failed` | `Paid` |
| `MarkUnapplied(reason, settledAt, paymentType, midtransTxId)` | `Pending`, `Expired`, `Failed` | `Unapplied` (raise `PaymentUnappliedEvent`) |
| `MarkFailed()` / `MarkExpired()` | `Pending` | `Failed` / `Expired` |
| `MarkAllocated(installmentId, adminId, now, note)` | `Unapplied` | `Allocated` |
| `MarkRefunded(reference, adminId?, now, note)` | `Unapplied` | `Refunded` |

`MarkPaid` / `MarkUnapplied` juga menerima `Expired` / `Failed` karena uang bisa saja masuk walau kita sudah menganggapnya kedaluwarsa (misalnya expire ke Midtrans gagal, atau urutan notifikasi terbalik). Uang yang sudah masuk selalu dicatat.

## 3. Application: `Chakra.Application`

### Kontrak eksternal

`IMidtransClient` (di `Common/`):

```csharp
Task<SnapResult> CreateSnapTransactionAsync(SnapRequest request, CancellationToken ct);
Task<MidtransStatus?> GetStatusAsync(string orderId, CancellationToken ct);   // null jika 404
Task ExpireAsync(string orderId, CancellationToken ct);                       // 404 dianggap sukses
```

`MidtransSettings` (section `Midtrans`, di `Features/Midtrans/Settings/`): `MerchantId`, `ClientKey`, `ServerKey`, `IsProduction` (default `false`), `PaymentExpiryHours = 24`, `ExpiryProcessorIntervalSeconds = 60`.

`IApplicationDbContext`: tambah `DbSet<PaymentTransaction> PaymentTransactions`.

### `IPaymentLinkService` (di `Services/`, dipakai endpoint dan nanti Scheduler)

`Task<Result<PaymentLinkDto>> GetOrCreateAsync(InstallmentId id, CancellationToken ct)`

1. Load premi + cicilan + user (email untuk `customer_details`).
2. `premi.CheckCanPay(id)` harus `null`; jika tidak → `Failure` dengan pesan sesuai alasan.
3. Ada transaksi `Pending`, tidak `ExpireRequested`, punya `RedirectUrl`, dan `ExpiresAt > now + 5 menit` → kembalikan itu.
4. Ada transaksi `Pending` lain untuk cicilan ini (hampir expire / tanpa token) → `MarkExpired()`.
5. `PaymentTransaction.Create(...)` lalu **simpan dulu** (agar setiap order yang dikirim ke Midtrans pasti punya baris di database).
6. Panggil Snap: `transaction_details { order_id, gross_amount }` (integer), `customer_details { email, first_name }`, `item_details` satu item `"Cicilan ke-N"`, `expiry { unit: "hour", duration: PaymentExpiryHours }`.
7. Sukses → `AttachSnap(...)`, simpan. Gagal → `MarkFailed()`, simpan, kembalikan `Failure("Payment gateway is unavailable, please try again.")`.

Pelanggaran `unique` partial index (dua request bersamaan) → `Failure("Payment link is being created, please retry.")`.

`PaymentLinkDto`: `OrderId`, `RedirectUrl`, `SnapToken`, `Amount`, `ExpiresAt`.

### Command dan query

| Request | Interface | Isi |
|---|---|---|
| `CreatePaymentLinkCommand(Guid InstallmentId)` | `IAuthorizedRequest` | Pemilik atau admin; bukan pemilik → `NotFound`. Memanggil `IPaymentLinkService` |
| `ProcessMidtransNotificationCommand(MidtransNotificationDto)` | — (anonymous) | Lihat bagian Webhook |
| `GetUnappliedPaymentsQuery` (paginated) | `IAdminRequest` | Transaksi `Unapplied`, terbaru dulu, berisi alasan, premi, user, cicilan tujuan, dan `CanAllocate` |
| `AllocatePaymentCommand(Guid PaymentId, string? Note)` | `IAdminRequest` | Lihat Alokasi |
| `RefundPaymentCommand(Guid PaymentId, string Reference, string? Note)` | `IAdminRequest` | `MarkRefunded(...)`; `Reference` wajib (nomor transfer / bukti) |

### Webhook: `ProcessMidtransNotificationCommand`

1. **Signature:** `SHA512(order_id + status_code + gross_amount + ServerKey)` hex lowercase, dibandingkan constant-time dengan `signature_key`. Tidak cocok → `Unauthorized` (401).
2. **Konfirmasi status:** `GetStatusAsync(order_id)`. Gagal (timeout / 5xx) → error sementara (5xx). Data selanjutnya memakai hasil ini, bukan body notifikasi.
3. Cari `PaymentTransaction` dengan `OrderId`. Tidak ada → log warning, 200 (termasuk tombol "test notification" di dashboard).
4. Berdasarkan `transaction_status`:

| Status Midtrans | Aksi |
|---|---|
| `settlement`, atau `capture` + `fraud_status = accept` | Lihat Penerapan Pembayaran |
| `pending`, `authorize`, `capture` + `challenge` | Tidak ada perubahan |
| `deny`, `cancel`, `failure` | `MarkFailed()` jika masih `Pending` |
| `expire` | `MarkExpired()` jika masih `Pending` |
| `refund` (penuh) | Jika `Unapplied` → `MarkRefunded("midtrans", null, now, null)`; status lain → log warning |
| `partial_refund` | Log warning saja (tidak didukung) |

5. `SaveChanges`. `DbUpdateConcurrencyException` → error sementara (5xx).

**Penerapan Pembayaran** (settlement):

- Transaksi sudah `Paid` / `Unapplied` / `Allocated` / `Refunded` → 200 tanpa perubahan (notifikasi ulang).
- Load premi + semua cicilan.
- `gross_amount != Amount` transaksi → `MarkUnapplied(AmountMismatch)`.
- `premi.CheckCanPay(installmentId)` tidak null → `MarkUnapplied(alasan)`.
- Lolos → `premi.MarkInstallmentPaid(installmentId, settledAt, orderId)` lalu `transaction.MarkPaid(...)`.

Semua berada dalam satu `SaveChanges`, sehingga cicilan lunas, premi `Completed`, outbox `PaymentSuccess`, dan status transaksi tersimpan atau gagal bersama.

### Alokasi: `AllocatePaymentCommand`

1. Transaksi harus `Unapplied`.
2. Load premi + cicilan. Premi harus `Active`, jika tidak → `Failure("Premi is not active, this payment can only be refunded.")`.
3. Target = `premi.NextUnpaidInstallment()`. Tidak ada → `Failure` (hanya bisa refund).
4. `transaction.Amount` harus sama dengan `target.Amount`, jika tidak → `Failure("Payment amount does not match the next installment, this payment can only be refunded.")`.
5. `premi.MarkInstallmentPaid(target.Id, transaction.SettledAt, transaction.OrderId)` lalu `transaction.MarkAllocated(target.Id, adminId, now, note)`. Satu `SaveChanges`.

`PaidAt` cicilan = waktu uang diterima (`SettledAt`), bukan waktu admin mengalokasikan. Cicilan mencatat `MidtransOrderId` milik transaksi tersebut.

### Event handler

| Event | Handler |
|---|---|
| `InstallmentPaidEvent` | Tahap 0 (outbox ke user), **dan** lapis 1: transaksi `Pending` lain untuk cicilan itu (selain `MidtransOrderId` event) → `RequestExpire()` |
| `PremiCancelledEvent` | Lapis 1: semua transaksi `Pending` premi itu → `RequestExpire()` |
| `PaymentUnappliedEvent(PaymentTransactionId, PremiId, OrderId, Amount, Reason)` | Outbox `AdminAlert` ke setiap admin yang sudah link Telegram |

Handler lapis 1 memeriksa ulang status di memori, karena entity yang sedang dilacak bisa sudah berubah dalam request yang sama.

Batasan yang diterima: event yang di-raise **oleh** event handler tidak di-dispatch (interceptor hanya mengumpulkan event sekali). Handler di atas tidak me-raise event baru.

## 4. Infrastructure: `Chakra.Infrastructure`

### `MidtransClient`

Typed `HttpClient`, timeout 15 detik, header `Authorization: Basic base64(ServerKey + ":")`, `Accept: application/json`.

| Kebutuhan | Sandbox | Production |
|---|---|---|
| Snap | `https://app.sandbox.midtrans.com/snap/v1/transactions` | `https://app.midtrans.com/snap/v1/transactions` |
| Status / expire | `https://api.sandbox.midtrans.com/v2/{order_id}/status` / `.../expire` | `https://api.midtrans.com/v2/...` |

Non-2xx (kecuali 404 untuk status dan expire) → `InvalidOperationException` berisi status + body, tanpa server key.

### `PaymentExpiryProcessor` (BackgroundService)

Pola sama dengan `OutboxProcessor`. Tiap `ExpiryProcessorIntervalSeconds`:

1. Klaim batch transaksi `Pending` dengan `ExpireRequested = true` (`FOR UPDATE SKIP LOCKED`), panggil `ExpireAsync`, lalu `MarkExpired()`. Gagal → log, dicoba lagi siklus berikutnya.
2. Transaksi `Pending` dengan `ExpiresAt < now - 1 jam` → `MarkExpired()` tanpa memanggil Midtrans (jaring pengaman jika notifikasi `expire` tidak sampai).

Catatan: jika user belum memilih metode bayar di halaman Snap, Midtrans belum punya transaksi dan `expire` membalas 404, sehingga link Snap masih bisa dipakai sampai kedaluwarsa. Pembayaran yang lolos dari celah ini ditangkap lapis 2.

### Persistence

- `PaymentTransactionConfiguration`: `OrderId` varchar(50) unique; enum disimpan string; `Amount` `decimal(18,2)`; `xmin` sebagai concurrency token; FK ke `Installments` dan `Premis` (restrict); index `(PremiId, Status)`; **unique partial index** `InstallmentId WHERE "Status" = 'Pending'`; partial index `ExpiresAt WHERE "Status" = 'Pending'`.
- Converter untuk `PaymentTransactionId`.
- Migration baru `AddPaymentTransactions`.

## 5. API: `Chakra.API`

| Endpoint | Akses | Hasil |
|---|---|---|
| `POST /api/installments/{id}/payment-link` | login (pemilik / admin) | 200 `PaymentLinkDto` |
| `POST /api/midtrans/notification` | anonymous | 200 / 401 / 500 |
| `GET /api/payments/unapplied` | admin | 200 daftar |
| `POST /api/payments/{id}/allocate` body `{ note? }` | admin | 200 transaksi + cicilan |
| `POST /api/payments/{id}/refund` body `{ reference, note? }` | admin | 200 transaksi |

Webhook mengembalikan 500 hanya untuk error sementara (konfirmasi status gagal, konflik konkurensi, database). Exception tak terduga juga menjadi 500, sehingga Midtrans mengirim ulang.

`MidtransSetup` (baru, di `Configurations/`) mendaftarkan settings, `IMidtransClient`, `IPaymentLinkService`, dan `PaymentExpiryProcessor`.

`appsettings.example.json`: section `Midtrans` diganti menjadi `MerchantId`, `ClientKey`, `ServerKey`, `IsProduction`, `PaymentExpiryHours`, `ExpiryProcessorIntervalSeconds`.

## 6. Verifikasi

Tanpa project test. User menjalankan build dan migration, lalu memakai sandbox Midtrans dan tunnel (ngrok / cloudflared) sebagai Notification URL di dashboard sandbox:

1. Link bayar cicilan ke-1 → bayar dengan simulator VA sandbox → cicilan `Paid`, transaksi `Paid`, pesan Telegram "berhasil" diterima.
2. Minta link dua kali → `OrderId` yang sama dikembalikan.
3. Minta link cicilan ke-3 saat ke-2 belum lunas → 400.
4. Kirim ulang notifikasi yang sama dari dashboard → tidak ada perubahan, tidak ada pesan Telegram kedua.
5. Notifikasi dengan signature salah → 401.
6. Buat link, admin bayar manual cicilan itu → transaksi `ExpireRequested` lalu `Expired`.
7. Buat link, pilih VA (jangan bayar), cancel premi, lalu bayar VA lewat simulator → jika lolos, transaksi `Unapplied` (`PremiNotActive`), admin menerima Telegram; allocate → 400; refund dengan reference → `Refunded`.
8. Kasus `InstallmentAlreadyPaid` pada premi aktif → allocate → cicilan berikutnya `Paid` dengan `PaidAt = SettledAt`.

## Diserahkan ke Builder

- Bentuk persis DTO request/response Midtrans dan nama propertinya (snake_case via `JsonPropertyName`).
- Teks pesan Telegram user dan admin.
- Cara mendeteksi pelanggaran unique index (`PostgresException.SqlState == "23505"`).
- Apakah `GetInstallmentById` ikut menampilkan link bayar yang masih aktif.

## Catatan di Luar Scope

- Scheduler akan memanggil `IPaymentLinkService.GetOrCreateAsync` untuk setiap reminder (H-3, H-1, lalu tiap 3 hari setelah overdue). Karena link dipakai ulang selama masih berlaku, reminder tidak membuat order baru setiap kali.
- Refund otomatis lewat API bisa ditambahkan nanti di atas status `Unapplied` tanpa mengubah alur webhook.
