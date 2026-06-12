namespace PlustekBCR.Models.Ocr
{
    public class OcrPageResult
    {
        public int Page { get; set; }
        public List<OcrTextBlock> Blocks { get; set; } = new();
    }
}
