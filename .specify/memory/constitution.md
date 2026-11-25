<!-- SYNC IMPACT REPORT (temporary; remove before committing)
Version change: template (unversioned) -> 1.0.0 (initial adoption)
Modified principles: all template placeholders replaced (new principles I-VII)
Added sections: Technology Stack Constraints, Development Workflow & Quality Gates
Removed sections: none (template sections replaced in place)
Deferred TODOs: none
Follow-up (outside this change): README.md states ASP.NET Core 8.0, but ContosoDashboard.csproj targets net10.0;
README.md should be aligned with the constitution.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-First Scope
ContosoDashboard is a training application for teaching Spec-Driven Development. It MUST NOT be presented
or deployed as a production system. The Known Limitations section of README.md MUST stay accurate; any
change that adds or removes a limitation MUST update README.md in the same change. Mock authentication
MUST be labeled as training-only wherever it is documented.
Rationale: the project's value is pedagogical, and overstating its security posture misleads learners.

### II. Offline-First, No External Dependencies
The application MUST run fully locally on SQL Server LocalDB with no cloud subscription, external network
service, or external identity provider. New features MUST NOT add a runtime dependency on an external service.
Rationale: training must work offline at no cost.

### III. Infrastructure Behind Interfaces
Every infrastructure dependency (for example file storage, authentication, or external services) MUST be
accessed through an interface registered with dependency injection. Business logic MUST NOT depend on a
concrete infrastructure implementation, so a local implementation can be replaced by a cloud implementation
(such as Azure SQL, Blob Storage, or Microsoft Entra ID) through configuration or DI registration alone.
Rationale: this is the migration path documented in README.md and the abstraction pattern learners practice.

### IV. Layered Architecture
Code MUST be organized into Models, Data, Services, and Pages/Shared layers. Razor pages and components
MUST NOT reference ApplicationDbContext directly; all data access and business rules MUST go through a
service in the Services folder.
Rationale: separation of concerns is a stated project goal, and it keeps authorization in one place.

### V. Security by Layered Defense
Access control MUST be enforced at every layer:
- Every protected page MUST carry `[Authorize]` (or an equivalent policy check).
- Role checks MUST follow the hierarchical RBAC model in README.md (User Roles).
- Every service method that accepts a resource identifier from the caller MUST verify that the current user
  is authorized for that resource (IDOR protection).
A page-level check alone MUST NOT be treated as sufficient protection.
Rationale: defense in depth is a taught concept, and each layer must be verifiable on its own.

### VI. Efficient, Consistent Data Access
Database operations MUST be asynchronous (async/await). Queries that load related entities MUST use explicit
eager loading (`.Include()`) to avoid N+1 query problems. Fields frequently used in filters or sorting MUST
have indexes defined in ApplicationDbContext.

### VII. Simplicity and Teaching Clarity
Code MUST favor simple, readable solutions that learners can follow. New patterns, libraries, or layers MUST
be justified in the plan's Complexity Tracking section before implementation.
Rationale: complexity that is not justified obscures the concepts the project exists to teach.

## Technology Stack Constraints

- Target framework: .NET 10.0, as set in ContosoDashboard/ContosoDashboard.csproj. Changing the target
  framework MUST be an explicit, documented change that also updates README.md.
- UI: Blazor Server with Bootstrap 5.3 and Bootstrap Icons.
- Data: SQL Server LocalDB through Entity Framework Core. Development schema creation uses EnsureCreated().
- Authentication and authorization: cookie-based mock authentication with claims-based identity and RBAC.
- New third-party packages MUST be justified in the plan and MUST NOT require a paid or cloud service.

## Development Workflow & Quality Gates

- Features MUST follow the Spec Kit sequence: specify, plan, tasks, then implement. Run speckit.analyze
  after tasks are generated and resolve any CRITICAL findings before implementation.
- A change MUST build with `dotnet build` and produce no errors before it is considered complete.
- Changes that affect security behavior MUST be verified against the scenarios in README.md
  ("Testing Security Features", Tests 1-4).
- Documentation directly affected by a change (README.md and StakeholderDocs/) MUST be updated in the
  same change.
- Code comments MUST be limited to code that needs clarification.

## Governance

This constitution supersedes other development practices in this repository. Where README.md conflicts
with a principle here, the constitution governs and README.md MUST be corrected.

- Amendments MUST be made by editing this file with a Sync Impact Report, a version bump, and updates to
  any dependent documentation in the same change.
- Versioning follows semantic rules: MAJOR for removing or incompatibly redefining a principle; MINOR for a
  new principle or section or materially expanded guidance; PATCH for clarifications and wording fixes.
- Every spec, plan, and code review MUST verify compliance with these principles. A violation MUST be
  reported as CRITICAL by speckit.analyze and MUST be resolved or approved as an amendment before implementation.
- Runtime development guidance is in README.md.

**Version**: 1.0.0 | **Ratified**: 2026-11-25 | **Last Amended**: 2026-11-25
