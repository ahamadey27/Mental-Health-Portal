using UglyToad.PdfPig; // The main namespace for the PdfPig library, used for reading PDF files.
using UglyToad.PdfPig.Content; // Provides access to specific content within a PDF, like text on a page.
using Xceed.Words.NET; // The namespace for the DocX library, used for reading .docx Word files.
using System.IO; // Provides types for reading and writing to files and data streams (like File.Exists).
using System.Text; // Contains classes for representing and manipulating strings, like StringBuilder.
using System.Threading.Tasks; // Provides types for asynchronous programming, like Task<string>.

namespace MentalHealthPortal.Services
{
    public class TextExtractionService
    {
        public async Task<string> ExtractTextAsync(string filePath, string documentType)
        {
            // Wrap the potentially long-running synchronous file operations in Task.Run
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    Console.WriteLine($"Error: File path is invalid or file does not exist: {filePath}");
                    return string.Empty;
                }

                StringBuilder textBuilder = new StringBuilder();
                try
                {
                    if (documentType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                    {
                        using (PdfDocument document = PdfDocument.Open(filePath))
                        {
                            foreach (Page page in document.GetPages())
                            {
                                textBuilder.Append(page.Text);
                                textBuilder.AppendLine();
                            }
                        }
                    }
                    else if (documentType.Equals("DOCX", StringComparison.OrdinalIgnoreCase))
                    {
                        using (DocX document = DocX.Load(filePath))
                        {
                            textBuilder.Append(document.Text);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported document type: {documentType} for file: {filePath}");
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting text from '{filePath}': {ex.Message}");
                    return string.Empty;
                }
                return textBuilder.ToString();
            });
        }
    }
}