using System;
using System.Runtime.CompilerServices;

namespace CSUpdater.Console
{
    /// <summary>
    /// static class for extension methods
    /// </summary>
    public static class ValidationExtensions
    {

        /// <summary>
        /// Generic check for null value with caller information
        /// </summary>
        /// <typeparam name="T">type</typeparam>
        /// <param name="parameter">object to be checked</param>
        /// <param name="optionalText">optional text</param>
        /// <param name="memberName">caller name</param>
        /// <param name="sourceFilePath">source file, full path</param>
        /// <param name="sourceLineNumber">source line number of offending call</param>

        public static void NullCheck<T>(
            [ValidatedNotNull] this T parameter,
            string optionalText = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
            ) where T : class
        {
            if (parameter != null)
                return;
            throw new ArgumentNullException(Checks.Format<T>(optionalText, memberName, sourceFilePath, sourceLineNumber));

        }

        /// <summary>
        /// Overload for string class
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="optionalText"></param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        public static void NullCheck(
            [ValidatedNotNull]  this string parameter,
            string optionalText = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
            )
        {
            if (parameter != null)
                return;
            throw new ArgumentNullException(Checks.Format<string>(optionalText, memberName, sourceFilePath, sourceLineNumber));


        }


    }
}