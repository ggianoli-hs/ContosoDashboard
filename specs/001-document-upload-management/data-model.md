# Data Model: Document Upload and Management

## Entities

### Document

One record per successfully stored file. In the local training MVP, the database row is created after private storage promotion and committed with the upload audit event; the file is not malware-scanned.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentId` | `int`, primary key, generated | Consistent with existing entity keys. |
| `Title` | required string, max 255 | User-entered display title. |
| `Description` | nullable string, max 2000 | Optional search/display text. |
| `Category` | required string, max 100 | Text value, not enum/integer. Allowed values: Project Documents, Team Resources, Personal Files, Reports, Presentations, Other. |
| `ProjectId` | nullable `int`, FK to Project | Null for unassociated documents. |
| `UploaderId` | required `int`, FK to User | Owner/uploader identity. |
| `UploadedUtc` | required UTC `DateTime` | Set by the service, not supplied by browser. |
| `SizeBytes` | required `long` | Must be greater than zero and at most 26,214,400 bytes (25 MiB). |
| `ContentType` | required string, max 255 | Server-validated allowlisted MIME type, not trusted directly from browser input. |
| `OriginalFileName` | required string, max 255 | Display/download metadata only; never used to construct a storage path. |
| `RelativeStoragePath` | required string, max 1024 | Generated portable relative path; must resolve under configured private storage root. |
| `ScanState` | required string, max 30 | Local training uploads use `Unscanned`. `PendingScan`, `Clean`, `Rejected`, and `ScanFailed` are reserved for future scanning-mode documents. `Unscanned` does not mean clean or malware-free. |

**Relationships**: many-to-one with `User` as uploader; optional many-to-one with `Project`; one-to-many with `DocumentTag`, `DocumentTask`, and `DocumentShare`.

**Validation**:

- Title, category, content type, original display name, uploader, and storage path are required.
- Category must be one of the defined user-facing category labels. Persist the label as text.
- Allow only `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.jpg`, `.jpeg`, and `.png`. Derive the content type from the accepted extension and reject unsupported/mismatched types according to the implementation allowlist.
- Reject zero-byte files and files greater than 25 MiB before permanent storage. Do not rely on the client-side limit.
- Store only generated path segments and a generated identifier; normalize and verify the final resolved path remains inside the configured root.
- Local training uploads require no scanner; successful storage and metadata/audit persistence permit availability with `Unscanned` state.
- Pending, rejected, and failed documents remain excluded from access. `Unscanned` training documents are available only under the normal owner/project/share authorization rules.

### DocumentTag

One normalized user-defined tag associated with one document.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentTagId` | `int`, primary key, generated | |
| `DocumentId` | required `int`, FK to Document | |
| `Tag` | required string, max 100 | Display form, trimmed. |
| `NormalizedTag` | required string, max 100 | Case-normalized form for duplicate prevention and lookup. |

**Constraints**: unique index on (`DocumentId`, `NormalizedTag`). Empty/whitespace tags are discarded; duplicate tags after normalization are saved once. Tag query results remain scoped to authorized documents.

### DocumentTask

Join entity associating documents with one or more tasks.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentId` | required `int`, FK to Document | Part of composite primary key. |
| `TaskId` | required `int`, FK to TaskItem | Part of composite primary key. |
| `LinkedUtc` | required UTC `DateTime` | Set by the service. |
| `LinkedByUserId` | required `int`, FK to User | Authenticated actor. |

**Constraints and rules**:

- Composite primary key: (`DocumentId`, `TaskId`); duplicate links are not allowed.
- The requesting user must be authorized for the task before linking.
- An upload from a task inherits that task's `ProjectId`. If the task has a project, the document must use the same project. Links to tasks in other projects are rejected.
- Task association provides no additional access beyond the document's project/share/owner access policy.

### DocumentShare

An active or revoked grant to one user or one project team.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentShareId` | `int`, primary key, generated | |
| `DocumentId` | required `int`, FK to Document | |
| `SharedByUserId` | required `int`, FK to User | Must be the document owner or an otherwise authorized sharer per policy. |
| `RecipientUserId` | nullable `int`, FK to User | Direct recipient. |
| `RecipientProjectId` | nullable `int`, FK to Project | Team recipient; membership is evaluated at access time. |
| `CreatedUtc` | required UTC `DateTime` | |
| `IsActive` | required `bool`, default true | Only active shares grant access. |
| `RevokedUtc` | nullable UTC `DateTime` | Populated when the share is revoked. |
| `RevokedByUserId` | nullable `int`, FK to User | Actor who revoked it. |

**Constraints and rules**:

- Exactly one recipient column (`RecipientUserId` or `RecipientProjectId`) must be populated.
- Prevent duplicate active grants for the same document/recipient.
- Sharing requires an authorized sharer. A project-team share is visible to current members of that project; removing a user from the project removes that implicit share access without rewriting the share record.
- A user share grants access only while active. Revocation immediately removes access.
- Notifications are sent to the direct recipient or current members of the recipient project when the share is created.

### DocumentAuditEvent

Append-oriented record of security-relevant document actions.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentAuditEventId` | `int`, primary key, generated | |
| `DocumentIdSnapshot` | nullable `int`, no FK | Document identifier captured when the event occurs; retained after a document is deleted. Null for a rejected upload that never created a Document. |
| `ActorUserId` | nullable `int`, FK to User | Null only when no authenticated user can be resolved. |
| `Action` | required string, max 50 | `Upload`, `UploadRejected`, `Download`, `Delete`, or `Share`. |
| `OccurredUtc` | required UTC `DateTime` | Set by the service. |
| `Outcome` | required string, max 50 | For example `Succeeded`, `Rejected`, or `Failed`. |
| `Details` | nullable string, max 1000 | Minimal non-sensitive context; do not store file contents or scanner output containing secrets. |

**Rules**: Audit uploads, downloads, deletions, and shares as required. Rejected/failed uploads may also be recorded. Audit data must not itself grant content access. Reporting queries are restricted to Administrators.

### DocumentScanJob

Future Azure asynchronous deployment design only; this entity is not part of the local training MVP.

| Field | Type / constraints | Notes |
|---|---|---|
| `DocumentScanJobId` | `int`, primary key, generated | |
| `DocumentId` | required `int`, FK to Document | |
| `CorrelationId` | required `Guid` | Tracing/idempotency key shared across upload, queue, function, and audit events. |
| `QuarantineLocation` | required string, max 1024 | Server-generated private blob/container reference; never a public URL or user-controlled path. |
| `State` | required string, max 30 | `Queued`, `Scanning`, `Clean`, `Rejected`, `RetryableFailure`, or `Poisoned`. |
| `DeliveryCount` | required `int`, default 0 | Queue delivery count snapshot for retry diagnostics. |
| `RequestedUtc` | required UTC `DateTime` | Job creation time. |
| `CompletedUtc` | nullable UTC `DateTime` | Set only for a terminal result. |

**Rules**:

- One active scan job per document; retries update the same job and remain idempotent by `CorrelationId`.
- Queue messages contain `DocumentId`, `QuarantineLocation`, `CorrelationId`, and `RequestedUtc`; file bytes and original filenames are never queued.
- A function must verify the document and quarantine object before updating a terminal state.
- `Clean`, `Rejected`, and terminal `Poisoned` outcomes require documented audit and cleanup behavior.

## Existing Entity Extensions

- **Project**: add navigation collections for documents and document-team shares.
- **User**: add navigation collections for uploaded documents, direct shares, and audit events where appropriate.
- **TaskItem**: add a `DocumentTask` navigation collection.
- **NotificationType**: add document-share and new-project-document values while retaining existing notification behavior.
- **DashboardSummary**: expose the current user's accessible document count; `IDashboardService` also returns their five most recently uploaded documents.

## Authorization and Lifecycle

### Read access

A caller can read document metadata or content if at least one applies:

1. The caller is the uploader.
2. The caller is the project manager or a current member of the associated project.
3. The caller has an active direct share.
4. The caller is a current member of a project team with an active team share.

`DocumentService` applies this predicate to list, search, preview, download, and related-document operations. Admin reporting does not imply document-content access.

### Write access

- The owner can edit metadata, replace the file, create/revoke shares, and delete.
- The associated project manager can upload to and delete documents in their project.
- Task attachment/upload requires access to the task and its project.
- Each operation rechecks access using the authenticated current-user ID; page visibility alone is not authorization.

### File lifecycle

`Incoming stream → validated and authorized → private staging → promotion → metadata and audit commit → available (Unscanned)`

Local training uploads do not invoke a scanner. Validation/storage/database failures must not create an available document; staging content is cleaned up and promoted content is deleted as compensation when persistence fails. Replacement does not create version history; a new validated file replaces the old reference only after storage and persistence succeed. Future Azure scanning retains its separate pending/terminal lifecycle.

### Referential behavior

- Restrict deletion of a User or Project while it is referenced by documents, shares, or audit history; handle deactivation/removal without silently transferring ownership or erasing audit evidence.
- Deleting a Task removes its `DocumentTask` links; it does not delete the document.
- Deleting a Document removes tags, task links, and shares after an authorized delete, removes the stored file, and retains an audit record with its document ID snapshot. Database/file cleanup failures must be surfaced and logged rather than reported as success.

## Indexes

At minimum, configure indexes for:

- (`UploaderId`, `UploadedUtc`) for My Documents and Recent Documents.
- (`ProjectId`, `UploadedUtc`) for project browsing.
- (`Category`, `UploadedUtc`) for category filtering.
- (`DocumentId`, `NormalizedTag`) unique, plus `NormalizedTag` for tag matching.
- (`TaskId`, `DocumentId`) for task details.
- Active share lookups by (`RecipientUserId`, `IsActive`) and (`RecipientProjectId`, `IsActive`), with document ID included as appropriate.
- Audit lookups by (`OccurredUtc`, `Action`) and (`ActorUserId`, `OccurredUtc`) for reports.

Index and query design should be confirmed against the LocalDB provider and exercised at the feature's specified 500-document target.
