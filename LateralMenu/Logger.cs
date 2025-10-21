//-----------------------------------------------------------------------------
// <copyright file="Logger.cs" company="AVEVA Software, LLC">
//     Copyright © 2018 AVEVA Group plc and its subsidiaries. All rights reserved.
// </copyright>
// <summary>
// This class provides a static wrapper to call into logger methods.
// Log messages will be logged to .Net Logger if an ILoggerFactory is injected by the host application.
// If not, then it will log to ArchestrA logger if it is installed in the local machine.
// </summary>
//
// This cs file is distributed with the ArchestrA.Diagnostics.LoggerClient NuGet package so consuming projects
// can leverage this static helper.
//
// -------------------------------------------------------------------------------------------------------------
namespace ArchestrA.Diagnostics
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    [ExcludeFromCodeCoverage]
    internal static class Logger
    {
        // Initialize the logger client when a method is invoked first time on this class.
        // Note that the CreateNew will return DotNetLogger client if ILoggerFactory is injected
        // in the host application. If not it will return ArchestrA logger client. If ArchestrA logger
        // is not installed then it will return null.
        private static ILoggerClient loggerClient = LoggerClient.CreateNew(Assembly.GetExecutingAssembly().GetName().Name);

        /// <summary>
        /// Gets a value indicating whether the error log level is enabled.
        /// </summary>
        /// <returns>Returns true if the custom category is enabled; otherwise, false.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsErrorEnabled => loggerClient != null ? loggerClient.IsErrorEnabled : false;

        /// <summary>
        /// Gets a value indicating whether the warning log level is enabled.
        /// </summary>
        /// <returns>Returns true if the custom category is enabled; otherwise, false.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsWarningEnabled => loggerClient != null ? loggerClient.IsWarningEnabled : false;

        /// <summary>
        /// Gets a value indicating whether the information log level is enabled.
        /// </summary>
        /// <returns>Returns true if the custom category is enabled; otherwise, false.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsInfoEnabled => loggerClient != null ? loggerClient.IsInfoEnabled : false;

        /// <summary>
        /// Gets a value indicating whether the trace log level is enabled.
        /// </summary>
        /// <returns>Returns true if the custom category is enabled; otherwise, false.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsTraceEnabled => loggerClient != null ? loggerClient.IsTraceEnabled : false;

        /// <summary>
        /// Gets a value indicating whether the custom log level is enabled.
        /// </summary>
        /// <param name="customLogName">Name of the custom log flag.</param>
        /// <returns>Returns true if the custom category is enabled; otherwise, false.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsCustomLogEnabled(string customLogName) => loggerClient != null ? loggerClient.IsCustomLogEnabled(customLogName) : false;

        /// <summary>
        /// Logs an error message. Use the getMessage callback to pass in the message to log.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        /// <param name="exception">Exception if exception details need to to be logged.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public static void LogError(Func<string> getMessage, Exception exception = null)
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            loggerClient?.LogError(getMessage, exception);
        }

        /// <summary>
        /// Logs a warning message. Use the getMessage callback to pass in the message to log.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        /// <param name="exception">Exception if exception details need to to be logged.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public static void LogWarning(Func<string> getMessage, Exception exception = null)
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            loggerClient?.LogWarning(getMessage, exception);
        }

        /// <summary>
        /// Logs an info message. Use the getMessage callback to pass in the message to log.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
        public static void LogInfo(Func<string> getMessage)
        {
            loggerClient?.LogInfo(getMessage);
        }

        /// <summary>
        /// Logs a trace message. Use the getMessage callback to pass in the message to log.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        /// <param name="exception">Exception if exception details need to to be logged.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public static void LogTrace(Func<string> getMessage, Exception exception = null)
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            loggerClient?.LogTrace(getMessage, exception);
        }

        /// <summary>
        /// Logs a custom message. Use the getMessage callback to pass in the message to log.
        /// </summary>
        /// <param name="customLogName">Name of the custom log flag.</param>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
        public static void LogCustom(string customLogName, Func<string> getMessage)
        {
            loggerClient?.LogCustom(customLogName, getMessage);
        }

        /// <summary>
        /// Logs a message. Use this mehtod to log a message at the begining of a method. Use the getMessage callback to pass in contextual information that will be useful for diagnostics.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        /// <param name="callerName">Leave callerName empty.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
        public static void LogEntry(Func<string> getMessage, [CallerMemberName] string callerName = "")
        {
            const string Prefix = "Entered";
            loggerClient?.LogEntryExit(getMessage, Prefix, callerName);
        }

        /// <summary>
        /// Logs a message. Use this mehtod to log a message before existing a method. Use the getMessage callback to pass in contextual information that will be useful for diagnostics.
        /// </summary>
        /// <param name="getMessage">Delegate that will be called to get a string to log. Note that this delegate is called only when the log flag corresponding to the calling component is enabled in the ArchestrA Logger Console.</param>
        /// <param name="callerName">Leave callerName empty.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
        public static void LogExit(Func<string> getMessage, [CallerMemberName] string callerName = "")
        {
            const string Prefix = "Exiting";
            loggerClient?.LogEntryExit(getMessage, Prefix, callerName);
        }

        /// <summary>
        /// Logs a message related to dispose related issues. This method is typically used from the dispose method of a class when an instance of that class is not properly disposed.
        /// </summary>
        /// <param name="className">Name of the class.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method does not have to be used by all clients of the Logger.")]
        public static void LogDispose(string className)
        {
            // Dispose issues are treated as warnings
            loggerClient?.LogWarning(() => $"Failed to call Dispose() for class {className}. Clean up of unmanaged resource was deferred to garbage collector.");
        }
    }
}