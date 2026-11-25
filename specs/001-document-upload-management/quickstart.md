# Quickstart: Document Upload and Management

This guide describes validation of the revised local training MVP, which does not perform malware scanning. Use only trusted training fixtures; private storage and input validation do not prove content safe. See [the specification](spec.md), [data model](data-model.md), and [contract](contracts/document-management.md).

## Prerequisites

- Windows training machine with .NET 10 SDK and SQL Server LocalDB available.
- Repository at the feature branch and a clean, disposable LocalDB database. The application uses `EnsureCreated()`; it does not migrate an existing database when new entities are introduced.
- No malware scanner installation or configuration is required by revised FR-007. Files must be explicitly represented as unscanned, and upload/My Documents pages must disclose that malware detection is not provided.
- The application has mock authentication accounts for System Administrator, Camille Nicole (Project Manager), Floris Kregel (Team Lead), and Ni Kang (Employee). Mock authentication is training-only.

## Revised training behavior

Scanner installation was unavailable on the managed training machine. The user subsequently approved removing scanning entirely from the local training MVP rather than substituting a fake scanner or claiming a clean scan.

The local MVP no longer invokes a scanner. Successful uploads are stored as `Unscanned`, with warnings on upload and My Documents pages. If you still see the old scanner-unavailable message, restart the application with the rebuilt code. Do not register a fake scanner or mark unscanned files clean.

## Start with an isolated database

From `C:\ContosoDashboard`, choose a fresh LocalDB database name for the feature validation. This avoids changing or deleting unrelated LocalDB databases:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\mssqllocaldb;Database=ContosoDashboardDocumentsQuickstart;Trusted_Connection=True;MultipleActiveResultSets=true'
dotnet build .\ContosoDashboard\ContosoDashboard.csproj
dotnet run --project .\ContosoDashboard\ContosoDashboard.csproj
```

Open the HTTPS URL printed by `dotnet run` and sign in through the training login page. On first start, `EnsureCreated()` creates the isolated schema and seed records. Stop the app with Ctrl+C when finished. Do not remove the LocalDB instance to reset the feature database; use a different disposable database name if you need a clean run.

Use a database name that has not been used by the original scanner-gated MVP. Its old `ScanState` check constraint does not permit `Unscanned`, and `EnsureCreated()` will not update it. The application does not automatically migrate or delete existing data.

## Validation scenarios

### 1. Unscanned training upload

With no scanner installed or configured:

1. Sign in as Ni Kang and open Documents → Upload.
2. Select a supported file no larger than 25 MiB. Enter a title and category; optionally enter a description and tags.
3. Submit and verify the per-file result reports success.
4. Open My Documents and verify the title, category, timestamp, size, and tags.
5. Confirm the file is stored under the configured private root, outside `wwwroot`, using a generated name and relative database path.

**Expected**: Validation, authorization, storage, and metadata/audit persistence produce a visible document with `ScanState = Unscanned`. UI warnings disclose the absence of malware scanning; neither UI nor audit describes the upload as scanned/clean. The current MVP does not include document detail, preview, or download.

### 2. Validation, authorization, and failure cleanup

1. Attempt a file larger than 25 MiB, an unsupported extension, and a zero-byte file. If the browser file picker prevents selecting an unsupported extension, record that case as not manually exercised.
2. Try submitting with a missing title/category and with a tag longer than 100 characters. Confirm required fields and service validation prevent an upload.
3. Attempt a project upload as a user who is not the project manager; confirm it is rejected.
4. Test storage/persistence failure only using a disposable test database and dedicated storage root; confirm the result is not reported as successful.
5. If inspecting the disposable database and private storage root, confirm rejected attempts create no visible `Document` row or permanent file, staged content is cleaned up, and rejected-upload audit events are recorded.

**Expected**: Invalid or unauthorized uploads fail with actionable messages. Storage/persistence failures are surfaced and cleanup failures logged; no incomplete upload is presented as successful. Scanner availability has no effect on the revised local training upload flow.

### 3. MVP authentication and My Documents isolation

1. While signed out, request `/documents` and `/documents/upload`; confirm the app redirects or challenges to the training login page.
2. Sign in as Ni Kang and upload a trusted training file.
3. Sign in as a different user and confirm Ni Kang's document does not appear in My Documents. Upload a second file for that user and verify each owner sees only their own uploads, including `Unscanned` documents.

**Expected**: Protected pages require authentication and My Documents does not reveal another user's documents. This MVP has no document-ID detail, preview, or download route, so IDOR/content-route checks are deferred.

### 4. Deferred beyond the current MVP

Project browsing, search, preview/download, sharing, replacement/deletion, task integration, dashboard/reporting, and their related authorization checks are not part of T001–T017. Validate these after their implementation tasks are completed.

## Deferred performance checks

List/search/preview checks require their later implementation. Upload timing can be measured for the revised MVP without a scanner. Use representative test data and record the environment:

- Document list and search each complete within 2 seconds.
- PDF/image preview loads within 3 seconds.
- A 25 MiB training upload, including private storage and persistence but no malware scan, completes within 30 seconds on the representative network.

Capture environment and storage/persistence timing when a goal is not met; do not weaken authorization or input validation to improve a timing result.

## Build/test note

The repository currently has no test project. `dotnet build` is the available automated repository-level validation. Verify upload acceptance without a scanner, explicit `Unscanned` state/disclosures, tag normalization, authorization, path confinement, and compensation. Continue the existing README security scenarios for authentication, user isolation, IDOR, and role behavior.

## Azure asynchronous scanning validation (optional deployment mode)

Future design only; it is not implemented or required by the revised local training MVP.

When Azure Storage and Azure Functions are configured:

1. Submit a valid upload and verify that private quarantine storage contains the object and that the document is `PendingScan`.
2. Verify that Azure Queue Storage receives a `DocumentScanRequested` message containing only the document, quarantine, correlation, and timestamp fields.
3. Start or invoke the Azure Functions Queue Storage trigger and verify clean, threat, unavailable, timeout, redelivery, and retry-exhaustion outcomes.
4. Confirm that clean results promote the private object and expose the document only after terminal state is updated; confirm that threat and terminal-failure results remain hidden and quarantine data is cleaned according to policy.
5. Verify poison-queue handling, managed identity/RBAC, idempotent duplicate delivery, and absence of public blob access.

Azure validation is not a prerequisite for the offline training application.
