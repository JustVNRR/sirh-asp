# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Azure Blob Storage Provider (`b9b0889`)**
  - Added `Azure.Storage.Blobs` (`12.24.0`) and `Azure.Identity` (`1.17.1`) via Central Package Management (`Directory.Packages.props`).
  - Implemented `AzureBlobFileStorageService` supporting Azurite (`UseDevelopmentStorage=true`) and Azure Managed Identity (`DefaultAzureCredential`), routing `EmployeeSensitive` payloads (encrypted with **AES-256-GCM** prior to upload) to `employees-documents` and `CommonPublic` payloads to `common-documents`.
  - Updated `DependencyInjection` provider selection (`Storage:Provider = "Local" | "AzureBlob"`), `FileStorageHealthCheck` (`GetAccountInfoAsync` probe), `appsettings.json`, `appsettings.Development.example.json`, and unit test coverage (`AzureBlobFileStorageServiceTests`).
- **HTTP Security Hardening & Authorization Audit (`b9b0889`)**
  - Added `SecurityHeadersMiddleware` setting `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `X-Permitted-Cross-Domain-Policies: none`, `Cross-Origin-Resource-Policy: same-origin`, and strict `Content-Security-Policy` (excluding `/scalar` and `/openapi`).
  - Added exhaustive integration audit tests (`SecurityAuditEndpointsTests`) verifying `FallbackPolicy` (`DenyAnonymousAuthorizationRequirement`), `EndpointDataSource` `[AllowAnonymous]` whitelist, `401 Unauthorized` across 33 protected endpoints, and `403 Forbidden` anti-IDOR / anti-privilege-escalation across all 6 modules.
- **Repository Documentation**
  - Added root `README.md` (in English) with architecture overview, quick-start instructions after cloning, demo accounts, Azurite configuration, and test commands.

### Changed

- Translated `CHANGELOG.md` to English and excluded internal `docs/` planning files from Git tracking via `.gitignore`.

## [0.6.0] - 2026-11-27

### Added

- **Structured Onboarding Quizzes & Attempt Scoring (`f7f1018`)**
  - Added `OnboardingQuiz`, `QuizQuestion`, `QuizChoice`, `QuizAttempt`, and `QuizAttemptAnswer` entities (`internal_life` schema, EF Core migration `AddOnboardingQuizzes`) plus `QuizScoringCalculator` domain service.
  - Added `/api/v1/onboarding-quizzes/*` endpoints: HR authoring & publishing (`400` if a question has fewer than 2 choices or no correct answer), employee attempt start strictly hiding `IsCorrect` and explanations prior to submission, atomic attempt submission (`409 Conflict` blocking re-submission), personal attempt history, and ABAC-scoped manager/HR result tracking via `IHumanResourcesLookup.GetEmployeeCompanyIdAsync`.

## [0.5.0] - 2026-11-26

### Added

- **Staff Representatives (CSE) Directory & RSE Idea Box (`ecd6f25`)**
  - Added `StaffRepresentative` and `RseIdea` entities (`internal_life` schema, migration `AddStaffRepresentativesAndRseIdeas`).
  - Added `/api/v1/staff-representatives/*` endpoints (open read with `staff-reps:read`, writes restricted to `CSE`/`HR`/`Admin`, per-company uniqueness, optimistic concurrency `RowVersion`).
  - Added `/api/v1/rse-ideas/*` endpoints with **AES-256-GCM** encrypted attachments (`StorageScope.EmployeeSensitive`), author isolation (`403`), `409 Conflict` lock once treated by HR (`PATCH /status`), and `ILocationReferenceChecker` deletion guard.
- **InternalLife Module, Common Documents & Welcome Kits (`e842d86`)**
  - Created autonomous `Sirh.Modules.InternalLife` module (SQL schema `internal_life`, migration `InitialInternalLife`), unencrypted `CommonPublic` file storage with magic-bytes validation, `isWelcomeKit` + `contractTypeId` filtering (targeted + universal), CSE category guards (`403` on company agreements), `IContractTypeReferenceChecker` deletion guard, and 3 `NetArchTest` isolation rules.

## [0.4.0] - 2026-11-26

### Added

- **Automated Health & Expiry Reminders and Notification Center (`87b23a2`)**
  - Added `INotificationLookup` and extended `IHumanResourcesLookup` (`EmployeeContactDto`, `ExpiringDocumentAlertDto`), wiring `/api/v1/auth/me` to live `unreadNotificationsCount`.
  - Added `Notification` entity (`health.notifications`, migration `AddNotifications`) and `/api/v1/notifications/*` endpoints with strict owner-only anti-IDOR isolation (`403`).
  - Added idempotent daily `DailyHealthRemindersJob` + `DailyHealthRemindersBackgroundService` (04:00 `Europe/Paris`) sending J-5 upcoming medical visit reminders and J-30/J-7 RQTH & Resident Permit expiration alerts via in-app notifications and email.
- **Health Module & Medical Visits (`c634302`)**
  - Created autonomous `Sirh.Modules.Health` module (SQL schema `health`, migration `InitialHealth`), `MedicalVisit` entity with mandatory encrypted convocation and optional encrypted report (`AES-256-GCM` + magic-bytes check), medical secrecy ABAC (`Self` or `HR+`), `/api/v1/medical-visits/*` endpoints, and 3 `NetArchTest` isolation rules.

## [0.3.0] - 2026-11-26

### Added

- **Employee File, RQTH & Resident Permits (`38b83a0`)**
  - Implemented 6-category employee document taxonomy (`PersonalFile`, `PersonalEvents`, `ContractualElements`, `Disciplinary`, `Departure`, `Integration`) with `EmployeeDocument`, `RqthDetail`, and `ResidentPermitDetail` entities (`hr` schema, migration `AddEmployeeDocuments`).
  - Added `/api/v1/employee-documents/*` endpoints with magic-bytes validation (`PDF`/`PNG`/`JPEG`/`WEBP`), AES-256-GCM encryption, strict exclusion of `Disciplinary`/`Departure` documents without `employee-docs:read:sensitive`, ABAC `Self`/`Team`/`All` access control, expiry validation, and `RowVersion` concurrency.
- **Contract Uploads, Signature Tracking & Job Sheets (`de192df`)**
  - Added multipart PDF contract upload with magic-bytes validation, signature status lifecycle with `RowVersion`, `inline`/`attachment` decrypted file streaming, and unique `JobSheet` per `Position` (migration `AddJobSheets`, ABAC direct-manager read, and position deletion guard).
- **Contract Templates, Dynamic Variables & Native PDF Generation (`8fa6173`)**
  - Created autonomous `Sirh.Modules.Contracts` module (`contracts` schema, migration `InitialContracts`), Scriban rendering with whitelisted variables, QuestPDF native PDF generation with AES-256-GCM encryption on save, and `/api/v1/contract-models/*` + `/api/v1/contracts/generate` endpoints.

## [0.2.0] - 2026-11-26

### Added

- **Encrypted Emergency Contacts & Positions History**
  - Added HTTP `422 Unprocessable Entity` (`ErrorType.UnprocessableEntity`) and centralized cross-module deletion-guard contracts (`ILocationReferenceChecker`, `IContractTypeReferenceChecker`, `IPositionReferenceChecker`) in `BuildingBlocks`.
  - Added `EmergencyContact` (AES-256-GCM encrypted fields, max 2 contacts enforced via `422`, ABAC `Self`/`HR+`, `RowVersion`) and `Position` entities (`hr` schema, migration `AddEmergencyContactsAndPositions`) with dynamic `IsCurrent` tracking synced with `Employee.CurrentlyInPosition` and `DirectManagerId`, plus `PositionLocationReferenceChecker` (`409` on location deletion).
- **HumanResources Module & Employee Lifecycle**
  - Created autonomous `Sirh.Modules.HumanResources` module (`hr` schema, migration `InitialHumanResources`, `Employee` entity) decoupled from `Organization` and `Identity` via `IOrganizationLookup` and `IIdentityUserProvisioningService`.
  - Added `/api/v1/employees` endpoints: immutable slug and collision-safe corporate email generation, AES-256-GCM `NationalId` encryption, linked Identity account provisioning, anti-escalation `403` (`Admin`/`SuperAdmin`), ABAC (`Self`/`DirectManager` with PII masking/`HR+`), `RowVersion` concurrency, and logical deactivation with immediate Refresh Token revocation.
- **Organization Module & Referentials**
  - Created autonomous `Sirh.Modules.Organization` module (`organization` schema, migration `InitialOrganization`) strictly isolated from `Identity`, plus `SlugGenerator`, `SiretValidator` (14-digit Luhn + La Poste exception), and `InMemory` `RowVersion` rotation.
  - Added `/api/v1/companies` (default `"Syntec"` collective agreement), `/api/v1/locations`, `/api/v1/contract-types` (protected system types `CDI`, `CDD`, `APPRENTISSAGE`, `CONTRAT_PRO`), and `GET /api/v1/referentials/departments`.

## [0.1.0] - 2026-11-26

### Added

- **Password Management & Persisted OTP Reset**
  - Added `SmtpEmailService` (`IEmailService`) in `Sirh.BuildingBlocks.Infrastructure` and `PasswordResetToken` (`identity` schema, migration `AddPasswordResetTokens`).
  - Added authenticated `POST /api/v1/auth/password/change` (complexity policy + global Refresh Token revocation), `POST /api/v1/auth/password/forgot` (`202 Accepted`, anti-enumeration, 60s cooldown, SHA-256 hashed 6-digit OTP with 15-min TTL), and `POST /api/v1/auth/password/reset` (constant-time comparison, invalidation after 5 attempts, Identity unlock, session revocation).
- **Current Profile & RBAC Roles/Permissions Matrix**
  - Added `GET /api/v1/auth/me` returning user profile, roles with max priority, effective permissions, and live `unreadNotificationsCount`, plus declarative `PermissionRequirement` / `PermissionAuthorizationHandler` policies and `/api/v1/roles` + `/api/v1/permissions` endpoints protecting `SUPER_ADMIN`.
- **JWT Authentication & Rotating Refresh Tokens**
  - Created `Sirh.Modules.Identity` module (`identity` schema, migration `InitialIdentity`) with `POST /api/v1/auth/login`, `/refresh` (SHA-256 hashed rotating tokens with family-wide reuse revocation), `/logout`, `/logout-all`, 5-attempt lockout, Auth rate limiting (`429` `ProblemDetails`), and RBAC role/permission seeding.
- **Modular Monolith Foundation**
  - Initialized `Sirh.slnx` (.NET 10, `TreatWarningsAsErrors=true`, Central Package Management), `Sirh.BuildingBlocks.*` (`Result<T>`, `Entity`, `AuditableEntityInterceptor`, `PagedResult<T>`, AES-256-GCM crypto, magic-bytes validator, local file storage, SQL/storage health checks), `Sirh.Api` pipeline (`X-Correlation-Id`, RFC 9457 `GlobalExceptionHandler`, Serilog, OpenAPI/Scalar), `docker-compose.yml`, `Sirh.ArchitectureTests`, `Sirh.UnitTests`, `Sirh.IntegrationTests`, and GitHub Actions CI.
