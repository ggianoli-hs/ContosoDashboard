# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-upload-management/`
**Prerequisites**: `plan.md`, `spec.md`, `data-model.md`, `contracts/document-management.md`

**Tests**: No automated test project is planned, and the specification does not explicitly request adding tests. Final validation uses the existing build and manual quickstart scenarios.

**Scope**: Implement the offline training application with LocalDB, local private file storage, and a fail-closed local scanner adapter. Azure Functions and Queue Storage are future deployment options, not runtime requirements for this feature.

## Phase 1: Foundational Infrastructure

**Purpose**: Add the data and infrastructure boundaries required by all user stories.

- [ ] T001 [P] Add `Document` in `ContosoDashboard/Models/Document.cs` with the exact fields and constraints from `data-model.md` (`Document.DocumentId`, `Document.Title`, `Document.Description`, `Document.Category`, `Document.ProjectId`, `Document.UploaderId`, `Document.UploadedUtc`, `Document.SizeBytes`, `Document.ContentType`, `Document.OriginalFileName`, `Document.RelativeStoragePath`, `Document.ScanState`).
- [ ] T002 [P] Add `DocumentTag` in `ContosoDashboard/Models/DocumentTag.cs` with `DocumentTag.DocumentTagId`, `DocumentTag.DocumentId`, `DocumentTag.Tag`, and `DocumentTag.NormalizedTag`; preserve the 100-character limits and per-document normalized-tag uniqueness from `data-model.md`.
- [ ] T003 [P] Add `DocumentTask` in `ContosoDashboard/Models/DocumentTask.cs` with the composite key `DocumentTask.DocumentId` + `DocumentTask.TaskId`, `DocumentTask.LinkedUtc`, and `DocumentTask.LinkedByUserId`, as defined in `data-model.md`.
- [ ] T004 [P] Add `DocumentShare` in `ContosoDashboard/Models/DocumentShare.cs` with the `DocumentShare` fields and exact-one-recipient, active-grant, and revocation rules from `data-model.md`.
- [ ] T005 [P] Add `DocumentAuditEvent` in `ContosoDashboard/Models/DocumentAuditEvent.cs` with the `DocumentAuditEvent` fields, allowed action values, retained document-ID snapshot, and non-sensitive details limits from `data-model.md`.
- [ ] T006 Extend `Project` in `ContosoDashboard/Models/Project.cs`, `User` in `ContosoDashboard/Models/User.cs`, and `TaskItem` in `ContosoDashboard/Models/TaskItem.cs` with the document navigation collections specified in `data-model.md`.
- [ ] T007 Configure document DbSets, relationships, delete behavior, composite/unique constraints, and required ownership/project/date/tag/task/share/audit indexes in `ContosoDashboard/Data/ApplicationDbContext.cs` per `data-model.md`; retain the existing LocalDB `EnsureCreated()` setup and document its disposable-database limitation.
- [ ] T008 Define `IFileStorageService` in `ContosoDashboard/Services/IFileStorageService.cs` for asynchronous staging, promotion, open, and delete operations using generated relative paths only.
- [ ] T009 Implement `LocalFileStorageService` in `ContosoDashboard/Services/LocalFileStorageService.cs` with a configurable private root outside `ContosoDashboard/wwwroot`, path containment checks, cancellation-aware streaming, and surfaced storage failures.
- [ ] T010 Define `IMalwareScanner` and scan result types in `ContosoDashboard/Services/IMalwareScanner.cs`; implement a configurable local scanner adapter in `ContosoDashboard/Services/LocalMalwareScanner.cs` that fails closed when unavailable, failing, timed out, or reporting a threat.
- [ ] T011 Define document input, filter, result, and service contracts in `ContosoDashboard/Services/IDocumentService.cs`; every operation receiving a caller-controlled resource ID must receive or resolve the authenticated caller identity.
- [ ] T012 Register document services, local storage, and scanner configuration in `ContosoDashboard/Program.cs` and `ContosoDashboard/appsettings.json`; do not add cloud or external runtime dependencies.
- [ ] T013 Implement shared document authorization and audit persistence in `ContosoDashboard/Services/DocumentService.cs`, applying owner, project-member/manager, and active-share rules from `data-model.md`; ensure audit events never grant content access.

**Checkpoint**: Data model, private storage, scanning, service contracts, and authorization foundations are ready for story implementation.

## Phase 2: User Story 1 - Upload and Organize a Document (Priority: P1)

**Goal**: An authorized employee uploads supported files with required metadata and can reopen only successfully scanned documents.

**Independent Test**: Upload a valid file and verify its metadata and content are available; attempt an unsupported, empty, oversized, unauthorized, scanner-failing, and storage-failing upload and verify it is not reported as successful or exposed.

- [ ] T014 [US1] Implement per-file upload validation and local upload orchestration in `ContosoDashboard/Services/DocumentService.cs`: require title/category; enforce the allowed extensions and 25 MiB maximum; reject empty files; derive and validate MIME type server-side; authorize project/task associations; stage privately; persist only after a clean scan; and compensate storage/database failures. Apply the `Document` field rules in `data-model.md`, including `Document.Title`, `Document.Category`, `Document.SizeBytes`, `Document.ContentType`, `Document.OriginalFileName`, `Document.RelativeStoragePath`, and `Document.ScanState`.
- [ ] T015 [US1] Implement tag trimming, normalization, duplicate elimination, and persistence in `ContosoDashboard/Services/DocumentService.cs` using `DocumentTag.Tag` and `DocumentTag.NormalizedTag` constraints from `data-model.md`.
- [ ] T016 [US1] Create the authorized multi-file upload page in `ContosoDashboard/Pages/DocumentUpload.razor` with required title/category, optional description/project/tags, accepted file types, per-file progress and results, actionable validation feedback, and reset file-input state after success.
- [ ] T017 [US1] Add a My Documents entry point and document navigation link in `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Shared/NavMenu.razor`; show only uploaded documents that passed scanning and authorization.

**Checkpoint**: A clean, valid upload is stored and discoverable; rejected or incomplete uploads are never presented as successful.

## Phase 3: User Story 2 - Browse, Search, and Download Documents (Priority: P1)

**Goal**: Employees and project team members can discover and retrieve only documents they are authorized to access.

**Independent Test**: Find and retrieve an authorized document through My Documents, project browsing, and search; verify an unauthorized user cannot discover its metadata or content.

- [ ] T018 [US2] Implement authorized My Documents and project-document queries in `ContosoDashboard/Services/DocumentService.cs`, including title/category/date/size/project filters, date range, and sorting; use explicit eager loading and the indexes configured in T007.
- [ ] T019 [US2] Implement authorized search in `ContosoDashboard/Services/DocumentService.cs` across title, description, tags, uploader display name, and project name, applying the access predicate before returning results and excluding non-clean scan states.
- [ ] T020 [US2] Implement document browse, filter, sort, search, and Shared with Me list views in `ContosoDashboard/Pages/Documents.razor`; display title, category, upload date, size, and project.
- [ ] T021 [US2] Add authorized download and PDF/JPEG/PNG preview endpoints in `ContosoDashboard/Program.cs` using the contract routes `/documents/{documentId:int}/download` and `/documents/{documentId:int}/preview`; delegate authorization and stream access to `IDocumentService`, return the specified status behavior, set safe content headers, and never expose physical storage paths.
- [ ] T022 [US2] Add download and preview actions to `ContosoDashboard/Pages/Documents.razor`, allowing preview only for supported preview MIME types and handling missing, unauthorized, and storage-failure responses without leaking resource details.
- [ ] T023 [US2] Record successful and failed download outcomes in `ContosoDashboard/Services/DocumentService.cs` using the `DocumentAuditEvent` rules from `data-model.md`.

**Checkpoint**: Browse, search, preview, and download return only clean documents the current user can access.

## Phase 4: User Story 3 - Manage and Share Documents (Priority: P2)

**Goal**: Owners and authorized project managers can manage document metadata, replacement, deletion, and sharing without weakening access controls.

**Independent Test**: Update and replace an owned document, share and revoke access, and delete an authorized document; verify permissions are rechecked in the service and storage cleanup failures are surfaced.

- [ ] T024 [US3] Implement owner-authorized metadata editing in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/Documents.razor` for title, description, category, and tags only; enforce the corresponding `Document` and `DocumentTag` constraints in `data-model.md`.
- [ ] T025 [US3] Implement owner-authorized file replacement in `ContosoDashboard/Services/DocumentService.cs` and the edit UI in `ContosoDashboard/Pages/Documents.razor`; stage and scan the replacement before switching the stored reference, clean the previous file after success, and preserve the existing document if validation or scanning fails.
- [ ] T026 [US3] Implement confirmed document deletion in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/Documents.razor`; allow only the owner or authorized project manager, recheck access, remove related tags/task links/shares and stored content safely, and retain `DocumentAuditEvent.DocumentIdSnapshot`.
- [ ] T027 [US3] Implement creation and revocation of direct-user and project-team shares in `ContosoDashboard/Services/DocumentService.cs`, enforcing the exact-one-recipient and active-grant constraints on `DocumentShare` in `data-model.md`; evaluate team membership at access time.
- [ ] T028 [US3] Add share and revoke controls to `ContosoDashboard/Pages/Documents.razor` for document owners, with validation that a recipient is exactly one user or project team.
- [ ] T029 [US3] Extend notification types and `NotificationService` in `ContosoDashboard/Models/Notification.cs` and `ContosoDashboard/Services/NotificationService.cs` to notify direct recipients and current team members on shares; record share and delete actions in `DocumentAuditEvent`.

**Checkpoint**: Metadata changes, replacement, deletion, and share grants are authorized and auditable; revoked shares immediately stop granting access.

## Phase 5: User Story 4 - Work with Documents from Tasks and the Dashboard (Priority: P2)

**Goal**: Users can attach documents to authorized tasks and see their recent uploads, accessible document count, and project activity notifications.

**Independent Test**: Attach/upload a document from a task and verify its project association and task link; verify the dashboard shows five recent uploads and a caller-scoped document count.

- [ ] T030 [US4] Implement authorized task association and task-upload project inheritance in `ContosoDashboard/Services/DocumentService.cs`; enforce `DocumentTask` composite-key/link rules and ensure a document cannot link to a task in a different project.
- [ ] T031 [US4] Add related-document display and upload/attach actions to `ContosoDashboard/Pages/ProjectDetails.razor` and `ContosoDashboard/Pages/Tasks.razor`; verify task access before showing or changing related documents.
- [ ] T032 [US4] Implement current-user recent-document and accessible-document-count queries in `ContosoDashboard/Services/DocumentService.cs`, returning at most five uploads newest-first and applying the same authorization policy as browsing.
- [ ] T033 [US4] Extend `DashboardSummary` and `IDashboardService` in `ContosoDashboard/Services/DashboardService.cs` with accessible document count and the current user's five latest uploads.
- [ ] T034 [US4] Display the document count and Recent Documents widget on `ContosoDashboard/Pages/Index.razor`, linking each result to an authorized document view.
- [ ] T035 [US4] Notify current project members other than the uploader when a clean project document is added by extending `ContosoDashboard/Services/NotificationService.cs`; do not notify users who are no longer project members.

**Checkpoint**: Task associations inherit the correct project, and dashboard data and project notifications are caller-scoped.

## Phase 6: Reporting, Documentation, and Validation

**Purpose**: Complete cross-cutting reporting, user guidance, and specified quality checks.

- [ ] T036 [US3] Implement Administrator-only aggregate reports for most-uploaded types, active uploaders, and document access patterns in `ContosoDashboard/Services/DocumentService.cs` and a dedicated `ContosoDashboard/Pages/DocumentReports.razor`; reporting access must not grant document-content access.
- [ ] T037 [P] Update `README.md` with document upload, private local storage, scanner prerequisites and fail-closed behavior, LocalDB `EnsureCreated()` recreation guidance, and remaining training-only limitations.
- [ ] T038 [P] Update `specs/001-document-upload-management/quickstart.md` with manual upload, validation, authorization, browse/search, preview/download, sharing/revocation, task, dashboard, reporting, scanner-failure, and storage-failure scenarios.
- [ ] T039 Verify list/search responsiveness for 500 documents, 25 MiB upload within 30 seconds, and PDF/image preview within 3 seconds using the specified local training setup; record any environment limitation rather than weakening the requirements.
- [ ] T040 Run `dotnet build` for `ContosoDashboard/ContosoDashboard.csproj` and complete the manual scenarios in `specs/001-document-upload-management/quickstart.md`; resolve build errors and document any scanner prerequisite that prevents local validation.

## Dependencies and Execution Order

### Phase Dependencies

- **Foundational Infrastructure (Phase 1)**: No user-story prerequisites; blocks all story work.
- **US1 (Phase 2)**: Depends on Phase 1; provides clean, stored documents.
- **US2 (Phase 3)**: Depends on Phase 1 and clean-document persistence from US1.
- **US3 (Phase 4)**: Depends on Phase 1 and document persistence from US1; sharing notifications use T029.
- **US4 (Phase 5)**: Depends on Phase 1 and document persistence from US1; task associations use T030 before task-page integration T031.
- **Reporting, Documentation, and Validation (Phase 6)**: Reporting depends on audit and document services; final validation depends on desired story implementation.

### Parallel Opportunities

- T001-T005 can be implemented in parallel as separate model files, followed by relationship configuration in T007.
- T008-T010 can proceed in parallel with model work; T011-T013 follow the required interfaces and models.
- Once the foundation is complete, US2 browse work and US3/US4 implementation can proceed in parallel where they touch separate files and services are coordinated.
- T037 and T038 can proceed in parallel with implementation after the documentation requirements are understood.

## Implementation Strategy

1. Complete Phase 1 and confirm the private storage and fail-closed scanner boundaries.
2. Deliver US1 as the MVP; verify clean upload and rejection behavior before proceeding.
3. Deliver US2, then US3 and US4; retain independent service authorization regardless of UI visibility.
4. Complete reporting and documentation, then run the build and manual quickstart scenarios.
