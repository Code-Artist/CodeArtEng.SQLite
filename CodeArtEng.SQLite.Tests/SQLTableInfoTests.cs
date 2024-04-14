using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeArtEng.SQLite.Tests
{

    [TestFixture]
    internal class SQLTableInfoTests
    {
        public class MainTable
        {
            [PrimaryKey]
            public int ID { get; set; }
            public List<User> Users { get; set; }
        }

        public class User
        {
            [SQLIndexTable]
            [SQLName("NameID")]
            public string Name { get; set; }
            [ParentKey(typeof(MainTable))]
            public int ParentID { get; set; }
        }
        [Test]
        public void MainTableInfo()
        {
            SQLTableInfo info = new SQLTableInfo(typeof(MainTable));
            Assert.That(info.ChildTables.Count, Is.EqualTo(1));
            Assert.That(info.ChildTables[0].ChildTableInfo.TableType, Is.EqualTo(typeof(User)));
            Assert.That(info.PrimaryKey.Name, Is.EqualTo("ID"));

            SQLTableInfo child = info.ChildTables[0].ChildTableInfo;
            Assert.That(child.Columns.FirstOrDefault(n => n.Name == "Name").SQLName, Is.EqualTo("NameID"));
        }

        public class MainTableNoPrimaryKey
        {
            public int ID { get; set; }
            public List<User2> Users { get; set; }
        }
        public class User2
        {
            public string Name { get; set; }
            [ParentKey(typeof(MainTableNoPrimaryKey))]
            public int ParentID { get; set; }
        }

        [Test]
        public void TestMainTableNoPrimaryKey()
        {
            Assert.Throws<FormatException>(() =>
            {
                SQLTableInfo info = new SQLTableInfo(typeof(MainTableNoPrimaryKey));
            });
        }

        public class MainTableWrongChild
        {
            public int ID { get; set; }
            public List<User3> Users { get; set; }
        }
        public class User3
        {
            public string Name { get; set; }
            [ParentKey(typeof(MainTable))]
            public int ParentID { get; set; }
        }

        [Test]
        public void TestMainTableWrongChild()
        {
            Assert.Throws<FormatException>(() =>
            {
                SQLTableInfo info = new SQLTableInfo(typeof(MainTableWrongChild));
            });
        }

        public class MainTableList
        {
            [PrimaryKey]
            public int ID { get; set; }
            public List<User10> Users { get; set; }
            public HashSet<User11> AllUsers { get; set; }
            public Dictionary<User12, string> DicUsers { get; set; }
        }
        public class User10
        {
            [ParentKey(typeof(MainTableList))]
            public int ParentID { get; set; }
            public string Name { get; set; }
        }
        public class User11
        {
            [ParentKey(typeof(MainTableList))]
            public int ParentID { get; set; }
            public string Name { get; set; }
        }
        public class User12
        {
            [ParentKey(typeof(MainTableList))]
            public int ParentID { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public void TestMainTableList()
        {
            Assert.Throws<FormatException>(() =>
            {
                SQLTableInfo info = new SQLTableInfo(typeof(MainTableList));
            });
        }
    }
}
