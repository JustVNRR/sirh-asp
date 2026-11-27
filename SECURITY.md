# Security Policy

## Supported Versions

Security fixes and dependency updates are actively maintained on the following version line:

| Version | Supported |
| :--- | :--- |
| `1.0.x` (`main`) | Yes |
| `< 1.0.0` | No |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues or pull requests.**

Instead, please submit a private vulnerability report via GitHub Security Advisories:

- [Report a vulnerability privately](https://github.com/JustVNRR/sirh-asp/security/advisories/new)

When reporting a vulnerability, please include as much detail as possible:
- A clear description of the issue and potential impact (e.g., authentication, RBAC/ABAC authorization bypass, IDOR, encryption, file storage, or dependency exposure).
- Step-by-step reproduction instructions, affected endpoints or modules, and any relevant proof-of-concept payloads.
- Any suggested remediation or mitigation, if available.

## Response Expectations

- You can expect an initial acknowledgment within **72 hours** of submitting a private advisory.
- Once confirmed, we will work on a fix on a private branch and coordinate disclosure once a patch is released.

## Security Baseline

This repository enforces:
- Global ASP.NET Core `FallbackPolicy` (`RequireAuthenticatedUser`), RBAC permission policies, and resource-level anti-IDOR checks across all 6 modules.
- Application-level **AES-256-GCM** encryption for sensitive PII and confidential employee files (`StorageScope.EmployeeSensitive`).
- Strict magic-bytes upload inspection and defensive HTTP security headers (`SecurityHeadersMiddleware`).
- Continuous automated checks via **SonarAnalyzer.CSharp** (`TreatWarningsAsErrors=true`), **CodeQL**, NuGet vulnerability audits, and **Dependabot**.
