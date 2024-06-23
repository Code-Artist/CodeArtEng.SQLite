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


            if (Attribute.IsDefined(Property, typeof(SQLIndexTableAttribute)))
            {
                if (Property.PropertyType != typeof(string))
                    throw new FormatException($"Expecting string with SQLIndex attribute for property ${Property.Name}!");
                IsIndexTable = true;
                DataType = SQLDataType.INTEGER;
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

            if (!IsChildTable)
            {
                if (Attribute.IsDefined(Property, typeof(SQLDataTypeAttribute)))
                {
                    IsDataTypeDefined = true;
                    DataType = (Attribute.GetCustomAttribute(property, typeof(SQLDataTypeAttribute))
                            as SQLDataTypeAttribute).DataType;
                }
                else
                {
                    Type itemType = Property.PropertyType;

                    //Assign SQL Column type based on property type.
                    if (typeof(int).IsAssignableFrom(itemType) ||
                        typeof(long).IsAssignableFrom(itemType) ||
                        (ItemType == typeof(bool)))
                        DataType = SQLDataType.INTEGER;

                    else if (typeof(double).IsAssignableFrom(itemType) ||
                            typeof(float).IsAssignableFrom(itemType))
                        DataType = SQLDataType.REAL;
                }
            }
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
