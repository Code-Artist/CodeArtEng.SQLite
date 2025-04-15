using System;
using System.IO;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// SQLite Database Handler, remote amd local sync.
    /// </summary>
    public abstract class SQLiteDatabaseHandler : SQLiteHelper
    {
        /// <summary>
        /// Remote database file full path.
        /// </summary>
        public new string DatabaseFilePath { get; private set; }
        /// <summary>
        /// Database local synced copy path.
        /// </summary>
        public string LocalFilePath { get; private set; } = null;
        /// <summary>
        /// Check if local database path is defined.
        /// </summary>
        public bool IsLocalDatabaseDefined => !string.IsNullOrEmpty(LocalFilePath);

        #region [ Database Sync Control ]

        /// <summary>
        /// Check if local sync is actviated. 
        /// Active when <see cref="SwitchToLocalDatabase"/> is execued to swith current databse to local.
        /// </summary>
        protected bool IsLocalSyncActive { get; private set; } = false;
        /// <summary>
        /// Local sync update interval in minutes.
        /// </summary>
        protected int UpdateIntervalMinutes { get; private set; } = 10;
        /// <summary>
        /// Local database file last sync time. 
        /// Read from last modified time when first connected.
        /// </summary>
        protected DateTime LastUpdate { get; private set; } = new DateTime();
        /// <summary>
        /// Temporary suspend sync between remote and local database.
        /// </summary>
        protected bool SuspendLocalSync { get; set; } = false;

        #endregion

        /// <summary>
        /// Check if remote databse is online.
        /// </summary>
        /// <returns></returns>
        public bool IsRemoteDatabaseOnline()
        {
            return File.Exists(DatabaseFilePath);
        }

        /// <summary>
        /// Constructor. Configure remote path without local sync option enabled.
        /// </summary>
        /// <param name="remotePath"></param>
        public SQLiteDatabaseHandler(string remotePath)
        {
            if(string.IsNullOrEmpty(remotePath)) throw new ArgumentNullException(nameof(remotePath), "Remote database path not defined!");
            DatabaseFilePath = remotePath;
            SwitchToRemoteDatabase();
        }

        /// <summary>
        /// Constructor. Configure remote and local synced path.
        /// </summary>
        /// <param name="remotePath"></param>
        /// <param name="localPath"></param>
        /// <param name="updateIntervalMin"></param>
        public SQLiteDatabaseHandler(string remotePath, string localPath, int updateIntervalMin)
        {
            if (string.IsNullOrEmpty(remotePath)) throw new ArgumentNullException(nameof(remotePath), "Remote database path not defined!");
            if (string.IsNullOrEmpty(localPath)) throw new ArgumentNullException(nameof(localPath), "Local database path not defined!");

            DatabaseFilePath = remotePath;
            LocalFilePath = localPath;
            UpdateIntervalMinutes = updateIntervalMin;
            SwitchToLocalDatabase();
        }

        /// <summary>
        /// Switch databse connection to remote databse.
        /// No effect if local database path not defined.
        /// </summary>
        /// <param name="readOnly"></param>
        protected void SwitchToRemoteDatabase(bool readOnly = false)
        {
            DisconnectDatabase();
            SetSQLPath(DatabaseFilePath, readOnly);
            IsLocalSyncActive = false;
        }

        /// <summary>
        /// Switch databse connection to local database.
        /// </summary>
        /// <exception cref="InvalidOperationException">Local database path not defined, unable to switch.</exception>
        protected void SwitchToLocalDatabase()
        {
            if (!IsLocalDatabaseDefined) throw new InvalidOperationException("Local database path not defined!");
            DisconnectDatabase();
            SetSQLPath(LocalFilePath, readOnly: true);
            IsLocalSyncActive = true;
            if (File.Exists(LocalFilePath)) LastUpdate = File.GetLastWriteTime(LocalFilePath);
        }

        /// <summary>
        /// Connect to database. When local sync mode is activated, update local database file when 
        /// difference between <see cref="LastUpdate"/> time and current time greater than <see cref="UpdateIntervalMinutes"/>
        /// </summary>
        protected override void Connect()
        {
            if (IsConnected) return; //Avoid reconnect keep alive database.
            if (IsLocalSyncActive)
            {
                if (!File.Exists(LocalFilePath))
                {
                    SyncDatabaseFile();
                    LastUpdate = DateTime.Now;
                }
                else if (!SuspendLocalSync)
                {
                    //Check last sync time and sync database.
                    double lastSync = (DateTime.Now - LastUpdate).TotalMinutes;
                    if (IsRemoteDatabaseOnline())
                    {
                        //Database online, sync at defined interval
                        if (lastSync > UpdateIntervalMinutes)
                        {
                            SyncDatabaseFile();
                            LastUpdate = DateTime.Now;
                        }
                    }
                    else
                    {
                        //Database offline, attempt to sync every 1 minutes max
                        // set last update time to due in 1 minute
                        LastUpdate = DateTime.Now.AddMinutes(1 - UpdateIntervalMinutes);
                    }
                }
            }//if
            base.Connect();
        }

        /// <summary>
        /// Sync database file from remote server to local path
        /// </summary>
        private void SyncDatabaseFile()
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(LocalFilePath));
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            //FileCopyNoLock(DatabaseFilePath, LocalFilePath);

            using (SQLiteDBLite sourceDB = new SQLiteDBLite(DatabaseFilePath))
            {
                sourceDB.BackupDatabaseTo(LocalFilePath);
            }
        }

        /// <summary>
        /// Copy file using file stream without locking file.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="destFile"></param>
        /// <exception cref="Exception"></exception>
        private void FileCopyNoLock(string sourceFile, string destFile)
        {
            try
            {
                const int bufferSize = 81920; // 80KB buffer
                using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (FileStream destinationStream = new FileStream(destFile, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var buffer = new byte[bufferSize];
                        int bytesRead;
                        var totalBytes = sourceStream.Length;
                        var bytesWritten = 0L;

                        while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            destinationStream.Write(buffer, 0, bytesRead);
                            bytesWritten += bytesRead;
                        }
                    }
                }    
            }
            catch (Exception ex) { throw new Exception("File copy failed! " + ex.Message); }
        }
    }
}
