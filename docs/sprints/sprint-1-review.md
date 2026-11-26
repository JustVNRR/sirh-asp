# Sprint 1 — Review & Rétrospective (`v0.2.0`)

- **Date de clôture** : 2026-11-26
- **Objectif du Sprint** : Mettre en place les référentiels d'organisation (`Company`, `Location`, `ContractType`, `Departments`) et le cœur Collaborateurs, Contacts d'urgence chiffrés et Postes (`Employee`, `EmergencyContact`, `Position`).
- **Statut** : ✅ **Accepté et clôturé** (`32 / 32 pts` — 8 / 8 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F2.1 — Module `Organization` & Référentiels** | `US-011`, `US-012`, `US-013` | 9 | `ebb7251` |
| **F3.1 — Cycle de vie des Collaborateurs** | `US-014`, `US-015`, `US-016` | 15 | `6a8509c` |
| **F3.2 — Contacts d'urgence chiffrés & Postes** | `US-017`, `US-018` | 8 | `318ccc3` |

### Livrables vérifiés
- **BuildingBlocks** : `SlugGenerator`, `SiretValidator` (Luhn + règle La Poste), `ErrorType.UnprocessableEntity` (`422`), rotation automatique `RowVersion` sous EF Core `InMemory`, et contrats inter-modules (`IOrganizationLookup`, `IIdentityUserProvisioningService`, `IReferenceCheckers`).
- **Module `Sirh.Modules.Organization`** (schéma `organization`, migration `InitialOrganization`) : CRUD `Company` (défaut `"Syntec"`, `RowVersion`), `Location` (slug unique auto, `#RRGGBB`, filtres, blocage `409` si référencé), `ContractType` (4 types système protégés `403`, blocage `409` si utilisés) et `/api/v1/referentials/departments`.
- **Module `Sirh.Modules.HumanResources`** (schéma `hr`, migrations `InitialHumanResources` et `AddEmergencyContactsAndPositions`) :
  - `Employee` : génération d'e-mail pro unique avec gestion de collision, chiffrement AES-256-GCM du `NationalId`, création automatique du compte utilisateur lié, contrôle ABAC (`Self` / `DirectManager` avec masquage des PII sensibles / `HR+`), désactivation logique avec révocation des Refresh Tokens et anti-escalade (`Admin`/`SuperAdmin`).
  - `EmergencyContact` : chiffrement AES-256-GCM des champs personnels, plafond strict de 2 contacts (`422`), ABAC `Self`/`HR+`.
  - `Position` : slug immuable `poste-{employeeSlug}[-n]`, calcul dynamique `IsCurrent` et synchronisation `Employee.CurrentlyInPosition` / `DirectManagerId`, vérification croisée réelle `PositionLocationReferenceChecker` (`409` à la suppression d'un lieu utilisé).

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **118 / 118 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **10 / 10** (isolation totale `Identity` / `Organization` / `HumanResources` vérifiée par `NetArchTest`)
  - `Sirh.UnitTests` : **76 / 76**
  - `Sirh.IntegrationTests` : **32 / 32**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - L'architecture par contrats légers dans `Sirh.BuildingBlocks.Application.Contracts` permet de faire coopérer les modules (création d'utilisateur depuis `HR`, protection `409` cross-module sur `Location`) avec zéro couplage direct.
  - Les tests de bout en bout ont permis de détecter et corriger immédiatement le binding Minimal API sur la pagination (`int page = 1, int pageSize = 20`) dès `F2.1`.
- **Points d'attention pour le Sprint 2 (`MVP Core`)** :
  - Activer explicitement `QuestPDF.Settings.License = LicenseType.Community` au démarrage du moteur de rendu PDF.
  - Encapsuler le rendu Scriban avec des limites d'exécution (`LoopLimit`, `RecursiveLimit`) et validation syntaxique à la création/édition des modèles.

---

## 4. Ouverture du Sprint 2 — Contrats PDF natifs & Dossier Salarié (MVP Core)

- **Cap** : `F4.1` Modèles Markdown/Scriban & Génération PDF QuestPDF (`US-019`, `US-020`), `F4.2` Contrats & Fiches de poste (`US-021`, `US-022`), puis `F5.1` Dossier Salarié, RQTH & Titres de séjour (`US-023`, `US-024`).
