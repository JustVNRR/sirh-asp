# Backlog Agile & Planification des Sprints — SIRH (.NET 10)

> [!NOTE]
> Ce backlog couvre l'intégralité du périmètre validé dans [01-vision-et-specifications-fonctionnelles.md](file:///d:/repositories/sirh/docs/01-vision-et-specifications-fonctionnelles.md) et [02-architecture-et-contrat-api.md](file:///d:/repositories/sirh/docs/02-architecture-et-contrat-api.md).
> **Échelle d'estimation** : Story Points Fibonacci (`1, 2, 3, 5, 8`).
> **Priorisation MoSCoW** : `Must` (indispensable MVP / cœur), `Should` (important), `Could` (confort / incrément avancé).

---

## 1. Rituels & Critères de Qualité Agile

### Definition of Ready (DoR)
Une User Story est prête à entrer en sprint lorsque :
- Le besoin métier et les règles de sécurité (RBAC + ABAC) sont explicités.
- Les critères d'acceptation Gherkin sont définis (nominal + cas d'erreur/sécurité).
- Le contrat d'API cible (routes, payloads, statuts HTTP) est identifié.
- Les dépendances techniques avec d'autres US sont levées.

### Definition of Done (DoD)
Une User Story est terminée lorsque :
- Le code respecte l'isolation du Monolithe Modulaire (tests d'architecture verts).
- Les validations (`FluentValidation`) et règles d'autorisation (`RBAC` + `ABAC`) sont en place.
- Les migrations EF Core SQL Server sont générées et testées sur base vierge.
- Les tests unitaires et d'intégration (`WebApplicationFactory` + SQL Server) couvrant tous les scénarios Gherkin passent sans avertissement.
- La documentation OpenAPI (Scalar) reflète exactement les endpoints et schémas livrés.

---

## 2. Catalogue Epics → Features → User Stories

### Epic E0 — Socle Technique, Monolithe Modulaire & DevOps Local

#### Feature F0.1 — Initialisation Solution & Infrastructure Locale
- **US-001 — Initialiser la solution Monolithe Modulaire .NET 10 et l'outillage local** (`Must`, `3 pts`)
  *En tant que* développeur, *je veux* disposer d'une solution .NET 10 structurée (`Sirh.Api`, `BuildingBlocks`, modules, projets de tests) et d'un `docker-compose.yml` (SQL Server + Mailpit), *afin de* développer et exécuter l'API localement de manière reproductible.
  ```gherkin
  Étant donné le dépôt Git initialisé avec .gitignore excluant legacy-backend et legacy-frontend
  Quand j'exécute "dotnet build" et "dotnet test"
  Alors toute la solution compile en .NET 10 sans erreur
  Et les tests d'architecture vérifient qu'aucun module ne référence les couches internes d'un autre module
  Et "GET /health" répond 200 OK avec le statut sain de SQL Server
  ```

- **US-002 — Mettre en place les briques transverses (ProblemDetails, Audit EF Core, Pagination, Serilog, OpenAPI)** (`Must`, `5 pts`)
  *En tant que* consommateur d'API, *je veux* recevoir des erreurs RFC 9457 normalisées, des réponses paginées uniformes, des logs corrélés et une documentation OpenAPI/Scalar, *afin d'*intégrer facilement n'importe quel frontend.
  ```gherkin
  Étant donné une requête API invalide ou déclenchant une erreur métier ou de concurrence
  Quand l'exception ou l'échec de validation est intercepté par le pipeline global
  Alors la réponse utilise le format "application/problem+json" avec status, title, detail et traceId
  Et toute entité auditable enregistrée renseigne automatiquement CreatedAtUtc, CreatedBy, UpdatedAtUtc et UpdatedBy
  ```

- **US-003 — Implémenter le service de stockage local chiffré AES-256-GCM et validation MIME** (`Must`, `5 pts`)
  *En tant que* responsable sécurité, *je veux* que les fichiers sensibles soient validés par magic bytes et chiffrés en AES-256-GCM avec une clé indépendante du JWT, *afin de* protéger les pièces RH sur disque.
  ```gherkin
  Étant donné un fichier uploadé via IFileStorageService en mode chiffré
  Quand le fichier est validé (taille <= 20 Mo et magic bytes autorisés) et sauvegardé
  Alors le fichier stocké sur disque est chiffré en AES-256-GCM avec un IV aléatoire unique
  Et la lecture via IFileStorageService restitue exactement les octets d'origine
  Et un fichier falsifié (ex. exécutable renommé en .pdf) est rejeté avec 400 Bad Request
  ```

---

### Epic E1 — Identité, Authentification & Permissions

#### Feature F1.1 — Authentification JWT & Sessions Persistées
- **US-004 — Authentifier un collaborateur actif avec JWT court et Refresh Token rotatif** (`Must`, `5 pts`)
  *En tant que* collaborateur actif, *je veux* me connecter avec mon email professionnel et mon mot de passe, *afin d'*obtenir un JWT d'accès (15 min) et un Refresh Token sécurisé.
  ```gherkin
  Étant donné un collaborateur actif avec des identifiants valides
  Quand il appelle POST /api/v1/auth/login
  Alors il reçoit 200 OK avec un accessToken JWT valide 15 min et un refreshToken opaque
  Et le refreshToken est enregistré hashé en SHA-256 dans la table identity.refresh_tokens
  Et si le compte est désactivé ou le mot de passe erroné, il reçoit 401 Unauthorized avec un message générique
  ```

- **US-005 — Renouveler, détecter le vol de Refresh Token et déconnecter les sessions** (`Must`, `5 pts`)
  *En tant que* système de sécurité, *je veux* faire tourner le Refresh Token à chaque usage et révoquer la famille de jetons en cas de réutilisation, *afin d'*empêcher le détournement de session.
  ```gherkin
  Étant donné un refreshToken valide
  Quand le client appelle POST /api/v1/auth/refresh
  Alors l'ancien refreshToken est marqué révoqué et un nouveau couple Access/Refresh est retourné
  Mais si cet ancien refreshToken déjà révoqué est présenté de nouveau
  Alors toute la famille de tokens de cette session est révoquée et l'appel échoue en 401 Unauthorized
  Et POST /api/v1/auth/logout révoque le token courant avec 204 No Content
  ```

- **US-006 — Protéger les endpoints d'authentification (Lockout & Rate Limiting)** (`Must`, `3 pts`)
  *En tant qu'*administrateur sécurité, *je veux* limiter les tentatives de connexion et d'OTP, *afin de* bloquer les attaques par force brute.
  ```gherkin
  Étant donné un compte collaborateur existant
  Quand 5 tentatives de connexion échouent consécutivement
  Alors le compte est verrouillé pendant 15 minutes (423 Locked ou 401 générique configuré)
  Et un dépassement du quota de requêtes sur /api/v1/auth/* retourne 429 Too Many Requests
  ```

#### Feature F1.2 — Gestion des Mots de Passe
- **US-007 — Changer son mot de passe en étant authentifié** (`Must`, `2 pts`)
  *En tant que* collaborateur connecté, *je veux* modifier mon mot de passe en fournissant mon mot de passe actuel, *afin de* sécuriser mon compte.
  ```gherkin
  Étant donné un utilisateur authentifié appelant POST /api/v1/auth/password/change
  Quand son mot de passe actuel est exact et le nouveau respecte la politique de complexité
  Alors le mot de passe est mis à jour, toutes ses sessions Refresh Token sont révoquées et l'API renvoie 204 No Content
  Et un appel anonyme sans JWT valide est rejeté avec 401 Unauthorized
  ```

- **US-008 — Réinitialiser un mot de passe oublié via OTP 6 chiffres persisté en base** (`Must`, `5 pts`)
  *En tant que* collaborateur ayant oublié son mot de passe, *je veux* recevoir un code à 6 chiffres valable 15 minutes et réinitialiser mon mot de passe, *afin de* retrouver l'accès même si l'API redémarre.
  ```gherkin
  Étant donné un collaborateur actif
  Quand il appelle POST /api/v1/auth/password/forgot avec son email
  Alors un OTP 6 chiffres hashé est stocké en base (TTL 15 min, max 5 essais, cooldown 60s) et envoyé par email
  Et la réponse reste 202 Accepted générique même si l'email n'existe pas
  Quand il soumet email + code valide + nouveau mot de passe sur POST /api/v1/auth/password/reset
  Alors son mot de passe est changé, l'OTP est consommé et ses Refresh Tokens sont révoqués
  Et après 5 codes erronés, l'OTP est définitivement invalidé
  ```

#### Feature F1.3 — Profil Connecté & Matrice Rôles / Permissions
- **US-009 — Consulter son profil courant et ses permissions calculées (`GET /auth/me`)** (`Must`, `3 pts`)
  *En tant que* collaborateur connecté, *je veux* récupérer mon identité, mes rôles, mes permissions métier et mon poste actif, *afin que* l'application cliente adapte dynamiquement son interface.
  ```gherkin
  Étant donné un collaborateur authentifié ayant les rôles "Employe" et "CSE"
  Quand il appelle GET /api/v1/auth/me
  Alors l'API retourne son employeeId, slug, nom, entreprise, poste courant, rôles, l'union dédupliquée des permissions et unreadNotificationsCount
  ```

- **US-010 — Gérer les permissions associées aux rôles** (`Should`, `3 pts`)
  *En tant qu'*Admin ou SuperAdmin, *je veux* consulter et modifier les permissions attribuées à chaque rôle, *afin de* gouverner finement les accès sans dépendre des routes UI.
  ```gherkin
  Étant donné un utilisateur ayant la permission "roles:manage-permissions" (Admin)
  Quand il appelle PUT /api/v1/roles/{id}/permissions avec une liste de codes de permissions valides
  Alors les permissions du rôle sont mises à jour en base
  Et s'il tente de modifier le rôle SuperAdmin sans être SuperAdmin, il reçoit 403 Forbidden
  ```

---

### Epic E2 — Organisation & Référentiels

#### Feature F2.1 — Entreprises, Lieux & Types de contrat
- **US-011 — Gérer le référentiel des entreprises (`Company`)** (`Must`, `3 pts`)
  *En tant qu'*Admin, *je veux* créer, lister, modifier et activer/désactiver des entreprises, *afin de* structurer les entités juridiques du groupe.
  ```gherkin
  Étant donné un Admin authentifié
  Quand il crée une entreprise avec un nom unique, une adresse et un SIRET valide sur POST /api/v1/companies
  Alors l'entreprise est créée (201 Created) avec la convention collective par défaut "Syntec" si omise
  Et un collaborateur sans rôle Admin reçoit 403 Forbidden en écriture mais peut lister les entreprises actives
  ```

- **US-012 — Gérer le CRUD complet des lieux / établissements (`Location`)** (`Must`, `3 pts`)
  *En tant qu'*Admin ou RH, *je veux* créer, consulter, modifier, publier et supprimer des lieux, *afin de* localiser correctement les postes et idées RSE.
  ```gherkin
  Étant donné un utilisateur avec la permission "locations:write"
  Quand il crée ou met à jour un lieu avec nom, adresse, ville et code couleur hexadécimal valide
  Alors le lieu est enregistré avec un slug unique généré automatiquement
  Et s'il tente de supprimer un lieu référencé par un poste ou une idée RSE, l'API retourne 409 Conflict
  ```

- **US-013 — Gérer les types de contrat et exposer le référentiel des départements** (`Must`, `3 pts`)
  *En tant que* RH, *je veux* gérer les types de contrat (hors types système protégés) et récupérer la liste officielle des départements, *afin de* qualifier les contrats et postes.
  ```gherkin
  Étant donné les types système seedés (CDI, CDD, APPRENTISSAGE, CONTRAT_PRO) marqués IsSystem=true
  Quand un RH tente de supprimer ou modifier le code d'un type système
  Alors l'API refuse avec 403 Forbidden
  Et la suppression d'un type personnalisé utilisé par un contrat retourne 409 Conflict
  Et GET /api/v1/referentials/departments retourne les codes et libellés français officiels
  ```

---

### Epic E3 — Collaborateurs, Contacts d'urgence & Postes

#### Feature F3.1 — Cycle de vie des Collaborateurs
- **US-014 — Créer un collaborateur avec génération d'email professionnel et attribution de rôles** (`Must`, `5 pts`)
  *En tant que* RH, *je veux* créer un collaborateur avec son compte utilisateur, ses rôles et son entreprise, *afin de* lui ouvrir l'accès au SIRH.
  ```gherkin
  Étant donné un RH authentifié créant "Alice Durand" dans l'entreprise "Nova Conseil"
  Quand il soumet POST /api/v1/employees avec mot de passe valide et rôle "Employe"
  Alors un slug unique ("alice-durand") et un email pro unique ("adurand@nova-conseil.fr", ou "adurand2@..." si collision) sont générés
  Et son numéro de sécurité sociale (NationalId) est chiffré en base
  Mais si le RH tente d'attribuer le rôle "Admin" ou "SuperAdmin", l'API retourne 403 Forbidden
  ```

- **US-015 — Rechercher, consulter et mettre à jour une fiche collaborateur (anti-IDOR & concurrence)** (`Must`, `5 pts`)
  *En tant que* collaborateur, manager ou RH, *je veux* consulter les fiches autorisées selon mon périmètre et que les RH puissent les mettre à jour sans écrasement silencieux, *afin de* maintenir l'annuaire fiable et confidentiel.
  ```gherkin
  Étant donné un collaborateur A et un collaborateur B
  Quand A appelle GET /api/v1/employees/{idDeB} sans être RH/Admin ni manager de B
  Alors l'API retourne 403 Forbidden
  Quand le manager direct de B appelle GET /api/v1/employees/{idDeB}
  Alors il reçoit 200 OK sans les champs ultra-sensibles (ex. NationalId masqué)
  Quand un RH met à jour une fiche avec un RowVersion obsolète
  Alors l'API retourne 409 Conflict
  ```

- **US-016 — Désactiver et réactiver un collaborateur avec révocation des sessions** (`Must`, `3 pts`)
  *En tant que* RH ou Admin, *je veux* désactiver logiquement un collaborateur sortant ou le réactiver, *afin de* couper immédiatement ses accès tout en préservant l'historique RH.
  ```gherkin
  Étant donné un collaborateur actif possédant des Refresh Tokens valides
  Quand un RH appelle POST /api/v1/employees/{id}/deactivate
  Alors IsActive passe à false, ses postes/documents restent intacts et tous ses Refresh Tokens sont révoqués
  Et si un RH tente de désactiver un compte Admin, l'API retourne 403 Forbidden
  ```

#### Feature F3.2 — Contacts d'urgence & Postes
- **US-017 — Gérer les contacts d'urgence chiffrés d'un collaborateur (max 2)** (`Should`, `3 pts`)
  *En tant que* collaborateur ou RH, *je veux* déclarer jusqu'à 2 contacts d'urgence stockés chiffrés en base, *afin de* garantir joignabilité et confidentialité.
  ```gherkin
  Étant donné un collaborateur ayant déjà 2 contacts d'urgence
  Quand il tente d'en ajouter un 3e via POST /api/v1/employees/{id}/emergency-contacts
  Alors l'API rejette la demande (422/400)
  Et les champs nom, lien et téléphones sont stockés chiffrés en SQL Server
  Et un autre collaborateur non-RH appelant GET sur ces contacts reçoit 403 Forbidden
  ```

- **US-018 — Gérer les affectations et l'historique des postes (`Position`)** (`Must`, `5 pts`)
  *En tant que* RH, *je veux* créer, modifier et supprimer des postes pour un collaborateur (avec dates, manager, lieu et temps de travail), *afin de* suivre son parcours et calculer son poste courant.
  ```gherkin
  Étant donné un collaborateur existant
  Quand un RH crée un poste avec StartAt = 2025-01-01, EndAt = null et ManagerId valide
  Alors le poste est créé avec un slug unique et devient le poste courant du collaborateur
  Et si EndAt < StartAt ou ManagerId == EmployeeId, l'API retourne 400 Bad Request
  Et si un contrat ou une fiche de poste est lié au poste, DELETE /api/v1/positions/{id} retourne 409 Conflict
  ```

---

### Epic E4 — Modèles de contrat, Génération PDF & Fiches de poste

#### Feature F4.1 — Modèles Markdown/Scriban & Génération PDF native
- **US-019 — Gérer les modèles de contrat en Markdown/Scriban et exposer le catalogue des variables** (`Must`, `5 pts`)
  *En tant que* RH, *je veux* créer et éditer des modèles de contrat en Markdown avec variables Scriban, *afin de* standardiser nos contrats sans dépendre de Word/LibreOffice.
  ```gherkin
  Étant donné un RH authentifié
  Quand il crée un modèle de contrat avec une syntaxe Scriban valide sur POST /api/v1/contract-models
  Alors le modèle est sauvegardé et validé syntaxiquement
  Et si le template Scriban contient une erreur de syntaxe, l'API retourne 400 Bad Request avec le détail de l'erreur
  Et GET /api/v1/contract-models/variables retourne toutes les variables employee.*, company.* et position.*
  ```

- **US-020 — Prévisualiser et générer un contrat PDF natif (QuestPDF) à partir d'un modèle** (`Must`, `8 pts`)
  *En tant que* RH, *je veux* prévisualiser puis générer un contrat PDF pour un poste donné avec pré-remplissage automatique, *afin de* produire un contrat prêt à signer et chiffré.
  ```gherkin
  Étant donné un poste sans contrat associé et un modèle de contrat valide
  Quand le RH appelle POST /api/v1/contract-models/{id}/preview avec positionId et variables personnalisées
  Alors l'API retourne un flux binaire application/pdf rendu nativement par QuestPDF sans rien persister
  Quand il appelle POST /api/v1/contracts/generate
  Alors le PDF généré est chiffré, stocké via IFileStorageService et rattaché au poste (1 seul contrat par poste)
  ```

#### Feature F4.2 — Contrats (Import/Statut) & Fiches de poste
- **US-021 — Importer, suivre la signature, consulter et supprimer un contrat** (`Must`, `5 pts`)
  *En tant que* RH ou collaborateur titulaire, *je veux* importer un contrat PDF signé, suivre son statut et télécharger le PDF sécurisé, *afin de* conserver les contrats officiels.
  ```gherkin
  Étant donné un contrat existant rattaché au poste du collaborateur A
  Quand A appelle GET /api/v1/contracts/{id}/file
  Alors l'API déchiffre et streame le PDF (200 OK)
  Quand un collaborateur B non-RH tente d'y accéder
  Alors l'API retourne 403 Forbidden
  Quand le RH tente de créer un 2e contrat sur le même poste
  Alors l'API retourne 409 Conflict
  ```

- **US-022 — Gérer la fiche de poste PDF unique d'un poste (`JobSheet`)** (`Must`, `3 pts`)
  *En tant que* RH, collaborateur ou manager, *je veux* gérer la fiche de poste d'un poste (RH) et la consulter (titulaire, manager direct, RH), *afin de* partager les missions attendues.
  ```gherkin
  Étant donné une fiche de poste PDF liée à un poste du collaborateur A managé par M
  Quand A, M ou un RH appelle GET /api/v1/job-sheets/{id}/file
  Alors le PDF déchiffré est retourné (200 OK)
  Et un autre salarié ou manager d'une autre équipe reçoit 403 Forbidden
  ```

---

### Epic E5 — Dossier Administratif & Cycle de vie Salarié

#### Feature F5.1 — Documents Salarié, RQTH & Titres de séjour
- **US-023 — Déposer, filtrer et télécharger les documents du dossier salarié par catégorie** (`Must`, `5 pts`)
  *En tant que* RH ou collaborateur, *je veux* gérer les documents classés par catégorie (`PersonalFile`, `PersonalEvents`, `ContractualElements`, `Disciplinary`, `Departure`, `Integration`) avec chiffrement et contrôle strict d'accès, *afin de* centraliser le dossier du personnel.
  ```gherkin
  Étant donné un document disciplinaire ("Warning") appartenant au salarié A
  Quand A ou son manager tente de le lister ou le télécharger
  Alors les documents des catégories Disciplinary et Departure sont exclus / retournent 403 Forbidden
  Quand un RH dépose un PDF sur POST /api/v1/employee-documents
  Alors le fichier est validé, chiffré AES-256-GCM et téléchargeable via GET /api/v1/employee-documents/{id}/file
  ```

- **US-024 — Gérer les attestations RQTH et Titres de séjour avec métadonnées d'échéance** (`Should`, `5 pts`)
  *En tant que* RH, *je veux* enregistrer les dates d'expiration et numéros/aménagements des RQTH et titres de séjour, *afin de* suivre les obligations légales et les renouvellements.
  ```gherkin
  Étant donné un document de type "RqthCertificate" ou "ResidentPermit"
  Quand un RH le crée sans ExpiryDate (ou sans PermitNumber pour un titre de séjour)
  Alors l'API retourne 400 Bad Request
  Quand les champs requis sont fournis
  Alors le document et ses détails spécialisés sont enregistrés atomiquement et retournés dans GET /api/v1/employee-documents/{id}
  ```

---

### Epic E6 — Santé au Travail (Visites Médicales) & Centre de Notifications

#### Feature F6.1 — Visites Médicales
- **US-025 — Planifier, mettre à jour et consulter les visites médicales (convocation & rapport chiffrés)** (`Must`, `5 pts`)
  *En tant que* RH, *je veux* planifier une visite médicale avec sa convocation obligatoire puis y joindre le rapport après passage, *afin d'*assurer le suivi médical réglementaire.
  ```gherkin
  Étant donné un RH créant une visite médicale sans fichier de convocation
  Alors l'API retourne 400 Bad Request
  Quand la visite est créée avec une convocation PDF valide
  Alors la convocation est chiffrée et le collaborateur concerné peut consulter sa visite et télécharger convocation et rapport
  Et un autre collaborateur reçoit 403 Forbidden
  ```

#### Feature F6.2 — Rappels Automatisés & Centre de Notifications
- **US-026 — Exécuter les jobs quotidiens de rappel de visite médicale et d'expiration RQTH/Titres de séjour** (`Should`, `5 pts`)
  *En tant que* collaborateur et RH, *je veux* recevoir automatiquement une notification (et un email) 5 jours avant une visite médicale et 30j/7j avant l'expiration d'une RQTH ou d'un titre de séjour, *afin de* ne manquer aucune échéance.
  ```gherkin
  Étant donné une visite médicale future prévue dans 3 jours avec ReminderSentAtUtc = null
  Et une visite passée d'il y a 2 jours avec ReminderSentAtUtc = null
  Quand le job quotidien s'exécute
  Alors seule la visite future génère une Notification + email et met à jour ReminderSentAtUtc
  Et les titres de séjour / RQTH expirant dans <= 30 jours ou <= 7 jours génèrent une alerte unique pour le salarié et les RH
  ```

- **US-027 — Consulter, marquer comme lues et supprimer ses notifications** (`Should`, `3 pts`)
  *En tant que* collaborateur authentifié, *je veux* consulter mes notifications, voir le compteur de non-lues, les marquer comme lues ou les supprimer, *afin de* suivre mes alertes dans l'application.
  ```gherkin
  Étant donné un collaborateur A ayant 2 notifications non lues et un collaborateur B ayant 1 notification
  Quand A appelle GET /api/v1/notifications/unread-count
  Alors il reçoit 2
  Quand A appelle PATCH /api/v1/notifications/{idDeB}/read
  Alors l'API retourne 403 Forbidden
  Quand A appelle POST /api/v1/notifications/read-all
  Alors toutes ses notifications passent à IsRead = true
  ```

---

### Epic E7 — Vie Interne (Documents Communs, Kits d'Accueil, CSE & RSE)

#### Feature F7.1 — Documents Communs & Kits d'Accueil
- **US-028 — Publier, filtrer, archiver et télécharger les documents communs et kits d'accueil** (`Must`, `5 pts`)
  *En tant que* RH ou membre CSE, *je veux* gérer les documents communs (CSE, RSE, accords, recrutement) et les kits d'accueil ciblés par type de contrat, *afin de* les diffuser aux collaborateurs.
  ```gherkin
  Étant donné un kit d'accueil "Mutuelle" ciblé pour "CDI" et un kit "Livret" sans type de contrat (tous)
  Quand un collaborateur filtre GET /api/v1/common-documents?isWelcomeKit=true&contractTypeId={idCDI}
  Alors il reçoit les deux documents pertinents
  Et un membre CSE peut créer/modifier un document de catégorie CsePv/CseBde/HarassmentBestPractices mais reçoit 403 Forbidden sur un accord d'entreprise
  ```

#### Feature F7.2 — Représentants du Personnel (CSE) & Boîte à Idées RSE
- **US-029 — Gérer l'annuaire des représentants du personnel (CSE)** (`Should`, `3 pts`)
  *En tant que* RH ou membre CSE, *je veux* gérer la liste des élus et référents harcèlement, *afin que* tous les collaborateurs sachent qui contacter.
  ```gherkin
  Étant donné un collaborateur sans rôle RH/Admin/CSE
  Quand il appelle GET /api/v1/staff-representatives
  Alors il reçoit la liste paginée filtrable par entreprise et référent harcèlement
  Mais s'il appelle POST, PUT ou DELETE sur /api/v1/staff-representatives, l'API retourne 403 Forbidden
  ```

- **US-030 — Soumettre des idées RSE et piloter leur traitement côté RH** (`Should`, `5 pts`)
  *En tant que* collaborateur, *je veux* proposer une idée RSE avec pièce jointe optionnelle, et *en tant que* RH, *je veux* suivre et statuer sur les idées proposées, *afin d'*animer la démarche RSE.
  ```gherkin
  Étant donné un collaborateur A ayant soumis une idée RSE (statut Submitted)
  Quand A tente d'appeler PATCH /api/v1/rse-ideas/{id}/status
  Alors l'API retourne 403 Forbidden
  Quand un RH passe l'idée à Treated avec un commentaire RH
  Alors le statut devient Treated et A ne peut plus modifier son idée (409/403)
  ```

---

### Epic E8 — Parcours & QCM d'Intégration Structuré

#### Feature F8.1 — Gestion des Questionnaires & Passages
- **US-031 — Créer, éditer et publier un QCM d'intégration côté RH** (`Should`, `5 pts`)
  *En tant que* RH, *je veux* concevoir des QCM d'intégration avec questions à choix unique/multiple et seuil de réussite, *afin de* valider l'intégration des nouveaux arrivants.
  ```gherkin
  Étant donné un RH créant un QCM avec 5 questions et des choix corrects définis
  Quand il tente de publier un QCM ayant une question sans aucune bonne réponse
  Alors l'API refuse la publication avec 400 Bad Request
  Quand le QCM est valide et publié
  Alors il apparaît dans la liste des QCM disponibles pour les collaborateurs de l'entreprise ciblée
  ```

- **US-032 — Passer un QCM d'intégration, calculer le score et suivre les résultats** (`Should`, `5 pts`)
  *En tant que* nouveau collaborateur, *je veux* répondre au QCM en ligne et voir mon score immédiatement, et *en tant que* RH, *je veux* consulter les résultats, *afin de* suivre le taux de complétion de l'onboarding.
  ```gherkin
  Étant donné un QCM publié
  Quand un collaborateur démarre une tentative via POST /api/v1/onboarding-quizzes/{id}/attempts
  Alors les questions et choix sont retournés SANS le champ IsCorrect
  Quand il soumet ses réponses sur POST /api/v1/onboarding-quizzes/attempts/{attemptId}/submit
  Alors le serveur calcule ScorePercent et Passed, enregistre la tentative et retourne la correction
  Et toute re-soumission sur une tentative déjà clôturée retourne 409 Conflict
  ```

---

### Epic E9 — Cloud Readiness (Azure Blob Storage) & Durcissement Sécurité

#### Feature F9.1 — Stockage Azure Blob & Audit Sécurité Final
- **US-033 — Implémenter le provider `AzureBlobFileStorageService` et le tester avec Azurite** (`Could`, `5 pts`)
  *En tant que* développeur souhaitant déployer sur Azure, *je veux* basculer le stockage de fichiers vers Azure Blob Storage par simple configuration tout en conservant le chiffrement AES-256-GCM des fichiers sensibles, *afin de* rendre l'API stateless et cloud-ready.
  ```gherkin
  Étant donné Storage:Provider configuré sur "AzureBlob" pointant vers Azurite
  Quand un fichier employé chiffré et un document commun sont sauvegardés puis relus
  Alors ils sont stockés dans des conteneurs Blob dédiés ("employees-documents" et "common-documents")
  Et les fichiers employés restent chiffrés avant envoi vers Azure Blob
  ```

- **US-034 — Valider la couverture d'autorisation globale (tests anti-IDOR exhaustifs & headers sécurité)** (`Must`, `3 pts`)
  *En tant que* responsable sécurité, *je veux* que chaque endpoint soit couvert par une politique explicite (`RequireAuthorization` par défaut via `FallbackPolicy`) et vérifié par des tests automatisés anti-IDOR, *afin d'*empêcher tout oubli de sécurisation lors de futures évolutions.
  ```gherkin
  Étant donné le pipeline ASP.NET Core configuré avec FallbackPolicy exigeant un utilisateur authentifié
  Quand un nouvel endpoint sans [AllowAnonymous] ni policy est appelé anonymement
  Alors il retourne systématiquement 401 Unauthorized
  Et la suite de tests d'intégration vérifie les statuts 401/403 sur chaque ressource sensible
  ```

---

## 3. Planification par Sprints (MVP puis Incréments)

| Sprint | Objectif principal | User Stories embarquées | Points | Livrable démontrable (Sprint Review) |
|---|---|---|---|---|
| **Sprint 0 — Socle & Auth** | Poser le Monolithe Modulaire .NET 10, SQL Server, stockage chiffré et toute l'authentification sécurisée. | US-001, US-002, US-003, US-004, US-005, US-006, US-007, US-008, US-009, US-010 | **39 pts** | Solution .NET 10 démarrant sur SQL Server + Mailpit ; login, rotation refresh token, OTP reset mdp, `/auth/me` et gestion des permissions visibles dans Scalar. |
| **Sprint 1 — Organisation & Collaborateurs** | Mettre en place les référentiels (Entreprises, Lieux, Types contrat) et le cœur Collaborateurs + Postes. | US-011, US-012, US-013, US-014, US-015, US-016, US-017, US-018 | **32 pts** | CRUD complet Entreprises, Lieux, Types de contrat, Collaborateurs (email pro auto, désactivation révoquant les sessions), Contacts d'urgence chiffrés et Historique des postes. |
| **Sprint 2 — Contrats PDF natifs & Dossier Salarié** | Livrer la génération PDF Scriban+QuestPDF, les contrats/fiches de poste et les documents employés. | US-019, US-020, US-021, US-022, US-023, US-024 | **31 pts** | **JALON MVP CORE** : édition de modèles Markdown/Scriban, prévisualisation & génération PDF QuestPDF, fiches de poste et dossier salarié chiffré (incl. RQTH & Titres de séjour). |
| **Sprint 3 — Santé au Travail & Notifications** | Compléter le suivi réglementaire médical et exposer le centre de notifications + jobs automatiques. | US-025, US-026, US-027 | **13 pts** | **JALON MVP COMPLET** : visites médicales + convocations/rapports chiffrés, jobs quotidiens (visites à 5j + alertes expiration J-30/J-7), API centre de notifications & emails Mailpit. |
| **Sprint 4 — Vie Interne (Docs, Kits, CSE, RSE)** | Couvrir l'ensemble de l'intranet documentaire, les élus CSE et la boîte à idées RSE. | US-028, US-029, US-030 | **13 pts** | Documents communs, kits d'accueil filtrables par type de contrat, annuaire CSE sécurisé, boîte à idées RSE avec workflow RH. |
| **Sprint 5 — QCM d'Intégration Structuré** | Remplacer le système de fichiers QCM par le module interactif complet de quiz d'onboarding. | US-031, US-032 | **10 pts** | Création/publication de QCM par RH, passage en ligne sécurisé (sans fuite des bonnes réponses), calcul automatique des scores et suivi RH. |
| **Sprint 6 — Azure Cloud Readiness & Audit Final** | Activer le driver Azure Blob Storage (Azurite) et exécuter l'audit automatisé anti-IDOR complet. | US-033, US-034 | **8 pts** | Bascule validée sur Azurite (`Azure.Storage.Blobs`), suite complète de tests anti-IDOR/architecture/intégration verte, API prête pour la production et le développement du nouveau front. |

**Total backlog : 34 User Stories — 146 Story Points.**
