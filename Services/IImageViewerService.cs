using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IImageViewerService
    {
        void Show(BusinessCard card, CardImageSide side);
        void Close(BusinessCard card);
        void Close();
    }
}
