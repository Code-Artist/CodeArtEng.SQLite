using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

//ToDo: Read and Write Async

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// SQLite helper class. Simplify SQLite Database operation.
    /// </summary>
    public abstract partial class SQLiteHelper : IDisposable
    {
        #region [ Internal SQLite Objects ]

        private SQLiteCommand SqlCommand;
        private SQLiteCommand Command
        {
            get
            {
                if (DBConnection == null) return null;
                if (SqlCommand == null) SqlCommand = DBConnection.CreateCommand();
                return SqlCommand;
            }
        }
        private bool IsSQLTransaction => Command?.Transaction != null;

        #endregion

        #region [ SQLiteConnection and Settings ]

        /// <summary>
        /// SQLite Database Connection.
        /// </summary>
        protected SQLiteConnection DBConnection { get; private set; } = new SQLiteConnection();

        /// <summary>
        /// Database connection string
        /// </summary>
        public string ConnectString { get; private set; } = string.Empty;
        /// <summary>
        /// Database connection string for readonly access, internal used to optimize read operation without locking.
        /// </summary>
        protected string ConnectStringReadOnly { get; private set; } = string.Empty;
        /// <summary>
        /// Databse file full path. Execute <see cref="SetSQLPath(string, bool)"/> to change.
        /// </summary>
        public string DatabaseFilePath { get; private set; } = string.Empty;
        /// <summary>
        /// Keep database open until flag is cleared or <see cref="DisconnectDatabase"/> is called.
        /// </summary>
        [DefaultValue(false)]
        public bool KeepDatabaseOpen
        {
            get => _KeepDBOpen;
            set
            {
                if (_KeepDBOpen == value) return;
                _KeepDBOpen = value;
                if (!_KeepDBOpen) Disconnect();
            }
        }
        private bool _KeepDBOpen = false;

        /// <summary>
        /// Return true if databse is open in readonly mode. Configure by <see cref="SetSQLPath(string, bool)"/>
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly { get; private set; } = false;

        /// <summary>
        /// Properties of SQLiteConnection, default timeout for SQLCommand. 
        /// Default value set in <see cref="SQLiteConnection"/> is 30 seconds
        /// </summary>
        protected int SQLDefaultTimeout
        {
            get => DBConnection.DefaultTimeout;
            set => DBConnection.DefaultTimeout = value;
        }
        /// <summary>
        /// Properties of SQLiteConnection, default timeout in ms to wait for a connection for write access.
        /// Default value set in <see cref="SQLiteConnection"/> is 0 ms.
        /// </summary>
        protected int SQLBusyTimeout
        {
            get => DBConnection.BusyTimeout;
            set => DBConnection.BusyTimeout = value;
        }
        /// <summary>
        /// Properties of SQLiteConnection, max of retries when executing a SQL step.
        /// Defualt value set in <see cref="SQLiteConnection"/> is 40.
        /// </summary>
        protected int SQLStepRetries
        {
            get => DBConnection.StepRetries;
            set => DBConnection.StepRetries = value;
        }

        /// <summary>
        /// Return true if connection to database is established.
        /// </summary>
        public bool IsConnected => DBConnection.State == ConnectionState.Open;
        /// <summary>
        /// Check if databsae file accessible. Return false if <see cref="DatabaseFilePath"/> not defined or not accessible.
        /// </summary>
        /// <returns></returns>
        public bool IsDatabaseOnline()
        {
            if (string.IsNullOrEmpty(DatabaseFilePath)) return false;
            return File.Exists(DatabaseFilePath);
        }
        public bool IsDatabaseReadOnly => DBConnection.ConnectionString.Contains("Read Only=True");

        public SQLiteWriteOptions WriteOptions { get; set; } = new SQLiteWriteOptions();

        #endregion

        /// <summary>
        /// Constructor. Nothing special happening here (",)
        /// </summary>
        public SQLiteHelper() { }

        /// <summary>
        /// Constructor with additonal retries and busy timeout input override default values.
        /// </summary>
        /// <param name="stepRetries">Refer to <see cref="SQLStepRetries"/></param>
        /// <param name="busyTimeout">Refer to <see cref="SQLBusyTimeout"/> </param>
        public SQLiteHelper(int stepRetries, int busyTimeout) : this()
        {
            SQLStepRetries = stepRetries;
            SQLBusyTimeout = busyTimeout;
        }

        #region [ IDisposable ]

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisconnectDatabase();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose object and close all connection.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region [ Connection Handling ]

        /// <summary>
        /// Set database path and compile connection string.
        /// </summary>
        /// <param name="databaseFilePath"></param>
        /// <param name="readOnly"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected virtual void SetSQLPath(string databaseFilePath, bool readOnly = false, bool createFile = false)
        {
            KeepDatabaseOpen = false;
            DisconnectDatabase();
            ConnectString = string.Empty; //Reset connect string

            DatabaseFilePath = databaseFilePath;
            if (string.IsNullOrEmpty(databaseFilePath)) throw new ArgumentNullException(nameof(databaseFilePath));
            ConnectString = @"Data Source=" + databaseFilePath + ";Version=3;";

            ReadOnly = readOnly;
            if (ReadOnly) ConnectString += "Read Only=True;";
            ConnectStringReadOnly = ConnectString + "Read Only=True;";
            DBConnection.ConnectionString = ConnectString;

            if (!File.Exists(DatabaseFilePath) && createFile) CreateDatabase();
        }

        /// <summary>
        /// Create database when target DB file not exists.
        /// </summary>
        private void CreateDatabase()
        {
            try
            {
                DBConnection.Open();
                KeepDatabaseOpen = true;
                ExecuteNonQuery("PRAGMA auto_vacuum = INCREMENTAL");
                ExecuteNonQuery("PRAGMA journal_mode = WAL");
                ExecuteNonQuery("PRAGMA synchronous = NORMAL");
                ExecuteNonQuery("PRAGMA journal_size_limit = 65536"); //WAL size 64KB
            }
            finally
            {
                KeepDatabaseOpen = false;
                DBConnection.Close();
            }
        }


        /// <summary>
        /// Connect to database. Do nothing if connection is already established.
        /// </summary>
        /// <exception cref="ArgumentNullException">Database path not defined. <see cref="SetSQLPath(string, bool)"/></exception>
        /// <exception cref="AccessViolationException">Database not accessible.</exception>
        protected virtual void Connect()
        {
            if (IsConnected) return;
            DBConnection.ConnectionString = ConnectString;
            ConnectInt();
        }

        /// <summary>
        /// Connect database as readonly.
        /// Optimized for read operation even <see cref="ReadOnly"/> flag is not set.
        /// </summary>
        private void ConnectRead()
        {
            if (IsConnected) return;
            ReadOnly = true;
            DBConnection.ConnectionString = ConnectStringReadOnly;
            ConnectInt();
        }

        /// <summary>
        /// Establish connection to Database
        /// </summary>
        /// <exception cref="ArgumentNullException">Database path not defined.</exception>
        /// <exception cref="AccessViolationException">Not able to access database</exception>
        private void ConnectInt()
        {
            if (IsConnected) return;
            if (string.IsNullOrEmpty(DatabaseFilePath)) throw new ArgumentNullException("Database path not defined!");
            if (!IsDatabaseOnline()) throw new AccessViolationException("Database not exists or not reachable!");

            DBConnection.ParseViaFramework = true;
            DBConnection.Open();
        }

        /// <summary>
        /// Disconnect from database. Do nothing if <see cref="KeepDatabaseOpen"/> flag is set.
        /// This method had no effect in middle of <see cref="ExecuteTransaction(Action)"/> 
        /// </summary>
        protected void Disconnect()
        {
            if (KeepDatabaseOpen) return;
            if (SqlCommand?.Transaction != null) return; //Do not disconnect in middle of transaction
            DisconnectDatabase();
        }

        /// <summary>
        /// Internal use method. Force disconnect database. Override <see cref="KeepDatabaseOpen"/>.
        /// </summary>
        internal void DisconnectDatabase()
        {
            SqlCommand?.Dispose(); SqlCommand = null;
            KeepDatabaseOpen = false;

            //Force garbage collector to release database lock when connection closed.
            DBConnection.Close();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #endregion

        #region [ Basic Query ]

        /// <summary>
        /// Execute query which return SQLiteDataReader object.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="processQueryResults">Callback action to process query result.</param>
        /// <returns></returns>
        protected void ExecuteQuery(string query, Action<SQLiteDataReader> processQueryResults)
        {
            SQLiteDataReader result = null;
            try
            {
                Connect();
                Command.CommandText = query;
                result = Command.ExecuteReader();
                processQueryResults(result);
            }
            finally
            {
                result?.Close();
                Disconnect();
            }
        }

        /// <summary>
        /// Execute query which return single value.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected object ExecuteScalar(string query)
        {
            try
            {
                Connect();
                Command.CommandText = query;
                object result = Command.ExecuteScalar();
                return result;
            }
            finally { Disconnect(); }
        }

        /// <summary>
        /// Execute non-query, return number of rows affected.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected int ExecuteNonQuery(string query)
        {
            try
            {
                Connect();
                Command.CommandText = query;
                int reader = Command.ExecuteNonQuery();
                return reader;
            }
            finally { Disconnect(); }
        }

        /// <summary>
        /// Execute SQL Transactions.
        /// </summary>
        protected void ExecuteTransaction(Action performTransactions)
        {
            Connect();
            KeepDatabaseOpen = true;
            SQLiteTransaction transaction = null;
            try
            {
                //Begin Transaction
                transaction = DBConnection.BeginTransaction();
                Command.Transaction = transaction;

                //User implemented callback
                performTransactions();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                //Clean up and disconnect
                Command.Transaction = null;
                Command.Parameters.Clear();
                transaction?.Dispose();
                KeepDatabaseOpen = false;
                Disconnect();
            }
        }

        #endregion

        #region [ Utility ]

        private class DBColumn
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        /// <summary>
        /// Return columns name and type from database
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        private DBColumn[] GetDBColumns(string table)
        {
            List<DBColumn> results = new List<DBColumn>();
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("$table", table);
            string query = "SELECT name, type from pragma_Table_Info($table)";
            ExecuteQuery(query,
                (r) =>
                {
                    while (r.Read())
                    {
                        results.Add(new DBColumn()
                        {
                            Name = r.GetString(0),
                            Type = r.GetString(1)
                        });
                    }
                });
            return results.ToArray();
        }

        /// <summary>
        /// Delete contents for all tables and compress database.
        /// </summary>
        protected void ClearAllTables()
        {
            List<string> tables = new List<string>();
            string query = "SELECT NAME FROM SQLITE_MASTER WHERE TYPE ='table'";
            ExecuteQuery(query, (r) =>
            {
                while (r.Read()) tables.Add(r.GetString(0));
            });
            foreach (string t in tables)
                ExecuteNonQuery("DELETE FROM " + t);
            CompactDatabase();
        }

        /// <summary>
        /// Clear all rows for selected table by type name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void ClearTable<T>()
        {
            string tableName = typeof(T).Name;
            ClearTable(tableName);
        }

        /// <summary>
        /// Clear all rows from selected table by table name.
        /// Perform VACUUM if method is not called within SQL Transaction.
        /// </summary>
        /// <param name="tableName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        protected void ClearTable(string tableName)
        {
            if (!VerifyTableExists(tableName))
                throw new InvalidOperationException($"Table [{tableName}] not exists in database!");
            ExecuteNonQuery("DELETE FROM " + tableName);
            if (!IsSQLTransaction) CompactDatabase();
        }

        /// <summary>
        /// Compress database. Do not execute this within SQL Transaction.
        /// </summary>
        protected void CompactDatabase() => ExecuteNonQuery("VACUUM");

        /// <summary>
        /// Return tables name from current connected database.
        /// </summary>
        /// <returns></returns>
        protected string[] GetTables()
        {
            List<string> tables = new List<string>();
            string query = "SELECT NAME FROM SQLITE_MASTER WHERE TYPE ='table'";
            ExecuteQuery(query, (r) =>
            {
                while (r.Read()) tables.Add(r.GetString(0));
            });
            return tables.ToArray();
        }

        /// <summary>
        /// Return schema for selected table. Return null if table not exists or invalid.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        protected string GetTableSchema(string tableName)
        {
            Command.Parameters.AddWithValue("$tablename", tableName); //prevent SQL Injection
            return ExecuteScalar("SELECT SQL FROM SQLITE_MASTER WHERE NAME = $tablename")?.ToString();
        }

        /// <summary>
        /// Return ROW_ID for selected table
        /// </summary>
        /// <remarks>Value reset once connection closed.</remarks>
        /// <returns></returns>
        protected int GetLastRowID(string tableName)
        {
            object value = ExecuteScalar("SELECT max(rowID) from " + tableName);
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Return primary keys from selected table.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        protected string[] GetPrimaryKeys(string tableName)
        {
            List<string> keys = new List<string>();
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("$tableName", tableName);
            ExecuteQuery("SELECT NAME FROM PRAGMA_TABLE_INFO($tableName) WHERE PK = 1",
                (r) => { while (r.Read()) keys.Add(r.GetString(0)); });
            return keys.ToArray();
        }

        /// <summary>
        /// Check if table exists in database
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private bool VerifyTableExists(string tableName)
        {
            return GetTables().Contains(tableName, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Check if database Journal Mode is configured as WAL (Write-Ahead Logging).
        /// </summary>
        /// <returns></returns>
        private bool IsWalMode()
        {
            try
            {
                return ExecuteScalar("PRAGMA journal_mode")?.ToString() == "wal";
            }
            catch { return false; }
        }

        /// <summary>
        /// Perform Full Checkpoint for WAL Mode database.
        /// </summary>
        private void CheckPoint()
        {
            if (IsWalMode()) ExecuteNonQuery("PRAGMA wal_checkpoint(FULL)");
        }

        #endregion

        #region [ Query with Class ]

        #region [NOTES: SQLite AutoIncrement (PRMARY KEY) ... ]

        //SQlite always include with ROWID, primary key declaration not necessary.
        //When a new row is inserted into an SQLite table,
        //the ROWID can either be specified as part of the INSERT statement or
        //it can be assigned automatically by the database engine.
        //To specify a ROWID manually,just include it in the list of values to be inserted.
        //
        //If a table contains a column of type INTEGER PRIMARY KEY, then that colummn
        //becomes an alias for the ROWID.
        //https://www.sqlite.org/autoinc.html

        #endregion

        private readonly List<SQLTableInfo> TableInfos = new List<SQLTableInfo>();
        /// <summary>
        /// Get or set table property by type. Return existing object if created, else create new.
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private SQLTableInfo GetTableInfo(Type sender, string tableName = null)
        {
            SQLTableInfo result = TableInfos.FirstOrDefault(n => n.TableType == sender && n.Name == tableName);
            if (result != null) return result;

            //Create entry, verify table exist
            result = new SQLTableInfo(sender, tableName);
            if (!result.Validated) ValidateTableinfo(result);
            TableInfos.Add(result);
            TableInfos.AddRange(result.ChildTables.Select(n => n.ChildTableInfo).ToArray());
            return result;
        }

        private bool ValidateTableinfo(SQLTableInfo info)
        {
            info.Validated = false;

            if (!VerifyTableExists(info.TableName))
            {
                if (WriteOptions.CreateTable && !IsDatabaseReadOnly)
                    CreateTable(info.TableType, info.TableName);
                else if (IsDatabaseReadOnly) //Database is readonly, table not exist, skip validation
                    return false;
                else
                    throw new InvalidOperationException($"Table [{info.TableName}] not exists in database!");
            }

            if (info.PrimaryKey != null) ValidatePrimaryKey(info);

            //Database Table may have more columns than .NET Object.
            //Ignore table columns which does not have matching in SQLTableInfo (Backward Compatible)
            DBColumn[] DBColumns = GetDBColumns(info.TableName);
            foreach (SQLTableItem item in info.Columns)
            {
                //Verify column exists in table.
                DBColumn column = DBColumns.FirstOrDefault(n => n.Name.Equals(item.SQLName, StringComparison.CurrentCultureIgnoreCase));
                if (column == null) throw new FormatException($"{info.TableName}: Missing column {item.SQLName}!");

                string errHeaderDB = $"Incorrect database format for {info.TableName}.{item.SQLName}: ";
                string errHeaderClass = $"Incorrect class declaration for {info.Name}.{item.Name}: ";
                Type itemType = item.Property.PropertyType;
                if (item.IsPrimaryKey)
                {
                    if ((itemType != typeof(int)) || (itemType != typeof(long))) throw new FormatException("Primary key must be int or long.");
                    if (!column.Type.Equals("INTEGER")) throw new FormatException(errHeaderDB + "Primary key must be INTEGER type.");
                }
                else if (item.IsIndexTable)
                {
                    if (!column.Type.Equals("INTEGER"))
                        throw new FormatException(errHeaderDB + "Expecting INTEGER type for index column.");
                }
                else
                {
                    //Data Type Check, compare and verify data type between class and database declaration.
                    //Incorrect type declaration might not affect write operation but readback value will get affected.
                    if (item.DataType == SQLDataType.INTEGER && !column.Type.Equals("INTEGER"))
                        throw new FormatException(errHeaderDB + "Type mismatched, expecting INTEGER!");

                    if (item.DataType == SQLDataType.TEXT && !column.Type.Equals("TEXT"))
                        throw new FormatException(errHeaderDB + "Type mismatched, expecting TEXT!");

                    if (item.DataType == SQLDataType.REAL && !column.Type.Equals("REAL"))
                        throw new FormatException(errHeaderDB + "Type mismatched, expecting REAL!");
                }
            }

            foreach (SQLTableItem i in info.ArrayTables)
            {
                if (!VerifyTableExists(i.TableName))
                {
                    if (WriteOptions.CreateTable && !IsDatabaseReadOnly)
                        CreateArrayTable(i.DataType, i.TableName);
                    else if (IsDatabaseReadOnly) //Database is readonly, table not exist, skip validation
                        return false;
                    else
                        throw new InvalidOperationException($"Table [{info.TableName}] not exists in database!");
                }
            }

            foreach (SQLTableItem i in info.ChildTables)
            {
                string dbBackupPath = SetSecondaryDBPath(i);
                try
                {
                    ValidateTableinfo(i.ChildTableInfo);
                }
                finally { RestorePrimaryDBPath(dbBackupPath); }
            }

            info.Validated = true;
            return true;
        }

        private readonly List<IndexTableHandler> IndexTables = new List<IndexTableHandler>();
        private IndexTableHandler GetIndexTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException("tableName");
            IndexTableHandler result = IndexTables.FirstOrDefault(n => n.Name == tableName);
            if (result != null) return result;

            if (!VerifyTableExists(tableName))
            {
                if (WriteOptions.CreateTable && !IsDatabaseReadOnly)
                    CreateTable<IndexTable>(tableName);
                else if (IsDatabaseReadOnly)
                    return null; //Database is readonly, table not exists, return nuothing.
                else
                    throw new InvalidOperationException($"Table [{tableName}] not exists in database!");
            }
            result = new IndexTableHandler(tableName);
            IndexTables.Add(result);
            return result;
        }

        /// <summary>
        /// Return contents for index table by table name.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        protected IndexTable[] IndexTable(string tableName)
        {
            IndexTableHandler table = IndexTables.FirstOrDefault(n => n.Name == tableName);
            if (table == null) throw new NullReferenceException($"Table {tableName} not exists / not valid index table!");
            return table.Items.Union(table.NewItems).ToArray();
        }

        private void ValidatePrimaryKey(SQLTableInfo sqlTable)
        {
            string tableName = sqlTable.TableName;
            PropertyInfo pKey = sqlTable.PrimaryKey.Property;

            //Verify primary key with database.
            string[] dbkeys = GetPrimaryKeys(tableName).Select(n => n.ToUpper()).OrderBy(n => n).ToArray();
            if (dbkeys.Length > 1) throw new NotSupportedException(
                $"Multiple primary keys defined in table {tableName}. " +
                $"Table with multiple primary keys is currently not supported!");
            else if (dbkeys?.Length == 0) throw new MissingPrimaryKeyException($"Primary key not defined in table {tableName}!");

            if (!pKey.Name.ToUpper().Equals(dbkeys[0].ToUpper()))
                throw new MissingPrimaryKeyException(
                    $"Primary key mismatched for table {tableName}. Expecting [{pKey}] but found [{dbkeys[0]}] in database.");
        }

        /// <summary>
        /// Unique ID for read operation, used to optimize Index table handling.
        /// </summary>
        private long ReadOpID = 0;

        /// <summary>
        /// A generic function to retrieve data from 
        /// a specified database table and map it to objects of a given class. 
        /// Class and properties name are not case sensitives.
        /// Entire operation is completed in one single transaction.
        /// </summary>
        /// <typeparam name="T">Output object type</typeparam>
        /// <returns></returns>
        protected IList<T> ReadFromDatabase<T>(string whereStatement = null, string tableName = null) where T : class, new()
        {
            ReadOpID = DateTime.Now.Ticks;
            bool keepDBOpenFlag = KeepDatabaseOpen;
            KeepDatabaseOpen = true;

            //NOTE: Cannot use transaction here, switching database will cause transaction closed.
            try
            {
                SQLTableInfo senderTable = GetTableInfo(typeof(T), tableName);
                if (!senderTable.Validated) return null;
                Command.Parameters.Clear();
                if (!string.IsNullOrEmpty(whereStatement))
                {
                    (string processedWhere, List<SQLiteParameter> parameters) = SqlParameterConverter.ConvertWhereToParameters(whereStatement);
                    Command.Parameters.AddRange(parameters.ToArray());
                    return ReadFromDatabaseInt<T>(senderTable, processedWhere);
                }
                return ReadFromDatabaseInt<T>(senderTable);
            }
            finally
            {
                KeepDatabaseOpen = keepDBOpenFlag;
                Disconnect();   //Disconnect if necessary
            }
        }

        /// <summary>
        /// Internal implementation for read operation. Modifier must be internal for reflection call.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="senderTable"></param>
        /// <param name="whereStatement"></param>
        /// <param name="tableName">Table name in SQLite database if different from class name.</param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        internal IList<T> ReadFromDatabaseInt<T>(SQLTableInfo senderTable, string whereStatement = null) where T : class, new()
        {
            List<T> results = new List<T>();
            string tableName = senderTable.TableName;
            SQLTableItem[] properties = senderTable.Columns;
            SQLTableItem primaryKey = senderTable.PrimaryKey;
            if (primaryKey != null) properties = properties.Append(primaryKey).ToArray();

            ////Table may have more columns than class.
            ////Ignore properties where name does not match with Table columns (Backward Compatible)
            //string[] dbColumns = GetColumnNames(tableName);
            //properties = properties.Except(properties.Where(n => !dbColumns.Contains(n.SQLName, StringComparer.InvariantCultureIgnoreCase))).ToArray();

            //Reload Index Table
            ReadIndexTables(senderTable);

            //Read Table
            string query = "SELECT * FROM " + tableName;
            if (!string.IsNullOrEmpty(whereStatement)) query += " " + whereStatement;
            ConnectRead();
            ExecuteQuery(query, processQueryResults: r =>
                {
                    while (r.Read())
                    {
                        T instance = new T();
                        foreach (SQLTableItem p in properties)
                        {
                            //Get value from database
                            object value = r[p.SQLName];
                            if (value == DBNull.Value) continue;

                            //Assign to class object
                            p.SetDBValue(instance, value);
                        }
                        results.Add(instance);
                    }
                });

            //Assign value to index key property (SQLIndex)
            foreach (SQLTableItem i in senderTable.IndexKeys)
            {
                IndexTableHandler indexTable = GetIndexTable(i.TableName);
                if (indexTable == null) throw new Exception($"Index table {i.TableName} not exists in database!");
                foreach (var r in results)
                {
                    string value = indexTable.GetValueById(Convert.ToInt32(i.Property.GetValue(r)));
                    if (!string.IsNullOrEmpty(value)) i.SetDBValue(r, value);
                }
            }

            foreach (SQLTableItem t in senderTable.ArrayTables)
            {
                //Get array primitive data type
                Type elementType = t.Property.PropertyType.GetElementType();
                Type listType = typeof(List<>).MakeGenericType(elementType);
                foreach (var k in results)
                {
                    //Get primary key for each item.
                    object pKey = primaryKey.GetDBValue(k);
                    query = $"SELECT VALUE FROM {t.TableName} WHERE ID == {pKey}";

                    //Query from array table.
                    List<object> list = new List<object>();
                    ExecuteQuery(query, processQueryResults: r =>
                    {
                        while (r.Read()) list.Add(r.GetValue(0));
                    });

                    Array newArray = Array.CreateInstance(elementType, list.Count);
                    for (int x = 0; x < list.Count; x++)
                        newArray.SetValue(Convert.ChangeType(list[x], elementType), x);
                    t.Property.SetValueEx(k, newArray);
                }
            }

            if ((senderTable.PrimaryKey != null) &&
                (senderTable.ChildTables.Length > 0))
            {
                //Read child tables
                foreach (SQLTableItem r in senderTable.ChildTables)
                {
                    //Get table info
                    SQLTableInfo childTableInfo = r.ChildTableInfo;
                    Type childType = childTableInfo.TableType;
                    if (childTableInfo.ParentKey?.ParentType != senderTable.TableType)
                        throw new FormatException("Parent key not defined for class " + childTableInfo.Name);

                    //Read from child table by parent key ID.
                    MethodInfo ptrReadMethod = this.GetType()
                        .GetMethod("ReadFromDatabaseInt", BindingFlags.Instance | BindingFlags.NonPublic)
                        .MakeGenericMethod(childType);

                    string dbBackup = SetSecondaryDBPath(r);
                    try
                    {
                        foreach (T i in results)
                        {
                            object pKey = senderTable.PrimaryKey.GetDBValue(i);
                            //Recursive call to ReadFromDatabse method.
                            object childItems = ptrReadMethod.Invoke(this, new object[] { childTableInfo, $"WHERE {childTableInfo.ParentKey.Name} == {pKey}" });
                            r.Property.SetValueEx(i, childItems);
                        }
                    }
                    finally { RestorePrimaryDBPath(dbBackup); }
                }
            }
            return results;
        }

        private void ReadIndexTables(SQLTableInfo table)
        {
            try
            {
                if (table.IndexKeys.Length == 0) return;
                foreach (SQLTableItem p in table.IndexKeys)
                {
                    IndexTableHandler tb = GetIndexTable(p.TableName);
                    if (tb == null) continue;
                    if (tb.LastReadID == ReadOpID) continue;
                    tb.LastReadID = ReadOpID;

                    tb.Items.Clear();
                    string query = $"SELECT ID,Name from {p.TableName}";
                    ExecuteQuery(query, processQueryResults: r =>
                    {
                        while (r.Read())
                        {
                            IndexTable t = new IndexTable()
                            {
                                ID = r.GetInt32(0),
                                Name = r.GetStringEx(1)
                            };
                            tb.Items.Add(t);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Read Index Table failed for table {table.TableName}!", ex);
            }
        }

        /// <summary>
        /// Write items to databse, input must be item or array represent the table class.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="senders"></param>
        /// <exception cref="ArgumentException"></exception>
        protected void WriteToDatabase<T>(params T[] senders) where T : class
        {
            //ToDo: Test with generic aaray like string and int.

            if (typeof(T).IsGenericType)
                throw new ArgumentException("Expected array but not generic type (List / Dictionary)! " +
                    "Use ToArray() to convert list to to array.");
            WriteToDatabase(senders, string.Empty);
        }

        /// <summary>
        /// Writes an array of objects to a specified database table.
        /// The column names and type in the table should match the properties of the class.
        /// Only properties with get and set are serialized.
        /// Use <see cref="IgnoreSQLColumnAttribute"/> to skip selected properties.
        /// </summary>
        /// <remarks>DateTime shall stored as TEXT. This method works well with <see cref="ExecuteTransaction(Action)"/></remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="senders"></param>
        protected void WriteToDatabase<T>(T[] senders, string tableName) where T : class
        {
            bool keepDBOpenFlag = KeepDatabaseOpen;
            KeepDatabaseOpen = true; //Overwrite keepdatabase open
            try
            {
                SQLTableInfo senderTable = GetTableInfo(typeof(T), tableName);
                WriteToDatabaseInt(senderTable, senders);

                //update index table with new items.
                foreach (IndexTableHandler i in IndexTables)
                {
                    if (i.NewItems.Count == 0) continue;
                    string query = $"INSERT OR REPLACE INTO {i.Name} (ID, Name) VALUES (@ID, @Name)";
                    foreach (IndexTable m in i.NewItems)
                    {
                        Command.Parameters.Clear();
                        Command.Parameters.AddWithValue("@ID", m.ID);
                        Command.Parameters.AddWithValue("@Name", m.Name);
                        ExecuteNonQuery(query);
                    }
                    i.NewItems.Clear();
                }
            }
            finally
            {
                KeepDatabaseOpen = keepDBOpenFlag; //Restore flag status
                Disconnect();   //Disconnect if necessary
            }
        }

        private void WriteToDatabaseInt<T>(SQLTableInfo senderTable, params T[] senders) where T : class
        {
            //IMPORTANT:
            //Connection to database shall not be close throughout the complete execution,
            //otherwise GetLastInsertedRowID will return null;

            //IMPORTANT:
            //DO NOT Use transaction as multiple read and write query are used.

            Type senderType = senders.First().GetType();
            string tableName = senderTable.TableName;

            // Execute query for each element in the array.
            // Each query update a row in table.
            foreach (T item in senders)
            {
                SQLTableItem[] arguments = senderTable.Columns;
                SQLTableItem primaryKey = senderTable.PrimaryKey;
                bool autoAssignPrimaryKey = false;
                long pKeyID = -1;
                string query = null;
                if (primaryKey != null)
                {
                    //ToDo: Assign primary key only possible if primary key type is INT. Allow string datatype as primary key
                    // User is responsible to ensure primary key value is defined and it's not null

                    // Include primary keys in query if value is not 0
                    pKeyID = (int)primaryKey.Property.GetValue(item);
                    if (pKeyID != 0) arguments = arguments.Append(primaryKey).ToArray();
                    else autoAssignPrimaryKey = true;
                }

                //Query without primary key, safe to use INSERT or REPLACE Statement
                //Create SQL query for insertion
                query = $"INSERT OR REPLACE INTO {tableName} " +
                    $"({string.Join(", ", arguments.Select(p => p.SQLName))}) VALUES " +
                    $"({string.Join(", ", arguments.Select(p => "@" + p.SQLName))})";

                if (autoAssignPrimaryKey) query = query.Replace("INSERT OR REPLACE", "INSERT OR IGNORE");

                //Create parameter list
                List<SQLiteParameter> parameters = arguments.Except(senderTable.IndexKeys).
                        Select(p => new SQLiteParameter($"@{p.SQLName}", p.GetDBValue(item))).ToList();

                //Replace value for property marked as SQLIndex with id.
                foreach (SQLTableItem i in senderTable.IndexKeys)
                {
                    IndexTableHandler indexTable = GetIndexTable(i.TableName);
                    string value = i.Property.GetValue(item)?.ToString();
                    if (string.IsNullOrEmpty(value))
                    {
                        //Value is null, set parameter value as DBNull.
                        parameters.Add(new SQLiteParameter($"@{i.SQLName}", DBNull.Value));
                    }
                    else
                    {
                        int id = indexTable.GetIdByName(i.Property.GetValue(item).ToString());
                        parameters.Add(new SQLiteParameter($"@{i.SQLName}", id));
                    }
                }

                // Execute SQL query for current table
                Command.Parameters.Clear();
                Command.Parameters.AddRange(parameters.ToArray());
                int rowChanged = ExecuteNonQuery(query); //ToDo: Unqiue Constraints may cause existing row get overwrite with different primary key

                //Assign Primary Key
                if (autoAssignPrimaryKey && rowChanged == 1)
                {
                    //Primary key value is 0, read assigned primary key value from database.
                    int lastRowID = GetLastRowID(tableName);
                    pKeyID = Convert.ToInt32(ExecuteScalar($"SELECT {primaryKey.Name} FROM {tableName} WHERE ROWID = {lastRowID}"));
                    //Update primary key value to object.
                    primaryKey.Property.SetValueEx(item, pKeyID);
                }
                else if (autoAssignPrimaryKey)
                {
                    autoAssignPrimaryKey = false;

                    //Unique constraint violated, row with same unique constrtin exists.
                    SQLTableItem[] uniqueColumns = arguments.Where(n => n.IsUniqueColumn || n.IsUniqueMulltiColumn).ToArray();
                    if (uniqueColumns == null || uniqueColumns.Length == 0) throw new InvalidOperationException("Expecting unique columns, but not found any!");
                    string queryUniqueItem = $"SELECT {primaryKey.SQLName} FROM {tableName} WHERE " +
                        string.Join(" AND ", uniqueColumns.Select(a => a.SQLName + " = @" + a.SQLName));

                    pKeyID = (long)ExecuteScalar(queryUniqueItem);
                    primaryKey.Property.SetValueEx(item, pKeyID);
                    arguments = arguments.Append(primaryKey).ToArray();
                    query = query.Replace("INSERT OR IGNORE", "INSERT OR REPLACE");
                    ExecuteNonQuery(query);
                }

                //Write array values to Array Table
                foreach (SQLTableItem t in senderTable.ArrayTables)
                {
                    Type elementType = t.Property.PropertyType.GetElementType();
                    bool isString = elementType == typeof(string);
                    string arrayTableName = t.TableName;
                    //Delete old records
                    query = $"DELETE FROM {arrayTableName} WHERE ID = {pKeyID}";
                    ExecuteNonQuery(query);
                    IList childs = t.Property.GetValue(item) as IList;
                    if (childs == null) continue;
                    ExecuteTransaction(() =>
                    {
                        foreach (var c in childs)
                        {
                            query = $"INSERT INTO {arrayTableName} (ID, VALUE) VALUES ({pKeyID}, ";
                            query += isString ? $"'{c}'" : c;
                            query += ")";
                            ExecuteNonQuery(query);
                        }
                    });
                }

                //Assign Value and parent key for nested table.
                foreach (SQLTableItem t in senderTable.ChildTables)
                {
                    //Get child table info and identify parent key
                    SQLTableInfo childTableInfo = t.ChildTableInfo;
                    if (childTableInfo.ParentKey?.ParentType != senderTable.TableType)
                        throw new FormatException("Parent key not defined for class " + childTableInfo.Name);

                    //Assign parent id to parent key
                    IList childs = t.Property.GetValue(item) as IList;
                    List<object> childList = new List<object>();
                    if (childs != null)
                    {
                        foreach (object c in childs)
                        {
                            childTableInfo.ParentKey.Property.SetValueEx(c, pKeyID);
                            childList.Add(c); //Convert to list which later pass as an object to method WriteToDatabase()
                        }
                    }

                    string dbBackup = SetSecondaryDBPath(t);
                    try
                    {
                        //When updating existing items, delete existing child items before writing updated one.
                        DeleteChildItemsByParentID(childTableInfo, pKeyID);
                        //Recusive write to child table items.
                        if (childList.Count > 0) WriteToDatabaseInt(childTableInfo, childList.ToArray());
                    }
                    finally { RestorePrimaryDBPath(dbBackup); }
                }

            }//for each items in senders
        }

        private void DeleteChildItemsByParentID(SQLTableInfo childTableInfo, long parentID)
        {
            string query = $"DELETE FROM {childTableInfo.TableName} WHERE {childTableInfo.ParentKey.Name} == {parentID}";
            ExecuteNonQuery(query);
        }

        /// <summary>
        /// Delete items and it's child items from database by primary key. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="senders"></param>
        protected void DeleteFromDatabaseByID<T>(params T[] senders)
        {
            DeleteFromDatabaseByID(senders, string.Empty);
        }

        /// <summary>
        /// Delete items and it's child items from database by primary key. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="senders"></param>
        /// <param name="tableName">Table name</param>
        protected void DeleteFromDatabaseByID<T>(T[] senders, string tableName = null)
        {
            Type senderType = senders.First().GetType();
            SQLTableInfo senderTable = GetTableInfo(senderType, tableName);

            if (senderTable.PrimaryKey == null) throw new ArgumentException("Unable to delete items, primary key not declared!");
            foreach (T item in senders)
            {
                //Get primary key for each item.
                object pKey = senderTable.PrimaryKey.Property.GetValue(item);

                //Delete all child items by parent's primary key.
                foreach (SQLTableItem t in senderTable.ChildTables)
                {
                    SQLTableInfo childTableInfo = t.ChildTableInfo;
                    ExecuteNonQuery($"DELETE FROM {childTableInfo.TableName} WHERE {childTableInfo.ParentKey.Name} == {pKey}");
                }

                //Delete parent instance
                ExecuteNonQuery($"DELETE FROM {senderTable.TableName} WHERE {senderTable.PrimaryKey.SQLName} == {pKey}");
            }
        }

        /// <summary>
        /// Conditional delete table from database 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="whereStatement"></param>
        protected void DeleteFromDatabase<T>(string whereStatement, string tableName = null)
        {
            if (string.IsNullOrEmpty(whereStatement)) throw new ArgumentNullException("whereStatement");
            SQLTableInfo senderTable = GetTableInfo(typeof(T), tableName);
            DeleteFromDatabase(whereStatement, senderTable.TableName);
        }

        /// <summary>
        /// Conditional delete table from defined table name.
        /// </summary>
        /// <param name="whereStatement"></param>
        /// <param name="tableName"></param>
        protected void DeleteFromDatabase(string whereStatement, string tableName)
        {
            ExecuteNonQuery($"DELETE FROM {tableName} {whereStatement}");
        }

        #endregion

        #region [ Switch Database ]

        /// <summary>
        /// Used by <see cref="WriteToDatabaseInt{T}(T[])"/> and <see cref="ReadFromDatabaseInt{T}(SQLTableInfo, string)"/>
        /// to switch SQL Connection to different database defined by <see cref="SQLDatabseAttribute"/>
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string SetSecondaryDBPath(SQLTableItem item)
        {
            string dbBackup = string.Empty;
            if (!string.IsNullOrEmpty(item.SecondaryDatabaseFilePath))
            {
                dbBackup = DatabaseFilePath;
                string newDbPath = item.SecondaryDatabaseFilePath;
                if (string.IsNullOrEmpty(Path.GetDirectoryName(item.SecondaryDatabaseFilePath)))
                    newDbPath = Path.Combine(Path.GetDirectoryName(DatabaseFilePath), item.SecondaryDatabaseFilePath);
                SetSQLPath(newDbPath);
            }
            return dbBackup;
        }

        /// <summary>
        /// Restore previous database connection. Use together with <see cref="SetSecondaryDBPath(SQLTableItem)"/>
        /// </summary>
        /// <param name="dbPath"></param>
        private void RestorePrimaryDBPath(string dbPath)
        {
            if (!string.IsNullOrEmpty(dbPath)) SetSQLPath(dbPath);
        }

        #endregion

        #region [ Create Table ]

        private string CreateArrayTable(SQLDataType sqlDataType, string tableName)
        {
            Type primitiveType;
            switch (sqlDataType)
            {
                case SQLDataType.TEXT: primitiveType = typeof(string); break;
                case SQLDataType.REAL: primitiveType = typeof(double); break;
                case SQLDataType.INTEGER: primitiveType = typeof(int); break;
                default: throw new IndexOutOfRangeException("Unknown / unsupported SQLDataType!");
            }

            Type tableType = typeof(ArrayTable<>).MakeGenericType(primitiveType);
            return CreateTable(tableType, tableName);
        }

        private string CreateTable(Type tableType, string tableName)
        {
            string createStatement = GetCreateStatement(tableType, tableName);
            ExecuteNonQuery(createStatement);
            return createStatement;
        }

        protected string CreateTable<T>(string tableName = null)
        {
            return CreateTable(typeof(T), tableName);
        }

        private string GetCreateStatement(Type tableType, string tableName = null)
        {
            SQLTableInfo ptrTable = TableInfos.FirstOrDefault(n => n.TableType == tableType);
            if (ptrTable == null) ptrTable = new SQLTableInfo(tableType);
            if (string.IsNullOrEmpty(tableName)) tableName = ptrTable.TableName;

            string paramList = string.Empty;
            if (ptrTable.PrimaryKey != null)
            {
                paramList += GetCreateParam(ptrTable.PrimaryKey);
            }
            foreach (SQLTableItem t in ptrTable.Columns)
            {
                if (t.IsChildTable) continue;
                paramList += GetCreateParam(t);
            }

            if (ptrTable.PrimaryKey != null)
            {
                paramList += $"PRIMARY KEY(\"{ptrTable.PrimaryKey.SQLName}\"),";
            }

            //Select columns defined with SQLUniqueMultiColumns attribute
            string[] uniqueMultiColumns = ptrTable.Columns.Where(n => n.IsUniqueMulltiColumn)
                    .Select(n => n.SQLName).ToArray();
            if (uniqueMultiColumns.Length > 0)
            {
                paramList += $"UNIQUE({string.Join(",", uniqueMultiColumns.Select(n => "\"" + n + "\""))})";
            }
            paramList = paramList.TrimEnd(',');

            string result = $"CREATE TABLE \"{tableName}\" ({paramList})";
            return result;
        }

        private string GetCreateParam(SQLTableItem item)
        {
            return $"\"{item.SQLName}\" {item.DataType}{(item.IsUniqueColumn ? " UNIQUE" : "")},";
        }

        #endregion

        #region [ Backup ]

        public uint BackupRetries { get; set; } = 10;
        public int BackupRetryInterval_ms { get; set; } = 1000;

        /// <summary>
        /// Backup current database to defined backup database file path.
        /// </summary>
        /// <param name="targetDatabaseFilePath"></param>
        public void BackupDatabaseTo(string targetDatabaseFilePath)
        {
            if (string.IsNullOrWhiteSpace(targetDatabaseFilePath))
                throw new ArgumentException("Backup file path cannot be null or empty.", nameof(targetDatabaseFilePath));

            try
            {
                for (int retryCount = 0; retryCount < BackupRetries; retryCount++)
                {
                    try
                    {
                        CheckPoint();
                        Connect();

                        // Use using statement to ensure proper resource management
                        using (var targetDB = new SQLiteDBLite(targetDatabaseFilePath))
                        {
                            // Perform backup with a timeout mechanism
                            targetDB.Connect();
                            DBConnection.BackupDatabase(targetDB.DBConnection, "main", "main", -1, null, 0);
                            Trace.WriteLine($"Database successfully backed up to {targetDatabaseFilePath}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log specific error details
                        Trace.WriteLine($"[WARNING]: Backup attempt {retryCount + 1} failed: {ex.Message}");

                        // If it's the last retry, rethrow
                        if (retryCount == BackupRetries)
                        {
                            throw new InvalidOperationException(
                                $"Failed to backup database to {targetDatabaseFilePath} after {BackupRetries + 1} attempts.",
                                ex);
                        }

                        // Wait before retrying
                        Thread.Sleep(BackupRetryInterval_ms);
                    }
                    finally
                    {
                        // Ensure database is disconnected after each attempt
                        Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                // Additional top-level error logging if needed
                Trace.WriteLine($"Unhandled error during database backup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sync current database content from source database using BackupDatabase method.
        /// </summary>
        /// <param name="sourceDatabasePath"></param>
        /// <exception cref="ArgumentException"></exception>
        public void SyncDatabaseFrom(string sourceDatabasePath)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabasePath))
                throw new ArgumentException("Source database path cannot be null or empty.", nameof(sourceDatabasePath));

            try
            {
                for (int retryCount = 0; retryCount < BackupRetries; retryCount++)
                {
                    try
                    {
                        CheckPoint();
                        // Use a separate SQLiteDBLite instance for the source database
                        using (var sourceDB = new SQLiteDBLite(sourceDatabasePath))
                        {
                            // Perform backup with a timeout mechanism
                            sourceDB.Connect();
                            Connect(); // Connect the current database instance

                            // Backup from the source database to the current database
                            sourceDB.DBConnection.BackupDatabase(DBConnection, "main", "main", -1, null, 0);
                            Trace.WriteLine($"Database successfully backed up from {sourceDatabasePath}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log specific error details
                        Trace.WriteLine($"[WARNING]: Backup attempt {retryCount + 1} failed: {ex.Message}");

                        // If it's the last retry, rethrow
                        if (retryCount == BackupRetries)
                        {
                            throw new InvalidOperationException(
                                $"Failed to backup database from {sourceDatabasePath} after {BackupRetries + 1} attempts.",
                                ex);
                        }

                        // Wait before retrying
                        Thread.Sleep(BackupRetryInterval_ms);
                    }
                    finally
                    {
                        // Ensure database is disconnected after each attempt
                        Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                // Additional top-level error logging if needed
                Trace.WriteLine($"Unhandled error during database backup: {ex.Message}");
                throw;
            }
        }

        #endregion
    }


    /// <summary>
    /// Minimum concreate class for internal usage
    /// </summary>
    internal class SQLiteDBLite : SQLiteHelper
    {
        public SQLiteDBLite(string databasePath) : base()
        {
            SetSQLPath(databasePath);
            DBConnection.Open();
        }

    }
}