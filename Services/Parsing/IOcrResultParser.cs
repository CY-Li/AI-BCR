using PlustekBCR.Models.Ocr;
using PlustekBCR.Models.Recognition;

namespace PlustekBCR.Services.Parsing
{
    public interface IOcrResultParser
    {
        RecognizedBusinessCardData Parse(OcrDocumentResult documentResult);
    }
}
