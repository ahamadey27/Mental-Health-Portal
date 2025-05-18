namespace MentalHealthPortal.Models
{
    public class SearchResultItem
    {
        public string? DocumentId { get; set; } // Can be a string representation of an ID or filename
        public string? FileName { get; set; }
        public string? DocType { get; set; }
        public float Score { get; set; }
        // Add any other properties you want to return for a search result
        // For example, a snippet of the content:
        // public string? Snippet { get; set; }
    }
}
