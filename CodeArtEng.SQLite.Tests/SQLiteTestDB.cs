using System;
using System.Collections.Generic;

namespace CodeArtEng.SQLite
{
    public class TableA
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string NameStr => ID + "-" + Name;
        [IgnoreSQLColumn]
        public string LocalProperty { get; set; } = "DO NOT CHANGE";
    }

    public enum Option
    {
        OptionA,
        OptionB,
        OptionC
    }

    public class TableB : TableA
    {
        public TableB() { }
        public TableB(int id, string name, double cost, Option option)
        {
            ID = id; Name = name; Cost = cost;
            OptionAsInt = Option = option;
        }
        public double Cost { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public bool Flag { get; set; } = true;
        [IgnoreSQLColumn]
        public TableA NestedTable { get; set; } = new TableA();
        public Option Option { get; set; }
        [SQLDataType(SQLDataType.INTEGER)]
        public Option OptionAsInt { get; set; }
    }


    internal class SQLiteTestDB : SQLiteHelper
    {
        public SQLiteTestDB(string databaseFile) : base()
        {
            SetSQLPath(databaseFile);
        }


        public Dictionary<int, string> TableAItems = new Dictionary<int, string>();
        public void ReadTableA()
        {
            //Execute query, read into Dictionary
            TableAItems.Clear();
            ExecuteQuery("SELECT * FROM TableA", (r) =>
            {
                while (r.Read())
                {
                    TableAItems.Add(r.GetInt32(0), r.GetString(1));
                }
            });
        }

        public object ExecuteScalar_TableA(int id)
        {
            return ExecuteScalar("SELECT Name FROM TableA WHERE Id = " + id);
        }

        public new object ExecuteScalar(string query)
        {
            return base.ExecuteScalar(query);
        }

        public new int ExecuteNonQuery(string query)
        {
            return base.ExecuteNonQuery(query);
        }

        public new void ExecuteTransaction(Action transaction)
        {
            base.ExecuteTransaction(transaction);
        }

        public string GetDBSchema(string table)
        {
            return base.GetTableSchema(table);
        }

        public new IList<T> ReadFromDatabase<T>(string c = null) where T : class, new()
        {
            return base.ReadFromDatabase<T>(c);
        }

        public void WriteItemsToDatabase(TableB[] items)
        {
            WriteToDatabase(items);
        }

        public new void WriteToDatabase<T>(T[] items) where T : class
        {
            base.WriteToDatabase<T>(items);
        }

        public new string[] GetTables() => base.GetTables();
        public new void ClearAllTables() => base.ClearAllTables();
        public new void ClearTable<T>() => base.ClearTable<T>();
        public new void ClearTable(string tableName) => base.ClearTable(tableName);

    }
}
