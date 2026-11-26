# Politique Git & Workflow de Livraison — SIRH (.NET 10)

> [!NOTE]
> Ce document formalise la convention Git utilisée par l'équipe sur le projet **SIRH .NET 10** (dépôt API dédié). Le projet démarre en **Git local** et est prêt à être relié à un dépôt distant GitHub à tout moment.

---

## 1. Modèle de branches : GitHub Flow

- **`main`** : branche principale protégée, **toujours compilable et verte** (`dotnet build` + `dotnet test`). Aucun développement direct n'y est effectué après l'initialisation.
- **Branches de travail courtes** : créées depuis `main` pour **une Feature (ou un correctif)** et supprimées après intégration.

### Convention de nommage des branches
Format : `<type>/<ref>-<description-kebab-case>`

| Préfixe | Usage | Exemple |
|---|---|---|
| `feat/` | Nouvelle fonctionnalité métier | `feat/F1.1-jwt-refresh-auth` |
| `fix/` | Correction de bug | `fix/US-026-medical-visit-date-filter` |
| `chore/` | Outillage, setup, dépendances, CI | `chore/F0.1-solution-setup` |
| `docs/` | Documentation uniquement | `docs/api-contract-update` |
| `refactor/` | Refactoring sans changement fonctionnel | `refactor/storage-pipeline` |
| `test/` | Ajout ou renforcement de tests | `test/E3-anti-idor-coverage` |

---

## 2. Convention de commits : Conventional Commits (en anglais)

Tous les commits sont rédigés en **anglais**, à l'impératif présent, et référencent la ou les **User Stories** concernées lorsqu'il s'agit d'un développement du backlog.

Format :
```text
<type>(<scope>): <concise imperative summary> [(US-xxx)]
```

Types autorisés :
- `feat`: nouvelle fonctionnalité ou capacité API
- `fix`: correction d'anomalie
- `chore`: setup, config, dépendances, outillage
- `docs`: documentation
- `refactor`: restructuration interne sans changement fonctionnel
- `test`: ajout ou modification de tests
- `ci`: pipelines GitHub Actions / Docker

Exemples :
- `chore(setup): initialize .NET 10 modular monolith solution (US-001)`
- `feat(building-blocks): add ProblemDetails, EF audit interceptor and OpenAPI (US-002)`
- `feat(storage): implement AES-256-GCM local file storage and magic bytes validation (US-003)`
- `test(f0.1): add architecture, unit and integration tests`

---

## 3. Granularité des Pull Requests & Stratégie de Merge

- **1 branche / 1 PR par Feature** (regroupant 1 à 3 User Stories cohérentes, ex. `F0.1`, `F1.1`, `F1.2`).
- **Commits atomiques** sur la branche de feature (chaque commit compile et isole une préoccupation claire).
- **Template de PR** : [.github/pull_request_template.md](file:///d:/repositories/sirh/.github/pull_request_template.md).
- **Stratégie de merge vers `main`** : **Squash & Merge** (ou `git merge --squash` en local) suivi de la suppression de la branche source, afin de conserver un historique linéaire lisible sur `main` (1 commit par Feature livrée).
- **Tags SemVer** en fin de sprint / jalon :
  - `v0.1.0-sprint0`, `v0.2.0-sprint1`, ..., `v1.0.0` (jalon MVP).

---

## 4. Workflow en local (avant ajout d'un remote GitHub)

1. Démarrer une feature depuis `main` à jour :
   ```bash
   git checkout main
   git checkout -b feat/F1.1-jwt-refresh-auth
   ```
2. Travailler par commits atomiques en anglais et vérifier :
   ```bash
   dotnet build --no-restore
   dotnet test --no-build
   ```
3. Une fois la revue de la feature validée, merger en squash dans `main` et supprimer la branche :
   ```bash
   git checkout main
   git merge --squash feat/F1.1-jwt-refresh-auth
   git commit -m "feat(identity): implement JWT and rotating refresh token auth (F1.1)"
   git branch -D feat/F1.1-jwt-refresh-auth
   ```
4. Lors du passage sur GitHub :
   ```bash
   git remote add origin https://github.com/JustVNRR/<repo>.git
   git push -u origin main --tags
   ```
