# Outbox + Telegram Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menambahkan tabel Outbox, worker pengirim Telegram yang aman multi-instance, alur linking akun Telegram via bot, dan endpoint test Development.

**Architecture:** `OutboxMessage` (Domain) disimpan dalam transaksi yang sama dengan perubahan bisnis. `OutboxProcessor` (BackgroundService, Infrastructure) mengklaim batch dengan `FOR UPDATE SKIP LOCKED`, mengirim lewat `ITelegramClient` (typed HttpClient), lalu menandai sukses/gagal. Chat id Telegram didapat lewat deep link `/start <token>` yang diterima endpoint webhook; token hanya disimpan sebagai hash SHA-256.

**Tech Stack:** .NET 9, EF Core 9.0.1 + Npgsql 9.0.2, MediatR 14.1.0, FluentValidation 12.1.1, Telegram Bot API (HTTP).

**Spec:** `docs/superpowers/specs/2026-09-30-outbox-telegram-design.md`

## Global Constraints

- **Jangan menjalankan `git commit`, `dotnet build`, `dotnet run`, maupun `dotnet ef`.** User menjalankannya sendiri. Langkah verifikasi di plan ini ditujukan untuk user.
- Bergantung pada hasil plan `2026-09-30-domain-foundation.md` (base class, typed ID, interceptor timestamp). `CreatedAt`/`UpdatedAt` tidak boleh di-set manual.
- Id entity baru memakai typed ID (`XxxId.New()` dengan `Guid.CreateVersion7()`).
- Semua perintah (untuk user) dijalankan dari root repo `C:\David\3_Self_Development\chakra-be`.
- Token bot dan webhook secret tidak boleh muncul di log maupun pesan exception.
- Style kode mengikuti codebase: constructor klasik, field `_camelCase`, file-scoped namespace, `Result<T>` + `ApiResponse`/`ApiErrorResponse` di endpoint.
- Batas Telegram: teks pesan maks 4096 karakter; parameter `start` maks 64 karakter `[A-Za-z0-9_-]`.

## File Structure

| File | Aksi | Tanggung jawab |
|---|---|---|
| `Chakra.Domain/Entities/Common/StronglyTypedIds.cs` | Modify | Tambah `OutboxMessageId` |
| `Chakra.Domain/Entities/Enums/OutboxMessageType.cs` | Create | Jenis pesan outbox |
| `Chakra.Domain/Entities/OutboxMessage.cs` | Create | Entity outbox + payload + state transitions |
| `Chakra.Domain/Entities/User.cs` | Modify | Field & method Telegram, hapus `ChatId` |
| `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs` | Modify | `OutboxMessageIdConverter` |
| `Chakra.Infrastructure/ApplicationDbContext.cs` | Modify | DbSet + convention |
| `Chakra.Infrastructure/Configuration/OutboxMessageConfiguration.cs` | Create | Mapping tabel `OutboxMessages` |
| `Chakra.Infrastructure/Configuration/UserConfiguration.cs` | Modify | Kolom Telegram |
| `Chakra.Application/Common/IApplicationDbContext.cs` | Modify | `DbSet<OutboxMessage>` |
| `Chakra.Application/Features/Users/Dtos/UserDto.cs` | Modify | `TelegramChatId` |
| `Chakra.Application/Features/Users/Queries/GetUsers.cs` | Modify | Mapping `TelegramChatId` |
| `Chakra.API/Endpoints/AuthEndpoint.cs` | Modify | `TelegramChatId` |
| `Chakra.API/Middlewares/EnsureUserMiddleware.cs` | Modify | Hapus `ChatId` |
| `Chakra.Application/Services/ITelegramClient.cs` | Create | Abstraksi kirim pesan |
| `Chakra.Application/Features/Telegram/Settings/TelegramSettings.cs` | Create | Konfigurasi bot |
| `Chakra.Application/Features/Outbox/Settings/OutboxSettings.cs` | Create | Konfigurasi worker |
| `Chakra.Infrastructure/Services/Telegram/TelegramClient.cs` | Create | Implementasi HTTP |
| `Chakra.Infrastructure/BackgroundJobs/OutboxProcessor.cs` | Create | Worker |
| `Chakra.API/Configurations/NotificationSetup.cs` | Create | Registrasi settings, HttpClient, worker |
| `Chakra.API/Program.cs` | Modify | Panggil setup + map endpoint |
| `Chakra.API/appsettings.example.json` | Modify | Section `Telegram`, `Outbox` |
| `Chakra.Application/Features/Telegram/TelegramLinkToken.cs` | Create | Generate & hash token |
| `Chakra.Application/Features/Telegram/Dtos/TelegramLinkResponseDto.cs` | Create | Response link |
| `Chakra.Application/Features/Telegram/Dtos/TelegramUpdateDto.cs` | Create | Payload webhook Telegram |
| `Chakra.Application/Features/Telegram/Command/CreateTelegramLink.cs` | Create | Buat deep link |
| `Chakra.Application/Features/Telegram/Command/ProcessTelegramWebhook.cs` | Create | Proses `/start <token>` |
| `Chakra.API/Endpoints/TelegramEndpoint.cs` | Create | `/api/telegram/link`, `/api/telegram/webhook` |
| `Chakra.Application/Features/Outbox/Command/EnqueueTestOutboxMessage.cs` | Create | Command test |
| `Chakra.API/Endpoints/OutboxEndpoint.cs` | Create | `/api/outbox/test` (Development) |

---

### Task 1: Domain — OutboxMessage dan field Telegram di User

**Files:**
- Modify: `Chakra.Domain/Entities/Common/StronglyTypedIds.cs`
- Create: `Chakra.Domain/Entities/Enums/OutboxMessageType.cs`
- Create: `Chakra.Domain/Entities/OutboxMessage.cs`
- Modify: `Chakra.Domain/Entities/User.cs`

**Interfaces:**
- Consumes: `AuditableEntity<TId>`, `UserId` (foundation).
- Produces:
  - `readonly record struct OutboxMessageId(Guid Value)` dengan `static New()`
  - `enum OutboxMessageType { TelegramReminder, PaymentSuccess, TelegramLinked, Test }`
  - `OutboxMessage : AuditableEntity<OutboxMessageId>` — `Type`, `Payload`, `IsProcessed`, `RetryCount`, `LastError`, `ProcessedAt`; `const int LastErrorMaxLength = 1000`; `static OutboxMessage CreateTelegram(OutboxMessageType, UserId, string)`; `void MarkAsProcessed(DateTime)`; `void MarkAsFailed(string)`; `void MarkAsDeadLetter(string, int)`
  - `sealed record TelegramOutboxPayload(Guid UserId, string Text)`
  - `User.TelegramChatId : long?`, `User.TelegramLinkTokenHash : string?`, `User.TelegramLinkTokenExpiresAt : DateTime?`; `void SetTelegramLinkToken(string, DateTime)`; `void LinkTelegram(long)`

- [ ] **Step 1: Tambah `OutboxMessageId`**

Di akhir `Chakra.Domain/Entities/Common/StronglyTypedIds.cs` tambahkan:

```csharp

public readonly record struct OutboxMessageId(Guid Value)
{
    public static OutboxMessageId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
```

- [ ] **Step 2: Buat enum `OutboxMessageType`**

`Chakra.Domain/Entities/Enums/OutboxMessageType.cs`:

```csharp
namespace Chakra.Domain.Entities.Enums;

public enum OutboxMessageType
{
    TelegramReminder,
    PaymentSuccess,
    TelegramLinked,
    Test
}
```

- [ ] **Step 3: Buat entity `OutboxMessage`**

`Chakra.Domain/Entities/OutboxMessage.cs`:

```csharp
using System.Text.Json;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

public class OutboxMessage : AuditableEntity<OutboxMessageId>
{
    public const int LastErrorMaxLength = 1000;

    public OutboxMessageType Type { get; set; } // jenis pesan, hanya informatif (worker memperlakukan semua tipe sama)
    public required string Payload { get; set; } // JSON berbentuk TelegramOutboxPayload
    public bool IsProcessed { get; set; } // true jika sudah berhasil terkirim
    public int RetryCount { get; set; } // jumlah percobaan gagal
    public string? LastError { get; set; } // pesan error percobaan terakhir
    public DateTime? ProcessedAt { get; set; } // waktu berhasil terkirim

    public static OutboxMessage CreateTelegram(OutboxMessageType type, UserId userId, string text) => new()
    {
        Id = OutboxMessageId.New(),
        Type = type,
        Payload = JsonSerializer.Serialize(new TelegramOutboxPayload(userId.Value, text))
    };

    public void MarkAsProcessed(DateTime processedAt)
    {
        IsProcessed = true;
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        LastError = Truncate(error);
    }

    // Untuk kegagalan yang tidak akan berhasil walau dicoba ulang (payload rusak, user belum link Telegram)
    public void MarkAsDeadLetter(string error, int maxRetries)
    {
        RetryCount = maxRetries;
        LastError = Truncate(error);
    }

    private static string Truncate(string error) =>
        error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
}

public sealed record TelegramOutboxPayload(Guid UserId, string Text);
```

- [ ] **Step 4: Ubah `User`**

Ganti isi `Chakra.Domain/Entities/User.cs`:

```csharp
using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Entities;

public class User : AuditableEntity<UserId>
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? SupabaseAuthId { get; set; }
    public long? TelegramChatId { get; set; } // diisi saat user menekan Start di bot lewat deep link
    public string? TelegramLinkTokenHash { get; set; } // SHA-256 hex dari token deep link, token asli tidak disimpan
    public DateTime? TelegramLinkTokenExpiresAt { get; set; }

    public void SetTelegramLinkToken(string tokenHash, DateTime expiresAt)
    {
        TelegramLinkTokenHash = tokenHash;
        TelegramLinkTokenExpiresAt = expiresAt;
    }

    public void LinkTelegram(long chatId)
    {
        TelegramChatId = chatId;
        TelegramLinkTokenHash = null;
        TelegramLinkTokenExpiresAt = null;
    }
}
```

- [ ] **Step 5: (User) cek build**

User menjalankan: `dotnet build Chakra.Domain`
Expected: `Build succeeded`. (Solution penuh baru build setelah Task 2 karena `ChatId` masih dipakai di Application/API.)

---

### Task 2: Persistence dan penggantian `ChatId`

**Files:**
- Modify: `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs`
- Modify: `Chakra.Infrastructure/ApplicationDbContext.cs`
- Create: `Chakra.Infrastructure/Configuration/OutboxMessageConfiguration.cs`
- Modify: `Chakra.Infrastructure/Configuration/UserConfiguration.cs`
- Modify: `Chakra.Application/Common/IApplicationDbContext.cs`
- Modify: `Chakra.Application/Features/Users/Dtos/UserDto.cs`
- Modify: `Chakra.Application/Features/Users/Queries/GetUsers.cs`
- Modify: `Chakra.API/Endpoints/AuthEndpoint.cs`
- Modify: `Chakra.API/Middlewares/EnsureUserMiddleware.cs`

**Interfaces:**
- Consumes: semua tipe Task 1.
- Produces: `IApplicationDbContext.OutboxMessages : DbSet<OutboxMessage>`; `ApplicationDbContext.OutboxMessages`; tabel `OutboxMessages`; `UserResponseDto.TelegramChatId : long?`.

- [ ] **Step 1: Tambah converter**

Di akhir `Chakra.Infrastructure/Common/StronglyTypedIdConverters.cs` tambahkan:

```csharp

public sealed class OutboxMessageIdConverter : ValueConverter<OutboxMessageId, Guid>
{
    public OutboxMessageIdConverter() : base(id => id.Value, value => new OutboxMessageId(value))
    {
    }
}
```

- [ ] **Step 2: Ubah `ApplicationDbContext`**

Di `Chakra.Infrastructure/ApplicationDbContext.cs`, setelah `public DbSet<Installment> Installments {get; set;}` tambahkan:

```csharp
    public DbSet<OutboxMessage> OutboxMessages {get; set;}
```

dan di `ConfigureConventions`, setelah baris `InstallmentId`, tambahkan:

```csharp
        configurationBuilder.Properties<OutboxMessageId>().HaveConversion<OutboxMessageIdConverter>();
```

- [ ] **Step 3: Ubah `IApplicationDbContext`**

Ganti isi `Chakra.Application/Common/IApplicationDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Chakra.Domain.Entities;

namespace Chakra.Application.Common;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Premi> Premis { get; }
    DbSet<Installment> Installments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Buat `OutboxMessageConfiguration`**

`Chakra.Infrastructure/Configuration/OutboxMessageConfiguration.cs`:

```csharp
using Chakra.Domain.Entities;
using Chakra.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(EfConstants.Length.Short)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(OutboxMessage.LastErrorMaxLength);

        // Partial index untuk query antrean worker (hanya baris yang belum terkirim)
        builder.HasIndex(x => x.CreatedAt)
            .HasFilter("\"IsProcessed\" = false");

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
```

- [ ] **Step 5: Ubah `UserConfiguration`**

Di `Chakra.Infrastructure/Configuration/UserConfiguration.cs` ganti baris:

```csharp
        builder.Property(x => x.ChatId).HasMaxLength(EfConstants.Length.Normal);
```

menjadi:

```csharp
        builder.Property(x => x.TelegramLinkTokenHash).HasMaxLength(64);

        builder.HasIndex(x => x.TelegramLinkTokenHash)
            .IsUnique()
            .HasFilter("\"TelegramLinkTokenHash\" IS NOT NULL");
```

- [ ] **Step 6: Ganti `ChatId` di DTO, query, endpoint, middleware**

Di `Chakra.Application/Features/Users/Dtos/UserDto.cs` (class `UserResponseDto`) ganti:

```csharp
    public Guid ChatId { get; set; }
```

menjadi:

```csharp
    public long? TelegramChatId { get; set; }
```

Di `Chakra.Application/Features/Users/Queries/GetUsers.cs` ganti `ChatId = u.ChatId,` menjadi `TelegramChatId = u.TelegramChatId,`.

Di `Chakra.API/Endpoints/AuthEndpoint.cs` ganti `user.ChatId,` menjadi `user.TelegramChatId,`.

Di `Chakra.API/Middlewares/EnsureUserMiddleware.cs` inisialisasi user menjadi:

```csharp
                    user = new User
                    {
                        Id = UserId.New(),
                        Name = name,
                        Email = email,
                        SupabaseAuthId = supabaseAuthId
                    };
```

- [ ] **Step 7: (User) build, generate migration, cek isi**

User menjalankan:
1. `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.
2. `.\add-migration.ps1 AddOutboxAndTelegramLink`
3. Buka `Chakra.Infrastructure/Migrations/*_AddOutboxAndTelegramLink.cs`. `Up()` harus berisi **hanya**:
   - `DropColumn` `ChatId` dari `Users`
   - `AddColumn` `TelegramChatId` (`bigint`, nullable), `TelegramLinkTokenHash` (`character varying(64)`, nullable), `TelegramLinkTokenExpiresAt` (`timestamp with time zone`, nullable) di `Users`
   - `CreateTable` `OutboxMessages` (kolom `Id uuid`, `Type varchar(50)`, `Payload jsonb`, `IsProcessed boolean`, `RetryCount integer`, `LastError varchar(1000)`, `ProcessedAt`, `CreatedAt`, `UpdatedAt`)
   - `CreateIndex` `IX_Users_TelegramLinkTokenHash` (unique, filter) dan `IX_OutboxMessages_CreatedAt` (filter)

   Jika ada `AlterColumn`/`DropTable` lain pada tabel `Premis`/`Installments`, hapus migration (`dotnet ef migrations remove --project Chakra.Infrastructure --startup-project Chakra.API`) dan laporkan ke Claude.
4. `.\update-database.ps1`

---

### Task 3: Telegram client dan OutboxProcessor

**Files:**
- Create: `Chakra.Application/Services/ITelegramClient.cs`
- Create: `Chakra.Application/Features/Telegram/Settings/TelegramSettings.cs`
- Create: `Chakra.Application/Features/Outbox/Settings/OutboxSettings.cs`
- Create: `Chakra.Infrastructure/Services/Telegram/TelegramClient.cs`
- Create: `Chakra.Infrastructure/BackgroundJobs/OutboxProcessor.cs`
- Create: `Chakra.API/Configurations/NotificationSetup.cs`
- Modify: `Chakra.API/Program.cs`
- Modify: `Chakra.API/appsettings.example.json`

**Interfaces:**
- Consumes: `OutboxMessage`, `TelegramOutboxPayload`, `UserId` (Task 1); `ApplicationDbContext.OutboxMessages` (Task 2); `TimeProvider` (foundation, registered singleton).
- Produces:
  - `Chakra.Application.Services.ITelegramClient` — `Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)`
  - `Chakra.Application.Features.Telegram.Settings.TelegramSettings` — `SectionName = "Telegram"`, `BotToken`, `BotUsername`, `WebhookSecret`, `LinkTokenLifetimeMinutes`
  - `Chakra.Application.Features.Outbox.Settings.OutboxSettings` — `SectionName = "Outbox"`, `IntervalSeconds`, `BatchSize`, `MaxRetries`
  - `Chakra.API.Configurations.NotificationSetup.AddNotificationSetup(IServiceCollection, IConfiguration)`

- [ ] **Step 1: Buat abstraksi dan settings**

`Chakra.Application/Services/ITelegramClient.cs`:

```csharp
namespace Chakra.Application.Services;

public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);
}
```

`Chakra.Application/Features/Telegram/Settings/TelegramSettings.cs`:

```csharp
namespace Chakra.Application.Features.Telegram.Settings;

public class TelegramSettings
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int LinkTokenLifetimeMinutes { get; set; } = 15;
}
```

`Chakra.Application/Features/Outbox/Settings/OutboxSettings.cs`:

```csharp
namespace Chakra.Application.Features.Outbox.Settings;

public class OutboxSettings
{
    public const string SectionName = "Outbox";

    public int IntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 5;
}
```

- [ ] **Step 2: Buat `TelegramClient`**

`Chakra.Infrastructure/Services/Telegram/TelegramClient.cs`:

```csharp
using System.Net.Http.Json;
using Chakra.Application.Features.Telegram.Settings;
using Chakra.Application.Services;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.Services.Telegram;

public class TelegramClient : ITelegramClient
{
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _settings;

    public TelegramClient(HttpClient httpClient, IOptions<TelegramSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"bot{_settings.BotToken}/sendMessage",
            new { chat_id = chatId, text },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Jangan sertakan URL request karena berisi bot token
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Telegram sendMessage failed with status {(int)response.StatusCode}: {body}");
        }
    }
}
```

- [ ] **Step 3: Buat `OutboxProcessor`**

`Chakra.Infrastructure/BackgroundJobs/OutboxProcessor.cs`:

```csharp
using System.Text.Json;
using Chakra.Application.Features.Outbox.Settings;
using Chakra.Application.Services;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.BackgroundJobs;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> settings,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.IntervalSeconds));

        try
        {
            do
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Outbox batch processing failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Aplikasi berhenti
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var telegramClient = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED: baris yang sedang diproses instance lain dilewati, sehingga pesan tidak terkirim dobel
        var messages = await context.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "IsProcessed" = false AND "RetryCount" < {_settings.MaxRetries}
                ORDER BY "CreatedAt"
                LIMIT {_settings.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var payloads = messages.ToDictionary(m => m.Id, m => TryDeserialize(m.Payload));

        var userIds = payloads.Values
            .Where(p => p != null)
            .Select(p => new UserId(p!.UserId))
            .Distinct()
            .ToList();

        var chatIds = (await context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id.Value, u => u.TelegramChatId);

        foreach (var message in messages)
        {
            var payload = payloads[message.Id];
            if (payload == null)
            {
                message.MarkAsDeadLetter("Invalid payload.", _settings.MaxRetries);
                continue;
            }

            if (!chatIds.TryGetValue(payload.UserId, out var chatId) || chatId == null)
            {
                message.MarkAsDeadLetter("User not found or has not linked Telegram.", _settings.MaxRetries);
                continue;
            }

            try
            {
                await telegramClient.SendMessageAsync(chatId.Value, payload.Text, cancellationToken);
                message.MarkAsProcessed(timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkAsFailed(ex.Message);
                _logger.LogWarning(ex, "Failed to send outbox message {OutboxMessageId} (attempt {RetryCount}).",
                    message.Id, message.RetryCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TelegramOutboxPayload? TryDeserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<TelegramOutboxPayload>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Buat `NotificationSetup` dan daftarkan di `Program.cs`**

`Chakra.API/Configurations/NotificationSetup.cs`:

```csharp
using Chakra.Application.Features.Outbox.Settings;
using Chakra.Application.Features.Telegram.Settings;
using Chakra.Application.Services;
using Chakra.Infrastructure.BackgroundJobs;
using Chakra.Infrastructure.Services.Telegram;

namespace Chakra.API.Configurations;

public static class NotificationSetup
{
    public static IServiceCollection AddNotificationSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.SectionName));

        services.AddHttpClient<ITelegramClient, TelegramClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
```

Di `Chakra.API/Program.cs`, ganti rantai registrasi:

```csharp
    .AddOpenApiSetup()
    .AddAuthSetup(builder.Configuration);
```

menjadi:

```csharp
    .AddOpenApiSetup()
    .AddAuthSetup(builder.Configuration)
    .AddNotificationSetup(builder.Configuration);
```

- [ ] **Step 5: Tambah section konfigurasi contoh**

Di `Chakra.API/appsettings.example.json`, ganti blok `Midtrans` terakhir:

```json
  "Midtrans": {
    "MerchantID": "REPLACE_ME",
    "ClientKey": "REPLACE_ME",
    "ServerKey": "REPLACE_ME"
  }
}
```

menjadi:

```json
  "Midtrans": {
    "MerchantID": "REPLACE_ME",
    "ClientKey": "REPLACE_ME",
    "ServerKey": "REPLACE_ME"
  },
  "Telegram": {
    "BotToken": "REPLACE_ME",
    "BotUsername": "REPLACE_ME_bot",
    "WebhookSecret": "REPLACE_ME",
    "LinkTokenLifetimeMinutes": 15
  },
  "Outbox": {
    "IntervalSeconds": 10,
    "BatchSize": 50,
    "MaxRetries": 5
  }
}
```

(`appsettings.Development.json` di-gitignore; user mengisi nilai asli sendiri.)

- [ ] **Step 6: (User) cek build**

User menjalankan: `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.

---

### Task 4: Linking akun Telegram (link + webhook)

**Files:**
- Create: `Chakra.Application/Features/Telegram/TelegramLinkToken.cs`
- Create: `Chakra.Application/Features/Telegram/Dtos/TelegramLinkResponseDto.cs`
- Create: `Chakra.Application/Features/Telegram/Dtos/TelegramUpdateDto.cs`
- Create: `Chakra.Application/Features/Telegram/Command/CreateTelegramLink.cs`
- Create: `Chakra.Application/Features/Telegram/Command/ProcessTelegramWebhook.cs`
- Create: `Chakra.API/Endpoints/TelegramEndpoint.cs`
- Modify: `Chakra.API/Program.cs`

**Interfaces:**
- Consumes: `User.SetTelegramLinkToken`, `User.LinkTelegram`, `OutboxMessage.CreateTelegram` (Task 1); `IApplicationDbContext.OutboxMessages` (Task 2); `TelegramSettings` (Task 3); `ICurrentUserService.DatabaseUserId : UserId?`, `TimeProvider` (foundation).
- Produces:
  - `TelegramLinkToken.Generate() : string`, `TelegramLinkToken.Hash(string) : string`
  - `CreateTelegramLinkCommand : IAuthorizedRequest<Result<TelegramLinkResponseDto>>`
  - `ProcessTelegramWebhookCommand(string? SecretToken, TelegramUpdateDto Update) : IRequest<Result<string>>`
  - `TelegramEndpoint.MapTelegramEndpoints(WebApplication)`

- [ ] **Step 1: Buat helper token**

`Chakra.Application/Features/Telegram/TelegramLinkToken.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Chakra.Application.Features.Telegram;

public static class TelegramLinkToken
{
    // 24 byte → 48 karakter hex, sesuai batas parameter start Telegram (maks 64, [A-Za-z0-9_-])
    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
```

- [ ] **Step 2: Buat DTO**

`Chakra.Application/Features/Telegram/Dtos/TelegramLinkResponseDto.cs`:

```csharp
namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramLinkResponseDto
{
    public required string DeepLink { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

`Chakra.Application/Features/Telegram/Dtos/TelegramUpdateDto.cs` (hanya field yang dipakai; nama JSON mengikuti Telegram Bot API):

```csharp
using System.Text.Json.Serialization;

namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }
}

public class TelegramMessageDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto? Chat { get; set; }
}

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
```

- [ ] **Step 3: Buat command `CreateTelegramLink`**

`Chakra.Application/Features/Telegram/Command/CreateTelegramLink.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;
using Chakra.Application.Features.Telegram.Settings;

namespace Chakra.Application.Features.Telegram.Command;

public record CreateTelegramLinkCommand : IAuthorizedRequest<Result<TelegramLinkResponseDto>>;

public class CreateTelegramLinkCommandHandler : IRequestHandler<CreateTelegramLinkCommand, Result<TelegramLinkResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TelegramSettings _settings;
    private readonly TimeProvider _timeProvider;

    public CreateTelegramLinkCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IOptions<TelegramSettings> settings,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<Result<TelegramLinkResponseDto>> Handle(CreateTelegramLinkCommand request, CancellationToken cancellationToken)
    {
        // AuthorizationBehavior sudah memastikan DatabaseUserId tidak null
        var userId = _currentUser.DatabaseUserId!.Value;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return Result<TelegramLinkResponseDto>.Failure("User not found.");

        var token = TelegramLinkToken.Generate();
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_settings.LinkTokenLifetimeMinutes);

        user.SetTelegramLinkToken(TelegramLinkToken.Hash(token), expiresAt);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TelegramLinkResponseDto>.Success(new TelegramLinkResponseDto
        {
            DeepLink = $"https://t.me/{_settings.BotUsername}?start={token}",
            ExpiresAt = expiresAt
        });
    }
}
```

- [ ] **Step 4: Buat command `ProcessTelegramWebhook`**

`Chakra.Application/Features/Telegram/Command/ProcessTelegramWebhook.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;
using Chakra.Application.Features.Telegram.Settings;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Telegram.Command;

public record ProcessTelegramWebhookCommand(string? SecretToken, TelegramUpdateDto Update) : IRequest<Result<string>>;

public class ProcessTelegramWebhookCommandHandler : IRequestHandler<ProcessTelegramWebhookCommand, Result<string>>
{
    private const string StartCommand = "/start ";
    private const string LinkedMessage =
        "Akun Chakra kamu berhasil terhubung dengan Telegram. Notifikasi cicilan akan dikirim ke sini.";

    private readonly IApplicationDbContext _context;
    private readonly TelegramSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessTelegramWebhookCommandHandler> _logger;

    public ProcessTelegramWebhookCommandHandler(
        IApplicationDbContext context,
        IOptions<TelegramSettings> settings,
        TimeProvider timeProvider,
        ILogger<ProcessTelegramWebhookCommandHandler> logger)
    {
        _context = context;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ProcessTelegramWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!IsValidSecret(request.SecretToken))
            return Result<string>.Failure("Invalid secret token.");

        var text = request.Update.Message?.Text;
        var chatId = request.Update.Message?.Chat?.Id;

        if (chatId == null || text == null || !text.StartsWith(StartCommand, StringComparison.Ordinal))
            return Result<string>.Success("Ignored.");

        var token = text[StartCommand.Length..].Trim();
        if (token.Length == 0)
            return Result<string>.Success("Ignored.");

        var tokenHash = TelegramLinkToken.Hash(token);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramLinkTokenHash == tokenHash, cancellationToken);

        if (user == null || user.TelegramLinkTokenExpiresAt < now)
        {
            _logger.LogInformation("Telegram link token not found or expired (update {UpdateId}).", request.Update.UpdateId);
            return Result<string>.Success("Ignored.");
        }

        user.LinkTelegram(chatId.Value);
        _context.OutboxMessages.Add(OutboxMessage.CreateTelegram(OutboxMessageType.TelegramLinked, user.Id, LinkedMessage));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Linked.");
    }

    private bool IsValidSecret(string? secretToken)
    {
        if (string.IsNullOrEmpty(_settings.WebhookSecret) || string.IsNullOrEmpty(secretToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(secretToken),
            Encoding.UTF8.GetBytes(_settings.WebhookSecret));
    }
}
```

- [ ] **Step 5: Buat `TelegramEndpoint` dan map di `Program.cs`**

`Chakra.API/Endpoints/TelegramEndpoint.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Command;
using Chakra.Application.Features.Telegram.Dtos;

namespace Chakra.API.Endpoints;

public static class TelegramEndpoint
{
    public static void MapTelegramEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/telegram").WithTags("Telegram");

        group.MapPost("/link", [Authorize] async (IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateTelegramLinkCommand());
            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Telegram link created successfully"))
                : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
        })
        .WithName("Create Telegram Link");

        // Dipanggil oleh server Telegram. Selalu 200 untuk secret valid agar Telegram tidak mengirim ulang.
        group.MapPost("/webhook", async (
            [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
            TelegramUpdateDto update,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new ProcessTelegramWebhookCommand(secretToken, update));
            return result.IsSuccess ? Results.Ok() : Results.Unauthorized();
        })
        .WithName("Telegram Webhook");
    }
}
```

Di `Chakra.API/Program.cs`, setelah `app.MapUserEndpoints();` tambahkan:

```csharp
app.MapTelegramEndpoints();
```

- [ ] **Step 6: (User) cek build**

User menjalankan: `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.

---

### Task 5: Endpoint test outbox (Development)

**Files:**
- Create: `Chakra.Application/Features/Outbox/Command/EnqueueTestOutboxMessage.cs`
- Create: `Chakra.API/Endpoints/OutboxEndpoint.cs`
- Modify: `Chakra.API/Program.cs`

**Interfaces:**
- Consumes: `OutboxMessage.CreateTelegram`, `OutboxMessageType.Test`, `UserId` (Task 1); `IApplicationDbContext.OutboxMessages` (Task 2).
- Produces: `EnqueueTestOutboxMessageCommand(Guid UserId, string Text) : IRequest<Result<Guid>>`; `OutboxEndpoint.MapOutboxEndpoints(WebApplication)`.

- [ ] **Step 1: Buat command + validator**

`Chakra.Application/Features/Outbox/Command/EnqueueTestOutboxMessage.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;

using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Outbox.Command;

public record EnqueueTestOutboxMessageCommand(Guid UserId, string Text) : IRequest<Result<Guid>>;

public class EnqueueTestOutboxMessageValidator : AbstractValidator<EnqueueTestOutboxMessageCommand>
{
    public EnqueueTestOutboxMessageValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required")
            .MaximumLength(4096).WithMessage("Text must be at most 4096 characters");
    }
}

public class EnqueueTestOutboxMessageCommandHandler : IRequestHandler<EnqueueTestOutboxMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public EnqueueTestOutboxMessageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(EnqueueTestOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = new UserId(request.UserId);

        var userExists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
            return Result<Guid>.Failure("User not found.");

        var message = OutboxMessage.CreateTelegram(OutboxMessageType.Test, userId, request.Text);
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(message.Id.Value);
    }
}
```

- [ ] **Step 2: Buat `OutboxEndpoint` dan map hanya di Development**

`Chakra.API/Endpoints/OutboxEndpoint.cs`:

```csharp
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Outbox.Command;

namespace Chakra.API.Endpoints;

public static class OutboxEndpoint
{
    // Hanya untuk uji coba worker di Development
    public static void MapOutboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/outbox").WithTags("Outbox (Development)");

        group.MapPost("/test", async (EnqueueTestOutboxMessageCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            if (!result.IsSuccess)
            {
                return result.Errors != null
                    ? Results.BadRequest(ApiErrorResponse.Validation(result.Error!, result.Errors, 400))
                    : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
            }

            return Results.Ok(ApiResponse<object>.Success(result.Data, 200, "Outbox message enqueued"));
        })
        .WithName("Enqueue Test Outbox Message");
    }
}
```

Di `Chakra.API/Program.cs`, setelah `app.MapTelegramEndpoints();` tambahkan:

```csharp

if (app.Environment.IsDevelopment())
    app.MapOutboxEndpoints();
```

- [ ] **Step 3: (User) cek build**

User menjalankan: `dotnet build Chakra.sln` → `Build succeeded`, `0 Error(s)`.

---

### Task 6: Setup bot dan verifikasi end-to-end (dilakukan user)

**Files:** — (hanya konfigurasi lokal user)

- [ ] **Step 1: Siapkan bot**

1. Di Telegram, chat `@BotFather` → `/newbot` → catat **token** dan **username** bot.
2. Buat secret webhook acak (hanya `A-Z a-z 0-9 _ -`), mis. PowerShell:
   `-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 40 | % {[char]$_})`
3. Isi `Chakra.API/appsettings.Development.json`:

```json
  "Telegram": {
    "BotToken": "<token dari BotFather>",
    "BotUsername": "<username bot tanpa @>",
    "WebhookSecret": "<secret>",
    "LinkTokenLifetimeMinutes": 15
  },
  "Outbox": {
    "IntervalSeconds": 10,
    "BatchSize": 50,
    "MaxRetries": 5
  }
```

- [ ] **Step 2: Jalankan API dan tunnel, daftarkan webhook**

1. `dotnet run --project Chakra.API --launch-profile "Chakra.API: Development"` (listening `http://localhost:5277`). Log tidak boleh berisi error `Outbox batch processing failed`.
2. Terminal lain: `ngrok http 5277` → catat URL `https://<sub>.ngrok-free.app`.
3. Daftarkan webhook:

```bash
curl -X POST "https://api.telegram.org/bot<TOKEN>/setWebhook" \
  -d "url=https://<sub>.ngrok-free.app/api/telegram/webhook" \
  -d "secret_token=<SECRET>" \
  -d "allowed_updates=[\"message\"]"
```

Expected: `{"ok":true,"result":true,"description":"Webhook was set"}`.

- [ ] **Step 3: Uji linking**

1. Dengan Bearer token Supabase: `POST /api/telegram/link` → response berisi `deepLink` `https://t.me/<bot>?start=<48 hex>` dan `expiresAt` ±15 menit.
2. Buka `deepLink`, tekan **Start**.
3. Expected dalam ±10 detik: bot mengirim "Akun Chakra kamu berhasil terhubung...". `GET /api/auth/me` → `telegramChatId` berisi angka.
4. Tekan Start lagi dengan link yang sama → tidak ada efek (token sudah terpakai).

- [ ] **Step 4: Uji outbox**

1. `POST /api/outbox/test` body `{ "userId": "<id user yang sudah link>", "text": "Halo dari outbox" }` → 200 berisi id pesan. Pesan diterima di Telegram dalam ±10 detik. Di tabel `OutboxMessages`: `IsProcessed = true`, `ProcessedAt` terisi.
2. Ulangi untuk user yang **belum** link → baris `IsProcessed = false`, `RetryCount = 5`, `LastError = "User not found or has not linked Telegram."`.
3. `POST /api/outbox/test` dengan `text` kosong → 400 validasi.

- [ ] **Step 5: Uji keamanan webhook**

```bash
curl -i -X POST http://localhost:5277/api/telegram/webhook \
  -H "Content-Type: application/json" \
  -H "X-Telegram-Bot-Api-Secret-Token: salah" \
  -d '{"update_id":1}'
```

Expected: `HTTP/1.1 401 Unauthorized`.

- [ ] **Step 6: Laporkan hasil**

Laporkan hasil tiap langkah ke Claude, termasuk error build/runtime bila ada.
