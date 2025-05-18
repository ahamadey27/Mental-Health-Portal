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
                new StringField("doc_type", metadata.DocumentType ?? "Unknown", Field.Store.YES),
                // Store the StoredFileName if available, otherwise use OriginalFileName as a fallback for the link
                new StringField("stored_filename", metadata.StoredFileName ?? metadata.OriginalFileName ?? "Unknown", Field.Store.YES)
            };
            
            _logger.LogDebug("Lucene document fields for Id {DocumentId}: document_id='{DocumentIdInDoc}', filename='{FileName}', doc_type='{DocType}', stored_filename='{StoredFileName}', content_length={ContentLength}", 
                documentIdString, luceneDoc.Get("document_id"), luceneDoc.Get("filename"), luceneDoc.Get("doc_type"), luceneDoc.Get("stored_filename"), extractedText.Length);

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

        public List<SearchResultItem> Search(string keywords, string? docTypeFilter = null)
        {
            _logger.LogInformation("Search initiated with keywords: '{Keywords}', docTypeFilter: '{DocTypeFilter}'", keywords, docTypeFilter ?? "N/A");
            var results = new List<SearchResultItem>();

            if (string.IsNullOrWhiteSpace(keywords) && string.IsNullOrWhiteSpace(docTypeFilter))
            {
                _logger.LogWarning("Search attempted with empty keywords and no document type filter.");
                return results; // Return empty list if no search criteria
            }

            // Corrected: Use DirectoryReader.IndexExists with the directory itself
            if (!DirectoryReader.IndexExists(_indexDirectory)) 
            {
                _logger.LogWarning("Search attempted but Lucene index does not exist or is empty.");
                return results;
            }

            using var reader = _writer.GetReader(applyAllDeletes: true);
            var searcher = new IndexSearcher(reader);
            
            var booleanQuery = new BooleanQuery();

            // Keyword query for 'content' field
            if (!string.IsNullOrWhiteSpace(keywords))
            {
                var queryParser = new QueryParser(AppLuceneVersion, "content", _analyzer);
                try
                {
                    Query keywordQuery = queryParser.Parse(QueryParserBase.Escape(keywords)); // Escape special characters
                    booleanQuery.Add(keywordQuery, Occur.MUST); // MUST: keywords must be present
                    _logger.LogDebug("Keyword query parsed: {KeywordQuery}", keywordQuery.ToString());
                }
                catch (ParseException ex)
                {
                    _logger.LogError(ex, "Error parsing keywords query: {Keywords}", keywords);
                    return results; 
                }
            }

            // Document type filter for 'doc_type' field
            if (!string.IsNullOrWhiteSpace(docTypeFilter))
            {
                var termQuery = new TermQuery(new Term("doc_type", docTypeFilter.ToUpperInvariant()));
                // Corrected: For filtering, Occur.MUST is appropriate if the condition is mandatory.
                // If it were optional or to exclude, Occur.SHOULD or Occur.MUST_NOT would be used.
                // For a filter-like behavior where it must match but doesn't contribute to score in the same way as keyword matches,
                // this is a common approach. More complex filtering can be achieved with FilteredQuery or other constructs
                // but for this simple case, adding as a required clause is standard.
                booleanQuery.Add(termQuery, Occur.MUST); 
                _logger.LogDebug("Document type filter added: {DocTypeFilterQuery}", termQuery.ToString());
            }

            if (booleanQuery.Clauses.Count == 0)
            {
                _logger.LogWarning("Search query is empty after processing inputs. No search will be performed.");
                return results; // No valid criteria to search on
            }

            _logger.LogInformation("Executing combined Lucene query: {LuceneQuery}", booleanQuery.ToString());

            TopDocs topDocs = searcher.Search(booleanQuery, n: 100); // Get top 100 results
            _logger.LogInformation("Search completed. Found {TotalHits} total matching documents.", topDocs.TotalHits);

            foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
            {
                Document resultDoc = searcher.Doc(scoreDoc.Doc);
                results.Add(new SearchResultItem
                {
                    DocumentId = resultDoc.Get("document_id"),
                    FileName = resultDoc.Get("filename"),
                    DocType = resultDoc.Get("doc_type"),
                    Score = scoreDoc.Score,
                    StoredFileName = resultDoc.Get("stored_filename") // Retrieve stored_filename
                });
            }
            _logger.LogInformation("Returning {ResultsCount} search results.", results.Count);
            return results;
        }
    }
}
