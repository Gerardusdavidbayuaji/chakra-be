# Folder Structure Setup - Clean Architecture

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the Clean Architecture folder structure for chakra-be, mirroring `chakra-app/Backend` with renamed projects (`Chakra.*` instead of `ChakraApp.*`).

**Architecture:** 4-layer Clean Architecture — API (entry point), Application (business logic/CQRS), Domain (entities/core), Infrastructure (data access/external services). Each layer is a separate .NET project with proper references.

**Tech Stack:** .NET 9.0, C# projects (.csproj), solution file (.slnx)

## Global Constraints

- .NET 9.0 (net9.0)
- Project naming: `Chakra.API`, `Chakra.Application`, `Chakra.Domain`, `Chakra.Infrastructure`
- Root namespace follows project name
- Working directory: `C:\David\3_Self_Development\chakra-be`
- Existing `chakra/` subfolder will be removed and replaced with the new structure at root level

---

## Target Structure (mirrors chakra-app/Backend)

```
chakra-be/
├── Chakra.API/
│   ├── Configurations/
│   ├── Endpoints/
│   ├── Middlewares/
│   ├── Properties/
│   ├── Chakra.API.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── appsettings.UAT.json
├── Chakra.Application/
│   ├── Common/
│   │   └── Behaviors/
│   ├── Features/
│   │   ├── Installments/
│   │   │   ├── Dtos/
│   │   │   └── Queries/
│   │   ├── Midtrans/
│   │   │   ├── Command/
│   │   │   ├── Dtos/
│   │   │   └── Settings/
│   │   ├── Premi/
│   │   │   ├── Command/
│   │   │   ├── Dtos/
│   │   │   └── Queries/
│   │   ├── Scheduler/
│   │   │   └── Settings/
│   │   └── Users/
│   │       ├── Commands/
│   │       ├── Dtos/
│   │       └── Queries/
│   ├── Mappers/
│   ├── Services/
│   └── Chakra.Application.csproj
├── Chakra.Domain/
│   ├── Entities/
│   │   ├── Common/
│   │   └── Enums/
│   ├── Events/
│   └── Chakra.Domain.csproj
├── Chakra.Infrastructure/
│   ├── Common/
│   ├── Configuration/
│   ├── Interceptors/
│   ├── Migrations/          (empty - will be generated fresh)
│   ├── Services/
│   └── Chakra.Infrastructure.csproj
├── Chakra.slnx
├── add-migration.ps1
├── update-database.ps1
├── .gitignore
└── README.md
```

---

### Task 1: Clean up existing structure and create root files

**Files:**
- Remove: `chakra/` (existing subfolder with old single-project structure)
- Create: `Chakra.slnx`
- Create: `.gitignore`
- Create: `add-migration.ps1`
- Create: `update-database.ps1`

- [ ] **Step 1: Remove existing `chakra/` subfolder**

```bash
rm -rf chakra/
```

- [ ] **Step 2: Create .gitignore for .NET**

```bash
dotnet new gitignore
```

- [ ] **Step 3: Create the 4 projects using dotnet CLI**

```bash
dotnet new webapi -n Chakra.API --no-openapi -f net9.0
dotnet new classlib -n Chakra.Application -f net9.0
dotnet new classlib -n Chakra.Domain -f net9.0
dotnet new classlib -n Chakra.Infrastructure -f net9.0
```

- [ ] **Step 4: Create solution and add projects**

```bash
dotnet new slnx -n Chakra
dotnet sln Chakra.slnx add Chakra.API/Chakra.API.csproj
dotnet sln Chakra.slnx add Chakra.Application/Chakra.Application.csproj
dotnet sln Chakra.slnx add Chakra.Domain/Chakra.Domain.csproj
dotnet sln Chakra.slnx add Chakra.Infrastructure/Chakra.Infrastructure.csproj
```

- [ ] **Step 5: Add project references (Clean Architecture dependency flow)**

```bash
# API depends on Application and Infrastructure
dotnet add Chakra.API/Chakra.API.csproj reference Chakra.Application/Chakra.Application.csproj
dotnet add Chakra.API/Chakra.API.csproj reference Chakra.Infrastructure/Chakra.Infrastructure.csproj

# Application depends on Domain
dotnet add Chakra.Application/Chakra.Application.csproj reference Chakra.Domain/Chakra.Domain.csproj

# Infrastructure depends on Application (and transitively Domain)
dotnet add Chakra.Infrastructure/Chakra.Infrastructure.csproj reference Chakra.Application/Chakra.Application.csproj
```

- [ ] **Step 6: Create migration helper scripts**

`add-migration.ps1`:
```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Name
)

dotnet ef migrations add $Name --project Chakra.Infrastructure --startup-project Chakra.API
```

`update-database.ps1`:
```powershell
dotnet ef database update --project Chakra.Infrastructure --startup-project Chakra.API
```

- [ ] **Step 7: Verify solution builds**

```bash
dotnet build Chakra.slnx
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: scaffold Clean Architecture solution with 4 projects"
```

---

### Task 2: Create Chakra.Domain folder structure

**Files:**
- Create folders and placeholder files in `Chakra.Domain/`

**Produces:** Empty folder structure matching the source project's Domain layer

- [ ] **Step 1: Remove default Class1.cs and create Domain folders**

```bash
rm Chakra.Domain/Class1.cs

# Entities
mkdir -p Chakra.Domain/Entities/Common
mkdir -p Chakra.Domain/Entities/Enums

# Events
mkdir -p Chakra.Domain/Events
```

Folders created:
```
Chakra.Domain/
├── Entities/
│   ├── Common/       # Base classes: Entity, Aggregate, AuditableEntity, interfaces, StronglyTypedIds
│   └── Enums/        # PremiStatus, InstallmentStatus, AuditLogs, OutboxMessages
├── Events/           # Domain events: InstallmentPaidEvent
└── Chakra.Domain.csproj
```

- [ ] **Step 2: Add .gitkeep to empty folders (so git tracks them)**

```bash
touch Chakra.Domain/Entities/Common/.gitkeep
touch Chakra.Domain/Entities/Enums/.gitkeep
touch Chakra.Domain/Events/.gitkeep
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: create Chakra.Domain folder structure"
```

---

### Task 3: Create Chakra.Application folder structure

**Files:**
- Create folders in `Chakra.Application/`

**Produces:** Empty folder structure matching the source project's Application layer

- [ ] **Step 1: Remove default Class1.cs and create Application folders**

```bash
rm Chakra.Application/Class1.cs

# Common
mkdir -p Chakra.Application/Common/Behaviors

# Features - Installments
mkdir -p Chakra.Application/Features/Installments/Dtos
mkdir -p Chakra.Application/Features/Installments/Queries

# Features - Midtrans
mkdir -p Chakra.Application/Features/Midtrans/Command
mkdir -p Chakra.Application/Features/Midtrans/Dtos
mkdir -p Chakra.Application/Features/Midtrans/Settings

# Features - Premi
mkdir -p Chakra.Application/Features/Premi/Command
mkdir -p Chakra.Application/Features/Premi/Dtos
mkdir -p Chakra.Application/Features/Premi/Queries

# Features - Scheduler
mkdir -p Chakra.Application/Features/Scheduler/Settings

# Features - Users
mkdir -p Chakra.Application/Features/Users/Commands
mkdir -p Chakra.Application/Features/Users/Dtos
mkdir -p Chakra.Application/Features/Users/Queries

# Mappers
mkdir -p Chakra.Application/Mappers

# Services
mkdir -p Chakra.Application/Services
```

Folders created:
```
Chakra.Application/
├── Common/
│   └── Behaviors/            # ValidationResultPipelineBehavior, LoggingPipelineBehavior
│   ├── IApplicationDbContext.cs
│   ├── IContext.cs
│   ├── ICurrentUserService.cs
│   ├── PaginatedRequest.cs
│   └── Response.cs
├── Features/
│   ├── Installments/
│   │   ├── Dtos/             # InstallmentDto
│   │   └── Queries/          # GetInstallmentById, GetInstallmentsByPremiId
│   ├── Midtrans/
│   │   ├── Command/          # ProcessMidtransWebhook
│   │   ├── Dtos/             # MidtransNotificationsDto
│   │   └── Settings/         # MidtransSettings
│   ├── Premi/
│   │   ├── Command/          # CreatePremi, UpdatePremi, CancelPremi, CreatePremiValidator
│   │   ├── Dtos/             # PremiDto
│   │   └── Queries/          # GetPaginationPremi, GetPremiById
│   ├── Scheduler/
│   │   └── Settings/         # SchedulerSettings
│   └── Users/
│       ├── Commands/         # CreateUser, CreateUserValidator
│       ├── Dtos/             # UserDto
│       └── Queries/          # GetUsers
├── Mappers/                  # InstallmentMappers, PremiMappers
├── Services/                 # IMidtransService, IOutboxDispatcher, PremiCompletionService
├── ApplicationAssemblyReference.cs
└── Chakra.Application.csproj
```

- [ ] **Step 2: Add .gitkeep to all leaf folders**

```bash
for dir in \
  Chakra.Application/Common/Behaviors \
  Chakra.Application/Features/Installments/Dtos \
  Chakra.Application/Features/Installments/Queries \
  Chakra.Application/Features/Midtrans/Command \
  Chakra.Application/Features/Midtrans/Dtos \
  Chakra.Application/Features/Midtrans/Settings \
  Chakra.Application/Features/Premi/Command \
  Chakra.Application/Features/Premi/Dtos \
  Chakra.Application/Features/Premi/Queries \
  Chakra.Application/Features/Scheduler/Settings \
  Chakra.Application/Features/Users/Commands \
  Chakra.Application/Features/Users/Dtos \
  Chakra.Application/Features/Users/Queries \
  Chakra.Application/Mappers \
  Chakra.Application/Services; do
  touch "$dir/.gitkeep"
done
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: create Chakra.Application folder structure"
```

---

### Task 4: Create Chakra.Infrastructure folder structure

**Files:**
- Create folders in `Chakra.Infrastructure/`

**Produces:** Empty folder structure matching the source project's Infrastructure layer

- [ ] **Step 1: Remove default Class1.cs and create Infrastructure folders**

```bash
rm Chakra.Infrastructure/Class1.cs

mkdir -p Chakra.Infrastructure/Common
mkdir -p Chakra.Infrastructure/Configuration
mkdir -p Chakra.Infrastructure/Interceptors
mkdir -p Chakra.Infrastructure/Migrations
mkdir -p Chakra.Infrastructure/Services
```

Folders created:
```
Chakra.Infrastructure/
├── Common/              # EfConstans
├── Configuration/       # UserConfiguration, PremiConfiguration, InstallmentConfiguration,
│                        # OutboxMessageConfiguration, AuditLogConfiguration
├── Interceptors/        # AuditableEntityInterceptor, DispatchDomainEventsInterceptor
├── Migrations/          # EF Core migrations (generated)
├── Services/            # CurrentUserService, MidtransService
├── ApplicationDbContext.cs
└── Chakra.Infrastructure.csproj
```

- [ ] **Step 2: Add .gitkeep to all leaf folders**

```bash
for dir in \
  Chakra.Infrastructure/Common \
  Chakra.Infrastructure/Configuration \
  Chakra.Infrastructure/Interceptors \
  Chakra.Infrastructure/Migrations \
  Chakra.Infrastructure/Services; do
  touch "$dir/.gitkeep"
done
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: create Chakra.Infrastructure folder structure"
```

---

### Task 5: Create Chakra.API folder structure

**Files:**
- Create folders in `Chakra.API/`

**Produces:** Empty folder structure matching the source project's API layer

- [ ] **Step 1: Create API subfolders**

```bash
mkdir -p Chakra.API/Configurations
mkdir -p Chakra.API/Endpoints
mkdir -p Chakra.API/Middlewares
```

Note: `Properties/` and `Program.cs` are already created by `dotnet new webapi`.

Folders created:
```
Chakra.API/
├── Configurations/      # ApplicationSetup, AuthSetup, MediatRSetup, OpenApiSetup,
│                        # PersistenceSetup, ValidationSetup
├── Endpoints/           # AuthEndpoint, UserEndpoint, PremiEndpoint,
│                        # InstallmentEndpoint, MidtransWebhookEndpoint
├── Middlewares/         # EnsureUserMiddleware
├── Properties/
│   └── launchSettings.json
├── Chakra.API.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json (to create)
└── appsettings.UAT.json (to create)
```

- [ ] **Step 2: Add .gitkeep and missing appsettings files**

```bash
touch Chakra.API/Configurations/.gitkeep
touch Chakra.API/Endpoints/.gitkeep
touch Chakra.API/Middlewares/.gitkeep
touch Chakra.API/appsettings.Production.json
touch Chakra.API/appsettings.UAT.json
```

- [ ] **Step 3: Verify final structure and build**

```bash
dotnet build Chakra.slnx
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: create Chakra.API folder structure"
```

---

## Final Structure Verification

After all tasks, the repo should look like:

```
chakra-be/
├── Chakra.API/
│   ├── Configurations/
│   ├── Endpoints/
│   ├── Middlewares/
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Chakra.API.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── appsettings.UAT.json
├── Chakra.Application/
│   ├── Common/
│   │   └── Behaviors/
│   ├── Features/
│   │   ├── Installments/
│   │   │   ├── Dtos/
│   │   │   └── Queries/
│   │   ├── Midtrans/
│   │   │   ├── Command/
│   │   │   ├── Dtos/
│   │   │   └── Settings/
│   │   ├── Premi/
│   │   │   ├── Command/
│   │   │   ├── Dtos/
│   │   │   └── Queries/
│   │   ├── Scheduler/
│   │   │   └── Settings/
│   │   └── Users/
│   │       ├── Commands/
│   │       ├── Dtos/
│   │       └── Queries/
│   ├── Mappers/
│   ├── Services/
│   └── Chakra.Application.csproj
├── Chakra.Domain/
│   ├── Entities/
│   │   ├── Common/
│   │   └── Enums/
│   ├── Events/
│   └── Chakra.Domain.csproj
├── Chakra.Infrastructure/
│   ├── Common/
│   ├── Configuration/
│   ├── Interceptors/
│   ├── Migrations/
│   ├── Services/
│   └── Chakra.Infrastructure.csproj
├── Chakra.slnx
├── add-migration.ps1
├── update-database.ps1
├── .gitignore
└── README.md
```
