# Sprint 0 — Review & Rétrospective (`v0.1.0`)

- **Date de clôture** : 2026-11-26
- **Objectif du Sprint** : Poser le Monolithe Modulaire .NET 10, SQL Server, le stockage sécurisé/chiffré et toute la chaîne d'authentification et d'autorisation RBAC.
- **Statut** : ✅ **Accepté et clôturé** (`39 / 39 pts` — 10 / 10 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F0.1 — Fondation & BuildingBlocks** | `US-001`, `US-002`, `US-003` | 11 | `668b9a6` |
| **F1.1 — Authentification JWT & Refresh Token rotatif** | `US-004`, `US-005`, `US-006` | 15 | `91a8e4e` |
| **F1.2 — Mots de passe & OTP persisté** | `US-007`, `US-008` | 8 | `cbb12fe` |
| **F1.3 — Profil `/auth/me` & Matrice RBAC** | `US-009`, `US-010` | 5 | `cbb12fe` |

### Livrables vérifiés
- Solution `Sirh.slnx` sous `.NET 10` avec `TreatWarningsAsErrors=true` et gestion centralisée des paquets (`Directory.Packages.props`).
- Briques transverses : `Result<T>`, `Error`, `Entity`, `AuditableEntity`, `AuditableEntityInterceptor`, `PagedRequest`/`PagedResult<T>`, `AesGcmCryptoService`, `MagicBytesFileValidator`, `LocalFileStorageService`, `SmtpEmailService`, `CorrelationIdMiddleware`, `GlobalExceptionHandler` (RFC 9457), Scalar/OpenAPI, health checks SQL Server + stockage, `docker-compose.yml` (SQL Server 2022 + Mailpit).
- Module `Sirh.Modules.Identity` (schéma `identity`, migrations `InitialIdentity` et `AddPasswordResetTokens`) : login avec lockout, refresh token rotatif avec détection de réutilisation, logout/logout-all, rate limiting `429`, changement/reset OTP 6 chiffres persisté et hashé SHA-256, profil `/api/v1/auth/me` et policies RBAC dynamiques + protection `SUPER_ADMIN`.

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **61 / 61 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **4 / 4**
  - `Sirh.UnitTests` : **40 / 40**
  - `Sirh.IntegrationTests` : **17 / 17**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - Séparation stricte en Monolithe Modulaire vérifiée automatiquement par `NetArchTest`.
  - Options bindées tardivement (`AddOptions<T>().BindConfiguration(...).ValidateOnStart()` + `IConfigureNamedOptions<JwtBearerOptions>`) pour garantir la compatibilité `WebApplicationFactory`.
  - Tests d'intégration complets couvrant verrouillage, vol de refresh token, cooldown OTP, 5 échecs OTP et protection `SUPER_ADMIN`.
- **Points d'attention pour le Sprint 1** :
  - Utiliser des interfaces cross-modules légères (ex. vérificateurs de références `ILocationReferenceChecker` / `IContractTypeReferenceChecker`) pour préserver l'absence de couplage entre modules tout en garantissant les `409 Conflict` métier.
  - Simuler la rotation `RowVersion` dans `AuditableEntityInterceptor` lorsque le provider EF Core est `InMemory` afin de tester la concurrence optimiste bout en bout sans SQL Server.

---

## 4. Ouverture du Sprint 1

- **Cap** : Référentiel Organisation (`F2.1` : `US-011`, `US-012`, `US-013`) suivi du cycle de vie Collaborateurs, Contacts d'urgence chiffrés et Postes (`F3.1`, `F3.2` : `US-014` à `US-018`).
