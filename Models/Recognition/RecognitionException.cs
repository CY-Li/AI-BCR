namespace PlustekBCR.Models.Recognition
{
    public class RecognitionException : Exception
    {
        public RecognitionFailureKind FailureKind { get; }

        public RecognitionException(RecognitionFailureKind failureKind, string message)
            : base(message)
        {
            FailureKind = failureKind;
        }

        public RecognitionException(RecognitionFailureKind failureKind, string message, Exception innerException)
            : base(message, innerException)
        {
            FailureKind = failureKind;
        }
    }
}
