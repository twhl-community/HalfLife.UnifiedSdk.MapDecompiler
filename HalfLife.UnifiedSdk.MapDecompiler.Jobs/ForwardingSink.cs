using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using System.Text;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    public sealed class ForwardingSink : ILogEventSink
    {
        const int DefaultWriteBufferCapacity = 256;

        private readonly Action<string> _delegate;

        private readonly MessageTemplateTextFormatter _formatter;

        public ForwardingSink(Action<string> @delegate, string outputTemplate)
        {
            _delegate = @delegate;
            _formatter = new MessageTemplateTextFormatter(outputTemplate);
        }

        public void Emit(LogEvent logEvent)
        {
            var buffer = new StringWriter(new StringBuilder(DefaultWriteBufferCapacity));
            _formatter.Format(logEvent, buffer);
            var formattedLogEventText = buffer.ToString();

            _delegate(formattedLogEventText);
        }
    }
}
