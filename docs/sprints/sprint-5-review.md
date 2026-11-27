# Sprint 5 — Review & Rétrospective (`v0.6.0`)

- **Date de clôture** : 2026-11-27
- **Objectif du Sprint** : Implémenter le parcours structuré de QCM d'intégration au sein du module `Sirh.Modules.InternalLife` : création, édition et publication par les RH, passage par les nouveaux arrivants sans fuite des réponses correctes, calcul automatique du score et suivi des résultats par les managers et RH.
- **Statut** : ✅ **Accepté et clôturé** (`10 / 10 pts` — 2 / 2 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F8.1 — QCM d'Intégration Structuré & Suivi des Résultats** | `US-031`, `US-032` | 10 | `f7f1018` |

### Livrables vérifiés
- **Module `Sirh.Modules.InternalLife`** (schéma `internal_life`, migration `AddOnboardingQuizzes`) :
  - Entités `OnboardingQuiz`, `QuizQuestion`, `QuizChoice`, `QuizAttempt`, `QuizAttemptAnswer` et service domaine `QuizScoringCalculator`.
  - Endpoints RH (`/api/v1/onboarding-quizzes`, `PUT /{id}`, `POST /{id}/publish`) validant qu'un QCM publiable contient au moins 1 question, >= 2 choix par question et >= 1 bonne réponse (`400 Bad Request` sinon).
  - Endpoints Collaborateur (`/api/v1/onboarding-quizzes/available`, `POST /{id}/attempts`, `POST /attempts/{attemptId}/submit`) garantissant l'absence de `IsCorrect` dans les DTO de passage, l'isolation de tentative par collaborateur (`403 Forbidden`), et le blocage de toute double soumission (`409 Conflict`).
  - Suivi des résultats (`GET /attempts/{attemptId}`, `GET /attempts/me`, `GET /{id}/results` avec périmètre ABAC Manager/RH) et extension `IHumanResourcesLookup.GetEmployeeCompanyIdAsync`.

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **154 / 154 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **19 / 19** (isolation de tous les 6 modules vérifiée par `NetArchTest`)
  - `Sirh.UnitTests` : **89 / 89**
  - `Sirh.IntegrationTests` : **46 / 46**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - La séparation nette entre les DTO de passage (`QuizTakeDto` sans `IsCorrect`) et les DTO de correction post-soumission (`QuizAttemptResultDto`) élimine tout risque de fuite d'information côté client.
- **Vigilance technique retenue** :
  - Avec EF Core (`InMemory` et SQL Server) et des entités enfants utilisant `Guid.CreateVersion7()` généré côté applicatif, l'ajout d'enfants à un agrégat déjà suivi nécessite un enregistrement explicite via `DbSet.Add(...)` / `AddRange(...)` afin d'éviter que `DetectChanges` ne les classe en `Modified`.
- **Ouverture du Sprint 6 (dernier sprint — `v1.0.0`)** :
  - `F9.1` (`US-033` : `AzureBlobFileStorageService` + Azurite avec chiffrement AES-256-GCM pré-envoi ; `US-034` : `SecurityHeadersMiddleware`, vérification `FallbackPolicy` et audit automatisé anti-IDOR `401`/`403` sur les 6 modules).
