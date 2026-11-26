# Tableau de bord Agile & Suivi des Sprints

Ce document assure le suivi vivant de l'avancement des User Stories (US), des Features et des Sprints du projet **SIRH .NET 10**.

> **Légende des statuts** : `⬜ To Do` · `🚧 In Progress` · `👀 In Review` · `✅ Done`

---

## Vue d'ensemble des Sprints

| Sprint | Objectif | Charge | Avancement | Statut |
| :--- | :--- | :---: | :---: | :--- |
| **Sprint 0** | Fondation technique, Sécurité, Authentification & RBAC (`US-001` à `US-010`) | 39 pts | 26 / 39 pts (67 %) | 🚧 In Progress |
| **Sprint 1** | Référentiel Organisation, Employés & Profils (`US-011` à `US-015`) | 29 pts | 0 / 29 pts (0 %) | ⬜ To Do |
| **Sprint 2** | Cycle Contractuel & Génération PDF native (`US-016` à `US-019`) | 26 pts | 0 / 26 pts (0 %) | ⬜ To Do |
| **Sprint 3** | Congés, Maladies & Workflow Manager/RH (`US-020` à `US-024`) | 29 pts | 0 / 29 pts (0 %) | ⬜ To Do |
| **Sprint 4** | Documents chiffrés, Coffre-fort & Demandes documentaires (`US-025` à `US-027`) | 18 pts | 0 / 18 pts (0 %) | ⬜ To Do |
| **Sprint 5** | Parcours d'Onboarding, Rémunération & Dashboard RH (`US-028` à `US-030`) | 18 pts | 0 / 18 pts (0 %) | ⬜ To Do |
| **Sprint 6** | Notifications, Audit Trail, RGPD & Durcissement Final (`US-031` à `US-034`) | 21 pts | 0 / 21 pts (0 %) | ⬜ To Do |
| **Total** | **MVP complet + Conformité** | **180 pts** | **26 / 180 pts** | — |

---

## Sprint 0 — Fondation technique, Authentification & RBAC (39 pts)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F0.1** | `US-001` | Initialisation de la solution .NET 10 & BuildingBlocks | 5 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F0.1** | `US-002` | Pipeline HTTP standardisé (CorrelationId, ProblemDetails, OpenAPI) | 3 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F0.1** | `US-003` | Health checks, logs structurés & Docker Compose local | 3 | `feat/F0.1-foundation-buildingblocks` | ✅ Done | `668b9a6` |
| **F1.1** | `US-004` | Connexion JWT & gestion du verrouillage | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.1** | `US-005` | Refresh token rotatif avec détection de réutilisation | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.1** | `US-006` | Déconnexion unitaire et révocation globale (`logout-all`) | 5 | `feat/F1.1-jwt-refresh-auth` | ✅ Done | `91a8e4e` |
| **F1.2** | `US-007` | Changement de mot de passe authentifié | 3 | `feat/F1.2-F1.3-passwords-profile-rbac` | 🚧 In Progress | — |
| **F1.2** | `US-008` | Réinitialisation de mot de passe par OTP persisté | 5 | `feat/F1.2-F1.3-passwords-profile-rbac` | 🚧 In Progress | — |
| **F1.3** | `US-009` | Profil courant (`/api/v1/auth/me`) | 2 | `feat/F1.2-F1.3-passwords-profile-rbac` | 🚧 In Progress | — |
| **F1.3** | `US-010` | Politiques de permissions RBAC & administration des rôles | 3 | `feat/F1.2-F1.3-passwords-profile-rbac` | 🚧 In Progress | — |

---

## Backlog des Sprints suivants (`⬜ To Do`)

- **Sprint 1 (29 pts)** : `US-011` (3), `US-012` (5), `US-013` (8), `US-014` (5), `US-015` (8)
- **Sprint 2 (26 pts)** : `US-016` (5), `US-017` (8), `US-018` (8), `US-019` (5)
- **Sprint 3 (29 pts)** : `US-020` (5), `US-021` (8), `US-022` (5), `US-023` (8), `US-024` (3)
- **Sprint 4 (18 pts)** : `US-025` (8), `US-026` (5), `US-027` (5)
- **Sprint 5 (18 pts)** : `US-028` (8), `US-029` (5), `US-030` (5)
- **Sprint 6 (21 pts)** : `US-031` (5), `US-032` (8), `US-033` (5), `US-034` (3)

---

## Rapports de clôture (Sprint Reviews)

- *Sprint 0* : en attente de la livraison et validation de `F1.2 + F1.3`.
