using System;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Ignore properties from table read and write operation.
    /// </summary>
    public class IgnoreSQLColumnAttribute : Attribute
    {
        public string Reason { get; } = string.Empty;
        public IgnoreSQLColumnAttribute() { }
        public IgnoreSQLColumnAttribute(string reason) => Reason = reason;
    }

    /// <summary>
    /// Mark property as primary key.
    /// </summary>
    public class PrimaryKeyAttribute : Attribute { }

    /// <summary>
    /// Define key as ID for parent table
    /// </summary>
    public class ParentKeyAttribute : Attribute
    {
        public Type Parent { get; }
        public ParentKeyAttribute(Type parentType) => Parent = parentType;
    }

    /// <summary>
    /// Define SQL Table / Column name for specific element.
    /// </summary>
    public class SQLNameAttribute : Attribute
    {
        public string Name { get; private set; } = string.Empty;
        public SQLNameAttribute(string columnName) => Name = columnName;
    }

    /// <summary>
    /// Define property where content is stored as ID in SQL Table. Value are stored separate in key value table.
    /// </summary>
    /// <remarks>Use for string properties only</remarks>
    public class SQLIndexTableAttribute : Attribute
    {
        public string Name { get; private set; } = string.Empty;
        public SQLIndexTableAttribute(string indexTableName) => Name = indexTableName;
        public SQLIndexTableAttribute() { }
    }

    public enum SQLDataType
    {
        TEXT,
        INTEGER,
        REAL
    }

    /// <summary>
    /// Properties to override data type in SQLite column
    /// </summary>
    public class SQLDataTypeAttribute : Attribute
    {
        public SQLDataType DataType { get; } = SQLDataType.TEXT;
        public SQLDataTypeAttribute(SQLDataType dataType) => DataType = dataType;

    }

    /// <summary>
    /// Define secondary database path where table is stored.
    /// If only filename is provided, use same path as main database file.
    /// Otherwise, use full file path provided.
    /// </summary>
    public class SQLDatabseAttribute : Attribute
    {
        public string DatabaseFilePath { get; }
        public SQLDatabseAttribute(string databaseFile) => DatabaseFilePath = databaseFile;
    }

    /// <summary>
    /// Mark column as unique, add unique keyword to column when create table.
    /// </summary>
    public class SQLUniqueAttribute : Attribute { }

    /// <summary>
    /// Define multi columns unique constraints.
    /// Example: UNIQUE ("ColA", "ColB" ... )
    /// </summary>
    public class SQLUniqueMultiColumnAttribute: Attribute { }  

}