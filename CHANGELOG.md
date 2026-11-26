# Changelog

Toutes les modifications notables de ce projet sont documentées dans ce fichier.

Le format repose sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), et ce projet suit le [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **F3.1 — Module `HumanResources` : Cycle de vie des Collaborateurs** (`US-014`, `US-015`, `US-016`)
  - Contrats inter-modules dans `Sirh.BuildingBlocks.Application` (`IOrganizationLookup`, `IIdentityUserProvisioningService`) avec implémentations découplées dans `Identity` et `Organization` (zéro couplage direct vers `HumanResources`).
  - Création du module `Sirh.Modules.HumanResources` (schéma SQL Server `hr`, migration `InitialHumanResources`, entité `Employee`).
  - `POST /api/v1/employees` : génération de slug immuable et d'e-mail professionnel (`<initiale><nom>@<entreprise>.fr` avec gestion des collisions), chiffrement AES-256-GCM de `NationalId`, création du compte utilisateur lié à `EmployeeId` et blocage `403` anti-escalade (`Admin`/`SuperAdmin`).
  - `GET /api/v1/employees`, `GET /{id}` et `PUT /{id}` : filtrage multi-critères, contrôle ABAC (`Self`, `DirectManager` avec masquage des PII sensibles, `HR`/`Admin`) et concurrence optimiste `RowVersion` (`409 Conflict`).
  - `POST /api/v1/employees/{id}/deactivate` et `/activate` : désactivation logique avec révocation immédiate de tous les Refresh Tokens et protection anti-escalade.
- **F2.1 — Module `Organization` & Référentiels** (`US-011`, `US-012`, `US-013`)
  - Création du module `Sirh.Modules.Organization` (schéma SQL Server `organization`, migration `InitialOrganization`) avec isolation stricte vis-à-vis de `Sirh.Modules.Identity`.
  - Utilitaires `BuildingBlocks` : `SlugGenerator` (normalisation ASCII/accents) et `SiretValidator` (validation SIRET 14 chiffres Luhn + exception La Poste), plus rotation de `RowVersion` sous EF Core `InMemory`.
  - Endpoints `/api/v1/companies` (convention collective par défaut `"Syntec"`, contrôle `RowVersion` `409 Conflict`, activation/désactivation), `/api/v1/locations` (slug unique auto-généré, `#RRGGBB`, filtres et protection `409` si référencé), `/api/v1/contract-types` (4 types système `CDI`, `CDD`, `APPRENTISSAGE`, `CONTRAT_PRO` protégés `403` et vérification d'usage `409`) et `GET /api/v1/referentials/departments`.

## [0.1.0] - 2026-11-26 (Sprint 0 — Socle & Auth)

### Added

- **F1.2 — Gestion des mots de passe & OTP persisté** (`US-007`, `US-008`)
  - Service `SmtpEmailService` (`IEmailService`) dans `Sirh.BuildingBlocks.Infrastructure` avec configuration `EmailOptions`.
  - Entité `PasswordResetToken` et migration EF Core `AddPasswordResetTokens` (schéma `identity`).
  - `POST /api/v1/auth/password/change` (authentifié) avec politique de complexité (>= 10 caractères, majuscule, minuscule, chiffre, caractère spécial) et révocation globale des Refresh Tokens.
  - `POST /api/v1/auth/password/forgot` (`202 Accepted`, anti-énumération, cooldown 60 s, OTP 6 chiffres hashé SHA-256 à TTL 15 min) et `POST /api/v1/auth/password/reset` (`204 No Content`, comparaison à temps constant, invalidation après 5 échecs, déverrouillage Identity et révocation des sessions).
- **F1.3 — Profil courant & Matrice Rôles / Permissions RBAC** (`US-009`, `US-010`)
  - `GET /api/v1/auth/me` retournant identifiants utilisateur, rôles avec priorité maximale, permissions effectives et compteur `unreadNotificationsCount`.
  - Policies d'autorisation déclaratives `PermissionRequirement` / `PermissionAuthorizationHandler` pour toutes les permissions métier.
  - Endpoints d'administration `GET /api/v1/roles`, `GET /api/v1/permissions`, `GET /api/v1/roles/{id}/permissions` et `PUT /api/v1/roles/{id}/permissions` avec protection du rôle `SUPER_ADMIN` (`403 Forbidden` si appelant non `SUPER_ADMIN`).
- **F1.1 — Authentification JWT & Refresh Token rotatif** (`US-004`, `US-005`, `US-006`)
  - Création du module `Sirh.Modules.Identity` (Domain, Application, Infrastructure, Presentation) avec schéma SQL Server `identity` et migration EF Core `InitialIdentity`.
  - Endpoints `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout` et `POST /api/v1/auth/logout-all`.
  - Access Token JWT HMAC-SHA256 courte durée (15 min) et Refresh Token rotatif stocké en hash SHA-256 (14 jours) avec détection de réutilisation révoquant toute la famille de tokens.
  - Verrouillage automatique du compte après 5 échecs de connexion (15 min) et Rate Limiting sur `/api/v1/auth/*` (`429 Too Many Requests` au format RFC 9457 `ProblemDetails`).
  - Seeding des rôles (`UNKNOWN`, `APPRENTICE`, `EMPLOYEE`, `MANAGER`, `CSE`, `HR`, `ADMIN`, `SUPER_ADMIN`) et permissions RBAC.
- **F0.1 — Fondation Monolithe Modulaire .NET 10** (`US-001`, `US-002`, `US-003`)
  - Initialisation de la solution `Sirh.slnx` (.NET 10, `Directory.Build.props` avec `TreatWarningsAsErrors`, `Directory.Packages.props`).
  - Implémentation de `Sirh.BuildingBlocks.Domain`, `Sirh.BuildingBlocks.Application` et `Sirh.BuildingBlocks.Infrastructure` (`Result<T>`, `Error`, `Entity`, `AuditableEntity`, `AuditableEntityInterceptor`, `PagedRequest`/`PagedResult<T>`, chiffrement AES-256-GCM, validation de fichiers par magic bytes, stockage local `IFileStorageService`, health checks SQL Server & stockage).
  - Configuration du host `Sirh.Api` avec corrélation `X-Correlation-Id`, gestion globale des exceptions RFC 9457 (`GlobalExceptionHandler`), Serilog, OpenAPI/Scalar et `docker-compose.yml` (SQL Server 2022 + Mailpit).
  - Mise en place des suites de tests (`Sirh.ArchitectureTests`, `Sirh.UnitTests`, `Sirh.IntegrationTests`) et du workflow CI GitHub Actions.
