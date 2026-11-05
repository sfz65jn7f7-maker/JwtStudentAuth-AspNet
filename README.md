# StudentAuth.Api

Exercice d'entraînement en **C# / ASP.NET Core** : un backend d'authentification destiné aux étudiants d'un site d'école.

Il permet de **créer un compte**, **se connecter**, **se déconnecter**, **consulter**, **modifier** et **supprimer** son compte. La sécurité repose sur **JWT** (jetons d'accès) et **BCrypt** (hachage des mots de passe), avec un système de **refresh tokens** stockés en base pour gérer correctement la déconnexion.

L'objectif de cet exercice est de pratiquer :

- une architecture en couches (Controllers, Services, Repositories) ;
- une API REST ;
- les principes SOLID ;
- l'injection de dépendances et le couplage faible ;
- l'authentification par JWT avec renouvellement par refresh token ;
- la validation des comportements avec Postman et SQL Server Management Studio.

---

## Sommaire

1. [Stack technique](#1-stack-technique)
2. [Architecture](#2-architecture)
3. [Modèle de données](#3-modèle-de-données)
4. [Endpoints REST](#4-endpoints-rest)
5. [Fonctionnement de JWT dans l'exercice](#5-fonctionnement-de-jwt-dans-lexercice)
6. [Hachage des mots de passe avec BCrypt](#6-hachage-des-mots-de-passe-avec-bcrypt)
7. [Principes SOLID et bonnes pratiques](#7-principes-solid-et-bonnes-pratiques)
8. [Configuration et lancement](#8-configuration-et-lancement)
9. [Tests Postman et résultats](#9-tests-postman-et-résultats)
10. [Vérifications SSMS et résultats](#10-vérifications-ssms-et-résultats)
11. [Tests prévus mais non réalisés](#11-tests-prévus-mais-non-réalisés)
12. [Points de sécurité](#12-points-de-sécurité)
13. [Pistes d'amélioration](#13-pistes-damélioration)

---

## 1. Stack technique

| Élément | Choix |
|---|---|
| Langage / framework | C# / ASP.NET Core Web API (contrôleurs) |
| Accès aux données | Entity Framework Core (Code First, migrations) |
| Base de données | SQL Server Express |
| Authentification | JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Hachage des mots de passe | BCrypt (`BCrypt.Net-Next`) |
| IDE | Visual Studio |
| Test des requêtes | Postman |
| Vérification de la base | SQL Server Management Studio (SSMS) 22 |

Packages NuGet utilisés :

- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `BCrypt.Net-Next`
- `System.Security.Cryptography.Pkcs` (mise à jour vers 10.0.14 pour corriger l'avertissement de vulnérabilité NU1902)

Swagger n'est volontairement pas utilisé : tous les tests passent par Postman.

---

## 2. Architecture

```
StudentAuth.Api/
├── Controllers/
│   ├── AuthController.cs            register, login, refresh, logout
│   └── StudentsController.cs        me, update, delete
├── Data/
│   ├── AppDbContext.cs
│   └── Configurations/
│       ├── StudentConfiguration.cs          Fluent API (index unique sur Email...)
│       └── RefreshTokenConfiguration.cs     relation avec Student, cascade
├── DTOs/
│   ├── Request/
│   │   ├── RegisterRequestDto.cs
│   │   ├── LoginRequestDto.cs
│   │   ├── UpdateStudentRequestDto.cs
│   │   └── RefreshTokenRequestDto.cs
│   └── Response/
│       ├── StudentResponseDto.cs            jamais de PasswordHash
│       └── AuthResponseDto.cs               tokens + profil
├── Enums/
│   └── UserRole.cs                          Student, Admin
├── Exceptions/
│   ├── AppException.cs                      classe de base (code HTTP)
│   ├── NotFoundException.cs                 404
│   ├── ConflictException.cs                 409
│   └── UnauthorizedException.cs             401
├── Mappers/
│   └── StudentMapper.cs                     Student -> StudentResponseDto
├── Middlewares/
│   └── ExceptionHandlingMiddleware.cs       exceptions -> réponses JSON propres
├── Migrations/                              générées par EF Core
├── Models/
│   ├── Student.cs
│   └── RefreshToken.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IStudentRepository.cs
│   │   └── IRefreshTokenRepository.cs
│   └── Implementations/
│       ├── StudentRepository.cs
│       └── RefreshTokenRepository.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IStudentService.cs
│   │   ├── ITokenService.cs
│   │   └── IPasswordHasher.cs
│   └── Implementations/
│       ├── AuthService.cs
│       ├── StudentService.cs
│       ├── TokenService.cs                  génération du JWT et du refresh token
│       └── BCryptPasswordHasher.cs
├── Settings/
│   └── JwtSettings.cs                       configuration JWT (pattern Options)
├── Program.cs                               injection de dépendances, pipeline
└── appsettings.json                         chaîne de connexion + JwtSettings
```

Il n'y a pas de dossier `Views` : l'exercice est une API REST, donc la « vue » est la réponse JSON (les DTOs de réponse).

### Rôle de chaque couche

| Couche | Responsabilité |
|---|---|
| **Controller** | Reçoit la requête HTTP, délègue au service, renvoie le bon code HTTP. Aucune logique métier. |
| **Service** | Règles métier : unicité de l'email, vérification du mot de passe, génération des tokens, révocation. |
| **Repository** | Accès aux données uniquement, via `AppDbContext`. |
| **Mapper** | Conversion entité vers DTO, pour ne jamais exposer l'entité directement. |
| **Middleware d'exceptions** | Transforme les exceptions métier en réponses HTTP propres, sans fuite de détails internes. |

### Flux d'une requête

```
Client (Postman)
   │
   ▼
Middleware d'exceptions ─► Authentification JWT ─► Autorisation
   │
   ▼
Controller ─► Service ─► Repository ─► AppDbContext ─► SQL Server
```

---

## 3. Modèle de données

### Table `Students`

| Colonne | Type | Remarque |
|---|---|---|
| `Id` | int, clé primaire | auto-incrémenté |
| `FirstName` | nvarchar(100) | obligatoire |
| `LastName` | nvarchar(100) | obligatoire |
| `Email` | nvarchar(256) | obligatoire, **index unique** |
| `PasswordHash` | nvarchar(200) | hash BCrypt |
| `Role` | nvarchar(20) | `Student` ou `Admin` (enum stocké en texte) |
| `CreatedAt` | datetime2 | |
| `UpdatedAt` | datetime2, nullable | rempli lors d'une modification |

### Table `RefreshTokens`

| Colonne | Type | Remarque |
|---|---|---|
| `Id` | int, clé primaire | |
| `Token` | nvarchar(200) | chaîne aléatoire, **index unique** |
| `CreatedAt` | datetime2 | |
| `ExpiresAt` | datetime2 | création + 7 jours |
| `RevokedAt` | datetime2, nullable | rempli quand le token est révoqué |
| `StudentId` | int, clé étrangère | **suppression en cascade** |

---

## 4. Endpoints REST

| Action | Méthode | Route | Accès |
|---|---|---|---|
| Créer un compte | POST | `/api/auth/register` | public |
| Se connecter | POST | `/api/auth/login` | public |
| Renouveler les tokens | POST | `/api/auth/refresh` | public |
| Se déconnecter | POST | `/api/auth/logout` | JWT requis |
| Consulter son compte | GET | `/api/students/me` | JWT requis |
| Modifier son compte | PUT | `/api/students/{id}` | JWT requis, propriétaire uniquement |
| Supprimer un compte | DELETE | `/api/students/{id}` | JWT requis, propriétaire ou `Admin` |

Codes de retour principaux :

| Code | Signification dans l'exercice |
|---|---|
| `200` | Succès avec contenu |
| `201` | Compte créé |
| `204` | Succès sans contenu (logout, delete) |
| `400` | Validation des champs échouée (gérée automatiquement par `[ApiController]`) |
| `401` | Non authentifié, identifiants incorrects, token expiré, invalide ou révoqué |
| `403` | Authentifié mais non autorisé (modifier le compte d'un autre) |
| `404` | Étudiant introuvable |
| `409` | Email déjà utilisé |
| `500` | Erreur interne, détail écrit dans les logs et jamais renvoyé au client |

---

## 5. Fonctionnement de JWT dans l'exercice

### 5.1 Principe général

Un **JWT** (JSON Web Token) est un jeton composé de trois parties séparées par des points :

```
en-tête . contenu (claims) . signature
```

- L'**en-tête** indique l'algorithme de signature (ici HS256).
- Le **contenu** regroupe les *claims* : des informations sur l'utilisateur et sur le jeton. Il est encodé en Base64 mais **pas chiffré** : n'importe qui peut le lire, d'où l'interdiction d'y placer un secret.
- La **signature** est calculée avec la clé secrète du serveur. Si quelqu'un modifie le contenu, la signature ne correspond plus et le jeton est rejeté.

Le serveur n'a **pas besoin de consulter la base** pour valider un JWT : il recalcule la signature et contrôle les champs. On dit que l'authentification est *stateless*.

### 5.2 Configuration utilisée (`appsettings.json`, section `JwtSettings`)

| Paramètre | Valeur | Rôle |
|---|---|---|
| `Issuer` | `StudentAuth.Api` | Émetteur du jeton |
| `Audience` | `StudentAuth.Client` | Destinataire prévu du jeton |
| `SecretKey` | chaîne d'au moins 32 caractères | Clé de signature HS256 (256 bits minimum) |
| `AccessTokenMinutes` | `15` | Durée de vie de l'access token |
| `RefreshTokenDays` | `7` | Durée de vie du refresh token |

### 5.3 Contenu de l'access token généré

Dans `TokenService.GenerateAccessToken`, les claims suivants sont ajoutés :

| Claim | Valeur | Utilité |
|---|---|---|
| `NameIdentifier` | id de l'étudiant | Identifier l'utilisateur dans les controllers (`GetCurrentUserId()`) |
| `Email` | email de l'étudiant | Information d'identité |
| `Role` | `Student` ou `Admin` | Contrôle d'accès (`User.IsInRole(...)`) |
| `Jti` | GUID aléatoire | Identifiant unique du jeton |
| `exp` | date d'expiration | Fin de validité (création + 15 minutes) |
| `iss` / `aud` | issuer / audience | Contrôlés à la validation |

### 5.4 Validation à chaque requête protégée

Configurée dans `Program.cs` (`TokenValidationParameters`), la validation vérifie :

| Contrôle | Effet |
|---|---|
| `ValidateIssuer` | L'émetteur doit être `StudentAuth.Api` |
| `ValidateAudience` | L'audience doit être `StudentAuth.Client` |
| `ValidateLifetime` | Le jeton ne doit pas être expiré |
| `ValidateIssuerSigningKey` | La signature doit correspondre à la clé secrète |
| `ClockSkew = TimeSpan.Zero` | Aucune tolérance de 5 minutes sur l'expiration : 15 minutes signifie 15 minutes |

Si un contrôle échoue, ASP.NET Core répond `401` avant même d'atteindre le controller.

### 5.5 Deux jetons aux rôles différents

| | **Access token** | **Refresh token** |
|---|---|---|
| Format | JWT signé | Chaîne aléatoire de 64 octets en Base64 |
| Contenu lisible | Oui (claims) | Non (aucune information) |
| Durée de vie | 15 minutes | 7 jours |
| Stockage côté serveur | Aucun | Table `RefreshTokens` |
| Utilisation | Header `Authorization: Bearer ...` sur chaque route protégée | Envoyé uniquement à `/refresh` et `/logout` |
| Révocable | Non (expire seul) | Oui (`RevokedAt`) |
| Rôle | Prouver l'identité à chaque requête | Obtenir un nouvel access token sans ressaisir le mot de passe |

L'access token est court pour limiter les dégâts en cas de vol. Le refresh token est long pour garder une session confortable, mais comme il est en base, il peut être révoqué.

### 5.6 Déroulement à l'exécution

```
1. REGISTER   Le client crée le compte. Aucun token n'est renvoyé.

2. LOGIN      Le serveur vérifie email + mot de passe (BCrypt).
              Il génère un access token (15 min) et un refresh token A (7 j, enregistré en base).
              Les deux sont renvoyés avec le profil.

3. REQUÊTES   Le client envoie l'access token dans le header Authorization.
              Le serveur valide la signature, l'issuer, l'audience et l'expiration,
              puis lit l'id et le rôle dans les claims.

4. EXPIRATION Après 15 minutes, l'access token est refusé : 401.

5. REFRESH    Le client envoie le refresh token A à /refresh.
              Le serveur le cherche en base. S'il est actif, il est révoqué (RevokedAt)
              et une nouvelle paire est émise : access token neuf + refresh token B.
              C'est la ROTATION : un refresh token ne sert qu'une seule fois.

6. LOGOUT     Le client envoie son refresh token à /logout.
              Le serveur le révoque. Plus aucun renouvellement n'est possible avec lui.
              L'access token en cours reste valable jusqu'à son expiration (15 min maximum).

7. REJEU      Si un refresh token DÉJÀ RÉVOQUÉ est présenté de nouveau à /refresh,
              le serveur suppose un vol possible : il révoque TOUS les refresh tokens
              de l'étudiant et répond 401. Il doit se reconnecter.
```

### 5.7 Qui déclenche le renouvellement

Le serveur ne prévient jamais le client. C'est **le client** qui appelle `/refresh`, soit après avoir reçu un `401` sur une route protégée, soit un peu avant l'expiration en lisant `accessTokenExpiresAt` (renvoyé par le login et le refresh). Dans cet exercice, Postman joue le rôle du client : le renouvellement est déclenché manuellement.

### 5.8 Comportement selon les routes

| Route | Token requis | Si l'access token est expiré |
|---|---|---|
| `register`, `login`, `refresh` | Non | Aucun effet |
| `logout`, `me`, `update`, `delete` | Oui | `401` avec corps vide (réponse du middleware JWT) |

---

## 6. Hachage des mots de passe avec BCrypt

- Le mot de passe en clair n'est **jamais stocké** : il n'existe que dans la requête d'inscription ou de connexion.
- `BCryptPasswordHasher` hache avec un **work factor de 12** : le hash obtenu commence par `$2a$12$`.
- BCrypt génère le **sel** automatiquement et l'intègre au hash : aucune colonne supplémentaire n'est nécessaire.
- La vérification se fait avec `BCrypt.Verify(motDePasse, hash)`.
- Le hash reste dans la base : le `StudentResponseDto` ne contient pas de champ `PasswordHash`, donc il ne peut jamais apparaître dans une réponse de l'API.
- Au login, un email inconnu et un mot de passe faux renvoient le **même message** (« Email ou mot de passe incorrect. »), pour ne pas révéler si un email existe.

---

## 7. Principes SOLID et bonnes pratiques

| Principe | Application dans l'exercice |
|---|---|
| **S** (responsabilité unique) | Chaque classe a un rôle : hachage, génération de tokens, accès aux données, règles métier, mapping. |
| **O** (ouvert / fermé) | On peut ajouter une autre implémentation (autre algorithme de hachage, autre base) sans modifier les services. |
| **L** (substitution de Liskov) | Toute implémentation respecte le contrat de son interface. |
| **I** (ségrégation des interfaces) | Interfaces petites et ciblées : `IPasswordHasher`, `ITokenService`, `IStudentRepository`... |
| **D** (inversion des dépendances) | Les services dépendent d'interfaces, jamais de classes concrètes. |

Enregistrement dans `Program.cs` :

| Interface | Implémentation | Durée de vie |
|---|---|---|
| `IStudentRepository` | `StudentRepository` | Scoped |
| `IRefreshTokenRepository` | `RefreshTokenRepository` | Scoped |
| `IAuthService` | `AuthService` | Scoped |
| `IStudentService` | `StudentService` | Scoped |
| `ITokenService` | `TokenService` | Scoped |
| `IPasswordHasher` | `BCryptPasswordHasher` | Singleton (sans état) |

Autres bonnes pratiques appliquées :

- DTOs séparés en requête et réponse, jamais d'entité exposée ;
- validation déclarative avec `DataAnnotations` ;
- emails normalisés (suppression des espaces et minuscules) avant comparaison ;
- exceptions métier centralisées dans un middleware ;
- configuration typée avec le pattern Options (`JwtSettings`) ;
- utilisation de l'heure UTC.

---

## 8. Configuration et lancement

### 8.1 Chaîne de connexion et JWT (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=StudentAuthDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Issuer": "StudentAuth.Api",
    "Audience": "StudentAuth.Client",
    "SecretKey": "REMPLACER-PAR-UNE-CLE-SECRETE-D-AU-MOINS-32-CARACTERES",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Le nom de l'instance SQL Server (`.\\SQLEXPRESS`) est à adapter selon la machine : il est visible dans SSMS au moment de la connexion.

### 8.2 Création de la base

Dans la Console du Gestionnaire de package de Visual Studio :

```powershell
Add-Migration InitialCreate
Update-Database
```

Une alternative avec la CLI :

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 8.3 Lancement

1. Choisir le profil **https** (ou **http**) dans Visual Studio.
2. Lancer avec `F5` ou `Ctrl+F5`.
3. Relever le port dans `Properties/launchSettings.json` ou dans la console. Lors des tests, l'API écoutait sur `http://localhost:5229`.

---

## 9. Tests Postman et résultats

### 9.1 Préparation

Une collection **StudentAuth** a été créée avec :

- **Variables de collection** :

| Variable | Valeur |
|---|---|
| `baseUrl` | `http://localhost:5229` |
| `accessToken` | rempli automatiquement par le script |
| `refreshToken` | rempli automatiquement par le script |
| `studentId` | rempli automatiquement par le script |

- **Authorization de la collection** : *Bearer Token* avec la valeur `{{accessToken}}`. Les requêtes protégées l'héritent (*Inherit auth from parent*).
- **Requêtes publiques** (`Register`, `Login`, `Refresh`) : *Auth type* sur **No Auth**.
- **Script « After response »** sur `Login` et `Refresh`, pour enregistrer les tokens automatiquement :

```javascript
if (pm.response.code === 200) {
    const json = pm.response.json();
    pm.collectionVariables.set("accessToken", json.accessToken);
    pm.collectionVariables.set("refreshToken", json.refreshToken);
    pm.collectionVariables.set("studentId", json.student.id);
}
```

### 9.2 Requêtes de la collection

| Requête | Méthode | URL | Authorization |
|---|---|---|---|
| `Register` | POST | `{{baseUrl}}/api/auth/register` | No Auth |
| `Login` | POST | `{{baseUrl}}/api/auth/login` | No Auth |
| `Me` | GET | `{{baseUrl}}/api/students/me` | Inherit |
| `Update` | PUT | `{{baseUrl}}/api/students/{{studentId}}` | Inherit |
| `Refresh` | POST | `{{baseUrl}}/api/auth/refresh` | No Auth |
| `Logout` | POST | `{{baseUrl}}/api/auth/logout` | Inherit |
| `Delete` | DELETE | `{{baseUrl}}/api/students/{{studentId}}` | Inherit |

### 9.3 Résultats des tests réalisés

Compte utilisé : `lotfi@test.com`, mot de passe `Motdepasse123`. Les heures sont en UTC.

#### Test 1 : Register

- **Requête** : `POST /api/auth/register` avec `firstName`, `lastName`, `email`, `password`.
- **Résultat** : `201 Created`.

```json
{
    "id": 1,
    "firstName": "Lotfi",
    "lastName": "Dahou",
    "email": "lotfi@test.com",
    "role": "Student",
    "createdAt": "2026-11-05T15:21:00.1152678Z",
    "updatedAt": null
}
```

- **Explication** :
  - Le compte est créé et le profil renvoyé, sans champ `passwordHash` (le DTO de réponse ne le contient pas).
  - Le rôle `Student` est attribué par le service : `RegisterRequestDto` n'a pas de champ `Role`, donc personne ne peut s'inscrire comme `Admin` en l'ajoutant au JSON.
  - Aucun token n'est renvoyé : l'étudiant doit ensuite se connecter.
  - La durée de 7,9 s est due à l'initialisation d'EF Core, de la connexion SQL et de BCrypt lors de la première requête. Les suivantes sont bien plus rapides.

#### Test 2 : Login

- **Requête** : `POST /api/auth/login` avec `email` et `password`.
- **Résultat** : `200 OK`.

```json
{
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "accessTokenExpiresAt": "2026-11-05T20:00:29.7413735Z",
    "refreshToken": "6vZvQ1LZjvwDcHuV2Ldz...==",
    "refreshTokenExpiresAt": "2026-11-12T19:45:29.9310768Z",
    "student": {
        "id": 1,
        "firstName": "Lotfi",
        "lastName": "Dahou",
        "email": "lotfi@test.com",
        "role": "Student",
        "createdAt": "2026-11-05T15:21:00.1152678",
        "updatedAt": null
    }
}
```

- **Explication** :
  - L'access token expire à 20:00:29, soit 15 minutes après le login (19:45:29) : `AccessTokenMinutes` est bien appliqué.
  - Le refresh token expire 7 jours plus tard (12 novembre) : `RefreshTokenDays` est bien appliqué.
  - Le refresh token est une chaîne aléatoire sans information lisible.
  - Le champ `student` contient le profil sans le hash.
  - `createdAt` s'affiche sans `Z` ici : EF Core relit la date depuis SQL Server sans indication de fuseau. La valeur reste en UTC.

#### Test 3 : enregistrement automatique des tokens

- **Vérification** : onglet *Variables* de la collection après le login.
- **Résultat** : `accessToken`, `refreshToken` et `studentId` (valeur `1`) sont renseignés. Dans l'onglet *Authorization* de la collection, `{{accessToken}}` passe de rouge (variable vide) à bleu (variable reconnue).
- **Explication** : le script *After response* fonctionne. Les requêtes protégées utilisent le token sans copie manuelle.

#### Test 4 : Me (route protégée, avec token)

- **Requête** : `GET /api/students/me`, *Authorization* héritée.
- **Résultat** : `200 OK` avec le profil.
- **Explication** : le JWT est validé (signature, issuer, audience, expiration). L'id est lu dans le claim `NameIdentifier`, puis le service charge l'étudiant en base. La route ne prend aucun id dans l'URL : le token dit de quel étudiant il s'agit.

#### Test 5 : Update

- **Requête** : `PUT /api/students/1` avec `firstName` = `Lotfi Updated`.
- **Résultat** : `200 OK`, `firstName` = `Lotfi Updated`, `updatedAt` = `2026-11-05T19:59:10.9011671Z`.
- **Explication** : la modification est enregistrée et `updatedAt` est renseigné. Le service accepte l'email inchangé (la vérification d'unicité ne s'applique que si l'email est modifié). L'id de l'URL correspond à celui du token, donc le controller autorise l'opération.

#### Test 6 : Refresh (rotation des tokens)

- **Requête** : `POST /api/auth/refresh` avec `{ "refreshToken": "{{refreshToken}}" }`.
- **Résultat** : `200 OK` avec une nouvelle paire de tokens. Les variables de la collection sont remplacées par le script.
- **Explication** : l'ancien refresh token est révoqué et un nouveau est créé (rotation). La preuve se trouve dans la base (voir le test SSMS 3 plus bas).

#### Test 7 : Logout

- **Requête** : `POST /api/auth/logout` avec `{ "refreshToken": "{{refreshToken}}" }`, token d'accès valide.
- **Résultat** : `204 No Content`, corps vide.
- **Explication** : le refresh token est révoqué (`RevokedAt` rempli). Le corps vide est normal pour un `204`. L'access token en cours reste valable jusqu'à son expiration, mais plus aucun renouvellement n'est possible avec ce refresh token.

#### Test 8 : Refresh après logout

- **Requête** : `POST /api/auth/refresh` avec le refresh token qui vient d'être révoqué.
- **Résultat** : `401 Unauthorized`.

```json
{
    "title": "Refresh token révoqué. Veuillez vous reconnecter.",
    "status": 401
}
```

- **Explication** : le token existe en base mais il est révoqué, donc le service refuse le renouvellement. Comme un token déjà révoqué est revenu, le service applique la **protection contre le rejeu** : il révoque tous les refresh tokens de l'étudiant. Le message est renvoyé proprement par `ExceptionHandlingMiddleware`.

#### Test 9 : Delete

- **Requête** : `DELETE /api/students/1` après un nouveau login (token valide).
- **Résultat** : `204 No Content`.
- **Explication** : le propriétaire du compte peut le supprimer. Les refresh tokens associés sont supprimés en cascade par la base (voir le test SSMS 4).

#### Test 10 : Login après suppression

- **Requête** : `POST /api/auth/login` avec les identifiants du compte supprimé.
- **Résultat** : `401 Unauthorized`.

```json
{
    "title": "Email ou mot de passe incorrect.",
    "status": 401
}
```

- **Explication** : l'email n'existe plus dans `Students`. Le message est identique à celui d'un mot de passe faux, pour ne pas révéler l'existence d'un compte.

---

## 10. Vérifications SSMS et résultats

Les requêtes sont exécutées sur la base `StudentAuthDb`. En cas d'affichage en mode texte, passer en mode grille avec le menu **Query → Results To → Results to Grid**.

### Test SSMS 1 : création des tables

- **Vérification** : Explorateur d'objets, base `StudentAuthDb`, dossier *Tables*.
- **Résultat** : trois tables présentes : `dbo.__EFMigrationsHistory`, `dbo.RefreshTokens`, `dbo.Students`.
- **Explication** : la migration `InitialCreate` a bien été appliquée. `__EFMigrationsHistory` est la table de suivi d'EF Core.

### Test SSMS 2 : contenu de `Students` après l'inscription

```sql
SELECT Id, FirstName, LastName, Email, PasswordHash, Role, CreatedAt, UpdatedAt
FROM Students;
```

- **Résultat** : une ligne, `Id = 1`, `Lotfi`, `Dahou`, `lotfi@test.com`, `Role = Student`, `UpdatedAt = NULL`, et un `PasswordHash` commençant par `$2a$12$...`.
- **Explication** : le mot de passe n'est pas stocké en clair. Le préfixe `$2a$12$` correspond à un hash BCrypt avec un work factor de 12. `UpdatedAt` est `NULL` tant que le compte n'a pas été modifié.

### Test SSMS 3 : état de `RefreshTokens` après les tests Refresh et Logout

```sql
SELECT Id, StudentId, CreatedAt, ExpiresAt, RevokedAt
FROM RefreshTokens;
```

- **Résultat** : trois lignes, toutes avec `RevokedAt` renseigné.

| Id | CreatedAt | ExpiresAt | RevokedAt |
|---|---|---|---|
| 1 | 2026-11-05 19:45:29 | 2026-11-12 19:45:29 | 2026-11-05 20:21:42 |
| 2 | 2026-11-05 20:10:09 | 2026-11-12 20:10:09 | 2026-11-05 20:12:47 |
| 3 | 2026-11-05 20:12:47 | 2026-11-12 20:12:47 | 2026-11-05 20:17:14 |

- **Explication** :
  - **Rotation** : le token 2 est révoqué à 20:12:47, à la seconde où le token 3 est créé. Le refresh a bien remplacé l'ancien token par un nouveau.
  - **Logout** : le token 3 est révoqué à 20:17:14, au moment de la déconnexion.
  - **Protection contre le rejeu** : le token 1, créé au premier login et resté actif, est révoqué à 20:21:42. C'est l'heure du refresh rejeté avec un token déjà révoqué : le service a révoqué toutes les sessions de l'étudiant.
  - Aucun refresh token n'est actif à la fin de cette séquence.

### Test SSMS 4 : suppression du compte et cascade

```sql
SELECT COUNT(*) AS Total FROM Students;
SELECT COUNT(*) AS Total FROM RefreshTokens;
```

- **Résultat** : `0` pour `Students` et `0` pour `RefreshTokens`.
- **Explication** : le compte est supprimé, et ses refresh tokens ont disparu avec lui grâce à `OnDelete(DeleteBehavior.Cascade)` configuré dans `RefreshTokenConfiguration`. Aucune ligne orpheline ne subsiste.

---

## 11. Tests prévus mais non réalisés

Les cas suivants étaient prévus dans le scénario mais n'ont pas été exécutés lors de cette session. Ils sont à rejouer pour compléter la validation (il suffit de refaire un `Register` pour recréer le compte).

| Test | Résultat attendu | Ce qu'il vérifierait |
|---|---|---|
| `Register` avec un email déjà utilisé | `409 Conflict` | Unicité de l'email (service et index unique) |
| `Register` avec un email invalide ou un mot de passe trop court | `400 Bad Request` | Validation par `DataAnnotations` |
| `Login` avec un mauvais mot de passe | `401` | Vérification BCrypt, message identique à un email inconnu |
| `Me` sans token (*No Auth*) | `401`, corps vide | Protection de la route |
| `Update` sur `/api/students/999` | `403 Forbidden` | Seul le propriétaire modifie son compte |
| Appel d'une route protégée avec un access token expiré (`AccessTokenMinutes` à 1) | `401`, corps vide | Expiration à 15 minutes sans tolérance (`ClockSkew` à zéro) |
| `Delete` par un compte `Admin` sur un autre compte | `204` | Autorisation par rôle |
| Rejeu d'un ancien refresh token juste après un refresh normal | `401` | Rotation et détection du rejeu (constatée indirectement par la base, voir le test SSMS 3) |

---

## 12. Points de sécurité

- **SecretKey** : celle de `appsettings.json` est une valeur d'entraînement. Dans un vrai dépôt, elle doit venir des *user-secrets* (`dotnet user-secrets`) ou d'une variable d'environnement, et ne jamais être versionnée.
- **HTTPS** : `UseHttpsRedirection` est actif ; les jetons ne doivent jamais circuler en clair en dehors d'un environnement de test local.
- **Réponses** : aucune entité n'est exposée, seuls les DTOs sortent de l'API.
- **Erreurs internes** : les détails d'une erreur `500` sont écrits dans les logs et jamais renvoyés au client.
- **Messages d'authentification** : le même message est utilisé pour « email inconnu » et « mot de passe faux ».
- **Rôle `Admin`** : aucun endpoint ne permet de le créer ; il se définit directement en base :

```sql
UPDATE Students SET Role = 'Admin' WHERE Email = 'lotfi@test.com';
```

Il faut ensuite se reconnecter pour obtenir un token qui contient le nouveau rôle.

- **Changement de mot de passe** : lors d'un `Update` avec un nouveau mot de passe, tous les refresh tokens de l'étudiant sont révoqués.

---

## 13. Pistes d'amélioration

1. Endpoint de changement de mot de passe qui exige l'ancien mot de passe.
2. Endpoint `logout-all` pour fermer toutes les sessions d'un étudiant.
3. Politique de mot de passe plus stricte (majuscule, chiffre, caractère spécial).
4. Rendre `/logout` accessible sans access token valide, puisqu'il reçoit déjà le refresh token à révoquer.
5. Limitation du nombre de tentatives de connexion (*rate limiting*).
6. Tests unitaires sur `AuthService` et `StudentService` avec des mocks des interfaces.
7. Nettoyage périodique des refresh tokens expirés ou révoqués.
8. Stockage du refresh token sous forme de hash en base, pour qu'une fuite de la table ne donne pas des jetons utilisables.
