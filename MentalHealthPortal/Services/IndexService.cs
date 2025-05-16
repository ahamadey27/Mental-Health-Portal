using Lucene.Net.Store;
using Microsoft.Extensions.Configuration;
using System.IO; // For Path and DirectoryInfo
// using Lucene.Net.Util; // LuceneVersion will be addressed later
using System;

namespace MentalHealthPortal.Services
{
    public class IndexService : IDisposable
    {
        // LuceneVersion constant removed for now, will be added with IndexWriterConfig

        private readonly FSDirectory _indexDirectory;
        private readonly string? _luceneIndexRootPathConfigValue;

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
        }

        public FSDirectory GetIndexDirectory() => _indexDirectory;

        public void Dispose()
        {
            _indexDirectory?.Dispose();
        }
    }
}
