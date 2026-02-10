using System;
using System.Collections.Generic;

namespace KeeFetch
{
    internal enum LogLevel
    {
        Debug,
        Warning,
        Error
    }

    internal sealed class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Context { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    internal static class Logger
    {
        private static readonly List<LogEntry> entries = new List<LogEntry>();
        private static readonly object lockObj = new object();

        public static void Debug(string context, string message)
        {
            Add(LogLevel.Debug, context, message);
        }

        public static void Debug(string context, Exception ex)
        {
            Add(LogLevel.Debug, context, ex.GetType().Name + ": " + ex.Message);
        }

        public static void Warn(string context, string message)
        {
            Add(LogLevel.Warning, context, message);
        }

        public static void Warn(string context, Exception ex)
        {
            Add(LogLevel.Warning, context, ex.GetType().Name + ": " + ex.Message);
        }

        public static void Error(string context, string message)
        {
            Add(LogLevel.Error, context, message);
        }

        public static void Error(string context, Exception ex)
        {
            Add(LogLevel.Error, context, ex.GetType().Name + ": " + ex.Message);
        }

        private static void Add(LogLevel level, string context, string message)
        {
            lock (lockObj)
            {
                entries.Add(new LogEntry
                {
                    Level = level,
                    Context = context,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public static IReadOnlyList<LogEntry> GetEntries()
        {
            lock (lockObj)
            {
                return new List<LogEntry>(entries);
            }
        }

        public static List<LogEntry> GetErrors()
        {
            lock (lockObj)
            {
                return entries.FindAll(e => e.Level == LogLevel.Error);
            }
        }

        public static void Clear()
        {
            lock (lockObj)
            {
                entries.Clear();
            }
        }
    }
}
