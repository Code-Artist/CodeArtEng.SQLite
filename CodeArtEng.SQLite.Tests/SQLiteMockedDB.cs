using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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

    /// <summary>
    /// DB table to simulate parent child relationship
    /// </summary>
    public class ChildTable
    {
        [ParentKey(typeof(ParentTable))]
        public int ParentID { get; set; }
        public int Value { get; set; }
    }
    #endregion

    /// <summary>
    /// DB Table for miscellaneous write operation.
    /// </summary>
    public class MiscTable
    {
        public string Text { get; set; }
    }

    internal class SQLiteMockedDB : SQLiteHelper
    {
        Random r = new Random((int)DateTime.Now.Ticks);
        public SQLiteMockedDB(string databaseFile, bool isReadOnly = false) : base()
        {
            base.SetSQLPath(databaseFile, isReadOnly);
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

        public new void ClearTable(string tableName) => base.ClearTable(tableName);

        #endregion

        public string GenerateString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;:',.<>? ";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[r.Next(s.Length)]).ToArray());

        }

        #region [ Table with Primary Key ]

        public TableWithPrimaryKey[] WriteTableWithPrimaryKey(int length)
        {
            List<TableWithPrimaryKey> results = new List<TableWithPrimaryKey>();
            for (int x = 0; x < length; x++)
            {
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

        public TableWithPrimaryKey[] ReadTableWithPrimaryKey()
        {
            TableWithPrimaryKey[] results = ReadFromDatabase<TableWithPrimaryKey>().ToArray();
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

        #region [ Parent Child Table ]

        public ParentTable[] WriteParentTable(int length, int maxChildLength)
        {
            List<ParentTable> results = new List<ParentTable>();
            for (int x = 0; x < length; x++)
            {
                ParentTable p = new ParentTable()
                {
                    Name = GenerateString(r.Next(5, 20)),
                    ChildItems = new List<ChildTable>()
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

        #endregion

        public void WriteMiscKey(string value)
        {
            MiscTable t = new MiscTable() { Text = value };
            WriteToDatabase(t);
        }

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
    }
}
