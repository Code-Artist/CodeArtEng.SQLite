using System;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Exception thrown when potential SQL injection attempts are detected.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when the converter detects potentially malicious patterns
    /// in the WHERE clause, such as attempts to execute multiple statements or use dangerous SQL commands.
    /// </remarks>
    public class SQLInjectionException : Exception
    {
        public SQLInjectionException(string message) : base(message) { }
    }
}