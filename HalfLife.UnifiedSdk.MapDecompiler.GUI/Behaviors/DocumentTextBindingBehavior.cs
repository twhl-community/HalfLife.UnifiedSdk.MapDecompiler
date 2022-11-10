using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit.Editing;
using System;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Behaviors
{
    public class DocumentTextBindingBehavior : Behavior<TextArea>
    {
        private TextArea? _textArea;
        private ScrollViewer? _scrollViewer;

        private bool _needToScrollToEnd;

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<DocumentTextBindingBehavior, string?>(nameof(Text));

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is TextArea textArea)
            {
                _textArea = textArea;
                _scrollViewer = _textArea.GetLogicalParent<ScrollViewer?>();
                this.GetObservable(TextProperty).Subscribe(TextPropertyChanged);

                if (_scrollViewer is not null)
                {
                    _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                }
            }
        }

        private void TextPropertyChanged(string? text)
        {
            if (_textArea is not null)
            {
                _textArea.Document ??= new();
                _textArea.Document.Text = text ?? string.Empty;
                _needToScrollToEnd = true;
            }
        }

        private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_needToScrollToEnd)
            {
                _needToScrollToEnd = false;
                _scrollViewer!.ScrollToEnd();
            }
        }
    }
}
