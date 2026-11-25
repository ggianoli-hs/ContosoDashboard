# Document Management Contracts

These are application and browser-facing contracts for the Blazor Server feature. There is no separate public REST API. Implementations are described in [plan.md](../plan.md); entity details are in [data-model.md](../data-model.md).

## Upload UI

**Entry point**: authorized document upload page, with entry links from My Documents and task details.

**Input per file**:

- Required title and one allowed category.
- Optional description, project, task attachment, and tags.
- One or more files, each individually limited to 25 MiB and the approved extension allowlist.

**Behavior**:

- Display per-file progress and a distinct success/error result for each file.
- Each file is an independent document record; a failure for one file must not report it as successful or suppress other results in the batch.
- Keep the selected file stream open only while copying it into private staging. Do not retain/reuse a disposed browser file stream.
- Do not show a document in lists, issue a content link, or return success until malware scanning is clean and storage plus metadata persistence have succeeded.
- In Azure deployment, a submitted file enters `PendingScan` and is not publicly or user-visible until a Queue Storage-triggered Azure Function reports a clean terminal result.
- Reject unsupported extension, oversize/empty file, unauthorized project/task, infected content, scanner unavailable/error/timeout, and storage/database failures with an actionable but non-sensitive message.
- Use a fresh input element state after successful uploads so selecting the same file again is supported.

## Document service

The service receives the authenticated caller identity from the UI/endpoint. Resource IDs and requested user IDs are untrusted input. Every operation taking an identifier re-evaluates permissions.

| Operation | Contract |
|---|---|
| Upload one or more files | Validate all fields and associations; authorize; quarantine; persist `PendingScan`; and either scan locally or enqueue `DocumentScanRequested` for Azure Functions. Return success per file only after terminal availability rules are met. |
| List My Documents | Return only documents uploaded by the caller, with title/category/date/size/project and sort/filter support. |
| List project documents | Require project manager/member access; return only documents associated with that project. |
| Search | Search title, description, tags, uploader display name, and project name, after applying caller access scope. Never return inaccessible metadata. |
| Get document details | Return metadata only after the caller passes the document access predicate. |
| Replace file | Require document owner permission; validate, stage, scan, promote new file, atomically switch metadata reference, clean old file, and audit. Does not create version history. |
| Edit metadata | Owner-only fields: title, description, category, tags. Project/task associations are changed only through separately authorized association operations. |
| Share/revoke | Require document owner permission. Recipient is one user or one project team; direct recipients/current team members are notified. Revoked shares grant no access. |
| Delete | Require owner or associated project manager; require explicit UI confirmation, recheck authorization, remove file/metadata safely, and audit. |
| Recent Documents | Return at most five latest uploads by caller, newest first. |
| Dashboard count | Return a caller-scoped count of documents accessible under the same policy as browsing. |
| Admin report | Require Administrator role; provide type, uploader, and access-pattern aggregates without implicitly granting content downloads. |

Service queries are asynchronous. Related uploader/project/task/share information needed by the result is loaded deliberately, not through accidental lazy-loading.

## Malware scanner abstraction

**Input**: readable staged file and cancellation token.  
**Result**: exactly one of `Clean`, `ThreatDetected`, `Unavailable`, or `Failed` (including timeout).

- Only `Clean` permits promotion to permanent storage.
- `ThreatDetected`, `Unavailable`, `Failed`, cancellation, malformed response, and scanner timeout reject the upload.
- Scanner failure must never fall back to accepting the file.
- The scanner implementation and configuration are local infrastructure; the business service depends only on the interface.

## Azure asynchronous scan contract

Azure deployment may replace the local scanner/storage implementations and add an Azure Functions Queue Storage trigger:

1. The web application stores the object in private quarantine storage and creates a `PendingScan` document/job record.
2. It publishes a `DocumentScanRequested` queue message containing `DocumentId`, server-generated quarantine location, `CorrelationId`, and `RequestedUtc`; it does not publish file bytes or original names.
3. The Queue Storage-triggered Azure Function reads the private quarantine object, calls `IMalwareScanner`, and writes an idempotent terminal result.
4. `Clean` promotes the object to permanent private storage and makes the document available. `ThreatDetected` removes/quarantines the object and rejects the document. `Unavailable`, `Failed`, and timeout leave a retryable state; retry exhaustion routes the message to poison handling and sets a terminal failure state.
5. Queue visibility timeout exceeds scanner timeout, concurrency is bounded, and redelivery rechecks terminal state before promotion, deletion, notification, or audit. Queue and poison messages contain identifiers only, and Blob Storage remains private.

## File storage abstraction

- Accepts only generated relative paths and streams; it does not accept a user-controlled physical path.
- Upload/promotion and download/open are asynchronous and cancellation-aware.
- Download returns content as a stream plus validated server-owned content type and a display filename.
- Delete is idempotent for a path already absent, but other failures are surfaced to the service for compensation/logging.
- The local implementation resolves all paths beneath its configured root, outside `wwwroot`; path escape attempts fail.
- No unauthenticated URL, web-root mapping, or storage-root disclosure is provided.

## Browser content endpoints

The endpoint implementation is a thin ASP.NET Core adapter. Exact route names are stable for UI links:

| Method and route | Success | Failure |
|---|---|---|
| `GET /documents/{documentId:int}/download` | `200` stream with attachment disposition and `X-Content-Type-Options: nosniff` | `401` unauthenticated; `404` missing or not authorized; `500` storage failure |
| `GET /documents/{documentId:int}/preview` | `200` inline stream for PDF/JPEG/PNG with `nosniff` and restrictive response policy | `401` unauthenticated; `404` missing or not authorized; `415` unsupported preview type; `500` storage failure |

The endpoint calls the document service for access decisions before opening storage. Unauthorized and nonexistent resources use the same not-found result to avoid disclosing identifiers. File names and MIME headers come from validated metadata, never raw request values. No API accepts or returns a physical storage path.

## Notification and audit behavior

- A direct document share creates an in-app notification for its recipient.
- A new project document creates a notification for current project members other than the uploader.
- Audit events are recorded for upload, download, delete, share, and optionally rejected upload attempts. Audit writes do not authorize an operation or expose document content.
- A failed audit write is not silently described as a successful audit event; the implementation must define safe error handling before deployment.
