# Tableau de bord Agile & Suivi des Sprints

Ce document assure le suivi vivant de l'avancement des User Stories (US), des Features et des Sprints du projet **SIRH .NET 10**.

> **Légende des statuts** : `⬜ To Do` · `🚧 In Progress` · `👀 In Review` · `✅ Done`

---

## Vue d'ensemble des Sprints

| Sprint | Objectif | Charge | Avancement | Statut |
| :--- | :--- | :---: | :---: | :--- |
| **Sprint 0** | Socle & Auth (`US-001` à `US-010`) | 39 pts | 39 / 39 pts (100 %) | ✅ Done |
| **Sprint 1** | Organisation & Collaborateurs (`US-011` à `US-018`) | 32 pts | 32 / 32 pts (100 %) | ✅ Done |
| **Sprint 2** | Contrats PDF natifs & Dossier Salarié (`US-019` à `US-024`) | 31 pts | 31 / 31 pts (100 %) | ✅ Done |
| **Sprint 3** | Santé au Travail & Notifications (`US-025` à `US-027`) | 13 pts | 13 / 13 pts (100 %) | ✅ Done |
| **Sprint 4** | Vie Interne — Docs, Kits, CSE, RSE (`US-028` à `US-030`) | 13 pts | 13 / 13 pts (100 %) | ✅ Done |
| **Sprint 5** | QCM d'Intégration Structuré (`US-031` à `US-032`) | 10 pts | 10 / 10 pts (100 %) | ✅ Done |
| **Sprint 6** | Azure Cloud Readiness & Audit Final (`US-033` à `US-034`) | 8 pts | 0 / 8 pts (0 %) | 🚧 In Progress |
| **Total** | **MVP complet + Conformité (34 US)** | **146 pts** | **138 / 146 pts (95 %)** | — |

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

## Sprint 1 — Organisation & Collaborateurs (32 pts) — ✅ Clôturé (`v0.2.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F2.1** | `US-011` | Gérer le référentiel des entreprises (`Company`) | 3 | `feat/F2.1-organization-referentials` | ✅ Done | `ebb7251` |
| **F2.1** | `US-012` | Gérer le CRUD complet des lieux / établissements (`Location`) | 3 | `feat/F2.1-organization-referentials` | ✅ Done | `ebb7251` |
| **F2.1** | `US-013` | Gérer les types de contrat et exposer le référentiel des départements | 3 | `feat/F2.1-organization-referentials` | ✅ Done | `ebb7251` |
| **F3.1** | `US-014` | Créer un collaborateur avec email pro auto et rôles | 5 | `feat/F3.1-employees-lifecycle` | ✅ Done | `6a8509c` |
| **F3.1** | `US-015` | Rechercher, consulter et mettre à jour une fiche collaborateur | 5 | `feat/F3.1-employees-lifecycle` | ✅ Done | `6a8509c` |
| **F3.1** | `US-016` | Désactiver et réactiver un collaborateur avec révocation des sessions | 3 | `feat/F3.1-employees-lifecycle` | ✅ Done | `6a8509c` |
| **F3.2** | `US-017` | Gérer les contacts d'urgence chiffrés (max 2) | 3 | `feat/F3.2-emergency-contacts-positions` | ✅ Done | `318ccc3` |
| **F3.2** | `US-018` | Gérer les affectations et l'historique des postes (`Position`) | 5 | `feat/F3.2-emergency-contacts-positions` | ✅ Done | `318ccc3` |

---

## Sprint 2 — Contrats PDF natifs & Dossier Salarié (31 pts — MVP Core) — ✅ Clôturé (`v0.3.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F4.1** | `US-019` | Gérer les modèles Markdown/Scriban et catalogue des variables | 5 | `main` | ✅ Done | `8fa6173` |
| **F4.1** | `US-020` | Prévisualiser et générer un contrat PDF natif (QuestPDF) | 8 | `main` | ✅ Done | `8fa6173` |
| **F4.2** | `US-021` | Importer, suivre la signature, consulter et supprimer un contrat | 5 | `main` | ✅ Done | `de192df` |
| **F4.2** | `US-022` | Gérer la fiche de poste PDF unique d'un poste (`JobSheet`) | 3 | `main` | ✅ Done | `de192df` |
| **F5.1** | `US-023` | Déposer, filtrer et télécharger les documents du dossier salarié | 5 | `main` | ✅ Done | `38b83a0` |
| **F5.1** | `US-024` | Gérer les attestations RQTH et Titres de séjour | 5 | `main` | ✅ Done | `38b83a0` |

---

## Sprint 3 — Santé au Travail & Notifications (13 pts) — ✅ Clôturé (`v0.4.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F6.1** | `US-025` | Planifier, mettre à jour et consulter les visites médicales | 5 | `main` | ✅ Done | `c634302` |
| **F6.2** | `US-026` | Exécuter les jobs quotidiens de rappel (visites + RQTH/titres) | 5 | `main` | ✅ Done | `87b23a2` |
| **F6.2** | `US-027` | Consulter, marquer comme lues et supprimer ses notifications | 3 | `main` | ✅ Done | `87b23a2` |

---

## Sprint 4 — Vie Interne — Docs, Kits, CSE, RSE (13 pts) — ✅ Clôturé (`v0.5.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F7.1** | `US-028` | Publier, filtrer, archiver et télécharger les documents communs et kits d'accueil | 5 | `main` | ✅ Done | `e842d86` |
| **F7.2** | `US-029` | Gérer l'annuaire des représentants du personnel (CSE) | 3 | `main` | ✅ Done | `ecd6f25` |
| **F7.2** | `US-030` | Soumettre des idées RSE et piloter leur traitement côté RH | 5 | `main` | ✅ Done | `ecd6f25` |

---

## Sprint 5 — QCM d'Intégration Structuré (10 pts) — ✅ Clôturé (`v0.6.0`)

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F8.1** | `US-031` | Créer, éditer et publier un QCM d'intégration côté RH | 5 | `main` | ✅ Done | `f7f1018` |
| **F8.1** | `US-032` | Passer un QCM d'intégration, calculer le score et suivre les résultats | 5 | `main` | ✅ Done | `f7f1018` |

---

## Sprint 6 — Azure Cloud Readiness & Audit Final (8 pts) — 🚧 En cours

| Feature | US | Titre | Points | Branche | Statut | Commit / PR |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **F9.1** | `US-033` | Implémenter le provider `AzureBlobFileStorageService` et le tester avec Azurite | 5 | `feat/F9.1-azure-blob-security-audit` | 👀 In Review | — |
| **F9.1** | `US-034` | Valider la couverture d'autorisation globale (tests anti-IDOR exhaustifs & headers sécurité) | 3 | `feat/F9.1-azure-blob-security-audit` | 👀 In Review | — |

---

## Rapports de clôture (Sprint Reviews)

- **Sprint 0 (`v0.1.0`)** : [sprint-0-review.md](sprint-0-review.md) — Clôturé le 2026-11-26 (`39 / 39 pts`, `61 / 61 tests`).
- **Sprint 1 (`v0.2.0`)** : [sprint-1-review.md](sprint-1-review.md) — Clôturé le 2026-11-26 (`32 / 32 pts`, `118 / 118 tests`).
- **Sprint 2 (`v0.3.0`)** : [sprint-2-review.md](sprint-2-review.md) — Clôturé le 2026-11-26 (`31 / 31 pts`, `138 / 138 tests`).
- **Sprint 3 (`v0.4.0`)** : [sprint-3-review.md](sprint-3-review.md) — Clôturé le 2026-11-26 (`13 / 13 pts`, `144 / 144 tests`).
- **Sprint 4 (`v0.5.0`)** : [sprint-4-review.md](sprint-4-review.md) — Clôturé le 2026-11-26 (`13 / 13 pts`, `150 / 150 tests`).
- **Sprint 5 (`v0.6.0`)** : [sprint-5-review.md](sprint-5-review.md) — Clôturé le 2026-11-27 (`10 / 10 pts`, `154 / 154 tests`).
