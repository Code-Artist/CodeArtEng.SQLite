using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using System.Linq;

namespace CodeArtEng.SQLite
{

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
    internal class SQLiteParameterConverter
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
        /// <exception cref="SQLInjectionException">
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

            // Pre-validate numeric values before regex processing
            ValidateNumericValues(whereClause);

            List<SQLiteParameter> parameters = new List<SQLiteParameter>();
            int paramCounter = 1;

            // Updated regex pattern to handle complex conditions with parentheses
            string valuePattern = @"([a-zA-Z0-9_]+)\s*(=|!=|<>|>|<|>=|<=)\s*('[^']*'|\d+\.?\d*|NULL)";

            string processedWhere = Regex.Replace(whereClause, valuePattern, match =>
            {
                string column = match.Groups[1].Value;
                string op = match.Groups[2].Value;
                string value = match.Groups[3].Value;
                string paramName = $"@param{paramCounter++}";

                if (!ValidOperators.Contains(op))
                {
                    throw new SQLInjectionException($"Invalid operator detected: {op}");
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
                        throw new SQLInjectionException($"Invalid numeric value: {value}");
                    }
                    paramValue = numericValue;
                }

                parameters.Add(new SQLiteParameter(paramName, paramValue));
                return $"{column} {op} {paramName}";
            });

            return (processedWhere, parameters);
        }

        private static void ValidateNumericValues(string whereClause)
        {
            // Find all potential numeric values that aren't properly formatted
            // Look for patterns where an operator is followed by something that 
            // starts with digits but contains non-digit characters (not in quotes)
            string invalidNumberPattern = @"(=|!=|<>|>|<|>=|<=)\s*(\d+[a-zA-Z_]+\w*)(?!')";

            Match match = Regex.Match(whereClause, invalidNumberPattern);
            if (match.Success)
            {
                throw new SQLInjectionException($"Invalid numeric value: {match.Groups[2].Value}");
            }
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
                    throw new SQLInjectionException($"Potential SQL injection detected: {pattern}");
                }
            }

            // Check for balanced parentheses
            int openParenCount = whereClause.Count(c => c == '(');
            int closeParenCount = whereClause.Count(c => c == ')');
            if (openParenCount != closeParenCount)
            {
                throw new SQLInjectionException("Unbalanced parentheses in WHERE clause");
            }

            // Validate column identifiers
            ValidateColumnIdentifiers(whereClause);
        }

        private static void ValidateColumnIdentifiers(string whereClause)
        {
            // Extract column identifiers
            string pattern = @"(?<!\')(?<column>[a-zA-Z0-9_]+)\s*(?:=|!=|<>|>|<|>=|<=)";

            MatchCollection matches = Regex.Matches(whereClause, pattern);

            foreach (Match match in matches)
            {
                string identifier = match.Groups["column"].Value;
                if (!IsValidIdentifier(identifier))
                {
                    throw new SQLInjectionException($"Invalid identifier: {identifier}");
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
                throw new SQLInjectionException("Invalid character in string value: nested quotes detected");
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
                    throw new SQLInjectionException($"Potentially dangerous pattern detected in string value: {pattern}");
                }
            }
        }
    }
}