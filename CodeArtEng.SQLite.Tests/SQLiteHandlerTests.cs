using NUnit.Framework;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace CodeArtEng.SQLite.Tests
{
    [TestFixture]
    internal class SQLiteHandlerTests
    {
        private readonly string dummyRemotePath = "NoSuchTable.db";
        private readonly string remoteDBPath = "TestDB.db";
        private readonly string localDBPath = "LocalTestDB.db";

        private void ClearLocalDB()
        {
            File.Delete(localDBPath);
        }

        [Test]
        public void ConnectRemoteDB()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            Assert.That(DB.IsDatabaseOnline, Is.True);
        }

        [Test]
        public void RemoteDB_SwitchToLocalDB_Exception()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            Assert.Throws<InvalidOperationException>(() => { DB.SwitchToLocalDatabase(); });
        }

        [Test]
        public void RemoteDB_SwitchToRemoteDB_NOP()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            DB.KeepDatabaseOpen = true;
            DB.GetTables();
            DB.SwitchToRemoteDatabase();
        }

        [Test]
        public void ConnectRemoteDB_NotExists_DataabseOffline()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(dummyRemotePath);
            Assert.That(DB.IsDatabaseOnline, Is.False);
        }

        public void ConnectRemoteDB_NotExists_ReadTable_Exception()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(dummyRemotePath);
            Assert.Throws<SQLiteException>(() => { DB.GetTables(); });
        }

        [Test]
        public void ConnectLocalDB_DBOnline()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.That(DB.IsDatabaseOnline, Is.False);
        }

        private SQLiteDatabaseHandlerMocked ConnectLocalDB()
        {
            ClearLocalDB();
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath, localDBPath, 10);
            return DB;
        }

        [Test]
        public void ReadFromLocalDB()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.That(DB.GetTables().Count(), Is.GreaterThan(0));
            Assert.That(File.Exists(localDBPath), Is.True);
            Assert.That(DB.ConnectString.Contains(localDBPath));
            Assert.That((DateTime.Now - DB.LastUpdate).TotalSeconds, Is.LessThan(1));
        }

        [Test]
        public void WriteToLocalDB_ReadOnly_SQLiteException()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.Throws<SQLiteException>(() => { DB.CompactDatabase(); });
        }

        [Test]
        public void LocalDB_SwitchToRemote_Write()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            DB.SwitchToRemoteDatabase();
            DB.CompactDatabase();
        }

    }
}
