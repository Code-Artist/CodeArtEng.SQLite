using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Information for class which represent SQL database table.
    /// </summary>
    internal class SQLTableInfo
    {
        /// <summary>
        /// Return string for current object to ease debugging process.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Name;
        /// <summary>
        /// Flag which mark table and columns are vefified with database
        /// </summary>
        public bool Validated { get; set; } = false;
        /// <summary>
        /// Object which describe the table structure.
        /// </summary>
        public Type TableType { get; private set; }
        /// <summary>
        /// Return name of current object, which is name of <see cref="TableType"/>
        /// </summary>
        public string Name => TableType.Name;
        /// <summary>
        /// SQL Table Name.
        /// In method <see cref="SQLiteHelper.ReadFromDatabase{T}(string)"/> and <see cref="SQLiteHelper.WriteToDatabase{T}(T[])"/>,
        /// type name is used as table name. When object is declared as child table, property name
        /// is used as table name.
        /// Use <see cref="SQLNameAttribute"/> to override default table name in properties or class declaration.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Properties excluded from database columns.
        /// </summary>
        public PropertyInfo[] IgnoredProperties { get; private set; }
        /// <summary>
        /// Database columns.
        /// </summary>
        public SQLTableItem[] Columns { get; private set; }
        /// <summary>
        /// Primary key for current table / object if defined.
        /// Return null if no primary key is defined.
        /// </summary>
        public SQLTableItem PrimaryKey { get; private set; }
        /// <summary>
        /// Properties which hold ID to parent table. Expecting integer value.
        /// </summary>
        public SQLTableItem ParentKey { get; private set; }
        /// <summary>
        /// Properties where values are defined in key value pair table in database.
        /// Table name defined by property name or <see cref="SQLNameAttribute"/>.
        /// Key index are stored in current table.
        /// </summary>
        public SQLTableItem[] IndexKeys { get; private set; }
        /// <summary>
        /// Array Table
        /// </summary>
        public SQLTableItem[] ArrayTables { get; private set; }
        /// <summary>
        /// Children table.
        /// </summary>
        public SQLTableItem[] ChildTables { get; private set; }


        /// <summary>
        /// Constructor for Child Table. Override table name when necessary.
        /// One to one: Property with class object.
        /// One to many: List property with class object.
        /// </summary>
        /// <param name="sender"></param>
        /// <exception cref="NotSupportedException">Error when mulitple primary keys is defined.</exception>
        /// <exception cref="FormatException">
        /// 1. Child table exists but Primary key not defined.
        /// 2. Child table does not contain parent key for current type.
        /// </exception>
        public SQLTableInfo(Type sender, string tableName = null)
        {
            TableType = sender;
            TableName = !string.IsNullOrEmpty(tableName) ? tableName : sender.SQLName();

            PropertyInfo[] properties = sender.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray();
            IgnoredProperties = properties.Where(n => Attribute.IsDefined(n, typeof(IgnoreSQLColumnAttribute))).ToArray();
            properties = properties.Except(IgnoredProperties).ToArray();

            List<SQLTableItem> items = new List<SQLTableItem>();
            foreach (PropertyInfo i in properties) items.Add(new SQLTableItem(i));
            Columns = items.ToArray();

            //Assign Variables
            PrimaryKey = Columns.FirstOrDefault(n => n.IsPrimaryKey);
            if (PrimaryKey != null) Columns = Columns.Except(new[] { PrimaryKey }).ToArray();

            //Limit to one parent key only.
            SQLTableItem[] parentKeys = Columns.Where(n => n.IsParentKey).ToArray();
            if (parentKeys.Length > 1) throw new FormatException($"Declaration error in class {Name}, only 1 parent key is allowed!");
            ParentKey = parentKeys.FirstOrDefault();
            IndexKeys = Columns.Where(n => n.IsIndexTable).ToArray();
            ArrayTables = Columns.Where(n => n.IsArrayTable).ToArray();

            ChildTables = Columns.Where(n => n.IsChildTable).ToArray();
            Columns = Columns.Except(ChildTables).Except(ArrayTables).ToArray();

            //Sanity Check
            if (Columns.Where(n => n.IsPrimaryKey).Count() > 1)
            {
                throw new NotSupportedException(
                $"Multiple primary keys attribute defined in class {Name}. " +
                $"Table with multiple primary keys is currently not supported!");
            }
            
            if (ChildTables.Length > 0)
            {
                if (PrimaryKey == null) throw new FormatException($"Missing primary key for table {TableName}!");
                foreach (SQLTableItem i in ChildTables)
                {
                    if (i.IsChildTable)
                    {
                        if (i.IsList)
                        {
                            if (i.ChildTableInfo.ParentKey == null)
                            {
                                throw new FormatException($"Parent key is not defined in class " +
                                    $"{i.ChildTableInfo.Name} for properties {TableName}.{i.Name}!");
                            }
                            if (i.ChildTableInfo.ParentKey.ParentType != TableType)
                                throw new FormatException($"Parent key {TableType} not found in table {i.SQLName}!");
                        }
                        else
                        {
                            if (i.ChildTableInfo.PrimaryKey == null)
                            {
                                throw new FormatException($"Primary key is not defined in class " +
                                    $"{i.ChildTableInfo.Name} for properties {TableName}.{i.Name}!");
                            }
                        }
                    }
                }
            }

            if(ArrayTables.Length > 0)
            {
                if (PrimaryKey == null) throw new FormatException($"Table {TableName} with array properties must have primary key!");
            }
        }

    }
}
