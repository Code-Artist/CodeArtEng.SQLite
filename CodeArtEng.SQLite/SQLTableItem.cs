using System;
using System.Diagnostics;
using System.Reflection;

namespace CodeArtEng.SQLite
{
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
        /// <summary>
        /// Return true if property is array of primitive data type
        /// </summary>
        public bool IsArrayTable { get; private set; } = false;
        public string TableName { get; private set; } = string.Empty;
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

        /// <summary>
        /// Define if column is unique. Set by <see cref="SQLUniqueAttribute"/>
        /// </summary>
        public bool IsUniqueColumn { get; private set; } = false;
        /// <summary>
        /// Define if column is unique with multiple constraint. Set by <see cref="SQLUniqueMultiColumnAttribute"/>
        /// </summary>
        public bool IsUniqueMulltiColumn { get; private set; } = false; 

        MethodInfo SetterMethod, GetterMethod;
        /// <summary>
        /// Constructor. Create table item from given property.
        /// </summary>
        /// <param name="property"></param>
        /// <exception cref="FormatException"></exception>
        public SQLTableItem(PropertyInfo property)
        {
            Property = property;
            Type ItemType = Property.PropertyType;
            SetterMethod = Property.GetSetMethod();
            GetterMethod = Property.GetGetMethod();

            IsPrimaryKey = Attribute.IsDefined(Property, typeof(PrimaryKeyAttribute));
            if (IsPrimaryKey) DataType = SQLDataType.INTEGER;

            TableName = SQLName = Property.Name;
            if (Attribute.IsDefined(Property, typeof(SQLNameAttribute)))
            {
                SQLName = (Attribute.GetCustomAttribute(property, typeof(SQLNameAttribute))
                            as SQLNameAttribute)?.Name;
            }
            if (Attribute.IsDefined(Property, typeof(SQLUniqueAttribute)))
            {
                IsUniqueColumn = true;
            }
            if (Attribute.IsDefined(Property, typeof(SQLUniqueMultiColumnAttribute)))
            {
                IsUniqueMulltiColumn = true;
            }

            if (Attribute.IsDefined(Property, typeof(ParentKeyAttribute)))
            {
                if (ItemType != typeof(int) && ItemType != typeof(long))
                    throw new FormatException($"Expecting int or long for ParentKey ${Property.Name}!");
                IsParentKey = true;
                ParentType = (Attribute.GetCustomAttribute(property, typeof(ParentKeyAttribute))
                            as ParentKeyAttribute).Parent;
            }
            if (Attribute.IsDefined(Property, typeof(SQLIndexTableAttribute)))
            {
                if (Property.PropertyType != typeof(string))
                    throw new FormatException($"Expecting string with SQLIndex attribute for property ${Property.Name}!");
                IsIndexTable = true;
                DataType = SQLDataType.INTEGER;
                string indexTableName = (Attribute.GetCustomAttribute(property, typeof(SQLIndexTableAttribute))
                        as SQLIndexTableAttribute).Name;
                if (!string.IsNullOrEmpty(indexTableName)) TableName = indexTableName;
            }
            else if (ItemType.IsGenericType && ItemType.IsClass)
            {
                if (ItemType.Name.StartsWith("List"))
                {
                    IsChildTable = true;
                    Type childType = ItemType.GetGenericArguments()[0];
                    ChildTableInfo = new SQLTableInfo(childType, SQLName);
                }
                else throw new FormatException($"Generic type {ItemType.Name} not supported, only List is allowed!");
            }
            else if (ItemType.IsArray)
            {
                IsArrayTable = true;
                DataType = ConvertToSQLDataType(ItemType.GetElementType());
                if (Attribute.IsDefined(Property, typeof(SQLArrayTableAtribute)))
                {
                    string arrayTableName = (Attribute.GetCustomAttribute(property, typeof(SQLArrayTableAtribute))
                            as SQLArrayTableAtribute).Name;
                    if (!string.IsNullOrEmpty(arrayTableName)) TableName = arrayTableName;
                }
            }
            else DataType = ConvertToSQLDataType(Property.PropertyType);

            if (Attribute.IsDefined(Property, typeof(SQLDatabseAttribute)))
            {
                if (!IsChildTable) throw new FormatException($"Invalid attribute for {Property.Name}. SQLDatabase attribute can only be define for child table!");
                SecondaryDatabaseFilePath = (Attribute.GetCustomAttribute(property, typeof(SQLDatabseAttribute))
                                    as SQLDatabseAttribute).DatabaseFilePath;
            }

            if (Attribute.IsDefined(Property, typeof(SQLDataTypeAttribute)))
            {
                IsDataTypeDefined = true;
                DataType = (Attribute.GetCustomAttribute(property, typeof(SQLDataTypeAttribute))
                        as SQLDataTypeAttribute).DataType;
            }
        }

        /// <summary>
        /// Convert primitive data type to SQL Data type.
        /// </summary>
        /// <param name="itemType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"></exception>
        private SQLDataType ConvertToSQLDataType(Type itemType)
        {
            SQLDataType result;

            //Assign SQL Column type based on property type.
            if (typeof(int).IsAssignableFrom(itemType) ||
                typeof(long).IsAssignableFrom(itemType) ||
                (itemType == typeof(bool)))
                result = SQLDataType.INTEGER;

            else if (typeof(double).IsAssignableFrom(itemType) ||
                    typeof(float).IsAssignableFrom(itemType))
                result = SQLDataType.REAL;

            else result = SQLDataType.TEXT;

            return result;
        }

        /// <summary>
        /// Conver property value to SQLite Database type.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public object GetDBValue(object item)
        {
            object result = GetterMethod.Invoke(item, null);
            //object result = Property.GetValue(item);
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
                //Property.SetValue(item, value);
                SetterMethod.Invoke(item, new object[] { value });
            }
            catch
            {
                Trace.WriteLine($"WARNING: Failed to set property {Property.Name} with value {value}!");
            }
        }
    }
}
