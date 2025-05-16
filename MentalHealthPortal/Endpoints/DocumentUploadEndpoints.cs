using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; //access model-view-controller
using Microsoft.Extensions.Hosting; //Adds IWebHostEnvironment which provides information about the web hosting environment the application is running in
using MentalHealthPortal.Data; // Added for ApplicationDbContext
using MentalHealthPortal.Models; // Added for DocumentMetadata
using MentalHealthPortal.Services;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider

namespace MentalHealthPortal.Endpoints
{
    public static class DocumentUploadEndpoints
    {
        
        public static void MapDocumentUploadEndpoints(this WebApplication app)
        {
            // Add IBackgroundTaskQueue to the parameters
            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFile file, IWebHostEnvironment env, ApplicationDbContext dbContext, TextExtractionService textExtractionService, IBackgroundTaskQueue backgroundTaskQueue) =>
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
                    await dbContext.SaveChangesAsync(); // Initial save of metadata before queuing background task

                    // ---- START: Asynchronous Text Extraction via Background Queue ----
                    backgroundTaskQueue.QueueBackgroundWorkItemAsync(async (serviceProvider, cancellationToken) =>
                    {
                        // Create a new scope for this background task
                        using (var scope = serviceProvider.CreateScope())
                        {
                            var scopedDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            var scopedTextExtractionService = scope.ServiceProvider.GetRequiredService<TextExtractionService>();

                            // It's crucial to re-fetch the metadata within this new scope
                            // to avoid issues with DbContext tracking if the original dbContext instance
                            // from the HTTP request is disposed before this background task runs.
                            var metadataForExtraction = await scopedDbContext.DocumentMetadata.FindAsync(new object[] { metadata.Id }, cancellationToken);

                            if (metadataForExtraction == null)
                            {
                                Console.WriteLine($"Error in background task: Metadata with ID {metadata.Id} not found.");
                                return; // Or handle more robustly
                            }
                            
                            try
                            {
                                Console.WriteLine($"Background task started for {metadataForExtraction.OriginalFileName} (ID: {metadataForExtraction.Id}). Extracting text...");
                                string extractedText = await scopedTextExtractionService.ExtractTextAsync(metadataForExtraction.StoragePath, metadataForExtraction.DocumentType);

                                metadataForExtraction.ExtractedTextLength = extractedText.Length;
                                // Potentially log the extracted text length or a snippet for verification
                                Console.WriteLine($"Text extraction complete for {metadataForExtraction.OriginalFileName}. Length: {extractedText.Length}. Updating database...");

                                await scopedDbContext.SaveChangesAsync(cancellationToken);
                                Console.WriteLine($"Successfully updated metadata for {metadataForExtraction.OriginalFileName} (ID: {metadataForExtraction.Id}) with extracted text length in background.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error during background text extraction or metadata update for {metadataForExtraction.OriginalFileName} (ID: {metadataForExtraction.Id}): {ex.Message}");
                                // Consider more robust error handling/logging here
                            }
                        }
                    });
                    // ---- END: Asynchronous Text Extraction via Background Queue ----

                    // 5. Return success with actual stored filename and path
                    // The response is sent before text extraction completes.
                    return Results.Ok(new
                    {
                        message = $"File '{originalFileName}' uploaded successfully as '{uniqueFileName}'. Text extraction has been queued and will process in the background.",
                        storedFileName = uniqueFileName,
                        filePath = filePath,
                        metadataId = metadata.Id // Optionally return the ID for client-side tracking
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