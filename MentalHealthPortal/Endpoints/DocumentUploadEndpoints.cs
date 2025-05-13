using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; //access model-view-controller
using Microsoft.Extensions.Hosting; //Adds IWebHostEnvironment which provides information about the web hosting environment the application is running in
using MentalHealthPortal.Data; // Added for ApplicationDbContext
using MentalHealthPortal.Models; // Added for DocumentMetadata
using MentalHealthPortal.Services;


namespace MentalHealthPortal.Endpoints
{
    public static class DocumentUploadEndpoints
    {
        
        public static void MapDocumentUploadEndpoints(this WebApplication app)
        {
            // Add TextExtractionService to the parameters
            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFile file, IWebHostEnvironment env, ApplicationDbContext dbContext, TextExtractionService textExtractionService) =>
            {
                if (file == null || file.Length == 0)
                {
                    return Results.BadRequest(new { message = "No file uploaded." });
                }

                var originalFileName = file.FileName;
                var fileExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}"; //Generates a unique file name

                if (string.IsNullOrEmpty(fileExtension) || (fileExtension != ".pdf" && fileExtension != ".docx"))
                {
                    return Results.BadRequest(new { message = "Invalid file type. Only PDF or DOCX are allowed" });
                }
                try
                {
                    // 1. Define storage path
                    var uploadsFolderPath = Path.Combine(env.ContentRootPath, "UploadedDocuments");

                    // 2. Ensure the directory exists
                    Directory.CreateDirectory(uploadsFolderPath);

                    // 3. Generate the full file path for the new file
                    var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

                    // 4. Save the file asynchronously
                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Create and save metadata
                    // Create and save metadata
                    var metadata = new DocumentMetadata
                    {
                        OriginalFileName = originalFileName, // This variable should already exist in your code
                        StoredFileName = uniqueFileName,   // This variable should already exist in your code
                        StoragePath = filePath,            // This variable should already exist in your code
                        DocumentType = fileExtension.TrimStart('.').ToUpperInvariant(), // Gets "PDF" or "DOCX"
                        UploadTimestamp = DateTime.UtcNow
                    };

                    dbContext.DocumentMetadata.Add(metadata);
                    await dbContext.SaveChangesAsync();

                    // ---- START: Text Extraction ----
                    try
                    {
                        // Extract text from the uploaded document
                        string extractedText = await textExtractionService.ExtractTextAsync(filePath, metadata.DocumentType);

                        // Log the length of the extracted text (or the text itself for debugging, if it's not too long)
                        Console.WriteLine($"Extracted text length for {originalFileName}: {extractedText.Length} characters.");
                        // You could also log: Console.WriteLine($"Extracted text for {originalFileName}: {extractedText}");


                        // Update metadata with extracted text length
                        metadata.ExtractedTextLength = extractedText.Length;
                        // In the future, you might store keywords or a summary here too.

                        // Save the updated metadata
                        await dbContext.SaveChangesAsync();
                        Console.WriteLine($"Successfully updated metadata for {originalFileName} with text extraction info.");
                    }
                    catch (Exception ex)
                    {
                        // Log any errors during text extraction
                        Console.WriteLine($"Error during text extraction or metadata update for {originalFileName}: {ex.Message}");
                        // Decide if this error should prevent the upload from being reported as successful.
                        // For now, we'll still return OK for the file upload itself, but log the extraction error.
                    }
                    // ---- END: Text Extraction ----

                    // 5. Return success with actual stored filename and path
                    return Results.Ok(new
                    {
                        message = $"File '{originalFileName}' uploaded successfully and saved as '{uniqueFileName}'.",
                        storedFileName = uniqueFileName,
                        filePath = filePath
                    });

                }
                catch (IOException ex) // Be more specific with IO exceptions
                {
                    // Log the exception (e.g., using ILogger in a real app)
                    Console.WriteLine($"Error saving file '{originalFileName}': {ex.Message}"); // Log specific error
                    return Results.Problem("An error occurred while saving the file. Please try again.", statusCode: 500);
                }
                catch (Exception ex)
                {
                    // Basic error handling for now. You'll want to log this error.
                    Console.WriteLine($"Error during file processing for '{originalFileName}': {ex.Message}"); // Log specific error
                    return Results.Problem("An unexpected error occurred while processing the file.", statusCode: 500);
                }
                // ---- END of new file saving logic ----

            })
            .DisableAntiforgery(); //For simple forms without antiforgery tokens
        }
    }
}