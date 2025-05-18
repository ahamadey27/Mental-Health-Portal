# Project: Mental Health Portal Prototype (MVP - Session-Only)

**Goal:** To create a Minimum Viable Product (MVP) web application using ASP.NET Core Minimal APIs and in-memory Lucene.NET for document management and search. This project serves as a portfolio piece for a junior developer, focusing on core ASP.NET Core and basic search functionality. **Uploaded documents and their search index are session-only**: they are forgotten if the application restarts or the browser session ends.

---

# Components (MVP - Session-Only)

## Environment/Hosting
- **Local Development Machine (Windows/macOS/Linux)**
  - .NET SDK (8.0 or higher recommended)
  - IDE: Visual Studio / Visual Studio Code
  - Git for version control
- **Prototype Deployment Target:** Local Machine (Kestrel/IIS Express)
- **Database (Metadata):** **None for MVP.** Metadata will be handled in-memory or implicitly via Lucene document fields for the session.
- **Document Storage:** **In-memory representation for the session.** Original files are processed and then discarded if not needed beyond text extraction.
- **Search Index:** **In-memory Lucene.NET `RAMDirectory`.**
- **Vector Store (for RAG):** **Out of scope for MVP.**

## Software Components (MVP - Session-Only)

### Web Application Backend
- **Framework:** ASP.NET Core Minimal APIs
- **Language:** C#
- **UI Elements (Basic):** Razor Pages or Minimal API with HTML/JS for:
    - Document upload form.
    - Success message display (including unique document name).
    - Simple search input (keywords).
    - Basic table display for search results.

### Document & Metadata Storage
- **Document Files:** Processed in-memory during upload; not persistently stored on file system for MVP.
- **Metadata Database Engine:** **None for MVP.**
- **ORM:** **None for MVP.**

### Text Extraction
- **PDF:** PdfPig library
- **DOCX:** DocX library

### Search Indexing & Retrieval
- **Engine:** Lucene.NET (in-process library, using `RAMDirectory`)

### AI-Powered Document Analysis
- **Local Machine Learning:** **Out of scope for MVP.**
- **LLM Integration (RAG):** **Out of scope for MVP.**

---

# Data Model (MVP - Session-Only)

## `DocumentMetadata` Class (In-Memory Model)
- `Id` (string, e.g., `Guid.NewGuid().ToString()`, generated on upload)
- `OriginalFileName` (string, Required)
- `DocumentType` (string, Required - e.g., "PDF", "DOCX")
- `UploadTimestamp` (datetime, Required)
- `ExtractedText` (string, held temporarily for indexing, not stored long-term in this model if RAM is a concern, but indexed into Lucene)

*Note: This class is primarily used to pass data between services during the upload and indexing process. It is not persisted.* 

## Lucene.NET Index Fields (In-Memory `RAMDirectory`)
- `document_id` (string, Stored, Indexed - unique ID, e.g., `Guid.NewGuid().ToString()`)
- `filename` (string, Stored, Indexed)
- `content` (string, Indexed, **Not Stored** - text extracted from document for searching)
- `doc_type` (string, Stored, Indexed - as keyword)

## Vector Store Chunks
- **Out of scope for MVP.**

---

# Development Plan (MVP - Session-Only)

## Phase 1: Core Document Handling & In-Memory Search (MVP Focus)
- [x] **Environment Setup:**
    - [x] Install .NET SDK (8.0 or higher).
    - [x] Install preferred IDE (Visual Studio / VS Code).
    - [x] Initialize Git repository for version control.
- [x] **Project Initialization:**
    - [x] Create new ASP.NET Core project (e.g., "Empty" or "Web API" template for Minimal APIs).
    - [x] Set up basic project structure (e.g., `Endpoints`, `Services`, `Models` folders).
- [x] **Document Upload Implementation (Session-Only):**
    - [x] Create basic UI for document upload (e.g., HTML form with `<input type="file">`).
    - [x] Implement backend endpoint to receive `IFormFile`.
    - [x] Implement `TextExtractionService` to extract text from PDF and DOCX files.
    - [x] Display success message on UI upon document upload, including a unique document name/identifier.
- [x] **In-Memory Search Indexing (Lucene.NET `RAMDirectory`):**
    - [x] Implement `IndexService` using `RAMDirectory`.
    - [x] On document upload, after text extraction, add document content to the in-memory Lucene index.
        - Fields: `document_id` (generated unique ID), `filename`, `content` (extracted text), `doc_type`.
- [ ] **Search UI & API (Session-Only):**
    - [x] Create a simple search UI (text input for keywords, optional filter for document type).
    - [ ] Implement a Minimal API endpoint (e.g., `GET /api/search?keywords=...&docType=...`) that uses `IndexService` to query the `RAMDirectory`.
    - [ ] Display search results in a basic table format on the UI (e.g., FileName, DocType).
- [ ] **Testing (MVP Workflow):**
    - [ ] Test document upload for PDF and DOCX files.
    - [ ] Verify success messages and unique identifiers.
    - [ ] Test search functionality with keywords expected to be in uploaded documents.
    - [ ] Verify search results are displayed correctly.
    - [ ] Verify data is session-only (restarting application clears documents and index).

## Phase 2: UI/UX Enhancements (Post-MVP, Optional)
- Improve visual styling of the UI.
- Add more robust error handling and user feedback.
- Implement client-side validation for uploads.

## Phase 3: Basic AI Analysis Integration (Out of Scope for MVP)
- This phase, involving ML.NET, Ollama, Semantic Kernel, and persistent storage (SQLite, File System for documents/index), is deferred.

---

## Key Changes for MVP (from original spec):
*   **No Database:** SQLite and EF Core are removed. Metadata is transient.
*   **No Persistent File Storage for Documents:** Documents are processed in memory.
*   **In-Memory Search Index:** Lucene.NET uses `RAMDirectory`, not `FSDirectory`.
*   **Session-Only Data:** All uploaded documents and their searchability are lost on application restart or session end.
*   **No AI Features:** ML.NET, Ollama, Semantic Kernel integration is out of scope.
*   **Simplified `DocumentMetadata`:** The model is simplified and used for transient data transfer, not persistence.
*   **Focus:** Core ASP.NET, file upload, text extraction, and basic in-memory Lucene search.