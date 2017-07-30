using System;

namespace CodeSearch
{
    /// <summary>
    /// Prevent spurious argument null errors in code analysis
    /// </summary>
    sealed class ValidatedNotNullAttribute : Attribute
    {
    }
}