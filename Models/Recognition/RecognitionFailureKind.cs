namespace PlustekBCR.Models.Recognition
{
    public enum RecognitionFailureKind
    {
        Configuration,
        JobFailed,
        TimedOut,
        UnreadableResult,
        UiUpdateFailed,
        InvalidInput,
        Network
    }
}
