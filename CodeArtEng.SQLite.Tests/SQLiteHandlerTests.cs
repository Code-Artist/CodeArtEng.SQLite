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
        private readonly string remoteDBPath = "TestRemoteDB.db";
        private readonly string localDBPath = "LocalTestDB.db";

        private void ClearLocalDB()
        {
            File.Delete(localDBPath);
        }

        [Test, Order (1)]
        public void ConnectRemoteDB()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            Assert.That(DB.IsDatabaseOnline, Is.True);
        }

        [Test, Order(2)]
        public void RemoteDB_SwitchToLocalDB_Exception()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            Assert.Throws<InvalidOperationException>(() => { DB.SwitchToLocalDatabase(); });
        }

        [Test, Order(3)]
        public void RemoteDB_SwitchToRemoteDB_NOP()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath);
            DB.KeepDatabaseOpen = true;
            DB.GetTables();
            DB.SwitchToRemoteDatabase();
        }

        [Test, Order(4)]
        public void ConnectRemoteDB_NotExists_DatabaseOffline()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(dummyRemotePath);
            Assert.That(DB.IsDatabaseOnline, Is.False);
        }

        [Test, Order(5)]
        public void ConnectRemoteDB_NotExists_ReadTable_Exception()
        {
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(dummyRemotePath);
            Assert.Throws<AccessViolationException>(() => { DB.GetTables(); });
        }

        [Test, Order(6)]
        public void ConnectLocalDB_DBOffline()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.That(DB.IsDatabaseOnline, Is.False);
        }

        private SQLiteDatabaseHandlerMocked ConnectLocalDB(bool deleteLocalDB = true)
        {
            if(deleteLocalDB) ClearLocalDB();
            SQLiteDatabaseHandlerMocked DB = new SQLiteDatabaseHandlerMocked(remoteDBPath, localDBPath, 10);
            return DB;
        }

        [Test, Order(8)]
        public void WriteToLocalDB_ReadOnly_SQLiteException()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.Throws<SQLiteException>(() => { DB.CompactDatabase(); });
        }

        [Test, Order(9)]
        public void LocalDB_SwitchToRemote_Write()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            DB.SwitchToRemoteDatabase();
            DB.CompactDatabase();
        }

        [Test, Order(10)]
        public void ReadFromLocalDB_AutoCopy()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB();
            Assert.That(DB.GetTables().Count(), Is.GreaterThan(0));
            Assert.That(File.Exists(localDBPath), Is.True);
            Assert.That(DB.ConnectString.Contains(localDBPath));
            Assert.That((DateTime.Now - DB.LastUpdate).TotalSeconds, Is.LessThan(1));
            DB.Dispose();
        }

        [Test, Order(11)]
        public void ConnectLocalDB_NoSync()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB(deleteLocalDB: false);
            
            DateTime fileTime = File.GetLastWriteTime(localDBPath);
            Assert.That(DB.GetTables().Count(), Is.GreaterThan(0));
            Assert.That(File.GetLastWriteTime(localDBPath), Is.EqualTo(fileTime));
            DB.Dispose();
        }

        [Test, Order(12)]
        public void ForceSyncReadOnly()
        {
            SQLiteDatabaseHandlerMocked DB = ConnectLocalDB(deleteLocalDB: false);
            DB.SwitchToRemoteDatabase(true);
            DB.SyncDatabaseFile();
            Assert.That((DateTime.Now - DB.LastUpdate).TotalSeconds, Is.LessThan(1));
            DB.Dispose();
        }
    }
}
