﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace CodeArtEng.SQLite.Tests
{
    #region [ Source - Table With Primary Key ]

    public enum Options
    {
        OptionA, OptionB, OptionC, OptionD, OptionE
    }

    /// <summary>
    /// DB table with primary key
    /// </summary>
    public class TableWithPrimaryKey : IComparable
    {
        [PrimaryKey]
        public int ID { get; set; }
        [SQLUnique]
        public string Name { get; set; }
        public DateTime Time { get; set; }
        [SQLDataType(SQLDataType.INTEGER)]
        public DateTime TimeAsTicks { get; set; }
        [SQLName("Integer")]
        public int ValueAsInt { get; set; }
        [SQLName("Double")]
        public double ValueAsDouble { get; set; }
        public bool Flag { get; set; }
        public Options OptionAsString { get; set; }
        [SQLDataType(SQLDataType.INTEGER)]
        public Options OptionAsNumber { get; set; }
        [SQLIndexTable("TextAsID")]
        [SQLName("TextID")]
        public string TextAsID { get; set; }

        public int CompareTo(object obj)
        {
            foreach (var property in this.GetType().GetProperties())
            {
                object value1 = property.GetValue(this);
                object value2 = property.GetValue(obj);
                if (!object.Equals(value1, value2))
                    return -1;
            }
            return 0;
        }
    }

    #endregion

    public class TableWithStringPrimaryKey
    {
        [PrimaryKey]
        public string ID { get; set; }
        public string Name { get; set; }
    }

    #region [ Source - Split Table ]
    /// <summary>
    /// DB test table, data stored in separated database
    /// </summary>
    public class SplitTable
    {
        [PrimaryKey]
        public int ID { get; set; }
        public string Name { get; set; }

        [SQLDatabse("TestSubTable.db")]
        [SQLName("SplitTableKeys")]
        public List<SplitTableKeys> Keys { get; set; } = new List<SplitTableKeys>();
    }

    public class SplitTableKeys
    {
        [ParentKey(typeof(SplitTable))]
        public int ParentID { get; set; }
        public string Key { get; set; }
    }

    #endregion

    #region [ Source - Parent and Child ]
    /// <summary>
    /// DB table to test parent and child relationship
    /// </summary>
    public class ParentTable : IComparable
    {
        [PrimaryKey]
        public int ID { get; set; }

        public string Name { get; set; }

        public List<ChildTable> ChildItems { get; set; } = new List<ChildTable>();

        /// <summary>
        /// One to one relationship Text
        /// </summary>
        public ParentExtension ParentEx { get; set; } = null;

        public int CompareTo(object obj)
        {
            foreach (var property in this.GetType().GetProperties())
            {
                object value1 = property.GetValue(this);
                object value2 = property.GetValue(obj);
                if (!object.Equals(value1, value2))
                    return -1;
            }
            return 0;
        }
    }

    public class ParentTable2
    {
        [PrimaryKey]
        public int ID { get; set; }
        public string Name { get; set; }

        public List<ChildTable2> ChildItems { get; set; } = new List<ChildTable2>();

        /// <summary>
        /// One to one relationship
        /// </summary>
        [SQLName("ParentExtension2")]
        public ParentExtension ParentEx { get; set; } = null;
    }

    public class ParentExtension
    {
        [PrimaryKey]
        public int ParentID { get; set; }
        public string Address { get; set; }
    }

    /// <summary>
    /// DB table to simulate parent child relationship
    /// </summary>
    public class ChildTable
    {
        [ParentKey(typeof(ParentTable))]
        public int ParentID { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// DB table to simulate parent child relationship
    /// </summary>
    public class ChildTable2
    {
        [ParentKey(typeof(ParentTable2))]
        public int ParentID { get; set; }
        public int Value { get; set; }
    }

    public class BadParentTable
    {
        [PrimaryKey]
        public int ID { get; set; }
        public List<ChildTableMissingParentKey> Childs { get; set; }
    }
    public class ChildTableMissingParentKey
    {
        public string Value { get; set; }
    }

    #endregion

    #region [ Source - Misc Table ]

    /// <summary>
    /// DB Table for miscellaneous write operation.
    /// </summary>
    public class MiscTable
    {
        public string Text { get; set; }
    }

    #endregion

    #region [ Source - Table with Generic Array ]

    public class TableWithArray
    {
        [PrimaryKey]
        public long ID { get; set; }
        /// <summary>
        /// This is mandatory, otherwise table will be empty with only primary key.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// String data array, many to many implementation
        /// </summary>
        public string[] ArrayData { get; set; }
        [SQLName("ArrayIntValue")]
        public int[] ItemValue { get; set; }
        public string[] EmptyData { get; set; } = null;
    }

    #endregion

    #region [ Source - Table with Multiple Unqiue Constraint ]

    public class TableMultiConstraint
    {
        [PrimaryKey]
        public int ID { get; set; }
        [SQLUniqueMultiColumn]
        public string Name { get; set; }
        [SQLUniqueMultiColumn]
        public int Value { get; set; }
        [SQLIndexTable]
        [SQLName("MultiConstraintLocation")]
        public string Location { get; set; }
    }

    #endregion

    internal class SQLiteMockedDB : SQLiteHelper
    {
        Random r = new Random((int)DateTime.Now.Ticks);
        public SQLiteMockedDB(string databaseFile, bool isReadOnly = false, bool createFile = true) : base()
        {
            base.SetSQLPath(databaseFile, isReadOnly, createFile);
        }

        #region [ Base Forward Methods ]

        public new void ExecuteTransaction(Action transaction) => base.ExecuteTransaction(transaction);
        public new int SQLDefaultTimeout
        {
            get => base.SQLDefaultTimeout;
            set => base.SQLDefaultTimeout = value;
        }
        public new int SQLBusyTimeout
        {
            get => base.SQLBusyTimeout;
            set => base.SQLBusyTimeout = value;
        }
        public new int SQLStepRetries
        {
            get => base.SQLStepRetries;
            set => base.SQLStepRetries = value;
        }

        public new string[] GetTables() => base.GetTables();
        public new string GetTableSchema(string tableName)
        {
            return base.GetTableSchema(tableName);
        }

        public new void ClearAllTables() => base.ClearAllTables();
        public new void ClearTable(string tableName) => base.ClearTable(tableName);
        public new string CreateTable<T>(string tableName = null) => base.CreateTable<T>(tableName);
        public new IndexTable[] IndexTable(string tableName) => base.IndexTable(tableName);

        public new bool TableExists(string tableName)
        {
            return base.TableExists(tableName);
        }

        public void DropTable(string tableName)
        {
            base.ExecuteNonQuery($"DROP TABLE {tableName}");
        }

        #endregion

        public string GenerateString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[r.Next(s.Length)]).ToArray());

        }

        #region [ Table with Primary Key ]

        public TableWithPrimaryKey[] WriteTableWithPrimaryKey(int length)
        {
            List<TableWithPrimaryKey> results = new List<TableWithPrimaryKey>();
            for (int x = 0; x < length; x++)
            {
                string t = "Item_" + (x + 1).ToString();
                TableWithPrimaryKey i = new TableWithPrimaryKey()
                {
                    Name = GenerateString(r.Next(5, 30)),
                    Time = DateTime.Now.AddMinutes(r.Next(1000)),
                    ValueAsInt = r.Next(900000),
                    ValueAsDouble = r.NextDouble() * 50000,
                    Flag = r.NextDouble() > 0.5 ? true : false,
                    OptionAsString = (Options)r.Next(4),
                    TextAsID = GenerateString(r.Next(5, 10))
                };
                i.TimeAsTicks = i.Time;
                i.OptionAsNumber = i.OptionAsString;
                results.Add(i);
            }

            WriteToDatabase(results.ToArray());
            return results.ToArray();
        }

        public TableWithPrimaryKey[] ReadFromTableWithPrimaryKey(string tbName)
        {
            return ReadFromDatabase<TableWithPrimaryKey>(tableName: tbName)?.ToArray();
        }

        public TableWithPrimaryKey[] ReadTableWithPrimaryKey(string condition = null)
        {
            TableWithPrimaryKey[] results = ReadFromDatabase<TableWithPrimaryKey>(whereStatement: condition).ToArray();
            return results;
        }
        public void UpdateTableWithPrimaryKey(params TableWithPrimaryKey[] items)
        {
            WriteToDatabase(items);
        }

        public void DeleteItemFromTableWithPrimaryKey(params TableWithPrimaryKey[] items)
        {
            foreach (TableWithPrimaryKey item in items)
                ExecuteNonQuery($"DELETE FROM TableWithPrimaryKey WHERE ID == {item.ID}");
        }

        #endregion

        #region [ Table With String Primary Key ]

        public TableWithStringPrimaryKey[] WriteTableWithStringPrimaryKey(int length)
        {
            List<TableWithStringPrimaryKey> results = new List<TableWithStringPrimaryKey>();
            for (int x = 0; x < length; x++)
            {
                TableWithStringPrimaryKey i = new TableWithStringPrimaryKey()
                {
                    ID = GenerateString(5),
                    Name = GenerateString(10)
                };
                results.Add(i);
            }

            WriteToDatabase(results.ToArray());
            return results.ToArray();
        }

        public TableWithStringPrimaryKey[] ReadTableWithStringPrimaryKey()
        {
            return ReadFromDatabase<TableWithStringPrimaryKey>()?.ToArray();
        }

        public void UpdateTableWithStringPrimaryKey(params TableWithStringPrimaryKey[] items)
        {
            WriteToDatabase(items);
        }

        #endregion

        #region [ Parent Child Table ]

        public void WriteParentTableEmptyChildrens()
        {
            ParentTable t = new ParentTable() { Name = "Table with Empty Child" };
            t.ChildItems = null;
            WriteToDatabase(t);
        }

        public ParentTable2[] WriteParentTable2(int length, int maxChildLength)
        {
            List<ParentTable2> results = new List<ParentTable2>();
            for (int x = 0; x < length; x++)
            {
                ParentTable2 p = new ParentTable2()
                {
                    Name = GenerateString(r.Next(5, 20)),
                    ChildItems = new List<ChildTable2>(),
                    ParentEx = (x > 2) ? new ParentExtension() { Address = GenerateString(r.Next(5, 20)) } : null
                };
                for (int y = 0; y < r.Next(1, maxChildLength); y++)
                {
                    ChildTable2 c = new ChildTable2()
                    {
                        Value = r.Next(1, 20000)
                    };
                    p.ChildItems.Add(c);
                }
                results.Add(p);
            }

            WriteToDatabase(results.ToArray());
            return results.ToArray();
        }
        public ParentTable2[] ReadParentTable2()
        {
            return ReadFromDatabase<ParentTable2>().ToArray();
        }

        public ParentTable[] WriteParentTable(int length, int maxChildLength)
        {
            List<ParentTable> results = new List<ParentTable>();
            for (int x = 0; x < length; x++)
            {
                ParentTable p = new ParentTable()
                {
                    Name = GenerateString(r.Next(5, 20)),
                    ChildItems = new List<ChildTable>(),
                    ParentEx = (x > 2) ? new ParentExtension() { Address = GenerateString(r.Next(5, 20)) } : null
                };
                for (int y = 0; y < r.Next(1, maxChildLength); y++)
                {
                    ChildTable c = new ChildTable()
                    {
                        Value = r.Next(1, 20000)
                    };
                    p.ChildItems.Add(c);
                }
                results.Add(p);
            }

            WriteToDatabase(results.ToArray());
            return results.ToArray();
        }
        public ParentTable[] ReadParentTable()
        {
            return ReadFromDatabase<ParentTable>().ToArray();
        }
        public void UpdateParentTable(params ParentTable[] items)
        {
            WriteToDatabase(items);
        }

        public void DeleteItemsFromParentTable(params ParentTable[] items)
        {
            DeleteFromDatabaseByID(items);
        }

        public int ParentTableCountChildItems()
        {
            return ReadFromDatabase<ChildTable>(tableName: "ChildItems").Count;
        }

        public void WriteBadParentTable()
        {
            WriteToDatabase(new BadParentTable()
            {
                Childs = new List<ChildTableMissingParentKey>() {
                    new ChildTableMissingParentKey() { Value ="Test"} }
            });
        }

        #endregion

        public void WriteMiscKey(string value)
        {
            MiscTable t = new MiscTable() { Text = value };
            WriteToDatabase(t);
        }

        #region [ Split Table ]

        public SplitTable[] WriteSplitTable(int length)
        {
            List<SplitTable> results = new List<SplitTable>();
            for (int x = 0; x < length; x++)
            {
                SplitTable s = new SplitTable()
                {
                    Name = GenerateString(r.Next(10, 30)),
                };
                for (int y = 0; y < 5; y++)
                {
                    s.Keys.Add(new SplitTableKeys() { Key = GenerateString(10) });
                }

                results.Add(s);
            }

            WriteToDatabase(results.ToArray());
            return results.ToArray();
        }
        public SplitTable[] ReadSplitTable()
        {
            return ReadFromDatabase<SplitTable>().ToArray();
        }

        #endregion

        #region [ Index Table ]

        public IndexTable[] ReadIndexTableFromDB(string tableName)
        {
            return ReadFromDatabase<IndexTable>(tableName: tableName).ToArray();
        }

        public void WriteIndexTableToDB(string tableName, params IndexTable[] items)
        {
            WriteToDatabase(items, tableName);
        }

        #endregion

        #region [ Table with Array ]

        public TableWithArray[] ReadTableWithArrays()
        {
            return ReadFromDatabase<TableWithArray>().ToArray();
        }

        public TableWithArray[] WriteTableWithArrays(int itemlength, int arraylength)
        {
            List<TableWithArray> items = new List<TableWithArray>();
            for (int x = 0; x < itemlength; x++)
            {
                TableWithArray i = new TableWithArray();
                i.ID = (long)((x + 1) * 1e9);
                i.Name = GenerateString(5);
                i.ArrayData = new string[arraylength];
                i.ItemValue = new int[arraylength];
                for (int y = 0; y < arraylength; y++)
                {
                    i.ArrayData[y] = GenerateString(10);
                    i.ItemValue[y] = r.Next(1000);
                }
                i.EmptyData = new string[2] { "A", "B" };
                items.Add(i);
            }
            WriteToDatabase(items.ToArray());
            return items.ToArray();
        }

        public void DeleteItemsFromArrayDataTable(long itemIndex)
        {
            DeleteFromDatabase($"WHERE ID == {itemIndex}", "ArrayData");
        }

        #endregion

        #region [ Table with Multi Columns Constraint ]

        public TableMultiConstraint[] ReadTableWithMultiConstraint()
        {
            return ReadFromDatabase<TableMultiConstraint>().ToArray();
        }

        public void WriteTableMultiConstraint(params TableMultiConstraint[] items)
        {
            WriteToDatabase(items);
        }

        #endregion
    }
}
