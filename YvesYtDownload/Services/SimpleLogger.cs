using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace YvesYtDownload.Services;

public class SimpleLogger<T> : ILogger<T>
{
    private readonly string _logFilePath;
    private readonly object _lock = new object();

    public SimpleLogger()
    {
        string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YvesYtDownload", "Logs");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        string message = formatter(state, exception);
        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{typeof(T).Name}] {message}";
        
        if (exception != null)
        {
            logEntry += $"\nException: {exception}";
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors to prevent disrupting the application
            }
        }
    }
}
