using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using System.Linq;

namespace CodeArtEng.SQLite
{
    public class SqlInjectionException : Exception
    {
        public SqlInjectionException(string message) : base(message) { }
    }

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