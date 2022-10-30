using Serilog;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    public sealed class MapDecompilerJob : INotifyPropertyChanged
    {
        public ILogger Logger { get; }

        public string BspFileName { get; }

        public string MapFileName { get; }

        public string From => Path.GetFileName(BspFileName);

        public string To => Path.GetFileName(MapFileName);

        private MapDecompilerJobStatus _status = MapDecompilerJobStatus.Waiting;

        public MapDecompilerJobStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        private string? _output;

        public string? Output
        {
            get => _output;
            set => this.RaiseAndSetIfChanged(ref _output, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<MapDecompilerJob, string>? MessageReceived;

        public MapDecompilerJob(string bspFileName, string mapFileName)
        {
            var sink = new ForwardingSink(LogMessage, "{Message:lj}{NewLine}{Exception}");

            Logger = new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .MinimumLevel.Information()
                .CreateLogger();

            BspFileName = bspFileName;
            MapFileName = mapFileName;
        }

        private TRet RaiseAndSetIfChanged<TRet>(ref TRet backingField, TRet value, [CallerMemberName] string? propertyName = null)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (!EqualityComparer<TRet>.Default.Equals(backingField, value))
            {
                backingField = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            return backingField;
        }

        private void LogMessage(string message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}
