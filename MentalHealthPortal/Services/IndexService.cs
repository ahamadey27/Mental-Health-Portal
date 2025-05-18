using Lucene.Net.Store;         // For Directory, FSDirectory, RAMDirectory
using Lucene.Net.Analysis.Standard; // For StandardAnalyzer
using Lucene.Net.Util;          // For LuceneVersion
using Lucene.Net.Index;         // For IndexWriter, IndexWriterConfig, OpenMode, Term
using Lucene.Net.Documents;     // For Document, Field, etc. (will be used soon)
using Lucene.Net.Search;        // For IndexSearcher, Query, TopDocs, ScoreDoc etc.
using Lucene.Net.QueryParsers.Classic; // For QueryParser
using System.Collections.Generic; // For List<T>
using MentalHealthPortal.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using Microsoft.Extensions.Logging; // Add for ILogger
using Microsoft.AspNetCore.Hosting; // Add for IWebHostEnvironment

namespace MentalHealthPortal.Services
{
    public class IndexService : IDisposable
    {
        public const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        private readonly RAMDirectory _indexDirectory; // Changed from FSDirectory
        private readonly StandardAnalyzer _analyzer;
        private readonly IndexWriter _writer;
        private readonly ILogger<IndexService> _logger;

        // Constructor for RAMDirectory
        public IndexService(ILogger<IndexService> logger)
        {
            _logger = logger;
            _logger.LogInformation("Initializing IndexService with RAMDirectory.");

            _indexDirectory = new RAMDirectory();
            _analyzer = new StandardAnalyzer(AppLuceneVersion);

            var writerConfig = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND // CREATE_OR_APPEND is fine for RAMDirectory, effectively CREATE on new RAMDirectory()
            };
            _writer = new IndexWriter(_indexDirectory, writerConfig);

            // For RAMDirectory, an initial commit might still be good practice if IndexExists is used,
            // or to ensure it's fully initialized before first search.
            // However, a brand new RAMDirectory is empty and ready.
            // Let's ensure it's "initialized" by a commit if we expect IndexExists to work immediately.
            // A newly instantiated RAMDirectory won't have segments until a commit.
            if (!DirectoryReader.IndexExists(_indexDirectory)) // This check might be less critical for RAM dir but good for consistency
            {
                _logger.LogInformation("Performing initial commit on new RAMDirectory.");
                _writer.Commit();
            }
            _logger.LogInformation("IndexService with RAMDirectory initialized.");
        }

        // Remove constructor that used IConfiguration and IWebHostEnvironment for FSDirectory

        public void Dispose()
        {
            _writer?.Dispose();
            _analyzer?.Dispose();
            _indexDirectory?.Dispose();
            _logger.LogInformation("IndexService disposed (RAMDirectory).");
        }

        // AddOrUpdateDocument method to add or update documents in the Lucene index
        public void AddOrUpdateDocument(DocumentMetadata metadata, string extractedText)
        {
            if (metadata == null)
            {
                _logger.LogError("AddOrUpdateDocument received null metadata.");
                throw new ArgumentNullException(nameof(metadata));
            }
            // metadata.Id is now a string, no need for ToString()
            string documentIdString = metadata.Id;

            if (string.IsNullOrEmpty(documentIdString))
            {
                _logger.LogError("AddOrUpdateDocument received metadata with null or empty Id.");
                throw new ArgumentException("Metadata.Id cannot be null or empty for indexing.", nameof(metadata.Id));
            }

            _logger.LogInformation("Attempting to add/update document Id: {DocumentId}, FileName: '{OriginalFileName}' to RAM Lucene index.", documentIdString, metadata.OriginalFileName);
            _logger.LogDebug("Extracted text for document Id {DocumentId}: '{ExtractedText}'", documentIdString, extractedText);

            if (string.IsNullOrEmpty(extractedText))
            {
                _logger.LogWarning("Extracted text for document Id {DocumentId} ('{OriginalFileName}') is empty. Indexing with empty content field.", documentIdString, metadata.OriginalFileName);
                extractedText = string.Empty; // Ensure it's not null
            }

            var luceneDoc = new Document
            {
                new StringField("document_id", documentIdString, Field.Store.YES),
                new TextField("filename", metadata.OriginalFileName ?? "Unknown", Field.Store.YES),
                new TextField("content", extractedText, Field.Store.NO), // Content is indexed but not stored
                new StringField("doc_type", metadata.DocumentType ?? "Unknown", Field.Store.YES)
            };
            
            _logger.LogDebug("Lucene document fields for Id {DocumentId}: document_id='{DocumentIdInDoc}', filename='{FileName}', doc_type='{DocType}', content_length={ContentLength}", 
                documentIdString, luceneDoc.Get("document_id"), luceneDoc.Get("filename"), luceneDoc.Get("doc_type"), extractedText.Length);

            try
            {
                _writer.UpdateDocument(new Term("document_id", documentIdString), luceneDoc);
                _logger.LogInformation("Document Id {DocumentId} ('{OriginalFileName}') prepared for update/add.", documentIdString, metadata.OriginalFileName);
                _writer.Commit(); 
                _logger.LogInformation("SUCCESS: Document Id {DocumentId} ('{OriginalFileName}') committed to RAM Lucene index.", documentIdString, metadata.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR adding/updating document \'{OriginalFileName}\' to RAM Lucene index: {ErrorMessage}", metadata.OriginalFileName, ex.Message);
            }
        }

        public List<SearchResultItem> Search(string searchTerm, string? docTypeFilter = null)
        {
            _logger.LogInformation("Search requested for term '{SearchTerm}' with docTypeFilter '{DocTypeFilter}' in RAM index", searchTerm, docTypeFilter);
            var results = new List<SearchResultItem>();

            if (_writer == null || _indexDirectory == null) {
                 _logger.LogWarning("Search attempted on uninitialized RAMDirectory or writer.");
                return results;
            }
            
            try
            {
                using var reader = DirectoryReader.Open(_indexDirectory); // Open reader on current state of RAMDirectory
                var searcher = new IndexSearcher(reader);
                
                // Ensure analyzer matches the one used at indexing time
                var queryParser = new QueryParser(AppLuceneVersion, "content", _analyzer);
                queryParser.AllowLeadingWildcard = true; // Allow leading wildcards for more flexible searches if needed

                Query query;
                try
                {
                    // Attempt to parse the raw search term first
                    query = queryParser.Parse(searchTerm);
                    _logger.LogInformation("Parsed query (raw): {ParsedQuery}", query.ToString());
                }
                catch (ParseException)
                {
                    _logger.LogWarning("Raw search term parse failed for: '{SearchTerm}'. Attempting with escaped wildcards.", searchTerm);
                    try
                    {
                        // Fallback to escaping and adding wildcards if direct parsing fails
                        string escapedSearchTerm = QueryParserBase.Escape(searchTerm);
                        query = queryParser.Parse("*" + escapedSearchTerm + "*");
                        _logger.LogInformation("Parsed query (escaped with wildcards): {ParsedQuery}", query.ToString());
                    }
                    catch (ParseException exWild)
                    {
                        _logger.LogError(exWild, "Could not parse search term (even with escaped wildcards): {SearchTerm}", searchTerm);
                        return results;
                    }
                }

                if (!string.IsNullOrEmpty(docTypeFilter))
                {
                    var booleanQuery = new BooleanQuery();
                    booleanQuery.Add(query, Occur.MUST); // Original search query

                    var docTypeTermQuery = new TermQuery(new Term("doc_type", docTypeFilter));
                    booleanQuery.Add(docTypeTermQuery, Occur.MUST); // Filter by document type
                    query = booleanQuery;
                    _logger.LogInformation("Applied docTypeFilter. Combined query: {CombinedQuery}", query.ToString());
                }
                
                _logger.LogDebug("Executing search with query: {FinalQuery}", query.ToString());
                var topDocs = searcher.Search(query, n: 10); // Get top 10 results
                _logger.LogInformation("Search found {HitCount} documents for term \'{SearchTerm}\'.", topDocs.TotalHits, searchTerm);

                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    var documentIdString = doc.Get("document_id"); 
                    // Assuming document_id is now a string (e.g. Guid.ToString()) for in-memory model
                    // If you need to convert back to Guid: Guid.TryParse(documentIdString, out var parsedGuid)

                    results.Add(new SearchResultItem
                    {
                        // For MVP, documentId might just be the original filename if that's how we retrieve/show it
                        DocumentId = documentIdString, // Storing as string
                        FileName = doc.Get("filename"),
                        DocType = doc.Get("doc_type"),
                        Score = scoreDoc.Score,
                    });
                }
            }
            catch(IndexNotFoundException indexNotFoundEx)
            {
                // This should be less likely with RAMDirectory if an initial commit is done,
                // but could happen if Open is called too early or on a disposed directory.
                _logger.LogWarning(indexNotFoundEx, "IndexNotFoundException during search in RAMDirectory. This might mean no documents have been indexed or an initial commit was missed.");
                return results; // Return empty list
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during search in RAMDirectory for term \'{SearchTerm}\'.", searchTerm);
                // Depending on the exception, you might want to return empty or rethrow
                return results; // Return empty list on other errors
            }
            return results;
        }
    }
}
