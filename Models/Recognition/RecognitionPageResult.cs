namespace PlustekBCR.Models.Recognition
{
    public class RecognitionPageResult
    {
        public int Page { get; set; }
        public List<RecognitionTextBlock> Blocks { get; set; } = new();
    }
}
