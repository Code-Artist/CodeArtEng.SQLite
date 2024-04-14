using System;

namespace CodeArtEng.SQLite.Tests
{
    internal class SQLiteDatabaseHandlerMocked : SQLite.SQLiteDatabaseHandler
    {
        public SQLiteDatabaseHandlerMocked(string remotePath)
            : base(remotePath) { }

        public SQLiteDatabaseHandlerMocked(string remotePath, string localPath, int updateIntervalMin)
            : base(remotePath, localPath, updateIntervalMin) { }

        public new DateTime LastUpdate => base.LastUpdate;
        public new string[] GetTables() => base.GetTables();
        public new void CompactDatabase() => base.CompactDatabase();
        public new void SwitchToLocalDatabase() => base.SwitchToLocalDatabase();
        public void SwitchToRemoteDatabase() => base.SwitchToRemoteDatabase();
    }
}
