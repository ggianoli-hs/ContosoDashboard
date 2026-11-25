# Quickstart: Document Upload and Management

This guide validates the planned feature end to end in the local training app. It does not configure a cloud service or bypass malware scanning. See [the data model](data-model.md) and [the contract](contracts/document-management.md) for access and failure behavior.

## Prerequisites

- Windows training machine with .NET 10 SDK and SQL Server LocalDB available.
- Repository at the feature branch and a clean, disposable LocalDB database. The application uses `EnsureCreated()`; it does not migrate an existing database when new entities are introduced.
- A locally installed malware scanner and the implementation's configured scanner adapter. Uploads must fail closed until this adapter reports a clean result. For service-level tests, an isolated fake scanner may return deterministic clean/threat/unavailable results; do not register a fake scanner in normal application runs.
- The application has mock authentication accounts for System Administrator, Camille Nicole (Project Manager), Floris Kregel (Team Lead), and Ni Kang (Employee). Mock authentication is training-only.

## Start with an isolated database

From `C:\ContosoDashboard`, choose a fresh LocalDB database name for the feature validation. This avoids changing or deleting unrelated LocalDB databases:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\mssqllocaldb;Database=ContosoDashboardDocumentsQuickstart;Trusted_Connection=True;MultipleActiveResultSets=true'
dotnet build .\ContosoDashboard\ContosoDashboard.csproj
dotnet run --project .\ContosoDashboard\ContosoDashboard.csproj
```

Open the HTTPS URL printed by `dotnet run` and sign in through the training login page. On first start, `EnsureCreated()` creates the isolated schema and seed records. Stop the app with Ctrl+C when finished. Do not remove the LocalDB instance to reset the feature database; use a different disposable database name if you need a clean run.

## Validation scenarios

### 1. Upload, scan, and verify metadata

1. Sign in as Ni Kang and open Documents → Upload.
2. Select an allowed PDF or image up to 25 MiB. Enter a title and category; optionally enter a description, project 1, and tags.
3. Submit and observe per-file progress, then success.
4. Open My Documents and verify the title, category, timestamp, file size, and project are displayed. Reopen the record and verify metadata and the authorized preview/download work.
5. Confirm the file exists under the configured private storage root and not under `wwwroot`; its stored name is generated and the database path is relative.

**Expected**: A clean scan and successful storage/database writes produce one visible document and an upload audit event. No browser URL or page exposes the physical storage root.

### 2. Validation and fail-closed scanning

1. Attempt a file larger than 25 MiB, an unsupported extension, and a zero-byte file.
2. With the local scanner configuration unavailable, attempt an otherwise valid upload.
3. If the configured scanner supports a harmless threat-test fixture, submit it in an isolated training database.

**Expected**: The size, extension, and empty-file checks reject input with clear messages. Scanner unavailable/error/threat also rejects; no document appears as uploaded and no permanent content is available. Temporary files are cleaned, and failure/cleanup outcomes are observable in logs/audit as designed.

### 3. Authorization for browse, preview, and download

1. Sign in as a project member or project manager and browse documents for project 1.
2. Sign in as an unrelated employee and try the same project/document URL, search query, download route, and preview route.
3. Sign out and request a content route.

**Expected**: The project member/manager can access project documents; an unrelated user receives no document metadata or bytes (missing and unauthorized resources have the same not-found response); an unauthenticated user is challenged. Hiding a page button is not the authorization check.

### 4. Search, filters, sharing, and task association

1. Search for a document by title, description, tag, uploader, and project; sort and filter My Documents by category and date.
2. As the document owner, share a document with a user and, separately, with a project team.
3. Sign in as a recipient and verify the share notification and Shared with Me entry. Revoke a share and verify further access is denied.
4. Open an authorized task with a project, upload/attach a document from its details, and verify the task link and inherited project.

**Expected**: Only authorized matches are returned; shared recipients can access only active grants; a task-linked document uses the task's project and cannot be attached to a different project.

### 5. Replace, delete, dashboard, audit, and reporting

1. Replace an owned document with a different supported file and verify the new scanned content is served while the prior file is cleaned up; verify no version history is exposed.
2. Confirm deletion in the UI and verify metadata/content are removed and the delete audit event remains.
3. Upload several documents as the current user. Verify the dashboard shows their five newest uploads and the accessible document total.
4. Sign in as Administrator and use the document report for most-uploaded types, active uploaders, and access patterns. Attempt to download a document not otherwise accessible to the administrator.

**Expected**: Replacement and deletion require service authorization; audit events remain queryable. The dashboard data is caller-scoped. Administrator reporting is available, but report permission alone does not grant document-content access.

## Performance checks

Measure in the local training environment with 500 documents and representative tags/project links:

- Document list and search each complete within 2 seconds.
- PDF/image preview loads within 3 seconds.
- A 25 MiB upload, including scanning and persistence, completes within 30 seconds on the representative network.

Capture environment and scanner timing when a goal is not met; do not weaken authorization or scan requirements to improve a timing result.

## Build/test note

The repository currently has no test project. `dotnet build` is the available automated repository-level validation before feature-specific tests are introduced. Implementations should add focused service/storage tests for authorization, path confinement, compensation, and each scanner result; continue the existing README security scenarios for authentication, user isolation, IDOR, and role behavior.

## Azure asynchronous scanning validation (optional deployment mode)

When Azure Storage and Azure Functions are configured:

1. Submit a valid upload and verify that private quarantine storage contains the object and that the document is `PendingScan`.
2. Verify that Azure Queue Storage receives a `DocumentScanRequested` message containing only the document, quarantine, correlation, and timestamp fields.
3. Start or invoke the Azure Functions Queue Storage trigger and verify clean, threat, unavailable, timeout, redelivery, and retry-exhaustion outcomes.
4. Confirm that clean results promote the private object and expose the document only after terminal state is updated; confirm that threat and terminal-failure results remain hidden and quarantine data is cleaned according to policy.
5. Verify poison-queue handling, managed identity/RBAC, idempotent duplicate delivery, and absence of public blob access.

Azure validation is not a prerequisite for the offline training application.
