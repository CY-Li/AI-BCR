using PlustekBCR.Models;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Models.Ocr
{
    public class OcrDocumentResult
    {
        public MarketCode Market { get; set; } = MarketCode.JP;
        public List<OcrPageResult> Pages { get; set; } = new();
        public RecognizedBusinessCardData? StructuredData { get; set; }
        public string JobStatus { get; set; } = string.Empty;
        public List<string> ServiceErrors { get; set; } = new();
    }
}
