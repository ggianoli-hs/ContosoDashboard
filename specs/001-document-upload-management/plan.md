# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)  
**Input**: Stakeholder requirements in `StakeholderDocs/document-upload-and-management-feature.md`

## Summary

Add secure document upload, discovery, preview/download, sharing, task/project integration, dashboard summaries, notifications, and audit/reporting to the existing Blazor Server application. Keep the training app local/offline: persist metadata in SQL Server LocalDB and files outside `wwwroot` behind `IFileStorageService`; scan every upload through a fail-closed local `IMalwareScanner` abstraction before promotion to permanent storage. For Azure deployment, process scans asynchronously through Azure Functions with Azure Queue Storage triggers. Extend the existing Models/Data/Services/Pages structure, and enforce resource permissions in services as well as protected UI and file endpoints. Design details and decisions are in [research.md](research.md).

## Technical Context

**Language/Version**: C# / .NET 10.0 (`ContosoDashboard/ContosoDashboard.csproj`)  
**Primary Dependencies**: ASP.NET Core Blazor Server, EF Core SQL Server 10.0.15, Bootstrap 5.3 / Bootstrap Icons; Azure Storage Queue and Azure Functions dependencies apply only to the Azure deployment mode  
**Storage**: SQL Server LocalDB for metadata; local filesystem under a configurable application-data root outside `wwwroot`; generated portable relative paths in the database  
**Testing**: `dotnet build`; manual end-to-end scenarios in [quickstart.md](quickstart.md), including authorization checks; no test project currently exists  
**Target Platform**: Windows training environment with .NET 10 and SQL Server LocalDB; no cloud or external runtime service  
**Project Type**: Single ASP.NET Core web application (Blazor Server)  
**Performance Goals**: Meet spec targets: up to 25 MB per file within 30 seconds; lists/search for 500 documents within 2 seconds; PDF/image preview within 3 seconds  
**Constraints**: Offline-first for training; LocalDB and local disk; max 25 MB per file; supported extensions limited to PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT, JPG/JPEG, PNG; fail closed on scanner unavailable/error/threat; storage remains non-web-accessible; no changes to mock-auth training posture; Azure Functions/Queue Storage is optional and not required by the local training implementation  
**Scale/Scope**: Existing dashboard data model and one application; target list/search performance for 500 documents; individual records use integer keys and string category values

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Before research

- **I. Training-First Scope — PASS**: This is an offline training feature, not a production security claim. Implementation must update README.md to describe document handling, local scanner prerequisites, and remaining training limitations.
- **II. Offline-First — PASS**: LocalDB and filesystem only; malware scanning is an installed local dependency, not a cloud service. The feature fails closed if it is not configured.
- **III. Infrastructure Behind Interfaces — PASS**: File storage and malware scanning are accessed through injectable interfaces. The feature does not bind business logic to the filesystem or a scanner vendor.
- **IV. Layered Architecture — PASS**: Models/Data, Services, and Pages/Shared follow the existing application layout; pages and endpoints call services rather than the DbContext.
- **V. Security by Layered Defense — PASS WITH DESIGN REQUIREMENTS**: Protected pages require authorization; every service operation taking a document/task/project/share identifier verifies caller access; download/preview endpoints authorize independently and never expose the storage root.
- **VI. Efficient, Consistent Data Access — PASS WITH DESIGN REQUIREMENTS**: Use async EF Core operations, eager-load required related entities, and add indexes for common ownership, project, date, and share filters.
- **VII. Simplicity and Teaching Clarity — PASS**: Reuse the current service, notification, EF Core, and Blazor patterns. New interfaces exist only for scanner/storage boundaries required by the spec. No new runtime package or framework is planned.

**Gate result**: No constitution violation identified for the training implementation. Scanner product selection is intentionally deployment configuration because the repository has no scanner dependency and the spec requires a local scanner abstraction, not a vendor. A missing/invalid scanner configuration is a hard upload rejection, never an implicit bypass. Azure Functions and Queue Storage are migration-mode components behind interfaces and are not runtime dependencies of the offline training application.

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
│   ├── DocumentScanJob.cs
│   └── DocumentAuditEvent.cs
├── Services/
│   ├── IDocumentService.cs / DocumentService.cs
│   ├── IFileStorageService.cs / LocalFileStorageService.cs
│   ├── IMalwareScanner.cs / configured local scanner adapter
│   ├── DashboardService.cs              # recent documents and count
│   └── NotificationService.cs           # reuse for document notifications
├── Pages/
│   ├── Documents.razor                  # browse/search, My Documents, shared view
│   ├── DocumentUpload.razor             # upload and metadata
│   └── [existing project/task pages]    # document panels and task attachment
├── Shared/
│   └── NavMenu.razor                    # document navigation
└── Program.cs                           # DI, configured storage/scanner, secured file endpoints
```

**Structure Decision**: Extend the existing single `ContosoDashboard` project for the training implementation. The listed files are planned placements, not created application code in this planning phase. Keep file storage outside `wwwroot`; browser downloads/previews use authenticated endpoints that delegate authorization and stream retrieval to `IDocumentService`. Azure deployment may add a separate Azure Functions project and Azure Storage implementation, but neither becomes a local runtime dependency. No public REST API or test project is introduced by this plan.

## Design Overview

1. **Upload pipeline**: Validate metadata, per-file size, and extension; authorize any project/task association; store the file through `IFileStorageService`; create metadata/tags/task links with `PendingScan` state; and keep the file unavailable until scanning succeeds.
2. **Training scan pipeline**: In the local mode, stage the file privately and call a fail-closed local `IMalwareScanner`. A clean result promotes the file and updates metadata to available; unavailable/error/threat results reject and clean the staging object.
3. **Azure scan pipeline**: In the Azure mode, upload the file to a private quarantine blob container and enqueue a `DocumentScanRequested` message in Azure Queue Storage. An Azure Functions Queue Storage trigger processes the message, scans the quarantined blob, and either promotes it to permanent private storage or rejects/quarantines it.
4. **Access policy**: Owners can read, edit metadata, replace, share, and delete their documents. Project members can read/download documents for their project; project managers can upload/delete project documents. Explicit user/project-team shares grant read access while active. Documents in `PendingScan`, `Rejected`, or `ScanFailed` state are excluded from lists, search, download, and preview. Administrators can use reporting required by FR-027; broad access to document contents is not added.
5. **Discovery**: `IDocumentService` owns authorized My Documents, project, shared, filtered, sorted, and search queries. Search is applied only after the access predicate; do not query all records and filter authorization in the UI.
6. **Task/project links**: Task associations use a join entity. A task upload inherits the task's project. A document linked to tasks in a project must keep the same project association.
7. **Replace/delete**: Replacement stages and scans a new file before switching the metadata reference; no file-version history is introduced. Deletion requires explicit confirmation in the UI and is authorized again by the service. Compensating cleanup and audit outcomes must be recorded for storage/database failures.
8. **Reporting and audit**: Append audit events for uploads, downloads, deletions, shares, rejected uploads, and scan state changes. Admin-only aggregate reports expose the required counts/patterns without granting general document-content access.
9. **Repository integration**: Extend the existing in-app notification types/service for shares and new project documents; extend `DashboardSummary` and the dashboard page for the accessible document total and current user's five latest uploads.

## Azure Functions / Queue Storage Background Scanning

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

- **Scanner availability/product**: No scanner is present in the repository. The local adapter must be configured with a host-installed scanner; scanner absence, timeout, failure, or threat rejects the upload. Verify supported scanner configuration on the training host before implementation testing.
- **Asynchronous Azure scan consistency**: Queue delivery and Blob/SQL updates cannot share a distributed transaction. Use pending/terminal scan states, stable correlation/DocumentId keys, idempotent Function processing, retry/redelivery, poison handling, and quarantine cleanup. Never expose a pending or failed scan.
- **Queue/poison security**: Queue messages contain identifiers only, not file bytes or user-controlled names. Blob containers remain private; managed identity/RBAC is required. Poison messages must not become public links or grant scan/delivery access.
- **Local file/database consistency**: Filesystem and LocalDB cannot share a transaction. Stage/promote/compensate as described above; do not return upload success until both file and metadata are committed. Log cleanup failures for manual recovery; never return a failed/orphaned upload as successful.
- **EnsureCreated schema behavior**: Startup currently uses `EnsureCreated()`. New entities are part of the schema for fresh training databases; because `EnsureCreated` does not migrate an existing schema, implementation documentation and quickstart must call out recreating a disposable training database when adding these tables. Do not silently add an unrelated migration framework.
- **Stakeholder/spec access scope**: Stakeholder notes say administrators have full access, while the approved spec expressly requires authorization-filtered results and an administrator reporting feature but does not grant universal content access. This plan follows the spec: administrators receive reporting access, while document content still requires owner/project/share authorization.
- **Documentation drift**: README currently describes .NET 8 while the project and constitution target .NET 10. Updating that unrelated discrepancy is not necessary for this feature; feature documentation updates must not broaden the security claims.

## Complexity Tracking

No constitution violations for the offline training implementation. Azure Functions and Queue Storage are planned only as a future deployment mode behind existing storage/scanner interfaces, with an optional Azure Storage implementation. The storage and scanner interfaces, pending/terminal scan state, audit entity, and task/share relationships are required to satisfy the feature and existing architectural principles.

## Post-design Constitution Check

- **I**: PASS — training-only behavior and README update requirement retained.
- **II**: PASS FOR TRAINING — LocalDB, local filesystem, and configured local scanner only; Azure Functions/Queue Storage is explicitly optional and outside the offline runtime path.
- **III**: PASS — storage and scanner implementations are behind DI interfaces. Azure implementations are isolated for deployment migration.
- **IV**: PASS — data access and access rules remain in Services; pages/endpoints do not use `ApplicationDbContext`.
- **V**: PASS — authorization applies at protected pages, endpoint, and every resource service operation; user-visible search is authorization-scoped.
- **VI**: PASS — asynchronous database and file operations, explicit eager loading, and query indexes are part of the design.
- **VII**: PASS — uses existing layers/patterns; no framework or package additions are required.

**Final gate result**: PASS. No unresolved technical clarification remains; the local scanner vendor is deliberately supplied by host configuration and a missing provider fails closed.
