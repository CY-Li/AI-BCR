namespace PlustekBCR.Models.Ocr
{
    public class OcrTextBlock
    {
        public string Text { get; set; } = string.Empty;
        public int[] Location { get; set; } = Array.Empty<int>();
        public int Page { get; set; }
    }
}
