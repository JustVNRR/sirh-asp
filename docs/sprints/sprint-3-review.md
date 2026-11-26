# Sprint 3 — Review & Rétrospective (`v0.4.0`)

- **Date de clôture** : 2026-11-26
- **Objectif du Sprint** : Mettre en place le module autonome de Santé au Travail (`MedicalVisit`), les rappels quotidiens automatisés (visites médicales + échéances RQTH / titres de séjour) et le centre de notifications temps réel — **Jalon MVP Complet**.
- **Statut** : ✅ **Accepté et clôturé** (`13 / 13 pts` — 3 / 3 User Stories livrées).

---

## 1. Périmètre livré

| Feature | User Stories | Points | Commit Squash sur `main` |
| :--- | :--- | :---: | :--- |
| **F6.1 — Module `Health` & Visites Médicales** | `US-025` | 5 | `c634302` |
| **F6.2 — Rappels Automatisés & Centre de Notifications** | `US-026`, `US-027` | 8 | `87b23a2` |

### Livrables vérifiés
- **Module `Sirh.Modules.Health`** (schéma `health`, migrations `InitialHealth` et `AddNotifications`) :
  - `MedicalVisit` : convocation obligatoire et rapport optionnel validés par *magic bytes* (PDF/PNG/JPEG/WEBP) et chiffrés **AES-256-GCM** (`StorageScope.EmployeeSensitive`), normalisation UTC et ABAC strict respectant le secret médical (`Self` ou `RH+`, managers exclus à `403`).
  - `Notification` : endpoints `/api/v1/notifications` (`GET /`, `/unread-count`, `PATCH /{id}/read`, `POST /read-all`, `DELETE /{id}`) avec contrôle anti-IDOR (`403 Forbidden`) et branchement temps réel sur `GET /api/v1/auth/me` via `INotificationLookup`.
  - `DailyHealthRemindersJob` & `DailyHealthRemindersBackgroundService` (04:00 `Europe/Paris`) :
    - Rappels J-5 ciblant exclusivement les visites **futures** non réalisées et non rappelées.
    - Alertes d'expiration **J-30 / J-7** des RQTH et Titres de séjour diffusées simultanément au salarié et aux utilisateurs `HR` (notifications + e-mails HTML/texte, 100 % idempotent).

---

## 2. Métriques Qualité (mesurées avant clôture)

- **Compilation** (`dotnet build Sirh.slnx --no-restore`) : **0 avertissement, 0 erreur**.
- **Tests automatisés** (`dotnet test Sirh.slnx --no-build`) : **144 / 144 réussis (100 %)**
  - `Sirh.ArchitectureTests` : **16 / 16** (isolation `Identity` / `Organization` / `HumanResources` / `Contracts` / `Health` vérifiée par `NetArchTest`)
  - `Sirh.UnitTests` : **87 / 87**
  - `Sirh.IntegrationTests` : **41 / 41**

---

## 3. Décisions techniques & Rétrospective

- **Ce qui a bien fonctionné** :
  - L'ajout d'une interface légère `INotificationLookup` dans `BuildingBlocks` a permis d'alimenter `/api/v1/auth/me` sans aucun couplage entre `Identity` et `Health`.
  - La séparation entre la logique pure de job (`DailyHealthRemindersJob`) et l'orchestrateur timer (`DailyHealthRemindersBackgroundService`) rend les tests d'idempotence instantanés et déterministes.
- **Points d'attention pour le Sprint 4 (`Vie Interne`)** :
  - Utiliser `StorageScope.CommonPublic` (sans chiffrement) pour les documents communs et kits d'accueil (`CommonDocument`).
  - Permettre aux élus `CSE` de gérer uniquement les catégories CSE (`CsePv`, `CseBde`, `HarassmentBestPractices`) tout en bloquant (`403`) les accords d'entreprise ou kits d'accueil.

---

## 4. Ouverture du Sprint 4 — Vie Interne (Docs, Kits, CSE, RSE)

- **Cap** : création du module autonome `Sirh.Modules.InternalLife` (schéma `internal_life`) avec `F7.1` Documents Communs & Kits d'accueil (`US-028`), puis `F7.2` Annuaire CSE & Boîte à idées RSE (`US-029`, `US-030`).
