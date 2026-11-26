# Tableau de bord Agile & Suivi des Sprints

Ce document assure le suivi vivant de l'avancement des User Stories (US), des Features et des Sprints du projet **SIRH .NET 10**.

> **Légende des statuts** : `⬜ To Do` · `🚧 In Progress` · `👀 In Review` · `✅ Done`

---

## Vue d'ensemble des Sprints

| Sprint | Objectif | Charge | Avancement | Statut |
| :--- | :--- | :---: | :---: | :--- |
| **Sprint 0** | Socle & Auth (`US-001` à `US-010`) | 39 pts | 39 / 39 pts (100 %) | ✅ Done |
| **Sprint 1** | Organisation & Collaborateurs (`US-011` à `US-018`) | 32 pts | 0 / 32 pts (0 %) | 🚧 In Progress |
| **Sprint 2** | Contrats PDF natifs & Dossier Salarié (`US-019` à `US-024`) | 31 pts | 0 / 31 pts (0 %) | ⬜ To Do |
| **Sprint 3** | Santé au Travail & Notifications (`US-025` à `US-027`) | 13 pts | 0 / 13 pts (0 %) | ⬜ To Do |
| **Sprint 4** | Vie Interne — Docs, Kits, CSE, RSE (`US-028` à `US-030`) | 13 pts | 0 / 13 pts (0 %) | ⬜ To Do |
| **Sprint 5** | QCM d'Intégration Structuré (`US-031` à `US-032`) | 10 pts | 0 / 10 pts (0 %) | ⬜ To Do |
| **Sprint 6** | Azure Cloud Readiness & Audit Final (`US-033` à `US-034`) | 8 pts | 0 / 8 pts (0 %) | ⬜ To Do |
| **Total** | **MVP complet + Conformité (34 US)** | **146 pts** | **39 / 146 pts (27 %)** | — |

---

## Sprint 0 — Socle & Auth (39 pts) — ✅ Clôturé (`v0.1.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F0.1** | `US-001` | Initialisation de la solution .NET 10 & BuildingBlocks | 5 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F0.1** | `US-002` | Pipeline HTTP standardisé (CorrelationId, ProblemDetails, OpenAPI) | 3 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F0.1** | `US-003` | Health checks, logs structurés & Docker Compose local | 3 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F1.1** | `US-004` | Connexion JWT & gestion du verrouillage | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.1** | `US-005` | Refresh token rotatif avec détection de réutilisation | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.1** | `US-006` | Déconnexion unitaire et révocation globale (`logout-all`) | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.2** | `US-007` | Changement de mot de passe authentifié | 3 | `feat/F1.2-F1.3-passwords-profile-rbac` | ✅ Done | `cbb12fe` |
| **F1.2** | `US-008` | Réinitialisation de mot de passe par OTP persisté | 5 | `feat/F1.2-F1.3-passwords-profile-rbac` | ✅ Done | `cbb12fe` |
| **F1.3** | `US-009` | Profil courant (`/api/v1/auth/me`) | 2 | `feat/F1.2-F1.3-passwords-profile-rbac` | ✅ Done | `cbb12fe` |
| **F1.3** | `US-010` | Politiques de permissions RBAC & administration des rôles | 3 | `feat/F1.2-F1.3-passwords-profile-rbac` | ✅ Done | `cbb12fe` |

---

## Sprint 1 — Organisation & Collaborateurs (32 pts) — 🚧 En cours

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F2.1** | `US-011` | Gérer le référentiel des entreprises (`Company`) | 3 | `feat/F2.1-organization-referentials` | 🚧 In Progress | — |
| **F2.1** | `US-012` | Gérer le CRUD complet des lieux / établissements (`Location`) | 3 | `feat/F2.1-organization-referentials` | 🚧 In Progress | — |
| **F2.1** | `US-013` | Gérer les types de contrat et exposer le référentiel des départements | 3 | `feat/F2.1-organization-referentials` | 🚧 In Progress | — |
| **F3.1** | `US-014` | Créer un collaborateur avec email pro auto et rôles | 5 | — | ⬜ To Do | — |
| **F3.1** | `US-015` | Rechercher, consulter et mettre à jour une fiche collaborateur | 5 | — | ⬜ To Do | — |
| **F3.1** | `US-016` | Désactiver et réactiver un collaborateur avec révocation des sessions | 3 | — | ⬜ To Do | — |
| **F3.2** | `US-017` | Gérer les contacts d'urgence chiffrés (max 2) | 3 | — | ⬜ To Do | — |
| **F3.2** | `US-018` | Gérer les affectations et l'historique des postes (`Position`) | 5 | — | ⬜ To Do | — |

---

## Backlog des Sprints suivants (`⬜ To Do`)

- **Sprint 2 — Contrats PDF natifs & Dossier Salarié (31 pts)** : `US-019` (5), `US-020` (8), `US-021` (5), `US-022` (3), `US-023` (5), `US-024` (5)
- **Sprint 3 — Santé au Travail & Notifications (13 pts)** : `US-025` (5), `US-026` (5), `US-027` (3)
- **Sprint 4 — Vie Interne (13 pts)** : `US-028` (5), `US-029` (3), `US-030` (5)
- **Sprint 5 — QCM d'Intégration Structuré (10 pts)** : `US-031` (5), `US-032` (5)
- **Sprint 6 — Azure Cloud Readiness & Audit Final (8 pts)** : `US-033` (5), `US-034` (3)

---

## Rapports de clôture (Sprint Reviews)

- **Sprint 0 (`v0.1.0`)** : [sprint-0-review.md](sprint-0-review.md) — Clôturé le 2026-11-26 (`39 / 39 pts`, `61 / 61 tests`).
