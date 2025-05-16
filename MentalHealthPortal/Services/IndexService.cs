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

namespace MentalHealthPortal.Services
{
    public class IndexService : IDisposable
    {
        // Re-introduce LuceneVersion - ensure this matches your Lucene.Net package version's compatibility
        public const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        private readonly FSDirectory _indexDirectory;
        private readonly StandardAnalyzer _analyzer; 
        private readonly string? _luceneIndexRootPathConfigValue;

        private readonly IndexWriter _writer;

        public IndexService(IConfiguration configuration)
        {
            _luceneIndexRootPathConfigValue = configuration.GetValue<string?>("LuceneSettings:IndexRootPath");

            string effectiveIndexRootPath;
            if (string.IsNullOrEmpty(_luceneIndexRootPathConfigValue))
            {
                effectiveIndexRootPath = Path.Combine("Data", "LuceneIndex_Default");
                var defaultFullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, effectiveIndexRootPath));
                Console.WriteLine($"Warning: LuceneSettings:IndexRootPath not found in configuration. Using default: {defaultFullPath}");
            }
            else
            {
                effectiveIndexRootPath = _luceneIndexRootPathConfigValue;
            }

            var fullIndexPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, effectiveIndexRootPath));

            if (!System.IO.Directory.Exists(fullIndexPath))
            {
                System.IO.Directory.CreateDirectory(fullIndexPath);
            }

            _indexDirectory = FSDirectory.Open(new DirectoryInfo(fullIndexPath));
            // Initialize the Analyzer (ONCE)
            _analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Configure and create the IndexWriter
            // Ensure Lucene.Net.Index.IndexWriterConfig and Lucene.Net.Index.OpenMode are resolved
            var writerConfig = new Lucene.Net.Index.IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = Lucene.Net.Index.OpenMode.CREATE_OR_APPEND
            };
            _writer = new Lucene.Net.Index.IndexWriter(_indexDirectory, writerConfig);
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
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (string.IsNullOrEmpty(extractedText))
            {
                // Decide how to handle empty extracted text. Log a warning? Skip indexing?
                // For now, let's assume we might still want to index metadata even if text extraction failed.
                // Or, you could throw an ArgumentNullException(nameof(extractedText));
                Console.WriteLine($"Warning: Extracted text for document ID {metadata.Id} is empty. Indexing with empty content field.");
                extractedText = ""; // Ensure it's not null for the TextField
            }

            var luceneDoc = new Lucene.Net.Documents.Document();

            // Add document_id: Stored and Indexed (but not tokenized for exact match)
            // Used as the term for UpdateDocument
            luceneDoc.Add(new StringField("document_id", metadata.Id.ToString(), Field.Store.YES));

            // Add filename: Stored and Indexed (tokenized for searching parts of the filename)
            if (!string.IsNullOrEmpty(metadata.OriginalFileName))
            {
                luceneDoc.Add(new TextField("filename", metadata.OriginalFileName, Field.Store.YES));
            }
            else
            {
                // Handle cases where OriginalFileName might be null or empty if that's possible
                luceneDoc.Add(new TextField("filename", "Unknown", Field.Store.YES)); // Placeholder
            }


            // Add content: Indexed (tokenized) for full-text search. Not stored to save space.
            // For snippets/highlighting, FieldType with term vectors would be needed.
            // Example for TextField.TYPE_NOT_STORED which is Indexed and Tokenized but not Stored:
            // public static readonly FieldType TYPE_NOT_STORED = new FieldType { IsIndexed = true, IsTokenized = true }.Freeze();
            // luceneDoc.Add(new Field("content", extractedText, TextField.TYPE_NOT_STORED));
            // For simplicity now, using constructor that defaults to not stored if not specified otherwise for TextField
            luceneDoc.Add(new TextField("content", extractedText, Field.Store.NO));


            // Add doc_type: Stored and Indexed (not tokenized for exact filtering)
            if (!string.IsNullOrEmpty(metadata.DocumentType))
            {
                luceneDoc.Add(new StringField("doc_type", metadata.DocumentType, Field.Store.YES));
            }
            else
            {
                luceneDoc.Add(new StringField("doc_type", "Unknown", Field.Store.YES)); // Placeholder
            }


            try
            {
                // UpdateDocument will delete any existing document with the same term (doc_id)
                // and then add the new document. If no document matches, it simply adds.
                _writer.UpdateDocument(new Term("document_id", metadata.Id.ToString()), luceneDoc);
                _writer.Commit(); // Commit changes to the index
                Console.WriteLine($"Document ID {metadata.Id} ('{metadata.OriginalFileName}') added/updated in Lucene index.");
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using ILogger)
                Console.WriteLine($"Error adding/updating document ID {metadata.Id} to Lucene index: {ex.Message}");
                // Depending on the error, you might want to re-throw or handle it
            }
        }

        public List<SearchResultItem> Search(string searchTerm, string? docTypeFilter = null) // Changed to string?
        {
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
