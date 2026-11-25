# Research: Document Upload and Management

**Feature**: `001-document-upload-management`  
**Date**: 2026-11-25  
**Inputs**: `spec.md`, `StakeholderDocs/document-upload-and-management-feature.md`, `.specify/memory/constitution.md`, current source and configuration under `ContosoDashboard/`

## Repository Findings

- The application is one ASP.NET Core web project targeting `net10.0`, using Blazor Server, EF Core SQL Server 10.0.15, cookie-based mock authentication, and LocalDB.
- Source layers are `Models/`, `Data/ApplicationDbContext.cs`, `Services/`, `Pages/`, and `Shared/`. `Program.cs` registers the DbContext and scoped application services; startup uses `Database.EnsureCreated()`.
- Entities use integer keys, data annotations, and navigation properties. `ApplicationDbContext.OnModelCreating` configures relationships and indexes and seeds initial records.
- Services use asynchronous EF Core operations, eager loading, and caller user IDs for service-level authorization. For example, `ProjectService.GetProjectByIdAsync` loads project memberships and returns no project to a non-member/non-manager.
- Pages use `[Authorize]`, `AuthenticationStateProvider`, and the `ClaimTypes.NameIdentifier` claim to obtain the current user ID. The dashboard delegates counts and data retrieval to `IDashboardService`.
- `NotificationService` persists in-app notifications. Existing notification types do not include document events.
- There is no existing file-storage service, malware scanner, document model, test project, or tracked upload directory. The working application uses Bootstrap-based Blazor pages.
- Repository ignore rules exclude common build output but do not currently ignore an application-data upload directory.

## Decisions

### D1. Extend the existing single project and service boundary

- **Decision**: Implement document entities and EF configuration in `Models/` and `Data/`; add `IDocumentService`/`DocumentService` under `Services/`; extend existing dashboard and notification services; add document pages/components in `Pages/` and navigation in `Shared/`.
- **Rationale**: This follows the actual structure and Constitution IV. A separate API/project would be unnecessary for the existing Blazor Server app and would add complexity without a required deployment boundary.
- **Alternatives considered**: New frontend/backend projects, direct DbContext access from pages, or a standalone document microservice. These do not fit the repository or training scope; direct page access also violates the constitution's service-layer boundary.

### D2. Store generated files locally behind a storage interface

- **Decision**: Define `IFileStorageService` with async upload/promote, download/open, and delete operations. Register a local filesystem implementation. Keep the physical root configurable and outside `wwwroot`; persist only normalized relative paths using generated names. Validate that resolved paths remain under the configured root.
- **Rationale**: Satisfies offline requirements and the existing cloud-migration abstraction pattern while preventing user filenames or identifiers from becoming arbitrary paths.
- **Alternatives considered**: Store bytes in LocalDB (unnecessarily couples large binary data and metadata), save under `wwwroot` (web bypasses authorization), persist absolute machine paths (not portable), or add an Azure storage dependency (violates offline/no external runtime service).

### D3. Remove malware scanning from the local training MVP (revised)

- **Decision**: Revised FR-007 removes scanning entirely from the local MVP. Remove the scanner dependency from normal local upload execution instead of registering a fake scanner. Persist successful uploads as `Unscanned`; disclose the lack of malware detection in UI and documentation.
- **Rationale**: The user explicitly revised the specification after scanner installation was unavailable on the managed training host. This replaces the earlier fail-closed requirement only for the local training MVP.
- **Alternatives considered**: Optional scanning or a Windows Defender adapter; the user selected no scanning in this MVP. Claiming a fake scan is clean is not an acceptable implementation.
- **Operational limitation**: Private storage, validation, and authorization do not prove content malware-free. Use trusted training fixtures; do not present the application as production-ready.

### D4. Stage, promote, then persist, with compensating cleanup

- **Decision**: Validate metadata, size, extension, caller access, and associations; stage privately; promote to a generated relative path; commit metadata with `Unscanned` state and the upload audit; then return success. Compensate persistence failure by deleting promoted content. Later replacements preserve this ordering without scanning.
- **Rationale**: A filesystem and LocalDB cannot share a transaction. This order avoids presenting incomplete uploads as successful; it does not detect malware.
- **Alternatives considered**: Persisting broken references or relying on a distributed transaction unavailable in the local architecture.

### D5. Use relational tags, task links, and typed share recipients

- **Decision**: Store custom tags in `DocumentTag`; model task attachments with `DocumentTask`; and use `DocumentShare` for either one user recipient or one project-team recipient (a project team is represented by existing `Project`/`ProjectMember`, not a new team subsystem). Enforce exactly one share recipient and prevent duplicates.
- **Rationale**: Relational tags are searchable and do not require fragile delimiter parsing. The join table supports the spec's task associations. Project membership is the established team structure and allows a document to be shared with a project team without inventing a separate team directory.
- **Alternatives considered**: Delimited tags in one string (harder to normalize/search), duplicate project ID on every task association without a join (limits task links), or a new team model (unnecessary parallel identity structure).

### D6. Enforce authorization in the service and file endpoint

- **Decision**: Use the existing owner/project manager/project membership model and active shares as the document access predicate. Apply it in every service call and again for download/preview endpoint requests. Search, filters, counts, and shared-with-me queries use that predicate in the database query. User/project IDs in the request are not themselves proof of access.
- **Rationale**: Matches the constitution's defense-in-depth and current project-service pattern; prevents IDOR and result leakage through search/counts.
- **Alternatives considered**: Hiding buttons in Blazor only, filtering unauthorized results after retrieval, or public/static URLs. Each bypasses service/endpoint authorization.

### D7. Keep HTTP surface limited to authorized content streaming

- **Decision**: Use authenticated application routes for document download and preview. Routes resolve document access through `IDocumentService`, stream content via the storage abstraction, set safe content disposition and `nosniff`, and allow inline preview only for PDF and raster images in the approved allowlist. Do not expose a storage path or permanent public URL.
- **Rationale**: Browser navigation needs a streamable response while the repository has no separate API layer. The endpoint remains a thin adapter around the service-level policy.
- **Alternatives considered**: Public blob-style URLs (not appropriate for private local storage), serving from `wwwroot` (bypasses checks), or adding a full REST API/controller architecture (not needed for the current client).

### D8. Reuse existing notifications and add append-only audit events

- **Decision**: Extend existing notification types/service for a direct share and a new project document; add a `DocumentAuditEvent` record for upload/download/delete/share actions, with rejected upload attempts available for audit without treating them as successful uploads. Keep administrator reporting separate from document-content authorization.
- **Rationale**: Reuses the existing notification pattern and meets FR-021/025/026/027 without another messaging dependency.
- **Alternatives considered**: External email/push notification (out of scope and not offline); logging only to transient application logs (not suitable for queryable activity reports); grant administrators universal download access to satisfy reporting (not required by the approved spec).

### D9. Use indexes and bounded queries for the specified 500-document workload

- **Decision**: Add indexes for uploader/date, project/date, category/date, task/document links, tag search, and active share recipients. Apply filtering/sorting/paging in SQL and cap list pages; use the existing LocalDB provider and EF async operations.
- **Rationale**: Aligns with the performance targets and Constitution VI while avoiding a search service or full-text dependency for the stated scale.
- **Alternatives considered**: External search engine (offline/runtime dependency) or loading all documents and filtering in Blazor (unbounded and can leak inaccessible data).

### D10. Follow approved spec for the administrator scope

- **Decision**: Administrators may access the audit/reporting functions required by FR-027; the plan does not grant global preview/download or bypass document-level access rules. Owners, project memberships/managers, and active shares determine document content access.
- **Rationale**: Stakeholder notes suggest broad administrator access, but the approved feature specification specifically grants reporting and repeatedly requires authorized, access-filtered results. The more restrictive approved authorization contract is the implementation baseline.
- **Alternatives considered**: Implicit global administrator access from stakeholder prose. Rejected because it expands data access beyond the approved spec's explicit requirements.

### D11. Use Azure Functions and Queue Storage for asynchronous cloud scanning

- **Decision**: Local training uploads are unscanned under revised FR-007. Future Azure scanning may stage in quarantine, persist `PendingScan`, and enqueue work for a fail-closed scanner implementation. This future design requires its own schema/runtime changes and does not apply to the local MVP.
- **Rationale**: Queue-triggered processing avoids blocking the web request on scanner latency and gives bounded retries, poison handling, and monitoring. Queue messages contain identifiers and server-generated references only. The storage and scanner interfaces preserve business-layer independence and permit Azure implementations to replace local implementations without changing pages or authorization logic.
- **Alternatives considered**: Blocking synchronous scanning in Azure (poor request responsiveness), putting file bytes in queue messages (unsafe and size-limited), exposing quarantine files publicly (bypass authorization), or making Azure dependencies mandatory for the offline training app (violates the constitution and FR-030).

## Clarifications Resolved

- **Local scanning policy**: Revised FR-007 removes the scanner requirement entirely from the local MVP; files are explicitly unscanned. Future Azure scanning is outside this scope.
- **Team recipient model**: A “team” recipient is an existing project team represented by `ProjectId` and its current members.
- **Task/document relationship**: A document may be attached to a task; task-uploaded documents inherit the task's project. The same document's task links must remain consistent with its single project association.
- **Admin content access**: Reporting is admin-only as required; document content remains governed by document-level authorization.
- **Schema setup**: New tables are included in EF model configuration. Since the app uses `EnsureCreated()`, an existing training database must be recreated to materialize the new schema; adding migrations is not part of this design.

## Research Exit Criteria

All technical context items are resolved from repository evidence, the approved specification, or explicit design choices above. No `NEEDS CLARIFICATION` items remain.
