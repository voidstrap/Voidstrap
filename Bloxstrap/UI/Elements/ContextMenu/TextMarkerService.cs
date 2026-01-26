using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public interface ITextMarkerService
    {
        ITextMarker Create(int startOffset, int length);
        void RemoveAll(Predicate<ITextMarker> predicate);
        IEnumerable<ITextMarker> TextMarkers { get; }
    }

    public interface ITextMarker
    {
        int StartOffset { get; }
        int Length { get; }
        Color? BackgroundColor { get; set; }
        Color? ForegroundColor { get; set; }
        string ToolTip { get; set; }
    }

    public class TextMarkerService : DocumentColorizingTransformer, IBackgroundRenderer, ITextMarkerService
    {
        private readonly TextDocument _document;
        private readonly List<TextMarker> _markers = new List<TextMarker>();

        public TextMarkerService(TextDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public IEnumerable<ITextMarker> TextMarkers => _markers;

        public ITextMarker Create(int startOffset, int length)
        {
            var m = new TextMarker { StartOffset = startOffset, Length = length };
            _markers.Add(m);
            // No RaiseChanged() needed
            return m;
        }

        public void RemoveAll(Predicate<ITextMarker> predicate)
        {
            _markers.RemoveAll(predicate.Invoke);
            // No RaiseChanged() needed
        }

        public void Draw(TextView textView, System.Windows.Media.DrawingContext drawingContext)
        {
            if (_markers.Count == 0) return;

            foreach (var marker in _markers)
            {
                var segment = new TextSegment { StartOffset = marker.StartOffset, Length = marker.Length };
                var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment);
                foreach (var r in rects)
                {
                    drawingContext.DrawRectangle(new SolidColorBrush(marker.BackgroundColor ?? Colors.Transparent), null, r);
                }
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        protected override void ColorizeLine(DocumentLine line)
        {
            foreach (var marker in _markers)
            {
                if (line.EndOffset < marker.StartOffset || line.Offset > marker.StartOffset + marker.Length)
                    continue;

                if (marker.ForegroundColor.HasValue)
                {
                    ChangeLinePart(
                        Math.Max(line.Offset, marker.StartOffset),
                        Math.Min(line.EndOffset, marker.StartOffset + marker.Length),
                        c => c.TextRunProperties.SetForegroundBrush(new SolidColorBrush(marker.ForegroundColor.Value)));
                }
            }
        }

        private class TextMarker : ITextMarker
        {
            public int StartOffset { get; set; }
            public int Length { get; set; }
            public Color? BackgroundColor { get; set; }
            public Color? ForegroundColor { get; set; }
            public string ToolTip { get; set; }
        }
    }
}