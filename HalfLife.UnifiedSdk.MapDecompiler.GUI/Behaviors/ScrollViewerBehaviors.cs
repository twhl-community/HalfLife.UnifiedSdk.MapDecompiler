using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Behaviors
{
    /// <summary>
    /// Properties added to the <see cref="ScrollViewer"/> class.
    /// </summary>
    public class ScrollViewerBehaviors : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> AutoScrollProperty = AvaloniaProperty.RegisterAttached<ScrollViewerBehaviors, Interactive, bool>(
            "AutoScroll", true, false, BindingMode.OneWay);

        public static void SetCommand(AvaloniaObject element, bool value)
        {
            element.SetValue(AutoScrollProperty, value);
        }

        public static bool GetCommand(AvaloniaObject element)
        {
            return element.GetValue(AutoScrollProperty);
        }
    }
}
