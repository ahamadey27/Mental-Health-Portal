using System;

namespace MentalHealthPortal.Models
{
    public class DocumentMetadata
    {
        public required string Id { get; set; } // Changed to string, ensure this is the case

        public required string OriginalFileName { get; set; }
        public required string DocumentType { get; set; } // e.g., "PDF", "DOCX"
        public DateTime UploadTimestamp { get; set; }
        public int? ExtractedTextLength { get; set; } 
    }
}
