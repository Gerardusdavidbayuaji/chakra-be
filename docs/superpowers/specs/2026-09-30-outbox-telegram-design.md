# Outbox + Telegram Design

**Tanggal:** 2026-09-30
**Status:** Disetujui
**Scope:** Tabel Outbox, worker pengirim (multi-instance safe), Telegram client, alur linking akun Telegram via bot, dan endpoint test Development.
**Bergantung pada:** `2026-09-30-domain-foundation-design.md` (base class, typed ID, interceptor).

## Latar Belakang

README menetapkan Outbox Pattern: notifikasi Telegram tidak dikirim di dalam transaksi bisnis, melainkan disimpan ke `OutboxMessages` lalu dikirim terpisah oleh worker dengan retry maksimal 5x. Fitur penghasil pesan (Scheduler reminder, Midtrans webhook) dibuat belakangan; fitur ini menyiapkan infrastrukturnya.

Masalah yang ikut diselesaikan: `User.ChatId` saat ini `Guid` acak (`Guid.NewGuid()`), sehingga tidak bisa dipakai mengirim pesan Telegram. Chat id Telegram adalah angka (`long`) yang hanya bisa didapat ketika user berinteraksi dengan bot.

## Keputusan

| Topik | Keputusan |
|---|---|
| Scope | Outbox + worker + Telegram sungguhan + linking bot. AuditLog **tidak** termasuk |
| Multi-instance | Klaim batch dengan `FOR UPDATE SKIP LOCKED` |
| Chat id | `User.TelegramChatId : long?`, diisi lewat deep link bot `/start <token>` |
| Payload | Generik `{ UserId, Text }`, teks disusun oleh pembuat pesan |
| Uji coba | `POST /api/outbox/test`, hanya di environment Development |
| Library Telegram | Tidak ada, `HttpClient` biasa |

## 1. Domain

### `OutboxMessage : AuditableEntity<OutboxMessageId>`

| Field | Tipe | Keterangan |
|---|---|---|
| `Id` | `OutboxMessageId` | typed ID baru di `StronglyTypedIds.cs` |
| `Type` | `OutboxMessageType` | enum, disimpan sebagai string |
| `Payload` | `string` | JSON (`jsonb`), bentuk `TelegramOutboxPayload` |
| `IsProcessed` | `bool` | |
| `RetryCount` | `int` | |
| `LastError` | `string?` | maks 1000 karakter (dipotong) |
| `ProcessedAt` | `DateTime?` | |
| `CreatedAt` / `UpdatedAt` | `DateTime` | dari base, diisi interceptor; `UpdatedAt` = percobaan terakhir |

- `OutboxMessageType`: `TelegramReminder`, `PaymentSuccess`, `TelegramLinked`, `Test`. Type hanya informatif; worker memperlakukan semua tipe sama.
- `TelegramOutboxPayload(Guid UserId, string Text)` sebagai `record`.
- Factory: `OutboxMessage.CreateTelegram(OutboxMessageType type, UserId userId, string text)`.
- Method: `MarkAsProcessed(DateTime)`, `MarkAsFailed(string error)` (retry++), `MarkAsDeadLetter(string error, int maxRetries)` (langsung habis retry).

### Perubahan `User`

- Hapus `ChatId (Guid)`.
- Tambah `TelegramChatId : long?`, `TelegramLinkTokenHash : string?` (SHA-256 hex, 64 karakter), `TelegramLinkTokenExpiresAt : DateTime?`.
- Method: `SetTelegramLinkToken(string tokenHash, DateTime expiresAt)`, `LinkTelegram(long chatId)` (set chat id dan hapus token).

## 2. Worker `OutboxProcessor` (BackgroundService, Infrastructure)

- `PeriodicTimer` tiap `Outbox:IntervalSeconds` (default 10). Siklus pertama langsung jalan saat startup.
- Tiap siklus: scope baru, lalu satu transaksi:
  1. Klaim batch:
     ```sql
     SELECT * FROM "OutboxMessages"
     WHERE "IsProcessed" = false AND "RetryCount" < @maxRetries
     ORDER BY "CreatedAt"
     LIMIT @batchSize
     FOR UPDATE SKIP LOCKED
     ```
  2. Ambil `TelegramChatId` semua user di payload dalam satu query.
  3. Per pesan:
     - payload tidak valid, atau user tidak ada / belum link Telegram: `MarkAsDeadLetter`
     - kirim sukses: `MarkAsProcessed`
     - kirim gagal: `MarkAsFailed` + log warning
  4. `SaveChanges` lalu commit.
- Pesan dengan `RetryCount >= MaxRetries` tidak diklaim lagi (dead letter, tetap di tabel untuk inspeksi).
- Exception di level siklus di-log dan tidak menghentikan worker.
- Trade-off yang diterima: lock baris dipegang selama pengiriman HTTP (maks `BatchSize` pesan, timeout HTTP 10 detik per pesan).

`OutboxSettings` (section `Outbox`): `IntervalSeconds = 10`, `BatchSize = 50`, `MaxRetries = 5`.

## 3. Telegram Client

- `ITelegramClient.SendMessageAsync(long chatId, string text, CancellationToken)` di `Chakra.Application/Services`.
- Implementasi `TelegramClient` (typed `HttpClient`, base `https://api.telegram.org/`, timeout 10 detik) memanggil `POST bot{token}/sendMessage`. Status non-2xx: lempar `InvalidOperationException` berisi status + body (tanpa token).
- `TelegramSettings` (section `Telegram`): `BotToken`, `BotUsername`, `WebhookSecret`, `LinkTokenLifetimeMinutes = 15`.

## 4. Linking Akun Telegram

### `POST /api/telegram/link` (butuh login)

1. Generate token: 24 byte acak (`RandomNumberGenerator`), hex lowercase (48 karakter, memenuhi batas parameter `start` Telegram: maks 64, `[A-Za-z0-9_-]`).
2. Simpan **hanya hash** SHA-256 (hex) dan waktu kedaluwarsa (`now + LinkTokenLifetimeMinutes`) di user. Token baru menimpa token lama.
3. Response: `{ deepLink: "https://t.me/<BotUsername>?start=<token>", expiresAt }`.

### `POST /api/telegram/webhook` (anonymous, dipanggil Telegram)

1. Validasi header `X-Telegram-Bot-Api-Secret-Token` terhadap `WebhookSecret` secara constant-time (`CryptographicOperations.FixedTimeEquals`). Secret kosong di konfigurasi berarti semua request ditolak. Tidak valid: **401**.
2. Update yang bukan pesan teks `/start <token>` diabaikan (**200**).
3. Cari user berdasarkan hash token. Tidak ketemu atau kedaluwarsa: abaikan (**200**, log information).
4. Valid: `user.LinkTelegram(chatId)` + insert `OutboxMessage` tipe `TelegramLinked` ("akun berhasil terhubung") dalam satu `SaveChanges`. **200**.

Idempotent: update ganda dari Telegram tidak berefek karena token sudah dihapus pada pemrosesan pertama. Endpoint selalu 200 untuk request ber-secret valid agar Telegram tidak retry.

Pendaftaran webhook dilakukan manual (`setWebhook` dengan `secret_token`). Untuk lokal butuh tunnel publik (ngrok / cloudflared).

## 5. Endpoint Test (Development)

`POST /api/outbox/test` body `{ userId, text }`. Hanya di-map jika `app.Environment.IsDevelopment()`. Validasi: `userId` wajib, `text` wajib dan maks 4096 karakter (batas Telegram). User harus ada. Insert pesan tipe `Test`, response berisi id pesan.

## 6. Dampak Lain

- **Kontrak API berubah (disengaja):** `chatId` pada `GET /api/v1/users` dan `GET /api/auth/me` diganti `telegramChatId` (`number | null`).
- `EnsureUserMiddleware` tidak lagi mengisi `ChatId`.
- `IApplicationDbContext` menambah `DbSet<OutboxMessage> OutboxMessages`.
- **Migration** (di-generate dan dijalankan user): drop `Users.ChatId`; tambah 3 kolom Telegram di `Users` + unique index filtered pada `TelegramLinkTokenHash`; create table `OutboxMessages` + partial index `CreatedAt WHERE "IsProcessed" = false`.
- `appsettings.example.json` menambah section `Telegram` dan `Outbox`.

## 7. Verifikasi

Tanpa project test. User menjalankan build, generate/apply migration, lalu:
1. `POST /api/telegram/link` menghasilkan deep link; membukanya di Telegram lalu menekan Start menghasilkan `telegramChatId` terisi dan pesan "terhubung" diterima.
2. `POST /api/outbox/test` membuat pesan diterima di Telegram dalam ±10 detik; baris outbox `IsProcessed = true`.
3. Test untuk user yang belum link: baris outbox `RetryCount = 5`, `LastError` berisi alasan.
4. Webhook dengan secret salah: 401.

## Di Luar Scope

AuditLog; balasan untuk token tidak valid; unlink akun; backoff eksponensial; pembersihan pesan lama; penghasil pesan dari Scheduler dan Midtrans.
