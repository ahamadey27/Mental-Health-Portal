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
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) //First, check if the provided filePath is null, empty, or just whitespace,
            {
                Console.WriteLine($"Error: File path is invalid or file does not exist: {filePath}"); //If the path is invalid or the file doesn't exist, log an error (optional)
                return string.Empty;
            }
            // Create a StringBuilder instance.
            // StringBuilder is efficient for concatenating multiple strings,
            // which is useful when collecting text from multiple pages or parts of a document.
            StringBuilder textBuilder = new StringBuilder();
            // Use a try-catch block to handle potential errors during file processing.
            // File operations can fail for various reasons (e.g., file is corrupted, locked).
            try
            {
                // Check the document type to decide which library to use.
                // StringComparison.OrdinalIgnoreCase ensures the comparison is case-insensitive (e.g., "pdf" == "PDF").
                if (documentType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        foreach (Page page in document.GetPages()) // Iterate through each page in the PDF document.
                        {
                            textBuilder.Append(page.Text); // Append the text content of the current page to the StringBuilder.
                            textBuilder.AppendLine(); // Add a newline character after each page's text to separate them.
                        }
                    }
                }

                else if (documentType.Equals("DOCX", StringComparison.OrdinalIgnoreCase))
                {
                    // If it's a DOCX file:
                    // Load the DOCX document using the DocX library.
                    // The 'using' statement ensures proper disposal of the DocX object.
                    using (DocX document = DocX.Load(filePath))
                    {
                        // Append all text from the DOCX document to the StringBuilder.
                        // DocX library often provides a simple way to get all text.
                        textBuilder.Append(document.Text);
                    }

                }
                else
                {
                    // If the document type is neither PDF nor DOCX (or not supported):
                    // Log an error or warning.
                    Console.WriteLine($"Unsupported document type: {documentType} for file: {filePath}");
                    // Return an empty string as no text can be extracted for unsupported types.
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Log the error message to the console.
                // In a production application, you would use a more robust logging framework.
                Console.WriteLine($"Error extracting text from '{filePath}': {ex.Message}");
                // Return an empty string indicating that text extraction failed.
                // Depending on requirements, you might rethrow the exception or handle it differently.
                return string.Empty;
            }

            // Convert the StringBuilder's content to a regular string and return it.
            // This will be the full extracted text from the document.
            return textBuilder.ToString();
        }
    }
}