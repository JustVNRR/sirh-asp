# Vision Produit & Spécifications Fonctionnelles — Refonte SIRH (.NET 10)

> [!NOTE]
> Ce document formalise la vision cible et les spécifications fonctionnelles du **Système d'Information des Ressources Humaines (SIRH)** refondu en **.NET 10 / SQL Server**, à partir de l'audit de l'existant Java/React ([audit-existant.md](file:///C:/Users/JustVNR/.gemini/antigravity/brain/ee5d7c80-8003-4ce3-890f-47da6388ee24/audit-existant.md)).
> L'API est conçue de manière **100 % agnostique du futur frontend** (React, Angular ou autre, hébergé dans un dépôt séparé).

---

## 1. Vision produit & Périmètre

### 1.1 Objectifs de la refonte
1. **Assainir le modèle métier et les contrats REST** : remplacer les endpoints hérités incohérents, les relations fragiles par `slug` sans clés étrangères, et les flux binaires encapsulés en base64 dans du JSON.
2. **Durcir la sécurité de bout en bout** : remplacer le contrôle manuel dispersé par une sécurité déclarative combinant **RBAC (Permissions métier)** et **ABAC (contrôle d'accès par ressource anti-IDOR)**, authentification robuste (JWT courte durée + Refresh Token rotatif persisté), chiffrement effectif des données sensibles et des documents employés.
3. **Moderniser l'écosystème documentaire** : abandonner LibreOffice et les templates `.docx` au profit de **modèles Markdown / Scriban** compilés nativement en PDF côté .NET (**QuestPDF**), tout en conservant la possibilité d'importer des PDF déjà signés.
4. **Combler les manques et corriger les dettes** : exposer un véritable centre de **Notifications** (visites médicales + alertes d'expiration RQTH / titres de séjour), doter les **Lieux** d'un CRUD complet, remplacer le fichier plat de **QCM d'intégration** par un module structuré (questions, réponses, tentatives, scores), et préparer le stockage abstrait pour une migration vers **Azure Blob Storage**.

### 1.2 Périmètre fonctionnel

| Statut | Éléments concernés |
|---|---|
| **Repris & assaini** | Authentification locale, Collaborateurs, Contacts d'urgence (désormais chiffrés), Historique des postes, Contrats & Fiches de poste, Dossier salarié & Vie professionnelle (incl. RQTH & Titre de séjour), Visites médicales, Entreprises, Types de contrat, Documents communs, Kits d'accueil, Représentants du personnel (CSE), Boîte à idées RSE. |
| **Corrigé & durci** | Contrôle d'accès ressource par ressource (anti-IDOR), protection anti-escalade de privilèges (`Admin` / `SuperAdmin`), séparation clé JWT / clé chiffrement, pagination serveur systématique, validation MIME par *magic bytes*, suppression des réponses `text/plain` sur les écritures, job de rappel visites médicales borné au futur (`[J, J+5]`). |
| **Ajouté** | **Centre de Notifications** complet (CRUD lecture/compteur/suppression + alertes d'expiration RQTH & Titres de séjour), **CRUD complet des Lieux**, **QCM d'intégration structuré** (banque de questions/réponses + passage + scoring + suivi RH), **Modèles de contrat Markdown/Scriban → PDF natif**, abstraction stockage **Local → Azure Blob Storage**. |
| **Retiré du backend** | Modélisation des routes UI et icônes CoreUI en base (`AccessLevel.frontRoute` / `icon` remplacés par des permissions métier), estimateur URSSAF (widget purement front), lien en dur eDocPerso (configuration purement front ou paramètre UI). |

---

## 2. Personas, Rôles & Matrice de Sécurité (RBAC + ABAC)

### 2.1 Rôles système
Les rôles sont hiérarchisés par priorité indicative (utilisée uniquement pour l'affichage et les règles anti-escalade ; **les autorisations reposent sur des permissions explicites** et non sur des comparaisons de chaînes) :

| Rôle | Code | Description |
|---|---|---|
| **Inconnu / Invité** | `UNKNOWN` | Rôle minimal technique (aucune permission métier sensible, attribué uniquement en repli si aucun rôle actif). |
| **Apprenti** | `APPRENTICE` | Alternant / stagiaire ayant accès à son espace personnel, son kit d'accueil et son parcours d'intégration. |
| **Collaborateur** | `EMPLOYEE` | Salarié standard : consultation de son dossier, ses postes, ses contrats/fiches de poste, ses visites médicales, vie interne et QCM. |
| **Manager** | `MANAGER` | Collaborateur encadrant une équipe : droits du Collaborateur + lecture ciblée sur ses subordonnés directs (postes où `ManagerId == currentEmployeeId`). |
| **Membre CSE** | `CSE` | Représentant du personnel : droits du Collaborateur + gestion des documents CSE (PV, BDE, harcèlement) et annuaire CSE (selon permissions). |
| **Ressources Humaines** | `HR` | Gestion opérationnelle complète des collaborateurs (hors comptes `Admin`/`SuperAdmin`), postes, contrats, documents, visites médicales, RSE, QCM. |
| **Administrateur** | `ADMIN` | Droits RH + gestion des entreprises, lieux, comptes privilégiés `Admin` et matrice des permissions. |
| **Super Administrateur** | `SUPER_ADMIN` | Contrôle total plateforme, y compris gestion des `SuperAdmin` et paramètres système critiques. |

### 2.2 Permissions métier (remplacement de `AccessLevel`)
Le futur frontend (React ou Angular) pilote sa navigation à partir du jeu de permissions retourné par `GET /api/v1/auth/me` (plus aucune route ni icône CoreUI n'est stockée côté API) :

- **Identité & Accès** : `roles:read`, `roles:manage-permissions`
- **Organisation** : `companies:read`, `companies:write`, `locations:read`, `locations:write`, `contract-types:read`, `contract-types:write`
- **Collaborateurs & Postes** : `employees:read:self`, `employees:read:team`, `employees:read:all`, `employees:write`, `employees:manage-privileged`, `positions:read:self`, `positions:read:team`, `positions:read:all`, `positions:write`, `emergency-contacts:read:self`, `emergency-contacts:read:all`, `emergency-contacts:write:self`, `emergency-contacts:write:all`
- **Contrats & Fiches de poste** : `contract-models:read`, `contract-models:write`, `contracts:read:self`, `contracts:read:all`, `contracts:write`, `jobsheets:read:self`, `jobsheets:read:team`, `jobsheets:read:all`, `jobsheets:write`
- **Documents salarié** : `employee-docs:read:self`, `employee-docs:read:team`, `employee-docs:read:all`, `employee-docs:read:sensitive`, `employee-docs:write:self`, `employee-docs:write:all`
- **Santé & Notifications** : `medical-visits:read:self`, `medical-visits:read:all`, `medical-visits:write`, `notifications:read:self`, `notifications:write:self`
- **Vie interne & Intégration** : `common-docs:read`, `common-docs:write`, `common-docs:write:cse`, `welcome-kits:read`, `welcome-kits:write`, `staff-reps:read`, `staff-reps:write`, `rse:read:self`, `rse:read:all`, `rse:write`, `rse:treat`, `quizzes:read`, `quizzes:write`, `quizzes:take`, `quizzes:results:all`

### 2.3 Matrice d'accès par portée (RBAC + ABAC anti-IDOR)

| Ressource | Soi-même (`self`) | Équipe managée (`team`) | Entreprise / Global (`all`) | Règle ABAC anti-IDOR |
|---|---|---|---|---|
| **Fiche Collaborateur** | Lecture (champs non sensibles + perso) | Lecture (hors NIR `nationalId` et coordonnées perso privées) | `HR`/`Admin` : Lecture & Écriture | Un manager ne lit que ses subordonnés directs actifs. Un `HR` ne peut ni créer/modifier les rôles `Admin`/`SuperAdmin`, ni activer/désactiver un `Admin`/`SuperAdmin`. |
| **Contacts d'urgence** | Lecture & Écriture (max 2) | Aucun accès | `HR`/`Admin` : Lecture & Écriture | Seul le salarié concerné ou un `HR`/`Admin` accède à ses contacts. |
| **Postes (`Position`)** | Lecture | Lecture subordonnés | `HR`/`Admin` : Lecture & Écriture | Vérification sur `EmployeeId` ou relation `ManagerId == currentEmployeeId`. |
| **Contrats (`Contract`)** | Lecture de ses contrats | Aucun accès par défaut | `HR`/`Admin` : Lecture & Écriture | Vérification stricte `contract.EmployeeId == currentEmployeeId` ou rôle `HR`+. |
| **Fiches de poste (`JobSheet`)** | Lecture de ses postes | Lecture subordonnés | `HR`/`Admin` : Lecture & Écriture | Un collaborateur accède enfin à sa propre fiche de poste (bug corrigé par rapport au legacy). |
| **Documents salarié** | Lecture (hors catégories `Disciplinary` / `Departure` si masquées) ; dépôt limité aux pièces autorisées (ex. QCM, justificatifs perso) | Lecture limitée (hors `Disciplinary`, RQTH, Titre de séjour, RIB) | `HR`/`Admin` : Lecture & Écriture toutes catégories | Les catégories `DISCIPLINARY` et `DEPARTURE` exigent `employee-docs:read:sensitive` (`HR`/`Admin`). |
| **Visites médicales** | Lecture (convocation + rapport) | Aucun accès | `HR`/`Admin` : Lecture & Écriture | Le salarié ne peut consulter et télécharger que **ses** propres visites. |
| **Notifications** | Lecture, marquage lu, suppression des siennes | Aucun accès | Système / Job uniquement en création | Filtrage impératif `notification.EmployeeId == currentEmployeeId`. |
| **Idées RSE** | Création, lecture, MAJ/suppression de ses idées non encore traitées | Aucun accès | `HR`/`Admin` : Lecture globale + changement statut (`Treat`) | Un salarié ne peut ni modifier une idée d'un tiers ni modifier le statut de traitement. |
| **QCM d'intégration** | Lecture des QCM publiés, passage, lecture de ses résultats | Aucun accès | `HR`/`Admin` : CRUD QCM/questions + suivi global des scores | Les réponses attendues (`IsCorrect`) ne sont **jamais** exposées dans le DTO de passage du QCM ! |

---

## 3. Spécifications fonctionnelles par domaine

### Domaine 1 — Identité, Authentification & Permissions

#### 1.1 Authentification & Cycle de vie de session
- **Connexion (`POST /auth/login`)** : authentification par email professionnel + mot de passe.
  - Refus si compte inexistant, mot de passe invalide, ou salarié désactivé (`IsActive = false`) avec message générique (`401 Unauthorized`) pour éviter l'énumération.
  - Verrouillage temporaire (*Lockout*) après **5 échecs consécutifs** pendant **15 minutes** (via ASP.NET Core Identity).
  - Émission d'un **JWT d'accès court (15 minutes)** et d'un **Refresh Token opaque (7 jours)** stocké **hashé (SHA-256)** en SQL Server (avec métadonnées : `ExpiresAt`, `CreatedByIp`, `UserAgent`, `FamilyId`).
- **Rafraîchissement (`POST /auth/refresh`)** : rotation stricte du Refresh Token (l'ancien est révoqué et lié au nouveau `ReplacedByTokenHash`).
  - **Détection de réutilisation** : si un Refresh Token déjà révoqué est présenté à nouveau (vol de jeton), **toute la famille de tokens de la session est immédiatement révoquée**.
- **Déconnexion (`POST /auth/logout` et `POST /auth/logout-all`)** : révocation immédiate du Refresh Token courant ou de toutes les sessions actives du collaborateur.
- **Profil courant (`GET /auth/me`)** : retourne l'identité du collaborateur connecté, ses rôles, la liste plate de ses **permissions calculées**, son entreprise, son poste actif et le compteur de notifications non lues.

#### 1.2 Gestion des mots de passe
- **Changement de mot de passe (`POST /auth/password/change`)** : **endpoint authentifié uniquement** (correction de la faille legacy). Exige le mot de passe actuel + nouveau mot de passe respectant la politique de complexité (≥ 10 car., majuscule, minuscule, chiffre, caractère spécial). Révoque tous les Refresh Tokens existants.
- **Mot de passe oublié (`POST /auth/password/forgot`)** : génère un code OTP aléatoire cryptographique à 6 chiffres, stocké **hashé en SQL Server** (`PasswordResetTokens`) avec :
  - Durée de vie : **15 minutes**.
  - Cooldown : **60 secondes** minimum entre deux envois pour un même email.
  - Réponse toujours `202 Accepted` avec message générique, que l'email existe ou non.
  - Envoi via `IEmailService` (loggé en clair uniquement en environnement `Development` quand l'envoi SMTP réel est désactivé).
- **Réinitialisation (`POST /auth/password/reset`)** : vérifie `email + code + newPassword`. Maximum **5 tentatives** par code (incrément atomique en base, invalidation immédiate au 5ᵉ échec), puis révocation de tous les Refresh Tokens actifs.

#### 1.3 Administration des Rôles & Permissions
- Liste des rôles disponibles (`GET /roles`) avec priorité.
- Lecture et mise à jour des permissions associées à chaque rôle (`GET /roles/{id}/permissions`, `PUT /roles/{id}/permissions`) réservées à `Admin` / `SuperAdmin` (un `Admin` ne peut pas modifier les permissions de `SuperAdmin`).

---

### Domaine 2 — Organisation & Référentiels

#### 2.1 Entreprises (`Company`)
- Attributs : `Id` (`Guid`), `Name` (unique), `LegalForm` (`SAS`, `SARL`, etc.), `Address`, `PostalCode`, `City`, `Country`, `Siret` (14 chiffres validés), `Register` (RCS), `Capital`, `CollectiveAgreement` (défaut `"Syntec"`), `Owner` (représentant légal), `LogoUrl`, `IsActive`, `RowVersion`.
- Règles :
  - Création/modification réservée `Admin`/`SuperAdmin`.
  - Désactivation logique possible (`IsActive = false`) ; suppression physique interdite si des collaborateurs ou documents communs y sont rattachés (`409 Conflict`).

#### 2.2 Lieux / Établissements (`Location`) — *Nouveau CRUD complet*
- Attributs : `Id` (`Guid`), `Slug` (unique, généré/normalisé), `Name`, `Address`, `PostalCode`, `City`, `Country`, `DepartmentCode`, `Siret` (optionnel), `ColorCode` (hex `#RRGGBB` validé), `IsPublished`, `CompanyId` (optionnel si site partagé groupe, ou rattaché), `RowVersion`.
- Règles :
  - Lecture filtrée (`?published=true|false`, recherche texte, paginée).
  - Création, mise à jour, publication/dépublication et suppression (`409 Conflict` si référencé par au moins un `Position` ou une idée `Rse`).

#### 2.3 Types de contrat (`ContractType`)
- Attributs : `Id` (`Guid`), `Code` (unique ex. `CDI`, `CDD`, `APPRENTISSAGE`, `CONTRAT_PRO`), `Label`, `IsSystem` (remplace l'ambigu `editable=false` du legacy : les types système seedés ne peuvent être ni renommés dans leur code ni supprimés), `IsActive`, `RowVersion`.
- Règles :
  - Suppression bloquée (`409 Conflict`) si `IsSystem = true` ou s'il existe des `Contract` ou `WelcomeKit` associés.

#### 2.4 Départements / Services
- Référentiel énuméré ou configurable (`DEPT_HR`, `DEPT_SALES`, `DEPT_EMPLOYMENT_ADVISOR`, `DEPT_ACCOUNTING`, `DEPT_PAY`, `DEPT_TRAINER`, `DEPT_OTHER`) exposé via un endpoint dédié (`GET /referentials/departments`) afin que les futurs frontends n'aient plus à dupliquer une table de correspondance codée en dur.

---

### Domaine 3 — Collaborateurs, Contacts d'urgence & Postes

#### 3.1 Collaborateurs (`Employee`)
- Attributs :
  - Identité : `Id` (`Guid`), `Slug` (unique, immuable, kebab-case), `Honorific` (`M`, `Mme`, `Autre`), `FirstName`, `LastName`, `BornAt`, `BirthPlace`, `BirthDepartmentCode`, `NationalId` (**chiffré en base**).
  - Coordonnées pro : `Email` (unique, généré automatiquement), `Landline`, `Mobile`.
  - Coordonnées perso : `PersonalEmail`, `PersonalLandline`, `PersonalMobile`, `Address` structurée (`Street`, `PostalCode`, `City`, `AdditionalInfo` — plus de parsing regex fragile à la volée, tout en acceptant une adresse libre en compatibilité si besoin).
  - Rattachement : `CompanyId` (FK obligatoire), `Departments`, `IsActive`, `CurrentlyInPosition`, `RowVersion`.
- Règles métier :
  - **Génération d'email professionnel** : `<initiale_prenom><nom_normalise>@<slug_entreprise>.fr` (partie locale ≤ 64 car., suffixe `2, 3...` en cas de collision). Recalculé si prénom, nom ou entreprise change (avec vérification d'unicité).
  - **Désactivation logique (`DELETE /employees/{id}`)** : passe `IsActive = false`, désactive le compte de connexion associé (`Lockout` / `IsActive = false`) et **révoque immédiatement tous ses Refresh Tokens**. Réactivation explicite via `POST /employees/{id}/activate` (ou `PATCH` dédié) au lieu d'un `PUT` massif.
  - **Anti-escalade** : un `HR` ne peut ni attribuer/retirer les rôles `Admin`/`SuperAdmin` ni activer/désactiver un utilisateur possédant ces rôles (`403 Forbidden`).
  - **Filtrage & Pagination** : recherche multi-critères serveur (`companyId`, `job`, `managerId`, `department`, `inPosition`, `active`, `search`, `role`, `page`, `pageSize`, `sort`).

#### 3.2 Contacts d'urgence (`EmergencyContact`)
- Attributs : `Id` (`Guid`), `EmployeeId` (FK obligatoire), `FullName` (**chiffré**), `Relationship` (**chiffré**), `MobilePhone` (**chiffré**), `HomePhone` (**chiffré**), `WorkPhone` (**chiffré**), `PriorityOrder` (1 ou 2), `RowVersion`.
- Règles :
  - **Chiffrement applicatif effectif** en AES-256-GCM (avec clé dédiée de chiffrement de données, distincte de la clé JWT).
  - Maximum **2 contacts d'urgence** par collaborateur (règle appliquée côté serveur, `422`/`400` au-delà, pas seulement masquée dans le bouton UI comme dans le legacy).

#### 3.3 Postes & Historique (`Position`)
- Attributs : `Id` (`Guid`), `Slug` (unique, immuable `poste-<employeeSlug>[-n]`), `EmployeeId` (FK obligatoire), `ManagerId` (FK optionnelle vers `Employee`), `LocationId` (FK optionnelle vers `Location`), `JobTitle`, `StartAt`, `EndAt`, `ContractLabel` (ou lien vers `ContractType`), `Coefficient`, `Rate`, `Supervisor` (maître d'apprentissage), `TimeContractType` (`Hours` | `Days`), `TimeContractAmount`, `HierarchicalPosition`, `Departments`, `RowVersion`.
- Règles :
  - Validation : `EndAt >= StartAt` si `EndAt` renseigné ; `ManagerId != EmployeeId` (un salarié ne peut pas être son propre manager) ; `Rate >= 0` et `TimeContractAmount >= 0`.
  - **Poste courant calculé** : poste dont `StartAt <= Today` et (`EndAt IS NULL` ou `EndAt >= Today`), trié par `StartAt DESC` (premier résultat).
  - Suppression interdite (`409 Conflict`) si un `Contract` ou une `JobSheet` est rattaché à ce `PositionId`.

---

### Domaine 4 — Modèles de contrat, Génération PDF & Fiches de poste

#### 4.1 Modèles de contrat (`ContractModel`) — *100 % natif .NET*
- Abandon total de LibreOffice et du format `.docx` côté serveur.
- Attributs : `Id` (`Guid`), `Title` (unique), `Description`, `ContractTypeId` (FK optionnelle pour filtrer les modèles par type de contrat), `TemplateContent` (**Markdown enrichi avec syntaxe Scriban** `{{ employee.full_name }}`, `{{ company.name }}`, `{{ position.start_at }}`, conditions `{{ if ... }}`), `CustomVariablesSchema` (liste des variables personnalisées déclarées), `IsActive`, `RowVersion`.
- **Catalogue des variables standardisées** exposé via `GET /contract-models/variables` :
  - `employee.*` (`first_name`, `last_name`, `full_name`, `honorific`, `email`, `born_at`, `birth_place`, `address`, `postal_code`, `city`, `national_id`)
  - `company.*` (`name`, `legal_form`, `address`, `siret`, `register`, `capital`, `owner`, `collective_agreement`)
  - `position.*` (`job_title`, `start_at`, `end_at`, `hierarchical_position`, `coefficient`, `rate`, `time_contract_type`, `time_contract_amount`, `location_name`, `supervisor`)
- **Prévisualisation & Génération PDF** :
  - `POST /contract-models/{id}/preview` : reçoit `positionId` (ou valeurs surchargées) + variables personnalisées, compile le template via **Scriban**, génère le PDF via **QuestPDF** (avec en-tête société, mise en page typographique propre, pieds de page numérotés), et renvoie directement le flux `application/pdf`.

#### 4.2 Contrats (`Contract`)
- Attributs : `Id` (`Guid`), `PositionId` (FK **unique**, 1 contrat actif par poste), `EmployeeId` (FK), `ContractTypeId` (FK), `ContractModelId` (FK optionnelle si généré depuis modèle), `Status` (`Draft` | `PendingSignature` | `Signed` | `Archived`), `SignedAt` (date de signature), `FileKey` (clé stockage chiffrée), `FileName`, `ContentType` (`application/pdf`), `FileSizeBytes`, `FileDate`, `Commentary`, `RowVersion`.
- Deux modes de création :
  1. **Génération depuis un modèle** (`POST /contracts/generate`) : rend le PDF via Scriban + QuestPDF, chiffre et stocke le PDF directement côté serveur, crée l'entité `Contract`.
  2. **Import d'un PDF existant** (`POST /contracts` multipart) : valide le PDF (magic bytes `%PDF-`), chiffre et stocke le fichier.
- Téléchargement uniquement via endpoint dédié `GET /contracts/{id}/file` (flux binaire `application/pdf`, jamais en base64 dans les DTOs JSON).

#### 4.3 Fiches de poste (`JobSheet`)
- Attributs : `Id` (`Guid`), `PositionId` (FK **unique**), `EmployeeId` (FK), `FileKey`, `FileName`, `ContentType`, `FileSizeBytes`, `FileDate`, `Commentary`, `RowVersion`.
- Flux binaire dédié `GET /job-sheets/{id}/file` (accessible au titulaire du poste, son manager direct, et `RH`/`Admin`).

---

### Domaine 5 — Dossier Administratif & Vie Professionnelle (`EmployeeDocument`)

#### 5.1 Catégories & Taxonomie propre
Regroupement structuré des documents personnels du collaborateur (remplace la liste plate de 32 enums mélangés) :
- **`PersonalFile`** : `CV`, `VitalCard`, `NationalIdCard`, `SocialSecurityCertificate`, `Passport`, `ResidentPermit`, `Iban`, `RqthCertificate`, `MutualAdhesionDia`, `MutualAdhesionBia`, `ExemptionForm`, `SepaMandate`, `ProvidentBulletin`, `EmergencyContactSheet`.
- **`PersonalEvents`** : `FamilyEventLeave`, `MaternityLeave`, `PaternityLeave`, `ParentalLeave`, `TrainingLeave`, `BirthLeave`, `OtherPersonalEvent`.
- **`ContractualElements`** : `ContractElements`, `Amendment`, `Annex`, `OtherContractualElement`.
- **`Disciplinary`** *(sensible, accès RH/Admin uniquement)* : `FormalNotice`, `Warning`, `Blame`, `PreliminaryInterviewInvitation`, `DisciplinarySuspension`, `Downgrading`, `Dismissal`, `OtherDisciplinary`.
- **`Departure`** *(sensible, accès RH/Admin uniquement)* : `ResignationLetter`, `DepartureInterviewNotice`, `DepartureInterviewDocument`, `MutualBreachAgreement`, `InvitationRc`, `CerfaRc`, `NotificationRc`, `DepartureNotification`, `DismissalNotification`, `OtherDeparture`.
- **`Integration`** : `IntegrationFile` (dossier de recrutement transféré). *(Le QCM d'intégration dispose désormais de son propre module structuré au Domaine 7).*

#### 5.2 Spécialisations structurées
- **Attestation RQTH (`RqthCertificateDetails`)** : `ExpiryDate` (obligatoire), `WorkplaceAccommodation` (commentaire d'aménagement de poste).
- **Titre de séjour (`ResidentPermitDetails`)** : `PermitNumber` (obligatoire, unique ou tracé), `ExpiryDate` (obligatoire).
- Tous les fichiers sont validés (PDF, PNG, JPEG — contrôle par *magic bytes*), chiffrés via `IFileStorageService` et téléchargés via `GET /employee-documents/{id}/file`.

---

### Domaine 6 — Santé au Travail (Visites Médicales) & Notifications

#### 6.1 Visites médicales (`MedicalVisit`)
- Attributs : `Id` (`Guid`), `EmployeeId` (FK obligatoire), `VisitType` (`Hiring`, `FollowUp`, `PreReturnToWork`, `ReturnToWork`, `EmployerRequested`, `EmployeeRequested`), `VisitDateTime` (UTC), `ValidityDate` (fin de validité), `MedicalCenter`, `CarriedOut` (booléen), `Comments`, `ConvocationFileKey`/`FileName`/`ContentType` (obligatoire à la création), `ReportFileKey`/`FileName`/`ContentType` (optionnel), `ReminderSentAt` (UTC, null tant que non rappelée), `RowVersion`.
- Règles :
  - Création/modification/suppression réservées à `RH`/`Admin`.
  - Consultation de la liste personnelle et téléchargement des fichiers (`convocation` et `report`) autorisés pour le collaborateur concerné (`ABAC`).

#### 6.2 Tâches planifiées & Alertes automatiques
1. **Rappel de visite médicale** (job quotidien à 04h00 Europe/Paris) :
   - Sélectionne uniquement les visites **futures** dans les 5 prochains jours (`VisitDateTime >= Now && VisitDateTime <= Now + 5 days && !CarriedOut && ReminderSentAt == null`). *(Correction du bug legacy qui rappelait le passé).*
   - Crée une `Notification` pour le salarié, met à jour `ReminderSentAt`, et envoie un email HTML via `IEmailService` si activé.
2. **Alertes d'échéance administrative (RQTH & Titres de séjour)** *(ajout)* :
   - Job quotidien vérifiant les `RqthCertificate` et `ResidentPermit` expirant dans **30 jours** et **7 jours** (`ExpiryReminder30SentAt`, `ExpiryReminder7SentAt` pour éviter les doublons).
   - Crée une `Notification` pour le salarié concerné et pour les utilisateurs ayant le rôle `HR` (alerte renouvellement).

#### 6.3 Centre de Notifications (`Notification`) — *Nouveau module exposé*
- Attributs : `Id` (`Guid`), `RecipientEmployeeId` (FK obligatoire), `Category` (`MedicalVisitReminder`, `DocumentExpiryAlert`, `RseUpdate`, `System`), `ReferenceEntity` (ex. `"MedicalVisit"`), `ReferenceId` (`Guid?`), `Subject`, `Content`, `IsRead`, `ReadAt`, `EmailSent`, `CreatedAt`.
- Fonctionnalités exposées :
  - `GET /notifications` : liste paginée des notifications de l'utilisateur connecté (filtrable par `isRead`, `category`).
  - `GET /notifications/unread-count` : compteur rapide pour le badge UI.
  - `PATCH /notifications/{id}/read` : marquer une notification comme lue.
  - `POST /notifications/read-all` : tout marquer comme lu.
  - `DELETE /notifications/{id}` : supprimer une de ses notifications.

---

### Domaine 7 — Vie Interne, CSE, RSE & QCM d'Intégration

#### 7.1 Base documentaire commune (`CommonDocument`) & Kits d'accueil (`WelcomeKit`)
- **Unification propre** : `CommonDocument` gère les documents accessibles à l'ensemble de l'entreprise (ou d'une filiale), classés par **domaine métier** (`RseReport`, `RseInvestigation`, `CsePv`, `CseBde`, `HarassmentBestPractices`, `CompanyAgreements`, `CollectiveAgreement`, `RecruitmentCv`, `RecruitmentHrInterview`, `RecruitmentTechnicalTests`, `RecruitmentTechnicalAudit`, `RecruitmentManagerInterview`, `WelcomeKitMutual*`, `WelcomeKitForesight*`, `WelcomeKitEmergencyContact`, `WelcomeKitLuncheonVoucher*`, `WelcomeKitBooklet`, `WelcomeKitMobility`).
- **Spécificité Kit d'accueil** : association optionnelle à un `ContractTypeId` (si null, applicable à tous les types de contrat ; sinon filtrable pour un `CDI`, `APPRENTISSAGE`, etc.).
- **Sécurité** :
  - Lecture ouverte à tout collaborateur authentifié (filtrage par `companyId`, `category`, `archived`, `contractTypeId`).
  - Écriture (`POST`, `PUT`, `PATCH /archive`, `DELETE`) réservée à `HR`/`Admin`, sauf catégories CSE (`CsePv`, `CseBde`, `HarassmentBestPractices`) où le rôle `CSE` peut aussi contribuer.
  - Téléchargement via `GET /common-documents/{id}/file` (stockage non chiffré car documents publics internes, via `IFileStorageService`).

#### 7.2 Représentants du personnel (`StaffRepresentative`)
- Attributs : `Id` (`Guid`), `EmployeeId` (FK optionnelle si rattaché à un compte salarié, garantissant la cohérence), `FirstName`, `LastName`, `Email`, `CompanyId` (FK obligatoire), `RepresentativeRole` (`TitularDelegate`, `AlternateDelegate`, `UnionDelegate`), `College` (`WorkersEmployees`, `TechniciansSupervisors`, `Executives`), `IsHarassmentAdvisor`, `RowVersion`.
- Écriture strictement réservée à `HR`/`Admin`/`CSE` (correction de l'absence de sécurité legacy).

#### 7.3 Boîte à idées RSE (`RseIdea`)
- Attributs : `Id` (`Guid`), `AuthorEmployeeId` (FK obligatoire), `LocationId` (FK optionnelle), `Title` / `IdeaContent`, `AttachmentFileKey`/`FileName`/`ContentType` (chiffré, optionnel), `Status` (`Submitted`, `InReview`, `Treated`, `Rejected`), `RhComment` (commentaire de suivi RH), `TreatedByEmployeeId`, `TreatedAt`, `CreatedAt`, `RowVersion`.
- Règles :
  - Tout collaborateur peut soumettre une idée et modifier/supprimer ses propres idées tant que `Status == Submitted`.
  - Seuls `HR`/`Admin` peuvent lister toutes les idées (filtrées par `locationId`, `status`, `authorId`) et changer le statut (`PATCH /rse-ideas/{id}/status`).

#### 7.4 QCM d'Intégration (`OnboardingQuiz`) — *Nouvelle fonctionnalité structurée*
Remplace l'échange artisanal de fichiers PDF/Word du legacy par un vrai module interactif piloté par API :
1. **Administration RH (`HR`/`Admin`)** :
   - Création et édition de questionnaires (`Title`, `Description`, `CompanyId` optionnel, `PassingScorePercent`, `IsPublished`, `Version`).
   - Gestion des questions (`OrderIndex`, `QuestionText`, `QuestionType` : `SingleChoice` | `MultipleChoice`, `Explanation`) et des choix (`ChoiceText`, `IsCorrect`).
   - Validation à la publication : au moins 1 question, chaque question a ≥ 2 choix et ≥ 1 bonne réponse (exactement 1 si `SingleChoice`).
2. **Passage par le collaborateur** :
   - Liste des QCM publiés applicables à son entreprise + son statut (`NotStarted`, `Completed`, meilleur score).
   - Démarrage d'une tentative (`POST /onboarding-quizzes/{id}/attempts`) : retourne les questions/choix **sans `IsCorrect`**.
   - Soumission des réponses (`POST /onboarding-quizzes/attempts/{attemptId}/submit`) : calcul automatique du score côté serveur, enregistrement du détail des réponses, retour du score, statut `Passed` et corrections commentées.
3. **Suivi RH** :
   - Consultation paginée des résultats par collaborateur ou par QCM (`GET /onboarding-quizzes/{id}/attempts`), taux de réussite.

---

## 4. Exigences non fonctionnelles

| Catégorie | Exigence cible |
|---|---|
| **Sécurité Transport & Auth** | HTTPS obligatoire en prod, JWT HMAC-SHA256 (≥ 256 bits) durée 15 min, Refresh Token opaque hashé SHA-256 durée 7 jours, lockout identité (5 essais / 15 min), Rate Limiting ASP.NET Core natif sur `/auth/*` (`login`, `forgot`, `reset`, `refresh`). |
| **Sécurité Fichiers & Données** | Clé maître chiffrement (`Encryption:MasterKey`) **strictement distincte** de `Jwt:SigningKey`. Chiffrement AES-256-GCM des fichiers employés et des champs sensibles (`EmergencyContact`, `Employee.NationalId`). Validation upload : taille max configurable (20 Mo), liste blanche d'extensions et vérification des *magic bytes* (PDF, PNG, JPEG, WebP). |
| **Auditabilité & Traçabilité** | Champs automatiques `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy` sur les entités métier via Interceptor EF Core. Logs structurés JSON (`Serilog`) avec masquage des PII/mots de passe/tokens et `CorrelationId`. |
| **Performance & Concurrence** | Pagination obligatoire sur toutes les listes (`page`, `pageSize` borné ex. max 100), requêtes sans N+1 (projections EF Core directes), `AsNoTracking()` en lecture, locking optimiste (`rowversion` SQL Server / `ConcurrencyCheck` retournant `409 Conflict`). |
| **Agnosticisme Front** | CORS configurable par environnement, format JSON `camelCase`, dates ISO 8601 UTC (`DateOnly` pour dates civiles type naissance/validité), erreurs normalisées RFC 9457 (`ProblemDetails`), OpenAPI 3.1 + Scalar en dev. |
