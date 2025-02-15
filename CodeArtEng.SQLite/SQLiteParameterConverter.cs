using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using System.Linq;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Exception thrown when potential SQL injection attempts are detected.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when the converter detects potentially malicious patterns
    /// in the WHERE clause, such as attempts to execute multiple statements or use dangerous SQL commands.
    /// </remarks>
    public class SqlInjectionException : Exception
    {
        public SqlInjectionException(string message) : base(message) { }
    }

    /// <summary>
    /// Provides functionality to safely convert SQL WHERE clauses into parameterized queries for SQLite.
    /// This class helps prevent SQL injection attacks by converting raw WHERE clause strings into
    /// parameter-based queries with proper value handling and validation.
    /// </summary>
    /// <remarks>
    /// The converter supports:
    /// <list type="bullet">
    ///   <item><description>Basic comparison operators (=, !=, &lt;, &gt;, &lt;=, &gt;=)</description></item>
    ///   <item><description>Logical operators (AND, OR)</description></item>
    ///   <item><description>String values (quoted)</description></item>
    ///   <item><description>Numeric values (integers and decimals)</description></item>
    ///   <item><description>NULL values</description></item>
    /// </list>
    /// 
    /// Example usage:
    /// <code>
    /// string whereClause = "age >= 18 AND name = 'John'";
    /// var result = SqlParameterConverter.ConvertWhereToParameters(whereClause);
    /// // Use result.ProcessedWhere and result.Parameters in your SQLite command
    /// </code>
    /// </remarks>
    internal class SqlParameterConverter
    {
        private static readonly HashSet<string> ValidOperators = new HashSet<string>
        {
            "=", "!=", "<>", ">", "<", ">=", "<="
        };

        private static readonly HashSet<string> ValidLogicalOperators = new HashSet<string>
        {
            "AND", "OR"
        };

        /// <summary>
        /// Converts a raw WHERE clause string into a parameterized query with SQLite parameters.
        /// </summary>
        /// <param name="whereClause">The raw WHERE clause to convert. Can include multiple conditions joined by AND/OR.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description>ProcessedWhere: The processed WHERE clause with parameters (@param1, @param2, etc.)</description></item>
        ///   <item><description>Parameters: List of SQLiteParameter objects containing the parameter values</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="SqlInjectionException">
        /// Thrown when potential SQL injection attempts are detected, including:
        /// <list type="bullet">
        ///   <item><description>Multiple SQL statements (using semicolons)</description></item>
        ///   <item><description>Dangerous SQL commands (DROP, DELETE, etc.)</description></item>
        ///   <item><description>Comment injection attempts</description></item>
        ///   <item><description>Invalid identifier names</description></item>
        ///   <item><description>Malformed WHERE clause structure</description></item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the input contains invalid numeric values.</exception>
        /// <example>
        /// Simple usage:
        /// <code>
        /// string whereClause = "age >= 18 AND name = 'John'";
        /// var (processedWhere, parameters) = SqlParameterConverter.ConvertWhereToParameters(whereClause);
        /// 
        /// using (var command = new SQLiteCommand(connection))
        /// {
        ///     command.CommandText = $"SELECT * FROM users WHERE {processedWhere}";
        ///     command.Parameters.AddRange(parameters.ToArray());
        ///     // Execute command...
        /// }
        /// </code>
        /// 
        /// Handling exceptions:
        /// <code>
        /// try
        /// {
        ///     var result = SqlParameterConverter.ConvertWhereToParameters(whereClause);
        ///     // Use result...
        /// }
        /// catch (SqlInjectionException ex)
        /// {
        ///     Console.WriteLine($"SQL Injection attempt detected: {ex.Message}");
        /// }
        /// catch (Exception ex)
        /// {
        ///     Console.WriteLine($"Unexpected error: {ex.Message}");
        /// }
        /// </code>
        /// </example>
        public static (string ProcessedWhere, List<SQLiteParameter> Parameters) ConvertWhereToParameters(string whereClause)
        {
            if (string.IsNullOrEmpty(whereClause))
                return (string.Empty, new List<SQLiteParameter>());

            // Validate the WHERE clause for potential SQL injection
            ValidateWhereClause(whereClause);

            List<SQLiteParameter> parameters = new List<SQLiteParameter>();
            int paramCounter = 1;

            string valuePattern = @"(=|!=|<>|>|<|>=|<=)\s*('[^']*'|\d+\.?\d*|NULL)";

            string processedWhere = Regex.Replace(whereClause, valuePattern, match =>
            {
                string op = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                string paramName = $"@param{paramCounter++}";

                if (!ValidOperators.Contains(op))
                {
                    throw new SqlInjectionException($"Invalid operator detected: {op}");
                }

                object paramValue;
                if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    paramValue = DBNull.Value;
                }
                else if (value.StartsWith("'") && value.EndsWith("'"))
                {
                    string stringValue = value.Substring(1, value.Length - 2);
                    ValidateStringValue(stringValue);
                    paramValue = stringValue;
                }
                else
                {
                    if (!decimal.TryParse(value, out decimal numericValue))
                    {
                        throw new SqlInjectionException($"Invalid numeric value: {value}");
                    }
                    paramValue = numericValue;
                }

                parameters.Add(new SQLiteParameter(paramName, paramValue));
                return $"{op} {paramName}";
            });

            // Validate the processed WHERE clause
            ValidateProcessedWhereClause(processedWhere);

            return (processedWhere, parameters);
        }

        private static void ValidateWhereClause(string whereClause)
        {
            // Check for common SQL injection patterns
            string[] dangerousPatterns = new string[]
            {
            ";",                    // Multiple statements
            "--",                   // Comments
            "/*",                   // Block comments
            "*/",
            "UNION",               // UNION attacks
            "DROP",                // DROP attacks
            "DELETE",              // DELETE attacks
            "UPDATE",              // UPDATE attacks
            "INSERT",              // INSERT attacks
            "ALTER",               // ALTER attacks
            "EXEC",                // EXEC attacks
            "EXECUTE",
            "xp_",                 // Extended stored procedures
            "sp_"                  // Stored procedures
            };

            foreach (string pattern in dangerousPatterns)
            {
                if (whereClause.ToUpper().Contains(pattern))
                {
                    throw new SqlInjectionException($"Potential SQL injection detected: {pattern}");
                }
            }

            // Validate basic WHERE clause structure
            ValidateWhereClauseStructure(whereClause);
        }

        private static void ValidateWhereClauseStructure(string whereClause)
        {
            // Split the WHERE clause into parts
            string[] parts = whereClause.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                throw new SqlInjectionException("WHERE clause is too short");
            }

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].ToUpper();

                // Check for valid logical operators between conditions
                if (ValidLogicalOperators.Contains(part))
                {
                    if (i == 0 || i == parts.Length - 1)
                    {
                        throw new SqlInjectionException($"Logical operator {part} cannot be at the start or end");
                    }
                    continue;
                }

                // Validate identifiers
                if (i % 4 == 0) // First part of each condition should be an identifier
                {
                    if (!IsValidIdentifier(parts[i]))
                    {
                        throw new SqlInjectionException($"Invalid identifier: {parts[i]}");
                    }
                }
            }
        }

        private static bool IsValidIdentifier(string identifier)
        {
            // Basic identifier validation: letters, numbers, and underscores only
            return Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        private static void ValidateStringValue(string value)
        {
            // Check for nested quotes that might indicate SQL injection
            if (value.Contains("'"))
            {
                throw new SqlInjectionException("Invalid character in string value: nested quotes detected");
            }

            // Check for other potentially dangerous patterns in string values
            string[] dangerousStringPatterns = new string[]
            {
            "@@",           // System variables
            "0x",           // Hex values
            "CHAR(",        // CHAR conversion
            "CONVERT(",     // Type conversion
            "CONCAT(",      // String concatenation
            };

            foreach (string pattern in dangerousStringPatterns)
            {
                if (value.ToUpper().Contains(pattern))
                {
                    throw new SqlInjectionException($"Potentially dangerous pattern detected in string value: {pattern}");
                }
            }
        }

        private static void ValidateProcessedWhereClause(string processedWhere)
        {
            // Validate the final processed WHERE clause
            if (string.IsNullOrWhiteSpace(processedWhere))
            {
                throw new SqlInjectionException("Processed WHERE clause is empty");
            }

            // Check for balanced conditions
            string[] parts = processedWhere.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                throw new SqlInjectionException("Processed WHERE clause is incomplete");
            }

            // Validate parameter names
            foreach (string part in parts)
            {
                if (part.StartsWith("@"))
                {
                    if (!Regex.IsMatch(part, @"^@param\d+$"))
                    {
                        throw new SqlInjectionException($"Invalid parameter name: {part}");
                    }
                }
            }
        }
    }
}