# Changelog

Toutes les modifications notables de ce projet sont documentées dans ce fichier.

Le format repose sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), et ce projet suit le [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- *Sprint 6 — Azure Cloud Readiness & Audit Final (en cours)*

## [0.6.0] - 2026-11-27 (Sprint 5 — QCM d'Intégration Structuré)

### Added

- **F8.1 (`f7f1018`) — QCM d'Intégration Structuré & Suivi des Résultats** (`US-031`, `US-032`)
  - Entités `OnboardingQuiz`, `QuizQuestion`, `QuizChoice`, `QuizAttempt`, `QuizAttemptAnswer` (`internal_life.onboarding_quizzes`, `quiz_questions`, `quiz_choices`, `quiz_attempts`, `quiz_attempt_answers`) et migration EF Core `AddOnboardingQuizzes`.
  - Calculateur de score `QuizScoringCalculator` dans le Domaine (`TotalQuestions`, `CorrectAnswers`, `ScorePercent`, `IsPassed` calculés à la soumission selon `PassingScorePercent`).
  - Endpoints `/api/v1/onboarding-quizzes/*` : CRUD & publication RH (`POST /`, `PUT /{id}`, `POST /{id}/publish`, `400` si une question n'a pas au moins 2 choix ou au moins 1 bonne réponse), démarrage de tentative salarié (`POST /{id}/attempts`, masquage strict de `IsCorrect`), soumission atomique (`POST /attempts/{attemptId}/submit`, verrouillage `409 Conflict` contre toute re-soumission), consultation des résultats (`GET /attempts/{attemptId}`, `GET /attempts/me`, `GET /{id}/results` pour RH/Managers avec accès restreint ABAC) et filtrage entreprise/type de contrat via `IHumanResourcesLookup.GetEmployeeCompanyIdAsync`.

## [0.5.0] - 2026-11-26 (Sprint 4 — Vie Interne : Docs, Kits, CSE, RSE)

### Added

- **F7.2 (`ecd6f25`) — Annuaire des Représentants du Personnel (CSE) & Boîte à idées RSE** (`US-029`, `US-030`)
  - Entités `StaffRepresentative` (`internal_life.staff_representatives`) et `RseIdea` (`internal_life.rse_ideas`) + migration `AddStaffRepresentativesAndRseIdeas`.
  - Endpoints `/api/v1/staff-representatives/*` : lecture ouverte (`staff-reps:read`), filtrage entreprise/référent harcèlement, écriture restreinte `CSE`/`RH`/`Admin`, unicité par entreprise et `RowVersion`.
  - Endpoints `/api/v1/rse-ideas/*` : dépôt avec pièce jointe chiffrée **AES-256-GCM** (`StorageScope.EmployeeSensitive`), ABAC auteur uniquement au statut `Submitted` (`409 Conflict` une fois traitée par RH via `PATCH /status`), et garde `RseIdeaLocationReferenceChecker` (`ILocationReferenceChecker`).
- **F7.1 (`e842d86`) — Module `InternalLife`, Documents Communs & Kits d'accueil** (`US-028`)
  - Création du module autonome `Sirh.Modules.InternalLife` (schéma SQL `internal_life`, migration `InitialInternalLife`), stockage `CommonPublic` non chiffré avec validation magic bytes, filtrage intelligent `contractTypeId` (ciblé + transverse), droits CSE restreints aux catégories CSE (`403` sur accords d'entreprise), garde `IContractTypeReferenceChecker` et 3 tests d'isolation `NetArchTest`.

## [0.4.0] - 2026-11-26 (Sprint 3 — Santé au Travail & Notifications)

### Added

- **F6.2 (`87b23a2`) — Rappels Automatisés & Centre de Notifications** (`US-026`, `US-027`)
  - Contrats inter-modules `INotificationLookup` et extension de `IHumanResourcesLookup` (`EmployeeContactDto`, `ExpiringDocumentAlertDto`) raccordant `/api/v1/auth/me` au compteur réel de notifications non lues.
  - Entité `Notification` (`health.notifications`, migration `AddNotifications`) et endpoints `/api/v1/notifications/*` (`GET /`, `/unread-count`, `PATCH /{id}/read`, `POST /read-all`, `DELETE /{id}`) avec contrôle strict d'appartenance (anti-IDOR `403`).
  - Job quotidien `DailyHealthRemindersJob` + `DailyHealthRemindersBackgroundService` (04h00 `Europe/Paris`) : rappels à J-5 sur les visites médicales futures uniquement et alertes J-30/J-7 sur RQTH & Titres de séjour vers le collaborateur et les RH (notifications + e-mails, 100 % idempotent).
- **F6.1 (`c634302`) — Module `Health` & Visites Médicales** (`US-025`)
  - Création du module autonome `Sirh.Modules.Health` (schéma SQL `health`, migration `InitialHealth`), entité `MedicalVisit`, convocation obligatoire + rapport optionnel chiffrés AES-256-GCM avec validation magic bytes, ABAC secret médical (`Self` ou `RH+`), endpoints `/api/v1/medical-visits/*` et 3 tests d'isolation `NetArchTest`.

## [0.3.0] - 2026-11-26 (Sprint 2 — Contrats PDF natifs & Dossier Salarié)

### Added

- **F5.1 (`38b83a0`) — Dossier Salarié, RQTH & Titres de séjour** (`US-023`, `US-024`)
  - Taxonomie structurée à 6 catégories (`PersonalFile`, `PersonalEvents`, `ContractualElements`, `Disciplinary`, `Departure`, `Integration`) et entités `EmployeeDocument`, `RqthDetail`, `ResidentPermitDetail` dans `Sirh.Modules.HumanResources` (schéma `hr`, migration `AddEmployeeDocuments`).
  - Endpoints `/api/v1/employee-documents/*` : validation magic bytes (PDF/PNG/JPEG/WEBP), chiffrement AES-256-GCM, exclusion stricte des catégories sensibles (`Disciplinary`, `Departure`) sans `employee-docs:read:sensitive`, ABAC `Self`/`Team`/`All`, validation atomique des champs d'échéance RQTH & Titres de séjour, concurrence optimiste `RowVersion`.
- **F4.2 (`de192df`) — Import/Statut des Contrats & Fiches de poste** (`US-021`, `US-022`)
  - Import multipart PDF avec contrôle magic bytes, suivi du statut de signature + concurrence optimiste (`RowVersion`), streaming déchiffré `inline`/`attachment`, fiches de poste uniques (`JobSheet`, migration `AddJobSheets`) avec ABAC manager direct (`jobsheets:read:team`) et protection de suppression de `Position`.
- **F4.1 (`8fa6173`) — Modèles de Contrats, Variables Dynamiques & Génération PDF Native** (`US-019`, `US-020`)
  - Création du module autonome `Sirh.Modules.Contracts` (schéma `contracts`, migration `InitialContracts`), moteur Scriban + liste blanche des variables, rendu PDF natif QuestPDF avec chiffrement AES-256-GCM à la volée, endpoints `/api/v1/contract-models/*` et `/api/v1/contracts/generate`.

## [0.2.0] - 2026-11-26 (Sprint 1 — Organisation & Collaborateurs)

### Added

- **F3.2 — Contacts d'urgence chiffrés & Postes** (`US-017`, `US-018`)
  - Support HTTP `422 Unprocessable Entity` (`ErrorType.UnprocessableEntity`) et centralisation des contrats de vérification inter-modules (`ILocationReferenceChecker`, `IContractTypeReferenceChecker`, `IPositionReferenceChecker`) dans `BuildingBlocks`.
  - Entités `EmergencyContact` et `Position` + migration EF Core `AddEmergencyContactsAndPositions` (schéma `hr`).
  - Endpoints `/api/v1/employees/{id}/emergency-contacts` et `/api/v1/emergency-contacts/{id}` : chiffrement AES-256-GCM des champs personnels, plafond strict de 2 contacts (`422`), ABAC `Self`/`HR+`, concurrence `RowVersion`.
  - Endpoints `/api/v1/employees/{id}/positions` et `/api/v1/positions` : slug immuable `poste-{employeeSlug}[-n]`, calcul dynamique `IsCurrent` avec synchronisation de `Employee.CurrentlyInPosition` et `DirectManagerId`, et `PositionLocationReferenceChecker` bloquant `409 Conflict` sur suppression d'un lieu référencé.
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
