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
        // Updated to accept a Stream and originalFileName (for context/logging)
        public async Task<string> ExtractTextAsync(Stream stream, string documentType, string originalFileName)
        {
            return await Task.Run(() =>
            {
                if (stream == null || stream.Length == 0)
                {
                    Console.WriteLine($"Error: Stream is null or empty for file: {originalFileName}");
                    return string.Empty;
                }

                StringBuilder textBuilder = new StringBuilder();
                try
                {
                    // Ensure stream position is at the beginning if it might have been read before
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }

                    if (documentType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                    {
                        // PdfDocument.Open now takes a stream
                        using (PdfDocument document = PdfDocument.Open(stream))
                        {
                            if (document.NumberOfPages == 0)
                            {
                                Console.WriteLine($"Warning: PDF file {originalFileName} has 0 pages.");
                            }
                            foreach (Page page in document.GetPages())
                            {
                                string pageText = page.Text;
                                // Log a snippet of the extracted text per page for debugging
                                Console.WriteLine($"Extracted from {originalFileName} (Page {page.Number}): {pageText.Substring(0, Math.Min(pageText.Length, 200))}[...]");
                                textBuilder.Append(pageText);
                                textBuilder.AppendLine(); // Add new line between pages for readability
                            }
                        }
                    }
                    else if (documentType.Equals("DOCX", StringComparison.OrdinalIgnoreCase))
                    {
                        // DocX.Load now takes a stream
                        using (DocX document = DocX.Load(stream))
                        {
                            textBuilder.Append(document.Text);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported document type: {documentType} for file: {originalFileName}");
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting text from stream for file \'{originalFileName}\' (Type: {documentType}): {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}"); // Log stack trace
                    // Consider logging the full exception details if using ILogger
                    return string.Empty;
                }
                // Log the final combined text before returning
                var finalText = textBuilder.ToString();
                Console.WriteLine($"Final extracted text for {originalFileName} (Length: {finalText.Length}): {finalText.Substring(0, Math.Min(finalText.Length, 500))}[...]");
                return finalText;
            });
        }
    }
}