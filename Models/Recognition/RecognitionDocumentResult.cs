using PlustekBCR.Models;

namespace PlustekBCR.Models.Recognition
{
    public class RecognitionDocumentResult
    {
        public MarketCode Market { get; set; } = MarketCode.JP;
        public List<RecognitionPageResult> Pages { get; set; } = new();
        public RecognizedBusinessCardData? StructuredData { get; set; }
        public string JobStatus { get; set; } = string.Empty;
        public List<string> ServiceErrors { get; set; } = new();
    }
}
