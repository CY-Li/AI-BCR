using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PlustekBCR.Controls
{
    public sealed class WrapPanel : Panel
    {
        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty UseUniformItemWidthProperty = DependencyProperty.Register(
            nameof(UseUniformItemWidth),
            typeof(bool),
            typeof(WrapPanel),
            new PropertyMetadata(false, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinimumItemWidthProperty = DependencyProperty.Register(
            nameof(MinimumItemWidth),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(220d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MaximumColumnsProperty = DependencyProperty.Register(
            nameof(MaximumColumns),
            typeof(int),
            typeof(WrapPanel),
            new PropertyMetadata(4, OnLayoutPropertyChanged));

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public bool UseUniformItemWidth
        {
            get => (bool)GetValue(UseUniformItemWidthProperty);
            set => SetValue(UseUniformItemWidthProperty, value);
        }

        public double MinimumItemWidth
        {
            get => (double)GetValue(MinimumItemWidthProperty);
            set => SetValue(MinimumItemWidthProperty, value);
        }

        public int MaximumColumns
        {
            get => (int)GetValue(MaximumColumnsProperty);
            set => SetValue(MaximumColumnsProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (UseUniformItemWidth)
            {
                return MeasureUniform(availableSize);
            }

            var availableWidth = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width);
            var lineWidth = 0d;
            var lineHeight = 0d;
            var desiredWidth = 0d;
            var desiredHeight = 0d;

            foreach (var child in Children)
            {
                child.Measure(new Size(availableWidth, double.PositiveInfinity));
                var childSize = child.DesiredSize;
                var nextWidth = lineWidth == 0
                    ? childSize.Width
                    : lineWidth + HorizontalSpacing + childSize.Width;

                if (lineWidth > 0 && nextWidth > availableWidth)
                {
                    desiredWidth = Math.Max(desiredWidth, lineWidth);
                    desiredHeight += lineHeight + VerticalSpacing;
                    lineWidth = childSize.Width;
                    lineHeight = childSize.Height;
                }
                else
                {
                    lineWidth = nextWidth;
                    lineHeight = Math.Max(lineHeight, childSize.Height);
                }
            }

            desiredWidth = Math.Max(desiredWidth, lineWidth);
            desiredHeight += lineHeight;
            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (UseUniformItemWidth)
            {
                return ArrangeUniform(finalSize);
            }

            var x = 0d;
            var y = 0d;
            var lineHeight = 0d;

            foreach (var child in Children)
            {
                var childSize = child.DesiredSize;
                var nextRight = x == 0
                    ? childSize.Width
                    : x + HorizontalSpacing + childSize.Width;

                if (x > 0 && nextRight > finalSize.Width)
                {
                    x = 0;
                    y += lineHeight + VerticalSpacing;
                    lineHeight = 0;
                }

                if (x > 0)
                {
                    x += HorizontalSpacing;
                }

                child.Arrange(new Rect(x, y, childSize.Width, childSize.Height));
                x += childSize.Width;
                lineHeight = Math.Max(lineHeight, childSize.Height);
            }

            return finalSize;
        }

        private Size MeasureUniform(Size availableSize)
        {
            var availableWidth = double.IsInfinity(availableSize.Width)
                ? Math.Max(MinimumItemWidth, 0)
                : Math.Max(availableSize.Width, 0);
            var columns = GetUniformColumnCount(availableWidth);
            var itemWidth = GetUniformItemWidth(availableWidth, columns);
            var desiredHeight = 0d;
            var rowHeight = 0d;

            for (var index = 0; index < Children.Count; index++)
            {
                var child = Children[index];
                child.Measure(new Size(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);

                var isRowEnd = (index + 1) % columns == 0 || index == Children.Count - 1;
                if (isRowEnd)
                {
                    desiredHeight += rowHeight;
                    if (index < Children.Count - 1)
                    {
                        desiredHeight += VerticalSpacing;
                    }

                    rowHeight = 0;
                }
            }

            var desiredWidth = double.IsInfinity(availableSize.Width)
                ? itemWidth * columns + HorizontalSpacing * Math.Max(0, columns - 1)
                : availableWidth;
            return new Size(desiredWidth, desiredHeight);
        }

        private Size ArrangeUniform(Size finalSize)
        {
            var availableWidth = Math.Max(finalSize.Width, 0);
            var columns = GetUniformColumnCount(availableWidth);
            var itemWidth = GetUniformItemWidth(availableWidth, columns);
            var y = 0d;

            for (var rowStart = 0; rowStart < Children.Count; rowStart += columns)
            {
                var rowEnd = Math.Min(rowStart + columns, Children.Count);
                var rowHeight = 0d;
                for (var index = rowStart; index < rowEnd; index++)
                {
                    rowHeight = Math.Max(rowHeight, Children[index].DesiredSize.Height);
                }

                for (var index = rowStart; index < rowEnd; index++)
                {
                    var column = index - rowStart;
                    var x = column * (itemWidth + HorizontalSpacing);
                    Children[index].Arrange(new Rect(x, y, itemWidth, rowHeight));
                }

                y += rowHeight + VerticalSpacing;
            }

            return finalSize;
        }

        private int GetUniformColumnCount(double availableWidth)
        {
            var maximumColumns = Math.Max(1, MaximumColumns);
            var minimumWidth = Math.Max(1, MinimumItemWidth);

            if (maximumColumns >= 4 && availableWidth >= minimumWidth * 4 + HorizontalSpacing * 3)
            {
                return 4;
            }

            if (maximumColumns >= 2 && availableWidth >= minimumWidth * 2 + HorizontalSpacing)
            {
                return 2;
            }

            return 1;
        }

        private double GetUniformItemWidth(double availableWidth, int columns)
        {
            var spacing = HorizontalSpacing * Math.Max(0, columns - 1);
            return Math.Max(0, (availableWidth - spacing) / columns);
        }

        private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is WrapPanel panel)
            {
                panel.InvalidateMeasure();
            }
        }
    }
}
