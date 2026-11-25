# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)  
**Input**: Stakeholder requirements in `StakeholderDocs/document-upload-and-management-feature.md`

## Summary

Add document upload, discovery, preview/download, sharing, task/project integration, dashboard summaries, notifications, and audit/reporting to the existing Blazor Server application. Keep metadata in LocalDB and files outside `wwwroot` behind `IFileStorageService`. Revised FR-007 removes malware scanning from the local training MVP: validate and authorize uploads, stage/promote privately, and commit metadata and audit before reporting success. Represent these files as `Unscanned` and disclose the lack of malware detection. Future Azure scanning remains outside this MVP.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`ContosoDashboard/ContosoDashboard.csproj`)  
**Primary Dependencies**: ASP.NET Core Blazor Server, EF Core SQL Server 10.0.15, Bootstrap 5.3 / Bootstrap Icons; Azure Storage Queue and Azure Functions dependencies apply only to the Azure deployment mode  
**Storage**: SQL Server LocalDB for metadata; local filesystem under a configurable application-data root outside `wwwroot`; generated portable relative paths in the database  
**Testing**: `dotnet build`; manual end-to-end scenarios in [quickstart.md](quickstart.md), including authorization checks; no test project currently exists  
**Target Platform**: Windows training environment with .NET 10 and SQL Server LocalDB; no cloud or external runtime service  
**Project Type**: Single ASP.NET Core web application (Blazor Server)  
**Performance Goals**: Meet spec targets: up to 25 MB per file within 30 seconds; lists/search for 500 documents within 2 seconds; PDF/image preview within 3 seconds  
**Constraints**: Offline-first for training; LocalDB and local disk; max 25 MB per file; supported extensions limited to PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT, JPG/JPEG, PNG; no malware scanning in the local MVP; explicit unscanned-file disclosures; storage remains non-web-accessible; no changes to mock-auth training posture; Azure Functions/Queue Storage is future-only
**Scale/Scope**: Existing dashboard data model and one application; target list/search performance for 500 documents; individual records use integer keys and string category values

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Before research

- **I. Training-First Scope — PASS WITH IMPLEMENTATION REQUIREMENTS**: Implementation must update README.md and upload/My Documents warnings to disclose the absence of malware scanning. This is not a production security claim.
- **II. Offline-First — PASS**: LocalDB and filesystem only; no scanner installation or external runtime dependency is required.
- **III. Infrastructure Behind Interfaces — PASS**: File storage remains behind an injectable interface. No scanner is registered or invoked in the local MVP.
- **IV. Layered Architecture — PASS**: Models/Data, Services, and Pages/Shared follow the existing application layout; pages and endpoints call services rather than the DbContext.
- **V. Security by Layered Defense — PASS WITH DESIGN REQUIREMENTS**: Protected pages require authorization; every service operation taking a document/task/project/share identifier verifies caller access; download/preview endpoints authorize independently and never expose the storage root.
- **VI. Efficient, Consistent Data Access — PASS WITH DESIGN REQUIREMENTS**: Use async EF Core operations, eager-load required related entities, and add indexes for common ownership, project, date, and share filters.
- **VII. Simplicity and Teaching Clarity — PASS**: Reuse current service, notification, EF Core, and Blazor patterns. The storage boundary is required; the local scanner dependency is removed. No new runtime package or framework is planned.

**Gate result**: The revised local design preserves the constitution's private infrastructure and layered access-control requirements. Implement the required training-only malware-risk disclosures before claiming completion. This revision changes the malware-scanning requirement explicitly; it does not remove validation, authorization, auditing, or cleanup.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── document-management.md
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Data/
│   └── ApplicationDbContext.cs         # document entity sets, relationships, indexes
├── Models/
│   ├── Document.cs
│   ├── DocumentTag.cs
│   ├── DocumentTask.cs
│   ├── DocumentShare.cs
│   └── DocumentAuditEvent.cs
├── Services/
│   ├── IDocumentService.cs / DocumentService.cs
│   ├── IFileStorageService.cs / LocalFileStorageService.cs
│   ├── DashboardService.cs              # recent documents and count
│   └── NotificationService.cs           # reuse for document notifications
├── Pages/
│   ├── Documents.razor                  # browse/search, My Documents, shared view
│   ├── DocumentUpload.razor             # upload and metadata
│   └── [existing project/task pages]    # document panels and task attachment
├── Shared/
│   └── NavMenu.razor                    # document navigation
└── Program.cs                           # DI, configured storage, secured file endpoints
```

**Structure Decision**: Extend the existing single `ContosoDashboard` project for the training implementation. The listed files are planned placements, not created application code in this planning phase. Keep file storage outside `wwwroot`; browser downloads/previews use authenticated endpoints that delegate authorization and stream retrieval to `IDocumentService`. Azure deployment may add a separate Azure Functions project and Azure Storage implementation, but neither becomes a local runtime dependency. No public REST API or test project is introduced by this plan.

## Design Overview

1. **Upload pipeline**: Validate metadata, size, and extension; authorize associations; stage/promote privately through `IFileStorageService`; commit metadata/tags/task links and upload audit; then report success.
2. **Training availability**: Store local uploads with `Unscanned` state. No scanner is invoked and no upload is blocked by missing scanner configuration. Disclose that validation and private storage do not detect malware.
3. **Azure scan pipeline**: In the Azure mode, upload the file to a private quarantine blob container and enqueue a `DocumentScanRequested` message in Azure Queue Storage. An Azure Functions Queue Storage trigger processes the message, scans the quarantined blob, and either promotes it to permanent private storage or rejects/quarantines it.
4. **Access policy**: Owners can read, edit metadata, replace, share, and delete their documents. Project members can read/download project documents; managers can upload/delete them. Active direct/team shares grant read access. Successfully persisted `Unscanned` training documents are available under these same rules; pending/rejected/failed scan-mode records remain hidden. Administrator reporting does not grant general content access.
5. **Discovery**: `IDocumentService` owns authorized My Documents, project, shared, filtered, sorted, and search queries. Search is applied only after the access predicate; do not query all records and filter authorization in the UI.
6. **Task/project links**: Task associations use a join entity. A task upload inherits the task's project. A document linked to tasks in a project must keep the same project association.
7. **Replace/delete**: Local replacement validates and stages a new file without scanning, switches the reference after persistence, and records `Unscanned` state. No version history is introduced. Deletion requires confirmation and service authorization. Surface/log compensating cleanup and storage/database failures.
8. **Reporting and audit**: Append audit events for uploads, downloads, deletions, shares, and rejected uploads; local successful uploads explicitly record that malware scanning was not performed. Admin aggregates do not grant content access.
9. **Repository integration**: Extend the existing in-app notification types/service for shares and new project documents; extend `DashboardSummary` and the dashboard page for the accessible document total and current user's five latest uploads.

## Azure Functions / Queue Storage Background Scanning

This section describes a future deployment design, not a local MVP requirement. Its scanner, scan-job schema, and pending/terminal rules require a separate implementation plan; they do not apply to unscanned local uploads.

### Components

- **Web application**: validates metadata and caller access, writes the file to private quarantine storage, creates a `PendingScan` document record, and publishes a queue message.
- **Azure Queue Storage**: holds `DocumentScanRequested` work messages. File bytes remain in private blob storage; the queue contains only document/job identifiers and server-generated blob references.
- **Azure Function**: .NET 10 function with an Azure Queue Storage trigger. It is responsible for retrieving the quarantine blob, invoking an `IMalwareScanner` adapter, updating terminal scan state, and promoting or deleting/quarantining the object.
- **Azure scanner adapter**: deployment-specific implementation of `IMalwareScanner`. The adapter receives the quarantined object reference or stream and returns `Clean`, `ThreatDetected`, `Unavailable`, `Failed`, or `TimedOut`.
- **Managed identity**: the web application and Function use managed identity/RBAC for Queue Storage and private Blob Storage access. Public blob access is disabled.

### Queue message contract

| Field | Rule |
|---|---|
| `DocumentId` | Integer document/job identifier; used for idempotent state updates |
| `QuarantineBlobName` | Server-generated private quarantine blob name; never a user filename or physical path |
| `CorrelationId` | Stable GUID used across upload, queue, Function, scanner, and audit telemetry |
| `RequestedUtc` | UTC timestamp used for queue-age monitoring and timeout handling |

Do not store file contents, MIME values supplied by the browser, or original display names in the queue message. The Function must verify that the document, blob, and correlation ID agree before processing.

### Function outcomes

- **Clean**: move/copy the blob from quarantine to permanent private storage, update `RelativeStoragePath` and scan state to `Clean`, and mark the document available for authorized users. Queue processing is complete and idempotent.
- **Threat detected**: delete the quarantine blob, set scan state to `Rejected`, audit the rejection, and keep the document hidden.
- **Unavailable, failed, or timed out**: retain the quarantine object and leave the scan retryable. Queue redelivery rechecks terminal state and performs no duplicate promotion.
- **Retry exhausted**: route the message to the poison queue after the configured delivery limit, set `ScanFailed` or leave an explicit retry record, delete only after the deployment retention policy permits, and audit the terminal failure.

### Reliability and scaling

- Use an idempotency key and recheck the document scan state before promotion, deletion, notification, or audit. The queue message must be deleted only after terminal state update or documented quarantine cleanup.
- Configure Queue Storage visibility timeout above the scanner timeout. Bound Azure Function concurrency to the scanner provider's supported limits.
- Monitor queue age, delivery count, poison queue depth, scanner failures, and quarantine storage growth.
- If queue publication or Azure storage/scanner infrastructure is unavailable, reject or report a pending infrastructure failure; never return upload success or expose the object as available.

## Risks and Decisions

- **Unscanned-file risk**: The local MVP intentionally does not detect malware. Use trusted training fixtures only; disclose this limitation in UI and README. Never label an unscanned file clean or imply that extension validation protects against malware.
- **Asynchronous Azure scan consistency**: Queue delivery and Blob/SQL updates cannot share a distributed transaction. Use pending/terminal scan states, stable correlation/DocumentId keys, idempotent Function processing, retry/redelivery, poison handling, and quarantine cleanup. Never expose a pending or failed scan.
- **Queue/poison security**: Queue messages contain identifiers only, not file bytes or user-controlled names. Blob containers remain private; managed identity/RBAC is required. Poison messages must not become public links or grant scan/delivery access.
- **Local file/database consistency**: Filesystem and LocalDB cannot share a transaction. Stage/promote/compensate as described above; do not return upload success until both file and metadata are committed. Log cleanup failures for manual recovery; never return a failed/orphaned upload as successful.
- **EnsureCreated schema behavior**: Startup currently uses `EnsureCreated()`. New entities are part of the schema for fresh training databases; because `EnsureCreated` does not migrate an existing schema, implementation documentation and quickstart must call out recreating a disposable training database when adding these tables. Do not silently add an unrelated migration framework.
- **Stakeholder/spec access scope**: Stakeholder notes say administrators have full access, while the approved spec expressly requires authorization-filtered results and an administrator reporting feature but does not grant universal content access. This plan follows the spec: administrators receive reporting access, while document content still requires owner/project/share authorization.
- **Documentation drift**: README currently describes .NET 8 while the project and constitution target .NET 10. Updating that unrelated discrepancy is not necessary for this feature; feature documentation updates must not broaden the security claims.

## Complexity Tracking

The local MVP keeps private storage behind an interface and preserves audit/task/share models. Removing scanning simplifies the training runtime but requires explicit malware-risk disclosures. Azure Functions, scanner interfaces, scan jobs, and pending/terminal processing are future design only, not required local components.

## Post-design Constitution Check

- **I**: PASS — training-only behavior and README update requirement retained.
- **II**: PASS FOR TRAINING — LocalDB and local filesystem only; no scanner prerequisite; Azure is outside the local runtime.
- **III**: PASS — local storage remains behind a DI interface; no concrete scanner dependency is added.
- **IV**: PASS — data access and access rules remain in Services; pages/endpoints do not use `ApplicationDbContext`.
- **V**: PASS — authorization applies at protected pages, endpoint, and every resource service operation; user-visible search is authorization-scoped.
- **VI**: PASS — asynchronous database and file operations, explicit eager loading, and query indexes are part of the design.
- **VII**: PASS — uses existing layers/patterns; no framework or package additions are required.

**Final gate result**: Revised design requires unscanned-training warnings and matching README changes alongside the code update. Validate successful unscanned persistence and existing authorization/cleanup behavior before claiming the MVP complete.
