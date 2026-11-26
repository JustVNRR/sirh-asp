# Sprint 4 — Review & Rétrospective (`v0.5.0`)

- **Date de clôture** : 2026-11-26
- **Objectif du Sprint** : Couvrir l'ensemble de la Vie Interne dans le module autonome `Sirh.Modules.InternalLife` : base documentaire commune, kits d'accueil filtrables par type de contrat, annuaire des élus CSE et boîte à idées RSE avec workflow de traitement RH.
- **Statut** : ✅ **Accepté et clôturé** (`13 / 13 pts` — 3 / 3 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F7.1 — Module `InternalLife`, Documents Communs & Kits d'accueil** | `US-028` | 5 | `e842d86` |
| **F7.2 — Représentants du Personnel (CSE) & Boîte à Idées RSE** | `US-029`, `US-030` | 8 | `ecd6f25` |

### Livrables vérifiés
- **Module `Sirh.Modules.InternalLife`** (schéma `internal_life`, migrations `InitialInternalLife` et `AddStaffRepresentativesAndRseIdeas`) :
  - `CommonDocument` : stockage `CommonPublic` non chiffré avec contrôle *magic bytes* (PDF/PNG/JPEG/WEBP), filtrage `isWelcomeKit` + `contractTypeId` retournant documents ciblés et universels (`null`), permissions CSE restreintes aux catégories CSE (`403` sur accords d'entreprise) et garde `IContractTypeReferenceChecker`.
  - `StaffRepresentative` : annuaire filtrable (`companyId`, `harassmentAdvisor`), unicité e-mail/collaborateur par entreprise (`409`), écriture réservée à `CSE`/`RH`/`Admin` (`403` salarié standard).
  - `RseIdea` : soumission JSON ou multipart avec pièce jointe chiffrée **AES-256-GCM**, isolation auteur (`403`), verrouillage `409 Conflict` après traitement RH (`InReview`/`Treated`/`Rejected`) et garde `ILocationReferenceChecker`.

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **150 / 150 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **19 / 19** (isolation de tous les 6 modules vérifiée par `NetArchTest`)
  - `Sirh.UnitTests` : **87 / 87**
  - `Sirh.IntegrationTests` : **44 / 44**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - Le module `InternalLife` héberge proprement à la fois les documents communs non chiffrés (`CommonPublic`) et les pièces jointes RSE chiffrées (`EmployeeSensitive`) via la même abstraction `IFileStorageService`.
- **Points d'attention pour le Sprint 5 (`QCM d'Intégration Structuré`)** :
  - Garantir au niveau DTO et sérialisation JSON que `POST /api/v1/onboarding-quizzes/{id}/attempts` n'expose jamais le champ `IsCorrect`.
  - Refuser la publication (`400`) si une question n'a pas ≥ 2 choix ou aucune bonne réponse, et bloquer toute re-soumission (`409`) sur une tentative déjà clôturée.

---

## 4. Ouverture du Sprint 5 — QCM d'Intégration Structuré

- **Cap** : implémenter `F8.1` (`US-031` & `US-032` — `OnboardingQuiz`, `QuizQuestion`, `QuizChoice`, `QuizAttempt`, `QuizAttemptAnswer`) au sein de `Sirh.Modules.InternalLife`.
