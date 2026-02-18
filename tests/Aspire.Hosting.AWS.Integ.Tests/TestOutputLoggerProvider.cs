using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Aspire.Hosting.AWS.Integ.Tests;

public sealed class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;
    private readonly LogLevel _minLevel;

    public TestOutputLoggerProvider(
        ITestOutputHelper output,
        LogLevel minLevel = LogLevel.Trace)
    {
        _output = output;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => new TestOutputLogger(_output, categoryName, _minLevel);

    public void Dispose()
    {
        // Nothing to dispose
    }

    internal sealed class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _category;
        private readonly LogLevel _minLevel;

        public TestOutputLogger(
            ITestOutputHelper output,
            string category,
            LogLevel minLevel)
        {
            _output = output;
            _category = category;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            var line =
                $"[{DateTime.UtcNow:HH:mm:ss.fff}] " +
                $"{logLevel,-11} " +
                $"{_category} " +
                $"{message}";

            if (exception != null)
            {
                line += Environment.NewLine + exception;
            }

            try
            {
                _output.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // xUnit throws if test already finished — swallow safely
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}