using Lucene.Net.Store;         // For FSDirectory
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
        // Re-introduce LuceneVersion - ensure this matches your Lucene.Net package version's compatibility
        public const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        private readonly FSDirectory _indexDirectory;
        private readonly StandardAnalyzer _analyzer; 

        private readonly IndexWriter _writer;
        private readonly ILogger<IndexService> _logger; // Add ILogger field
        private readonly string _indexPath; // Store the resolved path

        // Modify constructor to inject ILogger and IWebHostEnvironment
        public IndexService(IConfiguration configuration, ILogger<IndexService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            string? configuredPath = configuration.GetValue<string?>("LuceneSettings:IndexRootPath");

            if (string.IsNullOrEmpty(configuredPath))
            {
                _indexPath = Path.Combine(env.ContentRootPath, "Data", "LuceneIndex_Default");
                _logger.LogWarning("LuceneSettings:IndexRootPath not found in configuration. Using default: {DefaultPath}", _indexPath);
            }
            else
            {
                // Treat configuredPath as relative to ContentRootPath if it's not absolute
                if (Path.IsPathRooted(configuredPath))
                {
                    _indexPath = configuredPath;
                }
                else
                {
                    _indexPath = Path.Combine(env.ContentRootPath, configuredPath);
                }
            }

            _logger.LogInformation("Lucene index configured at path: {IndexPath}", _indexPath);

            if (!System.IO.Directory.Exists(_indexPath))
            {
                _logger.LogInformation("Creating Lucene index directory at: {IndexPath}", _indexPath);
                System.IO.Directory.CreateDirectory(_indexPath);
            }

            _indexDirectory = FSDirectory.Open(new DirectoryInfo(_indexPath));
            // Initialize the Analyzer (ONCE)
            _analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Configure and create the IndexWriter
            var writerConfig = new Lucene.Net.Index.IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = Lucene.Net.Index.OpenMode.CREATE_OR_APPEND
            };
            _writer = new Lucene.Net.Index.IndexWriter(_indexDirectory, writerConfig);

            // If the index is newly created (or was empty), perform an initial commit
            // to ensure segment files are present before any search is attempted.
            if (!DirectoryReader.IndexExists(_indexDirectory))
            {
                _logger.LogInformation("Performing initial commit on newly created Lucene index at: {IndexPath}", _indexPath);
                _writer.Commit(); // Perform an empty commit to initialize the segment files
            }
            _logger.LogInformation("IndexService initialized, IndexWriter created.");
        }

        public FSDirectory GetIndexDirectory() => _indexDirectory;

        public void Dispose()
        {
            // Dispose in an order that makes sense, typically reverse of creation or usage
            // Analyzer might be used by writer, writer uses directory.
            // However, Lucene's own examples often show writer closed first.
            // Closing the writer commits any pending changes and releases its lock on the directory.
            _writer?.Dispose();
            _analyzer?.Dispose(); 
            _indexDirectory?.Dispose(); 
        }

        // AddOrUpdateDocument method to add or update documents in the Lucene index
        public void AddOrUpdateDocument(DocumentMetadata metadata, string extractedText)
        {
            if (metadata == null)
            {
                _logger.LogError("AddOrUpdateDocument received null metadata.");
                throw new ArgumentNullException(nameof(metadata));
            }

            _logger.LogInformation("Attempting to add/update document ID {DocumentId} ('{OriginalFileName}') to Lucene index.", metadata.Id, metadata.OriginalFileName);

            if (string.IsNullOrEmpty(extractedText))
            {
                _logger.LogWarning("Extracted text for document ID {DocumentId} is empty. Indexing with empty content field.", metadata.Id);
                extractedText = string.Empty; 
            }

            var luceneDoc = new Lucene.Net.Documents.Document();
            luceneDoc.Add(new StringField("document_id", metadata.Id.ToString(), Field.Store.YES));
            luceneDoc.Add(new TextField("filename", metadata.OriginalFileName ?? "Unknown", Field.Store.YES));
            luceneDoc.Add(new TextField("content", extractedText, Field.Store.NO)); 
            luceneDoc.Add(new StringField("doc_type", metadata.DocumentType ?? "Unknown", Field.Store.YES));

            try
            {
                _logger.LogInformation("Calling IndexWriter.UpdateDocument for document ID {DocumentId}...", metadata.Id);
                _writer.UpdateDocument(new Term("document_id", metadata.Id.ToString()), luceneDoc);
                _logger.LogInformation("Calling IndexWriter.Commit for document ID {DocumentId}...", metadata.Id);
                _writer.Commit();
                _logger.LogInformation("SUCCESS: Document ID {DocumentId} ('{OriginalFileName}') added/updated in Lucene index at {IndexPath}", metadata.Id, metadata.OriginalFileName, _indexPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR adding/updating document ID {DocumentId} to Lucene index at {IndexPath}: {ErrorMessage}", metadata.Id, _indexPath, ex.Message);
                // Optionally re-throw or handle more gracefully depending on requirements
                // For now, just logging, as the background service might not handle re-thrown exceptions well without adjustment.
            }
        }

        public List<SearchResultItem> Search(string searchTerm, string? docTypeFilter = null)
        {
            _logger.LogInformation("Search requested for term '{SearchTerm}' with docTypeFilter '{DocTypeFilter}' in index {IndexPath}", searchTerm, docTypeFilter, _indexPath);
            var results = new List<SearchResultItem>();
            if (_indexDirectory == null) return results;

            using var reader = DirectoryReader.Open(_indexDirectory);
            var searcher = new IndexSearcher(reader);
            // Ensure "content" is the correct default field as per your AddOrUpdateDocument logic
            var queryParser = new QueryParser(AppLuceneVersion, "content", _analyzer); 

            Query query;
            try
            {
                query = queryParser.Parse(searchTerm);
            }
            catch (ParseException)
            {
                try
                {
                    // Attempt a wildcard query if the initial parse fails.
                    // Note: Leading wildcards can be slow. Consider alternatives if performance is an issue.
                    query = queryParser.Parse("*" + QueryParserBase.Escape(searchTerm) + "*");
                }
                catch (ParseException) 
                {
                    // If wildcard also fails, return empty or log the error
                    Console.WriteLine($"Could not parse search term (even with wildcards): {searchTerm}");
                    return results;
                }
            }

            if (!string.IsNullOrEmpty(docTypeFilter))
            {
                var booleanQuery = new BooleanQuery();
                booleanQuery.Add(query, Occur.MUST); 

                var docTypeTerm = new Term("doc_type", docTypeFilter);
                var docTypeQuery = new TermQuery(docTypeTerm);
                booleanQuery.Add(docTypeQuery, Occur.MUST); 

                query = booleanQuery;
            }

            var topDocs = searcher.Search(query, n: 10); 

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                var documentIdString = doc.Get("document_id");
                Guid documentId = Guid.Empty; // Default value

                if (!string.IsNullOrEmpty(documentIdString) && Guid.TryParse(documentIdString, out var parsedGuid))
                {
                    documentId = parsedGuid;
                }
                else
                {
                    // Log or handle cases where document_id is missing or not a valid Guid
                    Console.WriteLine($"Warning: Document ID '{documentIdString}' is missing or not a valid Guid for Lucene doc {scoreDoc.Doc}. Skipping.");
                    continue; // Skip this document
                }

                results.Add(new SearchResultItem
                {
                    DocumentId = documentId, // Correctly assign parsed Guid
                    FileName = doc.Get("filename"), // Corrected casing: FileName
                    DocType = doc.Get("doc_type"),
                    Score = scoreDoc.Score,
                });
            }
            return results;
        }
    }

    // Define a simple class to hold search results.
    // This could be in a separate Models file if preferred.
    public class SearchResultItem
    {
        public Guid DocumentId { get; set; }
        public string? FileName { get; set; } // Ensure this matches: FileName
        public string? DocType { get; set; }
        public float Score { get; set; }
    }
}
