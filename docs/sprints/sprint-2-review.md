# Sprint 2 — Review & Rétrospective (`v0.3.0`)

- **Date de clôture** : 2026-11-26
- **Objectif du Sprint** : Industrialiser la génération native de contrats PDF (Scriban + QuestPDF sans LibreOffice), la gestion des contrats & fiches de poste uniques, et le dossier salarié chiffré avec attestations RQTH & Titres de séjour.
- **Statut** : ✅ **Accepté et clôturé** (`31 / 31 pts` — 6 / 6 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F4.1 — Modèles Markdown/Scriban & Génération PDF QuestPDF** | `US-019`, `US-020` | 13 | `8fa6173` |
| **F4.2 — Import/Statut des Contrats & Fiches de poste** | `US-021`, `US-022` | 8 | `de192df` |
| **F5.1 — Documents Salarié, RQTH & Titres de séjour** | `US-023`, `US-024` | 10 | `38b83a0` |

### Livrables vérifiés
- **Module `Sirh.Modules.Contracts`** (schéma `contracts`, migrations `InitialContracts` et `AddJobSheets`) :
  - `ContractModel` : templates Markdown enrichis Scriban avec validation syntaxique stricte, limites d'exécution (`LoopLimit` / `RecursiveLimit`), catalogue typé `/api/v1/contract-models/variables` et prévisualisation PDF temps réel sans persistance (`/preview`).
  - `Contract` : génération PDF native QuestPDF (`/contracts/generate`) ou import multipart PDF (`/contracts`) avec validation magic bytes `%PDF-`, chiffrement **AES-256-GCM** (`StorageScope.EmployeeSensitive`), unicité par poste (`409`), suivi signature (`Draft`, `PendingSignature`, `Signed`, `Archived`) + concurrence optimiste `RowVersion` (`409`), streaming déchiffré `inline`/`attachment` et ABAC (`contracts:read:all` ou `Self`).
  - `JobSheet` : fiche de poste PDF unique par poste (`409`), ABAC `Self` / `DirectManager` (`jobsheets:read:team`) / `HR+`, et garde cross-module `ContractPositionReferenceChecker` bloquant la suppression d'un `Position` référencé (`409`).
- **Module `Sirh.Modules.HumanResources`** (migration `AddEmployeeDocuments`) :
  - `EmployeeDocument` : taxonomie structurée à 6 catégories (`PersonalFile`, `PersonalEvents`, `ContractualElements`, `Disciplinary`, `Departure`, `Integration`), validation magic bytes multi-formats (PDF, PNG, JPEG, WEBP), chiffrement AES-256-GCM, exclusion stricte des catégories sensibles (`Disciplinary`, `Departure`) sans `employee-docs:read:sensitive`, ABAC `Self` / `Team` / `All` et écriture *self-service* restreinte.
  - Spécialisations structurées `RqthDetail` (`ExpiryDate`, `WorkplaceAccommodation`) et `ResidentPermitDetail` (`PermitNumber`, `ExpiryDate`) validées et persistées atomiquement avec horodatages de rappels prêts pour le Sprint 3.

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **138 / 138 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **13 / 13** (isolation modulaire stricte incluant `Sirh.Modules.Contracts` vérifiée par `NetArchTest`)
  - `Sirh.UnitTests` : **87 / 87**
  - `Sirh.IntegrationTests` : **38 / 38**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - Le rendu natif **Scriban + QuestPDF** génère des contrats PDF structurés en quelques millisecondes sans aucune dépendance LibreOffice.
  - Les tests Minimal API ont rappelé deux subtilités précieuses (exposition `Content-Disposition: inline` sans passer `fileDownloadName` à `Results.Stream`, et désérialisation `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` dans les tests d'intégration).
- **Points d'attention pour le Sprint 3 (`Santé au Travail & Notifications`)** :
  - Respecter le secret médical sur `MedicalVisit` (aucune permission `:team` pour les managers, uniquement `Self` ou `HR+`).
  - Garantir l'idempotence des jobs quotidiens (visites futures dans les 5 jours uniquement, et alertes d'expiration RQTH / titres de séjour à 30j / 7j).

---

## 4. Ouverture du Sprint 3 — Santé au Travail & Notifications

- **Cap** : `F6.1` Visites Médicales (`US-025`) dans un nouveau module autonome `Sirh.Modules.Health` (schéma `health`), suivi de `F6.2` Rappels Automatisés & Centre de Notifications (`US-026`, `US-027`).
