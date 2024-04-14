using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

//ToDo: TestCase: Read from tables which have more columns than class
namespace CodeArtEng.SQLite.Tests
{
    [TestFixture]
    internal class SQLiteHelperTests
    {
        private readonly SQLiteTestDB DB;

        public SQLiteHelperTests()
        {
            DB = new SQLiteTestDB("TestDB.db");
        }

        #region [ Basic Operations ]

        [Test]
        public void DatabaseReadOnly()
        {
            Assert.That(DB.ReadOnly, Is.False);
        }

        [Test]
        public void DatabaseOnline()
        {
            Assert.That(DB.IsDatabaseOnline(), Is.True);
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test]
        public void DummyDatabaseOffline()
        {
            SQLiteTestDB db = new SQLiteTestDB("Dummy.db");
            Assert.That(db.IsDatabaseOnline(), Is.False);
        }

        [Test]
        public void ReadTableA()
        {
            DB.TableAItems.Clear();
            DB.ReadTableA();
            Assert.That(DB.TableAItems.Count, Is.EqualTo(3));
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test]
        public void ReadTableACondition()
        {
            IList<TableA> items = DB.ReadFromDatabase<TableA>("WHERE ID == 1");
            Assert.That(items.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadTableA_KeepOpen()
        {
            DB.KeepDatabaseOpen = true;
            try
            {
                DB.ReadTableA();
                Assert.That(DB.IsConnected, Is.True);
            }
            finally
            {
                DB.KeepDatabaseOpen = false;
            }
        }

        [Test]
        public void ExecuteScalar()
        {
            Assert.That(DB.ExecuteScalar_TableA(3), Is.EqualTo("Ethan"));
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test]
        public void ExecuteScalarTestInvalid_ReturnNull()
        {
            Assert.That(DB.ExecuteScalar_TableA(10), Is.Null);
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test]
        public void ExecuteNonQuery()
        {
            Assert.That(DB.ExecuteNonQuery("INSERT OR REPLACE INTO TABLEA (ID, Name) VALUES (2, 'MARY')"), Is.EqualTo(1));
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test]
        public void ExecuteNonQuery_BadQuery_Exception()
        {
            Assert.Throws<System.Data.SQLite.SQLiteException>(() => { DB.ExecuteNonQuery("BAD QUERY"); });
        }

        [Test]
        public void Transactions()
        {
            Dictionary<int, string> B = new Dictionary<int, string>
            {
                { 1, "A" },
                { 2, "B" },
                { 3, "C" },
                { 4, "D" }
            };


            DB.ExecuteTransaction(() =>
            {
                foreach (KeyValuePair<int, string> i in B)
                {
                    DB.ExecuteNonQuery($"INSERT INTO TABLEB (Id, Name) VALUES ({i.Key}, '{i.Value}')");
                    Assert.That(DB.IsConnected); //Make sure database still connected 
                }
            });
            Assert.That(DB.IsConnected, Is.False);
            Assert.That(DB.ExecuteScalar("SELECT COUNT(*) FROM TABLEB"), Is.EqualTo(4));
        }

        [Test]
        public void GetTableNames()
        {
            Assert.That(string.Join(",", DB.GetTables()).StartsWith("TableA,TableB,TableC"));
        }

        [Test]
        public void GetTableSchema()
        {
            Assert.That(DB.GetDBSchema("TableB").StartsWith("CREATE"), Is.True);
        }

        [Test]
        public void GetTableSchema_TableNotExists_ReturnNull()
        {
            Assert.That(DB.GetDBSchema("NoSuchTable"), Is.Null);
        }

        [Test]
        public void GetTableSchemaSQLInject()
        {
            //Make sure table is not dropped due to SQL Injection
            string schema = DB.GetDBSchema("TableB'; DROP TABLE TableB; --");
            Assert.That(DB.GetTables().Contains("TableB"));
            Assert.That(schema, Is.Null);
        }

        [Test]
        public void ReadFromDatabase_TableA()
        {
            TableA[] results = DB.ReadFromDatabase<TableA>().ToArray();
            Assert.That(results.Length, Is.EqualTo(3));
            Assert.That(results[0].LocalProperty, Is.EqualTo("DO NOT CHANGE"));
        }

        [Test]
        public void WriteItemsToDatabase_TableB()
        {
            DB.ClearTable(nameof(TableB));
            Assert.That(DB.ReadFromDatabase<TableB>().Count, Is.EqualTo(0));
            List<TableB> BItems = new List<TableB>
            {
                new TableB(10, "Lina Peck", 120.21, Option.OptionA),
                new TableB(11, "Dion Rice", 45.99, Option.OptionB),
                new TableB(12, "Jurassic Park", 68.10, Option.OptionC)
            };
            BItems.Last().Flag = false;

            DB.WriteItemsToDatabase(BItems.ToArray());
            TableB[] items = DB.ReadFromDatabase<TableB>().ToArray();
            Assert.That(items.Length, Is.EqualTo(3));
            Assert.That(items.Last().Flag, Is.False);
        }

        [Test]
        public void DeleteTable_NoSuchTable_Exception()
        {
            Assert.Throws<InvalidOperationException>(() => { DB.ClearTable("NoSuchTable"); });
        }

        #endregion 

        #region [ Primary Key ]
        public class TableC
        {
            public TableC() { }
            [PrimaryKey]
            public int ID { get; set; }
            public string Name { get; set; }
            public TableC(string name) { Name = name; }
        }

        [Test]
        public void WriteTableWithPrimaryKey()
        {
            string text = "Mary had a little lamb.";
            List<TableC> items = new List<TableC>();
            foreach (string t in text.Split(' '))
            {
                items.Add(new TableC(t));
            }
            DB.WriteToDatabase(items.ToArray());
            List<TableC> readback = DB.ReadFromDatabase<TableC>().ToList();
            Assert.That(readback.Count, Is.EqualTo(5));
            Assert.That(readback.First(n => n.ID == 3).Name, Is.EqualTo("a"));

            //Verify that primary key items does not get duplicated
            DB.WriteToDatabase(items.ToArray());
            readback = DB.ReadFromDatabase<TableC>().ToList();
            Assert.That(readback.Count, Is.EqualTo(5));
        }


        [Test]
        public void WriteTableWithPrimaryKey_Transaction()
        {
            DB.ClearTable<TableC>();
            string text = "Mary had a little lamb.";
            List<TableC> items = new List<TableC>();
            foreach (string t in text.Split(' '))
            {
                items.Add(new TableC(t));
            }
            DB.ExecuteTransaction(() =>
            {
                DB.WriteToDatabase(items.ToArray());
            });
            List<TableC> readback = DB.ReadFromDatabase<TableC>().ToList();
            Assert.That(readback.Count, Is.EqualTo(5));
            Assert.That(readback.First(n => n.ID == 5).Name, Is.EqualTo("lamb."));
        }

        #endregion

        #region [ SQLRelation, Foreign Key ]
        public class Department
        {
            //DB Table: ID, Name

            [PrimaryKey]
            public int ID { get; set; }
            public string Name { get; set; }
            [SQLName("Employee")]
            public List<Employee> Employees { get; set; } = new List<Employee>();
        }

        public class Employee
        {
            //DB Table: ID, Name, DepartmentID

            [PrimaryKey]
            public int ID { get; set; }
            [SQLIndexTable]
            [SQLName("NameID")]
            public string Name { get; set; }
            [ParentKey(typeof(Department))]
            public int DepartmentID { get; set; }
        }

        [Test]
        public void WriteTableWithRelation()
        {
            List<Department> department = new List<Department>();
            for (int n = 0; n < 3; n++)
            {
                Department pDept = new Department() { Name = "Dept " + n };
                department.Add(pDept);
                for (int x = 0; x < 3; x++)
                    pDept.Employees.Add(new Employee() { Name = pDept.Name + "_Employee " + x });
            }
            DB.WriteToDatabase(department.ToArray());
            TestContext.Progress.WriteLine("Write Department table completed.");
            Debug.WriteLine("Write operation completed.\n");

            List<Department> departmentRead = DB.ReadFromDatabase<Department>().ToList();
            Assert.That(departmentRead.Count, Is.EqualTo(3));
            Assert.That(departmentRead.First().Employees.Count, Is.EqualTo(3));

            //Validate Index Table conversion success.
            Assert.That(departmentRead.First().Employees.First().Name.StartsWith("Dept"));
        }
        #endregion

        #region [ Store DateTime as Ticks ]

        public class TimeTable
        {
            [SQLDataType(SQLDataType.INTEGER)]
            public DateTime Time { get; set; }
            public string Value { get; set; }
        }

        [Test]
        public void StoreTestTimeAsTicks()
        {
            if (DB.GetTables().Contains("TimeTable"))
                DB.ExecuteNonQuery("DROP TABLE TimeTable");

            DB.ExecuteNonQuery(@"CREATE TABLE ""TimeTable"" (
	                            ""Time""	INTEGER,
	                            ""Value""	TEXT
                            )");


            List<TimeTable> items = new List<TimeTable>();
            items.Add(new TimeTable()
            {
                Time = new DateTime(2024, 2, 1, 15, 30, 00),
                Value = "Time 1"
            });
            items.Add(new TimeTable()
            {
                Time = new DateTime(2024, 5, 1, 9, 23, 10),
                Value = "Time 2"
            });

            DB.WriteToDatabase(items.ToArray());
            List<TimeTable> readBack = DB.ReadFromDatabase<TimeTable>().ToList();

            Assert.That(items.Count, Is.EqualTo(readBack.Count));
            Assert.That(readBack[0].Time, Is.EqualTo(items[0].Time));
        }


        #endregion

        [SQLName("TableA")]
        class TableA2
        {
            public string Name { get; set; }
        }

        [Test]
        public void ReadFromTableA_ReducedColumns()
        {
            TableA2[] items = DB.ReadFromDatabase<TableA2>().ToArray();
            Assert.That(items.Count, Is.EqualTo(3));
        }
    }
}
