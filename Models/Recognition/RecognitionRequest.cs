using PlustekBCR.Models;

namespace PlustekBCR.Models.Recognition
{
    public class RecognitionRequest
    {
        public Guid BusinessCardId { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = "card.jpg";
        public string ContentType { get; set; } = "image/jpeg";
        public MarketCode Market { get; set; } = MarketCode.JP;
        public RecognitionSourceType SourceType { get; set; } = RecognitionSourceType.ImportImage;
    }
}
