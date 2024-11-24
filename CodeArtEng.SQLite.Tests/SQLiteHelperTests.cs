using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

//ToDo: TestCase: Read from tables which have more columns than class
namespace CodeArtEng.SQLite.Tests
{
    //Test Cases:
    // Read table
    // Read tablw with condition
    // Read table to class - class have less column
    // Read table to class - class have more columns
    // Write table 
    // Write table with properties from another DB
    // Utility:
    //  Get Table name
    //  Get table schema
    //  Get table schema, table not exitss.


    [TestFixture]
    internal class SQLiteHelperTests
    {
        private readonly SQLiteMockedDB DB;
        private string TestDBPath = "TestDB.db";

        public SQLiteHelperTests()
        {
            if (File.Exists(TestDBPath)) { File.Delete(TestDBPath); }
            DB = new SQLiteMockedDB(TestDBPath);
        }

        #region [ Status Check ]

        [Test, Order(0)]
        public void DatabaseReadOnly_False()
        {
            Assert.That(DB.ReadOnly, Is.False);
        }

        [Test, Order(0)]
        public void DatabaseReadOnly()
        {
            SQLiteMockedDB temp = new SQLiteMockedDB("TestDB.db", isReadOnly: true);
            Assert.That(temp.ReadOnly, Is.True);
        }

        [Test, Order(0)]
        public void DatabaseOnline_NotConnected()
        {
            Assert.That(DB.IsDatabaseOnline(), Is.True);
            Assert.That(DB.IsConnected, Is.False);
        }

        [Test, Order(0)]
        public void DummyDatabaseOffline_FileNotExists()
        {
            SQLiteMockedDB db = new SQLiteMockedDB("Dummy.db", createFile: false);
            Assert.That(db.IsDatabaseOnline(), Is.False);
        }

        #endregion

        #region [ Utility ]

        [Test]
        public void StringGeneratorCheck()
        {
            string result = DB.GenerateString(20);
            Assert.That(result.Length, Is.EqualTo(20));
        }

        [Test]
        public void GetTableNames()
        {
            string[] tables = DB.GetTables();
            Assert.That(tables.Length > 5);
            Assert.That(tables.Contains("ParentTable"));
            Assert.That(tables.Contains("TableWithPrimaryKey"));
        }

        [Test]
        public void GetTableSchema()
        {
            string schema = DB.GetTableSchema("ParentTable");
            Assert.That(schema.StartsWith("CREATE") && schema.Contains("ParentTable"));
        }

        [Test]
        public void GetTableSchema_TableNotExists_ReturnNull()
        {
            Assert.That(DB.GetTableSchema("NoSuchTable") == null);
        }

        [Test]
        public void GetTableSchemaSQLInject()
        {
            string schema = DB.GetTableSchema("'ParentTable'; DROP TABLE TableB; --");
            Assert.That(DB.GetTables().Contains("ParentTable"));
            Assert.That(schema, Is.Null);
        }

        [Test]
        public void ClearTable_NoSuchTable_Exception()
        {
            Assert.Throws<InvalidOperationException>(() => { DB.ClearTable("NoSuchTable"); });
        }

        #endregion

        #region [ 1 - Table with Primary Key ]

        int TbLength = 100;
        TableWithPrimaryKey[] Source, Readback;

        [Test, Order(10)]
        public void PK_ReadTableNotExists()
        {
            //Read table which is not exists in database, should not fail
            Readback = DB.ReadTableWithPrimaryKey();
            Assert.That(Readback.Length, Is.EqualTo(0));

            //Read non
            Assert.That(DB.ReadIndexTableFromDB("TextAsID").Length, Is.EqualTo(0));
        }


        [Test, Order(11)]
        public void PK_WriteTableWithPrimaryKey()
        {

            Source = DB.WriteTableWithPrimaryKey(TbLength);
            Assert.That(Source.Length, Is.EqualTo(TbLength));

            //Primary key check.
            Assert.That(!Source.Select(n => n.ID).Contains(0));
            Assert.That(Source.Select(n => n.ID).Distinct().Count() == TbLength);
        }

        [Test, Order(12)]
        public void PK_ReadTableWithPrimaryKey()
        {
            Readback = DB.ReadTableWithPrimaryKey();
            Assert.That(Readback.Length, Is.EqualTo(TbLength));
        }

        [Test, Order(13)]
        public void PK_CompareTableWithPrimaryKey()
        {
            foreach (TableWithPrimaryKey r in Readback)
            {
                TableWithPrimaryKey s = Source.FirstOrDefault(n => n.ID == r.ID);
                if (s == null) Assert.Fail("No match found for key " + r.ID);
                if (s.CompareTo(r) != 0) Assert.Fail("Readback value mismatched");
            }
        }

        [Test, Order(14)]
        public void PK_ModifyExistingItemsValue()
        {
            List<TableWithPrimaryKey> items = new List<TableWithPrimaryKey>();
            for (int x = 0; x < 3; x++)
            {
                TableWithPrimaryKey k = Source.First(n => n.ID == (x + 1));
                k.Name = "Name " + x.ToString();
                items.Add(k);
            }
            DB.UpdateTableWithPrimaryKey(items.ToArray());

            Readback = DB.ReadTableWithPrimaryKey();
            Assert.That(Readback.Length == TbLength);
            for (int x = 0; x < 3; x++)
            {
                Assert.That(items[x].CompareTo(Readback[x]) == 0, "Compare failed at index " + x.ToString());
            }
        }

        [Test, Order(15)]
        public void PK_RemoveItemsFromList()
        {
            TableWithPrimaryKey itemToDelete = Source[8];
            DB.DeleteItemFromTableWithPrimaryKey(itemToDelete);
            TableWithPrimaryKey[] readBackItems = DB.ReadTableWithPrimaryKey();
            Assert.That(readBackItems.Length == (TbLength - 1));
            Assert.That(readBackItems.FirstOrDefault(n => n.ID == itemToDelete.ID) == null);
        }

        [Test, Order(16)]
        public void PK_AddNewItems()
        {
            TableWithPrimaryKey newItem = new TableWithPrimaryKey() { Name = "NewItem" };
            DB.UpdateTableWithPrimaryKey(newItem);
            Assert.That(newItem.ID != 0);

            Readback = DB.ReadTableWithPrimaryKey();
            Assert.That(Readback.Length == TbLength);
        }

        [Test, Order(17)]
        public void PK_ReadIndexTable()
        {
            IndexTable[] indexTable = DB.IndexTable("TextAsID");
            Assert.That(indexTable.Length, Is.EqualTo(100));
        }

        #endregion

        #region [ 2 - Parent and Child Table ]

        //Test basic write and read
        //Modify content: add, remove.
        //Read back and verify again

        int ParentLength = 10;
        int MaxChildLength = 20;
        int ParentTableChildItems;
        ParentTable[] ParentTableItems, ParentTableReadback;
        ParentTable[] NewItems;

        [Test, Order(20)]
        public void R_WriteParentChildTable()
        {
            ParentTableItems = DB.WriteParentTable(ParentLength, MaxChildLength);
            ParentTableChildItems = ParentTableItems.Sum(n => n.ChildItems.Count);
            Assert.That(ParentTableItems.Length, Is.EqualTo(ParentLength));
        }

        [Test, Order(21)]
        public void R_ReadParentChildTable()
        {
            ParentTableReadback = DB.ReadParentTable();
            Assert.That(ParentTableReadback.Length, Is.EqualTo(ParentLength));
            Assert.That(ParentTableChildItems == ParentTableReadback.Sum(n => n.ChildItems.Count));
        }

        [Test, Order(22)]
        public void R_ModifyExistingItems()
        {
            List<ParentTable> items = new List<ParentTable>();
            for (int x = 0; x < 3; x++)
            {
                ParentTable k = ParentTableItems.First(n => n.ID == (x + 1));
                k.Name = "Name " + x.ToString();
                items.Add(k);
                for (int y = 0; y < k.ChildItems.Count; y++)
                {
                    k.ChildItems[y].Value = (x * 100) + y;
                }
            }
            DB.UpdateParentTable(items.ToArray());

            ParentTableReadback = DB.ReadParentTable();
            foreach (ParentTable i in items)
            {
                ParentTable ptrItem = ParentTableReadback.FirstOrDefault(n => n.ID == i.ID);
                Assert.That(ptrItem != null);
                Assert.That(i.ChildItems.Count == ptrItem.ChildItems.Count);
            }
        }

        [Test, Order(23)]
        public void R_AddNewItems()
        {
            List<ParentTable> items = new List<ParentTable>();
            for (int x = 0; x < 5; x++)
            {
                ParentTable ptrItem = new ParentTable() { Name = "NewItem " + x.ToString() };
                for (int y = 0; y < 10; y++)
                {
                    ptrItem.ChildItems.Add(new ChildTable() { Value = (x * 100) + y });
                }
                items.Add(ptrItem);
            }
            NewItems = items.ToArray();
            DB.UpdateParentTable(NewItems);

            ParentTable[] readback = DB.ReadParentTable();
            Assert.That(readback.Length == (ParentLength + 5));
            Assert.That(DB.ParentTableCountChildItems() != ParentTableChildItems);
        }

        [Test, Order(24)]
        public void R_DeleteItems()
        {
            DB.DeleteItemsFromParentTable(NewItems);
            ParentTable[] readback = DB.ReadParentTable();
            Assert.That(readback.Length == ParentLength);
            Assert.That(DB.ParentTableCountChildItems() == ParentTableChildItems);
        }


        [Test, Order(25)]
        public void WriteBadParentTable()
        {
            Assert.Throws<FormatException>(() => { DB.WriteBadParentTable(); });
        }

        [Test, Order(26)]
        public void WriteParentTableEmptyChildList()
        {
            DB.WriteParentTableEmptyChildrens();
        }

        #endregion

        #region [ 3 - Split Tables Test ]

        int SplitTableLength = 10;
        SplitTable[] SplitTableItems, SplitTableReadback;

        [Test, Order(30)]
        public void S_WriteSplitTable()
        {
            SplitTableItems = DB.WriteSplitTable(SplitTableLength);
            Assert.That(SplitTableItems.Length, Is.EqualTo(SplitTableLength));
        }

        [Test, Order(31)]
        public void S_ReadSplitTable()
        {
            SplitTableReadback = DB.ReadSplitTable();
            Assert.That(SplitTableReadback.Length, Is.EqualTo(SplitTableLength));
        }

        #endregion

        #region [ 4 - Database Lock ]

        [Test]
        public void SimulateLockedDatabase()
        {
            DB.WriteMiscKey("Dummy"); //Ensure table is created.

            //Simulate lock database for 2 seconds.
            Task t = new Task(LockDatabase);
            t.Start();
            SQLiteMockedDB db2 = new SQLiteMockedDB("TestDB.db");
            DateTime tStart = DateTime.Now;
            try
            {
                //Failed at ~500ms when all values set to 0
                //Failed at ~700ms with step retries set to 10
                //Failed at ~700ms with busy timeout set to
                //Default timeout had no effect on locked database retry
                //StepRetries = 10, BusyTimeout = 100 - Test time 3.2 seconds
                db2.SQLStepRetries = 10;
                db2.SQLBusyTimeout = 500;
                //db2.DBConnection.DefaultTimeout = 100;

                System.Threading.Thread.Sleep(500); //Delay make sure DB is locked
                db2.WriteMiscKey(nameof(SimulateLockedDatabase));

            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
            finally
            {
                double seconds = (DateTime.Now - tStart).TotalSeconds;
                Assert.That(seconds, Is.GreaterThan(1.5));
            }
        }

        private void LockDatabase()
        {
            DB.ExecuteTransaction(() =>
            {
                DB.WriteMiscKey("LOCK");
                System.Threading.Thread.Sleep(2000);
            });
        }

        #endregion

        #region [ 5 - Create Table ]

        [Test, Order(50)]
        public void ReadFromNonExistingTable()
        {
            //ToDo: Fail when table not found during read opoeration
            SQLiteMockedDB dbReadonly = new SQLiteMockedDB(TestDBPath, isReadOnly: true, createFile: true);
            dbReadonly.ReadFromTableWithPrimaryKey("C_TableWithPrimaryKey2");
        }

        [Test, Order(51)]
        public void CreateTableWithPrimaryKey()
        {
            string createStatement = "CREATE TABLE \"C_TableWithPrimaryKey\" (\"ID\" INTEGER,\"Name\" TEXT UNIQUE,\"Time\" TEXT,\"TimeAsTicks\" INTEGER,\"Integer\" INTEGER,\"Double\" REAL,\"Flag\" INTEGER,\"OptionAsString\" TEXT,\"OptionAsNumber\" INTEGER,\"TextID\" INTEGER,PRIMARY KEY(\"ID\"))";
            string genCreateStatement = DB.CreateTable<TableWithPrimaryKey>("C_TableWithPrimaryKey");
            Assert.That(genCreateStatement, Is.EqualTo(createStatement));
            Assert.That(DB.GetTables().Contains("C_TableWithPrimaryKey"));
        }

        #endregion

        #region [ 6 - Index Table, Unique Items ]

        [Test, Order(60)]
        public void AddUniqueItems()
        {
            string tableName = "ID_01";
            List<IndexTable> Items = new List<IndexTable>();
            for (int x = 0; x < 10; x++)
            {
                Items.Add(new IndexTable() { Name = x.ToString() });
            }
            DB.WriteIndexTableToDB(tableName, Items.ToArray());
            List<IndexTable> readBack = DB.ReadIndexTableFromDB(tableName).ToList();
            Assert.That(readBack.Count, Is.EqualTo(10));

            readBack.Add(new IndexTable() { Name = "1" });
            DB.WriteIndexTableToDB(tableName, readBack.ToArray());
            readBack = DB.ReadIndexTableFromDB(tableName).ToList();
            Assert.That(readBack.Count, Is.EqualTo(10));
        }

        class MultiUniqueColTable
        {
            [SQLUniqueMultiColumn]
            public string Name { get; set; }
            [SQLUniqueMultiColumn]
            public string Value { get; set; }
            [SQLUniqueMultiColumn]
            public string Station { get; set; }
        }


        [Test]
        public void CreateMultiUniqueColumnTable()
        {
            DB.CreateTable<MultiUniqueColTable>();
        }


        #endregion

        #region [ 7 - Table with Arrays ]

        int arrayTableLength = 2;
        int arrayItemsLength = 20;
        TableWithArray[] ArrayTable, ArrayTableReadback;

        [Test, Order(70)]
        public void A_WriteTableWithArray()
        {
            ArrayTable = DB.WriteTableWithArrays(arrayTableLength, arrayItemsLength);
            Assert.That(ArrayTable.Length, Is.EqualTo(arrayTableLength));
            foreach (TableWithArray i in ArrayTable)
                Assert.That(i.ArrayData.Length, Is.EqualTo(arrayItemsLength));

            Assert.That(DB.GetTables().Contains("ArrayData"));
        }

        [Test, Order(71)]
        public void A_ReadTableWithArray()
        {
            ArrayTableReadback = DB.ReadTableWithArrays();
            Assert.That(ArrayTableReadback.Length, Is.EqualTo(arrayTableLength));
            foreach (TableWithArray i in ArrayTableReadback)
            {
                Assert.That(i.ArrayData.Length, Is.EqualTo(arrayItemsLength));
                Assert.That(i.ItemValue.Length, Is.EqualTo(arrayItemsLength));

                TableWithArray s = ArrayTable.FirstOrDefault(n => n.ID.Equals(i.ID));
                Assert.That(s, Is.Not.Null);
                Assert.That(s.ArrayData.OrderBy(n => n), Is.EqualTo(i.ArrayData.OrderBy(n => n)));
            }
        }

        #endregion

        #region [ 8 - Unique Constraint with Primary Key ]

        [Test, Order(80)]
        public void U_UpdateItemWithUniqueConstraint()
        {
            IndexTable item = new IndexTable() { Name = "Test" };
            DB.WriteIndexTableToDB("UniqueConstraint", item);
            Assert.That(item.ID != 0);
            int pKey = item.ID;


            DB.WriteIndexTableToDB("UniqueConstraint", item);
            Assert.That(item.ID, Is.EqualTo(pKey));

            item.ID = 0;
            DB.WriteIndexTableToDB("UniqueConstraint", item);
            Assert.That(item.ID, Is.EqualTo(pKey));
        }

        [Test, Order(81)]
        public void U_UpdateItemWithMultiUniqueConstraint()
        {
            TableMultiConstraint item = new TableMultiConstraint() { Name = "Test", Value = 100 };
            DB.WriteTableMultiConstraint(item);
            Assert.That(item.ID != 0);
            int pKey = item.ID;

            item.Value = 200;
            DB.WriteTableMultiConstraint(item);
            Assert.That(item.ID, Is.EqualTo(pKey));

            item.ID = 0;
            DB.WriteTableMultiConstraint(item);
            Assert.That(item.ID, Is.EqualTo(pKey));
        }


        #endregion
    }

}
