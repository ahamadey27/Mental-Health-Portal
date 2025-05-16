# Project: Mental Health Portal Prototype

**Goal:** To create a prototype web application using ASP.NET Core Minimal APIs, local AI processing (ML.NET, Ollama with Semantic Kernel), and local storage (SQLite, File System, Lucene.NET) to manage, analyze, search, and query mental health provider documents. This project serves to validate core RAG functionality, test document handling, and evaluate the chosen technology stack for a potential future production system, prioritizing zero-cost and local processing for the prototype phase.

---

# Components

## Environment/Hosting (Recommended Initial Setup)
- **Local Development Machine (Windows/macOS/Linux)**
  - .NET SDK (8.0 or higher recommended)
  - IDE: Visual Studio / Visual Studio Code
  - Git for version control
  - Ollama for local LLM hosting
- **Prototype Deployment Target (Initial):** Local Machine (Kestrel/IIS Express)
- **Database (Metadata):** SQLite file (stored within the application's local files)
- **Document Storage:** Local File System
- **Search Index:** Local File System (Lucene.NET directory)
- **Vector Store (for RAG):** In-memory (Semantic Kernel default) or Local File System (e.g., Lucene.NET directory if adapted)

## Software Components (Recommended Technologies)

### Web Application Backend
- **Framework:** ASP.NET Core Minimal APIs
- **Language:** C#
- **Potential UI Elements (Basic):** Razor Pages or Minimal API with HTML/JS for simple interfaces (e.g., document upload, search, chat).

### Document & Metadata Storage
- **Document Files:** Local File System
- **Metadata Database Engine:** SQLite
- **ORM:** Entity Framework Core (EF Core)
- **Provider:** `Microsoft.EntityFrameworkCore.Sqlite`

### Text Extraction
- **PDF:** PdfPig library
- **DOCX:** DocX library

### Search Indexing & Retrieval
- **Engine:** Lucene.NET (in-process library)

### AI-Powered Document Analysis
- **Local Machine Learning:** ML.NET (for keyword extraction, categorization)
- **Alternative/Supplementary (Cloud - for evaluation if local is insufficient):** Azure AI Language Free Tier (for keyword extraction/categorization, monitor limits strictly)

### LLM Integration (RAG)
- **Orchestration Framework:** Microsoft Semantic Kernel
- **Local LLM Hosting:** Ollama
- **LLM Model (Example):** Phi-3-mini, Mistral 7B (quantized versions like GGUF)
- **Vector Embeddings & Storage:** Semantic Kernel with local embedding generation (via Ollama or default) and InMemoryStore (or local file-based alternative for persistence if explored).

---

# Data Model (Database & Index Schemas)

## `DocumentMetadata` Table (SQLite)
- `Id` (int, Primary Key, Auto-increment)
- `OriginalFileName` (string, Required)
- `StoredFileName` (string, Required, Unique) // To avoid conflicts
- `StoragePath` (string, Required) // Path to the document on the local file system
- `DocumentType` (string, Required - e.g., "PDF", "DOCX")
- `UploadTimestamp` (datetime, Required)
- `ExtractedTextLength` (int, Nullable) // Length of extracted text
- `Keywords` (string, Nullable) // Comma-separated or JSON array of extracted keywords
- `AssignedCategory` (string, Nullable) // Assigned category from AI analysis
- `LastAnalyzedTimestamp` (datetime, Nullable)

## Lucene.NET Index Fields
- `document_id` (string, Stored, Indexed - links to `DocumentMetadata.Id`)
- `filename` (string, Stored, Indexed)
- `content` (string, Indexed, Term Vectors for snippets if needed)
- `doc_type` (string, Stored, Indexed - as keyword)
- `keywords_facet` (string, Indexed - for faceted search if explored later)
- `category_facet` (string, Indexed - for faceted search if explored later)

## Vector Store Chunks (Conceptual Schema for InMemoryStore or similar)
- `ChunkId` (string, Primary Key)
- `DocumentId` (string, Foreign Key linking to `DocumentMetadata.Id`)
- `ChunkText` (string, The actual text chunk)
- `EmbeddingVector` (array of float, The semantic vector)
- `OriginalDocumentOrder` (int, To maintain order of chunks from a document)

---

# Development Plan (MVP - Recommended Approach)

## Phase 1: Project Setup & Core Document Handling
- [x] **Environment Setup:**
    - [x] Install .NET SDK (8.0 or higher).
    - [x] Install preferred IDE (Visual Studio / VS Code).
    - [x] Initialize Git repository for version control.
- [x] **Project Initialization:**
    - [x] Create new ASP.NET Core project (e.g., "Empty" or "Web API" template for Minimal APIs).
    - [x] Set up basic project structure (e.g., `Endpoints`, `Services`, `Models`, `Data` folders).
- [x] **Document Upload Implementation:**
    - [x] Create basic UI for document upload (e.g., HTML form with `<input type="file">`, JavaScript for drag-and-drop).
    - [x] Implement backend endpoint (e.g., dedicated MVC Controller or Razor Page handler) to receive `IFormFile` for PDF and DOCX files.
    - [x] Implement logic for generating unique stored filenames and saving files to a configured local file system directory.
- [x] **Testing PART 1:**
    - [x] **Test the File Upload:**
        - [x] On the `index.html` page, choose a small PDF or DOCX file using the "Choose document" input.
        - [x] Click the "Upload" button.
        - [x] Check for a success message on the webpage (e.g., "File 'yourfile.pdf' uploaded successfully and saved as 'unique-guid.pdf'.").
        - [x] Check your file system: Look in the `c:\\Users\\hamad\\Documents\\GitHub\\Mental-Health-Portal\\MentalHealthPortal\\UploadedDocuments` folder. The uploaded file (with its new unique name) should be there.
        - [x] **Check Browser Developer Tools (Optional but Recommended):**
            - [x] Press F12 in your browser to open developer tools.
            - [x] Go to the "Network" tab.
            - [x] Upload a file again. You should see a request to `/api/documents/upload`. Click on it to see the request details, headers, and the JSON response from your server. This is very helpful for debugging.
        - [ ] **Check Application Output (Terminal/VS Output Window):**
            - [x] Look at the console output where `dotnet run` is executing (or the Visual Studio Output window). If there were errors during file saving, you should see the `Console.WriteLine` messages from your catch blocks in `DocumentUploadEndpoints.cs`.
- [x] **Metadata Storage Setup:**
    - [x] Integrate Entity Framework Core with Nuget Packages `Microsoft.EntityFrameworkCore.Sqlite` and 'Microsoft.EntityFrameworkCore.Design'
    - [x] Define `DocumentMetadata.cs` entity model class with appropriate properties and validation attributes.
    - [x] Create `ApplicationDbContext` inheriting from `DbContext`.
    - [x] Configure EF Core for SQLite connection in `appsettings.json` (or directly in `Program.cs` for simplicity).
    - [x] Add EF Core migrations to create the initial `DocumentMetadata` table.
    - [x] Apply migrations (`dotnet ef database update`).
    - [x] Implement service logic to save document metadata upon successful upload.

## Phase 2: Text Extraction & Search Implementation
- [x] **Text Extraction Integration:**
    - [x] Add NuGet packages: `PdfPig` and `DocX`.
    - [x] Implement a service to extract text content from uploaded PDF files using PdfPig.
    - [x] Implement a service to extract text content from uploaded DOCX files using DocX.
    - [x] Integrate text extraction into an asynchronous background process triggered after document upload and successful storage.
- [x] **Local Search Indexing Setup (Lucene.NET):**
    - [x] Add NuGet package: `Lucene.Net`.
    - [x] Design and implement `IndexService` for managing Lucene.NET index:
        - [x] Configure `FSDirectory` to store index files in a local directory.
        - [x] Define Lucene `Document` schema (fields: `document_id`, `filename`, `content`, `doc_type`).
        - [x] Implement logic to add/update documents in the index using `IndexWriter` after text extraction.
        - [x] Choose and configure an appropriate `Analyzer` (e.g., `StandardAnalyzer`).
- [ ] **Basic Search Functionality:**
    - [x] Implement search logic in `IndexService` using `IndexSearcher` and `QueryParser`.
    - [x] Create a Minimal API endpoint (e.g., `GET /api/search`) that accepts keywords and optional document type filter.
    - [ ] Develop a basic Search UI:
        - [ ] Text input for search queries.
        - [ ] Option to filter by document type (PDF/DOCX).
        - [ ] Display search results (e.g., list of matching original filenames).
        - [ ] Allow users to click a result to download/open the original document.

## Phase 3: Basic AI Analysis Integration
- [ ] **Asynchronous AI Analysis Framework:**
    - [ ] Ensure AI analysis tasks (keyword extraction, categorization) are triggered by the background processing service after text extraction.
- [ ] **Keyword Extraction (Local ML.NET):**
    - [ ] Add NuGet package: `Microsoft.ML`.
    - [ ] Implement a basic keyword extraction mechanism using ML.NET:
        - [ ] Preprocess extracted text (e.g., stop-word removal, stemming - optional).
        - [ ] Apply TF-IDF (Term Frequency-Inverse Document Frequency) or similar text featurization.
        - [ ] Extract top N keywords based on scores.
    - [ ] Store extracted keywords in the `DocumentMetadata` table.
    - [ ] Optional: Evaluate Azure AI Language Key Phrase Extraction free tier if local results are insufficient (ensure API key management and usage monitoring).
- [ ] **Content-Based Categorization (Local ML.NET):**
    - [ ] Define a simple set of target categories (e.g., "Intake", "Progress Note", "General").
    - [ ] Prepare a small, labeled dataset of example document texts for these categories.
    - [ ] Implement a text classification pipeline using ML.NET:
        - [ ] Load labeled data.
        - [ ] Featurize text and map labels.
        - [ ] Train a classification model (e.g., `TextClassification` trainer).
        - [ ] Use the trained model to predict the category for new documents.
    - [ ] Store the predicted category in the `DocumentMetadata` table.
    - [ ] Optional: Evaluate Azure AI Language Custom Text Classification free tier if local model training is too complex or results are poor (requires data upload for training).

## Phase 4: LLM Interaction (RAG) Setup
- [ ] **Local LLM Environment Setup:**
    - [ ] Install Ollama on the development machine.
    - [ ] Download a suitable LLM (e.g., `phi3:mini`, `mistral:7b-instruct-q4_K_M`) via Ollama.
    - [ ] Verify Ollama is serving the model locally (e.g., `http://localhost:11434`).
- [ ] **Microsoft Semantic Kernel Integration:**
    - [ ] Add NuGet packages for Semantic Kernel (e.g., `Microsoft.SemanticKernel.Core`, `Microsoft.SemanticKernel.Connectors.Ollama`, `Microsoft.SemanticKernel.Memory.Volatile`).
    - [ ] Configure Semantic Kernel services in `Program.cs`.
    - [ ] Set up connection to the local Ollama LLM for chat completion.
    - [ ] Set up an embedding generation client (e.g., using Ollama or a default Semantic Kernel embedder).
- [ ] **RAG Pipeline Implementation:**
    - [ ] Implement text chunking for extracted document content (e.g., fixed size, sentence-based).
    - [ ] For each chunk, generate embeddings using the configured client.
    - [ ] Store text chunks and their embeddings in an `InMemoryStore` (Semantic Kernel's volatile memory).
    - [ ] Implement retrieval logic:
        - [ ] When a user query is received, generate an embedding for the query.
        - [ ] Search the `InMemoryStore` for the most semantically similar text chunks.
    - [ ] Implement prompt engineering: Construct a prompt for the LLM including the user's query and the retrieved context chunks, instructing it to answer based only on the provided context.
    - [ ] Send the augmented prompt to the local LLM via Semantic Kernel and get the response.
- [ ] **Basic Chat UI Development:**
    - [ ] Create a simple UI for chat interaction:
        - [ ] Text input for user questions.
        - [ ] Display area for the conversation history (user questions and LLM answers).

## Phase 5: Initial Testing Checklist (MVP Functionality)
- [ ] Project builds and runs locally without critical errors.
- [ ] **Document Handling:**
    - [ ] Document upload (PDF, DOCX) is successful.
    - [ ] Files are correctly stored in the configured local file system location.
    - [ ] Document metadata (filename, type, path, timestamp) is saved accurately in the SQLite database.
- [ ] **Text Extraction:**
    - [ ] Text is successfully extracted from PDF documents.
    - [ ] Text is successfully extracted from DOCX documents.
    - [ ] Extraction process runs asynchronously without blocking upload.
- [ ] **Search & Retrieval (Lucene.NET):**
    - [ ] Lucene.NET index is created/updated after text extraction.
    - [ ] Basic keyword search returns relevant document filenames.
    - [ ] Filtering search results by document type (PDF/DOCX) functions correctly.
    - [ ] Users can access/download the original document from search results.
- [ ] **AI Analysis (Basic):**
    - [ ] Keyword extraction populates keywords in the metadata (qualitatively check relevance).
    - [ ] Document categorization assigns a category in the metadata (qualitatively check accuracy against simple cases).
    - [ ] AI analysis runs asynchronously.
- [ ] **LLM Interaction (RAG):**
    - [ ] User questions to the chat interface trigger the RAG pipeline.
    - [ ] Relevant text chunks are retrieved from uploaded documents based on query similarity.
    - [ ] The local LLM (Ollama) generates answers based on the provided document context.
    - [ ] LLM correctly indicates when an answer cannot be found in the provided documents.
    - [ ] Chat interface displays the conversation flow.
- [ ] **Input Validation (Basic):**
    - [ ] System handles unsupported file types gracefully during upload (e.g., user notification).

## Phase 6: Prototype Documentation & Deployment Strategy Outline
- [ ] **SPEC.md Finalization:**
    - [ ] Continuously update this `SPEC.md` document throughout development with any changes, decisions, or detailed API endpoint definitions.
    - [ ] Review and finalize `SPEC.md` to accurately reflect the implemented MVP.
- [ ] **Local Deployment & Testing:**
    - [ ] Ensure the prototype is fully runnable and testable on a local development machine (primary goal for this prototype phase given local LLM).
- [ ] **Outline Optional Cloud Deployment Test (for Application Components, *excluding Local LLM*):**
    - [ ] (If explored) Document steps to deploy the ASP.NET Core application to Azure App Service Free Tier (F1).
    - [ ] (If explored) Document steps to configure with Azure SQL Database Free Tier and Azure Blob Storage Free Tier.
    - [ ] (If explored) Document steps to temporarily switch Semantic Kernel to use a cloud-based LLM API Free Tier (e.g., Google Gemini) for this test, noting this is not the primary LLM strategy for the prototype.
    - [ ] (If explored) Verify core application functionality (upload, non-LLM analysis, search) on the Azure test deployment.
- [ ] **Summarize Deferred Features:**
    - [ ] Reiterate key features out of scope for MVP (HIPAA compliance, robust security, user accounts/roles, scalability optimizations, advanced AI, OCR, etc.) for clarity on future work.

---

# General Notes (Placeholder)
- Ensure all local paths for storage (documents, SQLite DB, Lucene Index) are configurable (e.g., via `appsettings.json`).
- Prioritize functional PoC over UI/UX polish for the prototype.
- Log key operations and errors for easier debugging during development.