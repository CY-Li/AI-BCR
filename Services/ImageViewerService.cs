using PlustekBCR.Models;
using PlustekBCR.Views;

namespace PlustekBCR.Services
{
    public sealed class ImageViewerService : IImageViewerService
    {
        private ImageViewerWindow? _window;
        private BusinessCard? _displayedCard;
        private bool _isAlwaysOnTop = true;

        public void Show(BusinessCard card, CardImageSide side)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (_window == null)
            {
                _window = new ImageViewerWindow(_isAlwaysOnTop);
                _window.ViewerClosed += OnViewerClosed;
                _window.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
            }

            _displayedCard = card;
            _window.ShowCard(card, side);
            _window.Activate();
        }

        public void Close(BusinessCard card)
        {
            if (ReferenceEquals(_displayedCard, card))
            {
                Close();
            }
        }

        public void Close()
        {
            _window?.Close();
        }

        private void OnViewerClosed(object? sender, EventArgs e)
        {
            if (_window == null)
            {
                return;
            }

            _window.ViewerClosed -= OnViewerClosed;
            _window.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
            _window = null;
            _displayedCard = null;
        }

        private void OnAlwaysOnTopChanged(object? sender, bool isAlwaysOnTop)
        {
            _isAlwaysOnTop = isAlwaysOnTop;
        }
    }
}
