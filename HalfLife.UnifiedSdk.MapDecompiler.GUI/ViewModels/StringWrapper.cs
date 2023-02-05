using ReactiveUI;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class StringWrapper : ViewModelBase
    {
        private string _value = string.Empty;

        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public StringWrapper(string value)
        {
            Value = value;
        }
    }
}
