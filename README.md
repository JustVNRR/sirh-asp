# SIRH — .NET 10 Modular Monolith HR API

A clean, production-ready **.NET 10 Modular Monolith** REST API for Human Resources Management (SIRH), backed by SQL Server, EF Core 10, application-level AES-256-GCM encryption, native PDF generation (`Scriban` + `QuestPDF`), and switchable local or Azure Blob Storage.

---

## Architecture Overview

The solution follows **Clean Architecture** and **Modular Monolith** principles with strict module boundaries enforced by automated `NetArchTest` rules:

| Module | SQL Schema | Responsibilities |
| :--- | :--- | :--- |
| **Identity** | `identity` | JWT Bearer authentication, rotating Refresh Tokens with reuse detection, account lockout, OTP password reset, RBAC roles & permissions, `/auth/me` |
| **Organization** | `organization` | Companies (SIRET Luhn validation), Locations, Contract Types (`CDI`, `CDD`, `APPRENTISSAGE`, `CONTRAT_PRO`), French departments referential |
| **HumanResources** | `hr` | Employees lifecycle (slug & work email generation, encrypted `NationalId`), encrypted Emergency Contacts (max 2), Positions history, Employee Documents (6-category taxonomy, RQTH & Resident Permit expirations) |
| **Contracts** | `contracts` | Scriban contract templates, whitelisted dynamic variables, native QuestPDF rendering, contract upload/signature tracking, Job Sheets per Position |
| **Health** | `health` | Medical Visits (`Hiring`, periodic follow-ups, encrypted convocations & reports, medical secrecy ABAC), Notification center, daily background reminder job (04:00 `Europe/Paris`) |
| **InternalLife** | `internal_life` | Common Documents & contract-filtered Welcome Kits, Staff Representatives (CSE) directory, RSE Idea Box workflow, Onboarding Quizzes authoring & attempt scoring |

### Key Security & Reliability Features
- **Default-Deny Authorization**: Global ASP.NET Core `FallbackPolicy` requiring authenticated users, declarative RBAC permission policies, and resource-level ABAC / anti-IDOR ownership checks across all 6 modules.
- **Defensive HTTP Pipeline**: Correlation IDs (`X-Correlation-Id`), `SecurityHeadersMiddleware` (`nosniff`, `DENY`, strict CSP outside dev docs, `Referrer-Policy`, `Permissions-Policy`), RFC 9457 `ProblemDetails`, Serilog structured logging, and auth endpoint rate limiting (`429`).
- **Encryption at Rest**: Sensitive PII (`NationalId`, emergency contacts) and confidential employee files (`StorageScope.EmployeeSensitive`) are encrypted with **AES-256-GCM** at the application boundary before hitting SQL Server, disk, or Azure Blob Storage.
- **Upload Validation**: Extension whitelist + file size limits + strict **magic-bytes** header inspection (`.pdf`, `.png`, `.jpg`, `.jpeg`, `.webp`).

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) (`10.0.402` or later — see [`global.json`](global.json))
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local SQL Server 2022, Mailpit, and optional Azurite)

---

## Quick Start (After Cloning)

### 1. Clone and create your local development config

```bash
git clone <repository-url>
cd sirh
cp src/Sirh.Api/appsettings.Development.example.json src/Sirh.Api/appsettings.Development.json
```

> `appsettings.Development.json` is gitignored so local secrets and dev overrides are never committed.

### 2. Start local infrastructure (SQL Server + Mailpit)

```bash
docker compose up -d
```

This starts:
- **SQL Server 2022** on `localhost:1433` (`sa` / `Sirh_Dev_Password123!`)
- **Mailpit SMTP** on `localhost:1025` and **Mailpit Web UI** at `http://localhost:8025`

### 3. Run the API

```bash
dotnet run --project src/Sirh.Api
```

In `Development` (with the example config), EF Core migrations and seed data run automatically on startup across all 6 module schemas.

- **Scalar API Reference**: `https://localhost:54235/scalar/v1` (or `http://localhost:54236/scalar/v1`)
- **OpenAPI JSON**: `https://localhost:54235/openapi/v1.json`
- **Health Check**: `https://localhost:54235/health`
- **Mailpit UI** (OTP & reminder emails): `http://localhost:8025`

### Seeded Demo Accounts (`Development`)

| Email | Role(s) | Password |
| :--- | :--- | :--- |
| `admin.rh@nova-conseil.fr` | `RH`, `Admin` | `SirhDemo123!` |
| `manager.rh@nova-conseil.fr` | `Manager` | `SirhDemo123!` |
| `employe.rh@nova-conseil.fr` | `Employe` | `SirhDemo123!` |

---

## Testing the API (Scalar UI, Postman, `.http`)

In .NET 10, `Microsoft.AspNetCore.OpenApi` + **Scalar UI** replaces legacy `Swashbuckle` / Swagger UI out of the box.

### Option 1 — Scalar UI (Built-in Interactive Docs)
1. Start Docker (`docker compose up -d`) and the API (`dotnet run --project src/Sirh.Api`).
2. Open **`https://localhost:54235/scalar/v1`** (or `http://localhost:54236/scalar/v1`) in your browser.
3. Browse endpoints grouped by module/tag and click **Test Request** to run calls directly.

### Option 2 — Postman, Bruno, or Insomnia (OpenAPI Auto-Import)
No manual collection is required:
1. Ensure the API is running in `Development`.
2. In Postman (or Bruno / Insomnia), choose **Import** and enter the OpenAPI specification URL:
   - `http://localhost:54236/openapi/v1.json` (or `https://localhost:54235/openapi/v1.json`)
3. All endpoints, request schemas, and the global `Bearer` JWT security scheme are imported automatically.
4. Set **Authorization → Type: Bearer Token** at the collection root once to reuse your JWT across requests.

### Option 3 — Native `.http` Files (Visual Studio / Rider / VS Code)
You can also invoke endpoints directly from `.http` files in Visual Studio 2022+, JetBrains Rider, or VS Code (*REST Client* extension).

### Authenticating Protected Requests (JWT Workflow)
1. Call `POST /api/v1/auth/login` with a seeded account:
   ```json
   {
     "email": "admin.rh@nova-conseil.fr",
     "password": "SirhDemo123!"
   }
   ```
2. Copy the `accessToken` value from the response.
3. Paste it into Scalar's global **Authentication → Bearer** field (or Postman's collection Bearer token / `Authorization: Bearer <accessToken>` header).

---

## Storage Providers (`Local` vs. `AzureBlob` / Azurite)

File storage is configured under the `Storage` section in `appsettings.json` / `appsettings.Development.json`:

- **Local filesystem (default)**:
  ```json
  "Storage": {
    "Provider": "Local",
    "LocalRootPath": "./storage"
  }
  ```
- **Azure Blob Storage / Azurite**:
  Start the optional Azurite emulator profile:
  ```bash
  docker compose --profile azure up -d azurite
  ```
  Then set:
  ```json
  "Storage": {
    "Provider": "AzureBlob",
    "AzureConnectionString": "UseDevelopmentStorage=true",
    "EmployeeSensitiveContainer": "employees-documents",
    "CommonPublicContainer": "common-documents",
    "CreateContainersIfMissing": true
  }
  ```
  - For Azure production deployments with Managed Identity, leave `AzureConnectionString` empty and configure `Storage:AzureServiceUri` (the service uses `DefaultAzureCredential`).
  - Sensitive files (`StorageScope.EmployeeSensitive`) remain encrypted with AES-256-GCM **before** upload to Azure Blob Storage / Azurite.

---

## Testing & Verification

The test suites run against EF Core InMemory databases and SDK doubles, so no external Docker container is required to execute tests:

```bash
dotnet build Sirh.slnx
dotnet test Sirh.slnx
```

- `tests/Sirh.ArchitectureTests` — `NetArchTest` rules enforcing module isolation and Clean Architecture layer dependencies
- `tests/Sirh.UnitTests` — Domain logic, validators, Scriban/QuestPDF rendering, AES-256-GCM crypto, magic bytes, and `AzureBlobFileStorageService` unit tests
- `tests/Sirh.IntegrationTests` — End-to-end HTTP integration tests across all 6 modules plus `401`/`403` anti-IDOR and security header audits

---

## Solution Structure

```text
├── src/
│   ├── BuildingBlocks/
│   │   ├── Sirh.BuildingBlocks.Domain/          # Result<T>, Error, Entity, Slug & SIRET helpers
│   │   ├── Sirh.BuildingBlocks.Application/     # Inter-module contracts, IFileStorageService, crypto abstractions
│   │   └── Sirh.BuildingBlocks.Infrastructure/  # EF interceptors, AES-256-GCM, Local & Azure Blob storage, SMTP, health checks
│   ├── Modules/
│   │   ├── Identity/                            # Auth, JWT/Refresh tokens, OTP reset, RBAC roles
│   │   ├── Organization/                        # Companies, Locations, ContractTypes, Departments
│   │   ├── HumanResources/                      # Employees, EmergencyContacts, Positions, EmployeeDocuments
│   │   ├── Contracts/                           # ContractModels (Scriban/QuestPDF), Contracts, JobSheets
│   │   ├── Health/                              # MedicalVisits, Notifications, daily reminder job
│   │   └── InternalLife/                        # CommonDocuments, Welcome Kits, StaffReps, RSE Ideas, OnboardingQuizzes
│   └── Sirh.Api/                                # Minimal API host, Serilog, RateLimiter, SecurityHeadersMiddleware
└── tests/
    ├── Sirh.ArchitectureTests/
    ├── Sirh.UnitTests/
    └── Sirh.IntegrationTests/
```

---

## Production Configuration Notes

Before deploying to non-development environments, supply secrets via environment variables, Azure Key Vault, or user secrets (never commit them):
- `ConnectionStrings:DefaultConnection`
- `Jwt:SigningKey` (minimum 32 characters)
- `Encryption:MasterKey` (minimum 32 characters)
- `Cors:AllowedOrigins` (explicit frontend origins)
- Keep `Identity:AutoMigrate`, `Identity:SeedDemoUsers`, `Organization:SeedDemoData`, and module `AutoMigrate` flags disabled (`false`), and apply EF Core migrations during your deployment pipeline.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and unreleased changes.
