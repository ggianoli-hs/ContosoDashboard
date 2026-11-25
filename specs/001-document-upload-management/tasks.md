# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-upload-management/`
**Prerequisites**: `plan.md`, `spec.md`, `data-model.md`, `contracts/document-management.md`

**Tests**: No automated test project is planned, and the specification does not explicitly request adding tests. Final validation uses the existing build and manual quickstart scenarios.

**Scope**: Implement the offline training application with LocalDB and private local file storage, without malware scanning. Uploads must be explicitly labeled unscanned and the lack of malware detection disclosed. Azure scanning is future-only.

**Revision status**: The local upload implementation no longer invokes a scanner and records new uploads as `Unscanned`. Later story tasks remain outside the MVP implementation scope.

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
- [ ] T010 Remove the local MVP scanner dependency and registration from `ContosoDashboard/Services/DocumentService.cs`, `ContosoDashboard/Program.cs`, and `ContosoDashboard/appsettings.json`; do not substitute a fake scanner. Disclose unscanned training uploads on `ContosoDashboard/Pages/DocumentUpload.razor`, `ContosoDashboard/Pages/Documents.razor`, and `README.md` per revised FR-007.
- [ ] T011 Define document input, filter, result, and service contracts in `ContosoDashboard/Services/IDocumentService.cs`; every operation receiving a caller-controlled resource ID must receive or resolve the authenticated caller identity.
- [ ] T012 Register document services and local storage in `ContosoDashboard/Program.cs` and `ContosoDashboard/appsettings.json`; do not register a scanner or add cloud or external runtime dependencies.
- [ ] T013 Implement shared document authorization and audit persistence in `ContosoDashboard/Services/DocumentService.cs`, applying owner, project-member/manager, and active-share rules from `data-model.md`; ensure audit events never grant content access.

**Checkpoint**: Data model, private storage, service contracts, authorization, and unscanned-training disclosures are ready for story implementation.

## Phase 2: User Story 1 - Upload and Organize a Document (Priority: P1)

**Goal**: An authorized employee uploads supported files with required metadata without a scanner prerequisite; successful uploads are available as unscanned training documents.

**Independent Test**: With no scanner configured, upload a trusted training file and verify its metadata and private stored content with `Unscanned` state. Attempt unsupported, empty, oversized, unauthorized, and storage-failing uploads; verify none is reported as successful or exposed. No download/preview endpoint is required at this checkpoint.

- [ ] T014 [US1] Implement per-file upload validation and local upload orchestration in `ContosoDashboard/Services/DocumentService.cs`: require title/category; enforce allowed extensions and 25 MiB maximum; reject empty files; derive MIME type server-side; authorize associations; stage and promote privately without scanning; commit metadata with `Document.ScanState = Unscanned` and the upload audit event; compensate storage/database failures. Apply the exact `Document.Title`, `Document.Category`, `Document.SizeBytes`, `Document.ContentType`, `Document.OriginalFileName`, `Document.RelativeStoragePath`, and `Document.ScanState` definitions in `data-model.md`; never mark an unscanned upload `Clean`.
- [ ] T015 [US1] Implement tag trimming, normalization, duplicate elimination, and persistence in `ContosoDashboard/Services/DocumentService.cs` using `DocumentTag.Tag` and `DocumentTag.NormalizedTag` constraints from `data-model.md`.
- [ ] T016 [US1] Create the authorized multi-file upload page in `ContosoDashboard/Pages/DocumentUpload.razor` with required title/category, optional description/project/tags, accepted file types, per-file progress and results, actionable validation feedback, and reset file-input state after success.
- [ ] T017 [US1] Add My Documents and navigation in `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Shared/NavMenu.razor`; show only successfully persisted documents uploaded by the caller, including `Unscanned` training documents, with an explicit unscanned warning. Keep pending/rejected/failed records hidden.

**Checkpoint**: Without a scanner installed or configured, a valid authorized upload is stored and discoverable as unscanned; rejected or incomplete uploads are never presented as successful.

## Phase 3: User Story 2 - Browse, Search, and Download Documents (Priority: P1)

**Goal**: Employees and project team members can discover and retrieve only documents they are authorized to access.

**Independent Test**: Find and retrieve an authorized document through My Documents, project browsing, and search; verify an unauthorized user cannot discover its metadata or content.

- [ ] T018 [US2] Implement authorized My Documents and project-document queries in `ContosoDashboard/Services/DocumentService.cs`, including title/category/date/size/project filters, date range, and sorting; use explicit eager loading and the indexes configured in T007.
- [ ] T019 [US2] Implement authorized search in `ContosoDashboard/Services/DocumentService.cs` across title, description, tags, uploader display name, and project name, applying the access predicate and allowing persisted `Unscanned` training documents while excluding pending/rejected/failed records.
- [ ] T020 [US2] Implement document browse, filter, sort, search, and Shared with Me list views in `ContosoDashboard/Pages/Documents.razor`; display title, category, upload date, size, and project.
- [ ] T021 [US2] Add authorized download and PDF/JPEG/PNG preview endpoints in `ContosoDashboard/Program.cs` using the contract routes `/documents/{documentId:int}/download` and `/documents/{documentId:int}/preview`; delegate authorization and stream access to `IDocumentService`, return the specified status behavior, set safe content headers, and never expose physical storage paths.
- [ ] T022 [US2] Add download and preview actions to `ContosoDashboard/Pages/Documents.razor`, allowing preview only for supported preview MIME types and handling missing, unauthorized, and storage-failure responses without leaking resource details.
- [ ] T023 [US2] Record successful and failed download outcomes in `ContosoDashboard/Services/DocumentService.cs` using the `DocumentAuditEvent` rules from `data-model.md`.

**Checkpoint**: Browse, search, preview, and download return only available documents the current user can access; unscanned training files are not represented as clean.

## Phase 4: User Story 3 - Manage and Share Documents (Priority: P2)

**Goal**: Owners and authorized project managers can manage document metadata, replacement, deletion, and sharing without weakening access controls.

**Independent Test**: Update and replace an owned document, share and revoke access, and delete an authorized document; verify permissions are rechecked in the service and storage cleanup failures are surfaced.

- [ ] T024 [US3] Implement owner-authorized metadata editing in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/Documents.razor` for title, description, category, and tags only; enforce the corresponding `Document` and `DocumentTag` constraints in `data-model.md`.
- [ ] T025 [US3] Implement owner-authorized replacement in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/Documents.razor`; validate and stage without scanning, commit the new reference as `Unscanned`, clean the previous file after success, and preserve the existing document on validation/storage/persistence failure.
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
- [ ] T035 [US4] Notify current project members other than the uploader after a project document is successfully persisted by extending `ContosoDashboard/Services/NotificationService.cs`; do not notify users who are no longer project members.

**Checkpoint**: Task associations inherit the correct project, and dashboard data and project notifications are caller-scoped.

## Phase 6: Reporting, Documentation, and Validation

**Purpose**: Complete cross-cutting reporting, user guidance, and specified quality checks.

- [ ] T036 [US3] Implement Administrator-only aggregate reports for most-uploaded types, active uploaders, and document access patterns in `ContosoDashboard/Services/DocumentService.cs` and a dedicated `ContosoDashboard/Pages/DocumentReports.razor`; reporting access must not grant document-content access.
- [ ] T037 [P] Update `README.md` with document upload, private local storage, the lack of malware scanning, LocalDB `EnsureCreated()` recreation guidance, and remaining training-only limitations.
- [ ] T038 [P] Update `specs/001-document-upload-management/quickstart.md` with manual unscanned training upload, validation, authorization, browse/search, preview/download, sharing/revocation, task, dashboard, reporting, and storage-failure scenarios.
- [ ] T039 Verify list/search responsiveness for 500 documents, 25 MiB upload within 30 seconds, and PDF/image preview within 3 seconds using the specified local training setup; record any environment limitation rather than weakening the requirements.
- [ ] T040 Run `dotnet build` for `ContosoDashboard/ContosoDashboard.csproj` and complete the manual scenarios in `specs/001-document-upload-management/quickstart.md`; resolve errors and record remaining environment limitations. The local training MVP requires no scanner.

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

1. Complete Phase 1 and confirm private storage, authorization, and explicit unscanned-training disclosures.
2. Deliver US1 as the MVP; verify uploads succeed without a scanner and validation/storage failures are rejected.
3. Deliver US2, then US3 and US4; retain independent service authorization regardless of UI visibility.
4. Complete reporting and documentation, then run the build and manual quickstart scenarios.
