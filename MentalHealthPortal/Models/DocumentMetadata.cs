using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Added this using

namespace MentalHealthPortal.Models
{
    public class DocumentMetadata
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Added this attribute
        public int Id { get; set; }

        // Changed to 'required string'
        public required string OriginalFileName { get; set; }
        public required string StoredFileName { get; set; }
        public required string StoragePath { get; set; }
        public required string DocumentType { get; set; } // e.g., "PDF", "DOCX"

        [Required]
        public DateTime UploadTimestamp { get; set; }

        public int? ExtractedTextLength { get; set; }
        public string? Keywords { get; set; }
        public string? AssignedCategory { get; set; }
        public DateTime? LastAnalyzedTimestamp { get; set; }
    }
}
