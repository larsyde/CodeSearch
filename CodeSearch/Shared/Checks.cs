using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CodeSearch
{
    /// <summary>
    /// checker methods to validate objects
    /// </summary>
    public static class Checks
    {
        /// <summary>
        /// check argument for null and throw appropriate exception if so
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameter"></param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        /// <param name="optionalText"></param>
        public static void IsNull<T>(
            [ValidatedNotNull]T parameter,
            string optionalText = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
            ) where T : class
        {
            if (parameter != null)
                return;
            throw new ArgumentNullException(Format<T>(optionalText, memberName, sourceFilePath, sourceLineNumber));
        }

        internal static string Format<T>(string optionalText, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            string callerInfo = string.Format(
                CultureInfo.InvariantCulture,
                "caller = {0}{3}source file ={1}{3}source line = {2}{3}",
                memberName, sourceFilePath, sourceLineNumber, Environment.NewLine);

            var type = typeof(T);
            string typeFullName = type.FullName;
            string optionalTextOutput = string.IsNullOrEmpty(optionalText) ?
                string.Empty : string.Format(CultureInfo.InvariantCulture, "Additional information: {0}{1}", optionalText, Environment.NewLine);

            string objectInfo = string.Format(
                CultureInfo.InvariantCulture,
                "Argument of type {0} was null{1}{2}",
                typeFullName,
                Environment.NewLine,
                optionalTextOutput
                );

            string message = string.Format(CultureInfo.InvariantCulture, @"{0}{1}", callerInfo, objectInfo);
            return message;
        }
    }
}