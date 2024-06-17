using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
            TableName = !string.IsNullOrEmpty(tableName) ? tableName:  sender.SQLName();

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

            ChildTables = Columns.Where(n => n.IsChildTable).ToArray();
            Columns = Columns.Except(ChildTables).ToArray();

            //Sanity Check
            if (Columns.Where(n => n.IsPrimaryKey).Count() > 1)
            {
                throw new NotSupportedException(
                $"Multiple primary keys attribute defined in class {Name}. " +
                $"Table with multiple primary keys is currently not supported!");
            }
            else if (ChildTables.Length > 0)
            {
                if (PrimaryKey == null) throw new FormatException($"Missing primary key for table {TableName}!");
                foreach (SQLTableItem i in ChildTables)
                {
                    if (i.ChildTableInfo.ParentKey.ParentType != TableType)
                        throw new FormatException($"Parent key {TableType} not found in table {i.SQLName}!");
                }
            }
        }
    }

    /// <summary>
    /// Property item which describe SQL table columns / child tables.
    /// </summary>
    internal class SQLTableItem
    {
        /// <summary>
        /// Return string for current object to ease debugging process.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Name;
        /// <summary>
        /// Property name.
        /// </summary>
        public string Name => Property.Name;
        /// <summary>
        /// Database file path if defined by <see cref="SQLDatabseAttribute"/>
        /// </summary>
        public string SecondaryDatabaseFilePath { get; private set; } = string.Empty;
        /// <summary>
        /// Handle to property info.
        /// </summary>
        public PropertyInfo Property { get; private set; }
        /// <summary>
        /// Return true if <see cref="PrimaryKeyAttribute"/> is defined.
        /// </summary>
        public bool IsPrimaryKey { get; private set; } = false;
        /// <summary>
        /// Return true if <see cref="SQLIndexTableAttribute"/> is defined.
        /// </summary>
        public bool IsIndexTable { get; private set; } = false;
        public string IndexTableName { get; private set; } = string.Empty;
        /// <summary>
        /// SQL column name / table name. Default value is property name unless
        /// <see cref="SQLNameAttribute"/> is defined.
        /// </summary>
        public string SQLName { get; private set; }
        /// <summary>
        /// Return true if <see cref="ParentKeyAttribute"/> is defined.
        /// </summary>
        public bool IsParentKey { get; private set; } = false;
        /// <summary>
        /// Return parent type defined in <see cref="ParentKeyAttribute"/>, otherwise return null;
        /// </summary>
        public Type ParentType { get; private set; } = null;

        /// <summary>
        /// Define if <see cref="SQLDataTypeAttribute"/> is defined.
        /// </summary>
        public bool IsDataTypeDefined { get; private set; } = false;
        /// <summary>
        /// Return data type specified by <see cref="SQLDataTypeAttribute"/>. Default value is <see cref="SQLDataType.TEXT"/>
        /// </summary>
        public SQLDataType DataType { get; private set; } = SQLDataType.TEXT;

        /// <summary>
        /// Define if property represent child table.
        /// Property which is class object and list of class are identified as child tables.
        /// </summary>
        public bool IsChildTable { get; private set; } = false;
        /// <summary>
        /// Information table for child object.
        /// </summary>
        internal SQLTableInfo ChildTableInfo { get; private set; }


        Action<Type, object> SetterMethod;
        /// <summary>
        /// Constructor. Create table item from given property.
        /// </summary>
        /// <param name="property"></param>
        /// <exception cref="FormatException"></exception>
        public SQLTableItem(PropertyInfo property)
        {
            Property = property;
            Type ItemType = Property.PropertyType;

            IsPrimaryKey = Attribute.IsDefined(Property, typeof(PrimaryKeyAttribute));

            IndexTableName = SQLName = Property.Name;
            if (Attribute.IsDefined(Property, typeof(SQLNameAttribute)))
            {
                SQLName = (Attribute.GetCustomAttribute(property, typeof(SQLNameAttribute))
                            as SQLNameAttribute)?.Name;
            }

            if (Attribute.IsDefined(Property, typeof(ParentKeyAttribute)))
            {
                if (ItemType != typeof(int) && ItemType != typeof(long))
                    throw new FormatException($"Expecting int or long for ParentKey ${Property.Name}!");
                IsParentKey = true;
                ParentType = (Attribute.GetCustomAttribute(property, typeof(ParentKeyAttribute))
                            as ParentKeyAttribute).Parent;
            }

            if (Attribute.IsDefined(Property, typeof(SQLDataTypeAttribute)))
            {
                IsDataTypeDefined = true;
                DataType = (Attribute.GetCustomAttribute(property, typeof(SQLDataTypeAttribute))
                        as SQLDataTypeAttribute).DataType;
            }

            if (Attribute.IsDefined(Property, typeof(SQLIndexTableAttribute)))
            {
                if (Property.PropertyType != typeof(string))
                    throw new FormatException($"Expecting string with SQLIndex attribute for property ${Property.Name}!");
                IsIndexTable = true;
                string indexTableName = (Attribute.GetCustomAttribute(property, typeof(SQLIndexTableAttribute))
                        as SQLIndexTableAttribute).Name;
                if (!string.IsNullOrEmpty(indexTableName)) IndexTableName = indexTableName;
            }

            if (ItemType.IsGenericType && ItemType.IsClass)
            {
                if (ItemType.Name.StartsWith("List"))
                {
                    IsChildTable = true;
                    Type childType = ItemType.GetGenericArguments()[0];
                    ChildTableInfo = new SQLTableInfo(childType, SQLName);
                }
                else throw new FormatException($"Generic type {ItemType.Name} not supported, only List is allowed!");
            }

            if (Attribute.IsDefined(Property, typeof(SQLDatabseAttribute)))
            {
                if (!IsChildTable) throw new FormatException($"Invalid attribute for {Property.Name}. SQLDatabase attribute can only be define for child table!");
                SecondaryDatabaseFilePath = (Attribute.GetCustomAttribute(property, typeof(SQLDatabseAttribute))
                                    as SQLDatabseAttribute).DatabaseFilePath;
            }
        }

        /// <summary>
        /// Conver property value to SQLite Database type.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public object GetDBValue(object item)
        {
            object result = Property.GetValue(item);
            SQLDataTypeAttribute dType = (SQLDataTypeAttribute)Property.GetCustomAttribute(typeof(SQLDataTypeAttribute));
            if (dType != null)
            {
                switch (dType.DataType)
                {
                    case SQLDataType.TEXT:
                        result = Convert.ChangeType(result, typeof(string));
                        break;

                    case SQLDataType.INTEGER:
                        if (result.GetType() == typeof(DateTime))
                            result = ((DateTime)result).Ticks;
                        else
                            result = Convert.ChangeType(result, typeof(Int64));
                        break;
                }
            }
            result = Property.PropertyType.IsEnum ? result.ToString() : result;
            return result;
        }

        /// <summary>
        /// Convert value from SQLite Database type to property type.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="value"></param>
        public void SetDBValue(object item, object value)
        {
            try
            {
                Type propertyType = Property.PropertyType;
                Type valueType = value.GetType();

                if (propertyType != valueType)
                {
                    //Type conversion
                    if (propertyType.IsEnum) value = Enum.Parse(propertyType, value.ToString());
                    else if ((propertyType == typeof(DateTime)) && (valueType == typeof(long)))
                    {
                        value = new DateTime((long)value);
                    }
                    else value = Convert.ChangeType(value, propertyType);
                }
                Property.SetValue(item, value);
            }
            catch
            {
                Trace.WriteLine($"WARNING: Failed to set property {Property.Name} with value {value}!");
            }
        }
    }
}
