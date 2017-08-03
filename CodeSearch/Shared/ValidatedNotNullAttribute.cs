using System;

namespace CodeSearch
{
    /// <summary>
    /// Prevent spurious argument null errors in code analysis
    /// </summary>
    internal sealed class ValidatedNotNullAttribute : Attribute
    {
    }
}