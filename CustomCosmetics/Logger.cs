using System;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Logging;

namespace CustomCosmetics;

internal class Logger
{
    private static ManualLogSource logSource { get; set; }

    internal static void SetLogSource(ManualLogSource Source)
    {
        if (ConsoleManager.ConsoleEnabled) System.Console.OutputEncoding = Encoding.UTF8;
        logSource = Source;
    }

    public static void Info(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag);
    }

    public static void Message(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag, LogLevel.Message);
    }

    public static void Warn(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag, LogLevel.Warning);
    }

    public static void Error(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag, LogLevel.Error);
    }

    public static void Debug(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag, LogLevel.Debug);
    }

    public static void Fatal(object text, [CallerMemberName] string Tag = "")
    {
        SendLog(text.ToString(), Tag, LogLevel.Fatal);
    }

    public static void SendLog(string text, string tag = "", LogLevel logLevel = LogLevel.Info)
    {
        if (logSource == null) return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        var prefix = string.IsNullOrWhiteSpace(tag) ? "" : $" [{tag}]";
        var logMessage = $"[{time}]{prefix} {text}";

        switch (logLevel)
        {
            case LogLevel.Message: logSource.LogMessage(logMessage); break;
            case LogLevel.Error: logSource.LogError(logMessage); break;
            case LogLevel.Warning: logSource.LogWarning(logMessage); break;
            case LogLevel.Fatal: logSource.LogFatal(logMessage); break;
            case LogLevel.Info: logSource.LogInfo(logMessage); break;
            case LogLevel.Debug: logSource.LogDebug(logMessage); break;
            default: System.Console.WriteLine($"[Error] {logMessage}"); break;
        }
    }
}