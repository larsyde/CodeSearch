using System;
using System.Runtime.InteropServices;

namespace CodeSearch
{
    public static class Helpers
    {
        public static void Error(this Exception e, string loggerName = "Main")
        {
            var logger = NLog.LogManager.GetLogger(loggerName);
            logger.Error(e);
        }

        public static void Error(this string s, string loggerName = "Main")
        {
            var logger = NLog.LogManager.GetLogger(loggerName);
            logger.Error(s);
        }

        public static void Warning(this string s, string loggerName = "Main")
        {
            var logger = NLog.LogManager.GetLogger(loggerName);
            logger.Warn(s);
        }

        public static string Info(this string s, string loggerName = "Main")
        {
            var logger = NLog.LogManager.GetLogger(loggerName);
            logger.Info(s);
            return s;
        }

        public static string Trace(this string s, string loggerName = "Main")
        {
            var logger = NLog.LogManager.GetLogger(loggerName);
            logger.Trace(s);
            return s;
        }

        public static bool IsDiskFull(Exception ex)
        {
            const int ERROR_HANDLE_DISK_FULL = 0x27;
            const int ERROR_DISK_FULL = 0x70;

            int win32ErrorCode = ex.HResult & 0xFFFF;
            return win32ErrorCode == ERROR_HANDLE_DISK_FULL || win32ErrorCode == ERROR_DISK_FULL;
        }
    }
}