namespace PlustekBCR.Models.Recognition
{
    public class RecognitionTextBlock
    {
        public string Text { get; set; } = string.Empty;
        public int[] Location { get; set; } = Array.Empty<int>();
        public int Page { get; set; }
    }
}
