using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters;

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
        #endregion

        /// <summary>
        /// SQLite Database Connection.
        /// </summary>
        public SQLiteConnection DBConnection { get; private set; } = new SQLiteConnection();

        /// <summary>
        /// Database connection string
        /// </summary>
        public string ConnectString { get; private set; } = string.Empty;
        /// <summary>
        /// Database connection string for readonly access, internal used to optimize read operation without locking.
        /// </summary>
        protected string ConnectStringReadOnly => ReadOnly ? ConnectString : ConnectString + "Read Only=True;";
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
        /// Maximum time to wait for a query to return in seconds.
        /// </summary>
        [DefaultValue(30)]
        protected int QueryTimeout
        {
            get => _QueryTimeout;
            set
            {
                _QueryTimeout = value;
                if (Command != null) Command.CommandTimeout = value;
            }
        }
        private int _QueryTimeout = 30;

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

        /// <summary>
        /// Constructor. Nothing special happening here (",)
        /// </summary>
        public SQLiteHelper() { }

        #region [ IDisposable ]

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Command.Transaction.Dispose();
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
        protected virtual void SetSQLPath(string databaseFilePath, bool readOnly = false)
        {
            KeepDatabaseOpen = false;
            DisconnectDatabase();
            ConnectString = string.Empty; //Reset connect string

            DatabaseFilePath = databaseFilePath;
            if (string.IsNullOrEmpty(databaseFilePath)) throw new ArgumentNullException(nameof(databaseFilePath));
            ConnectString = @"Data Source=" + databaseFilePath + ";Version=3;";

            ReadOnly = readOnly;
            if (readOnly) ConnectString += "Read Only=True;";
            DBConnection.ConnectionString = ConnectString;
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
            Command.CommandTimeout = QueryTimeout;
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
            finally
            {
                //Clean up and disconnect
                Command.Transaction = null;
                Command.Parameters.Clear();
                transaction?.Dispose();
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
        /// Get column names for selected table.
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        private string[] GetColumnNames(string table)
        {
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("$table", table);
            List<string> results = new List<string>();
            ExecuteQuery("SELECT NAME FROM pragma_table_info($table)",
                (r) => { while (r.Read()) results.Add(r.GetString(0)); });
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
        /// </summary>
        /// <param name="tableName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        protected void ClearTable(string tableName)
        {
            VerifyTableExists(tableName);
            ExecuteNonQuery("DELETE FROM " + tableName);
            CompactDatabase();
        }

        /// <summary>
        /// Compress database.
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
        /// Return ROW_ID for last updated row.
        /// </summary>
        /// <remarks>Value reset once connection closed.</remarks>
        /// <returns></returns>
        protected int GetLastInsertedRowID()
        {
            object value = ExecuteScalar("SELECT last_insert_rowid()");
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
        protected void VerifyTableExists(string tableName)
        {
            if (!GetTables().Contains(tableName, StringComparer.InvariantCultureIgnoreCase))
                throw new InvalidOperationException($"Table [{tableName}] not exists in database!");
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
        private SQLTableInfo GetTableInfo(Type sender)
        {
            SQLTableInfo result = TableInfos.FirstOrDefault(n => n.TableType == sender);
            if (result != null) return result;

            //Create entry, verify table exist
            result = new SQLTableInfo(sender);
            if (!result.Validated) ValidateTableinfo(result);
            TableInfos.Add(result);
            return result;
        }

        private bool ValidateTableinfo(SQLTableInfo info)
        {
            info.Validated = false;

            VerifyTableExists(info.TableName);
            if (info.PrimaryKey != null) ValidatePrimaryKey(info);

            //Table may have more columns than class.
            //Ignore properties where name does not match with Table columns (Backward Compatible)
            DBColumn[] DBColumns = GetDBColumns(info.TableName);
            foreach (SQLTableItem item in info.Columns)
            {
                DBColumn column = DBColumns.FirstOrDefault(n => n.Name.Equals(item.SQLName, StringComparison.CurrentCultureIgnoreCase));
                if (column == null) throw new FormatException($"{info.TableName}: Missing column {item.SQLName}!");

                if (item.IsIndexTable && !column.Type.Equals("INTEGER"))
                    throw new FormatException($"{info.TableName}.{item.SQLName}: Expecting INTEGER type for index column.");

                if (item.IsDataTypeDefined)
                {
                    if (item.DataType == SQLDataType.TEXT && !column.Type.Equals("TEXT"))
                        throw new FormatException($"{info.TableName}.{item.SQLName}: Type mismatched, expecting TEXT!");
                    else if (item.DataType == SQLDataType.INTEGER && !column.Type.Equals("INTEGER"))
                        throw new FormatException($"{info.TableName}.{item.SQLName}: Type mismatched, expecting INTEGER!");
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

        private readonly List<IndexTable> IndexTables = new List<IndexTable>();
        private IndexTable GetIndexTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException("tableName");
            IndexTable result = IndexTables.FirstOrDefault(n => n.Name == tableName);
            if (result != null) return result;

            VerifyTableExists(tableName);
            result = new IndexTable(tableName);
            IndexTables.Add(result);
            return result;
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
        /// <see cref="ReadFromDatabase{T}(string)"/> recursive loop tracking.
        /// </summary>
        private int ReadDepth = 0;
        /// <summary>
        /// Unique ID for read operation, used to optimize Index table handling.
        /// </summary>
        private long ReadOpID = 0;

        /// <summary>
        /// A generic function to retrieve data from 
        /// a specified database table and map it to objects of a given class. 
        /// Class and properties name are not case sensitives.
        /// </summary>
        /// <typeparam name="T">Output object type</typeparam>
        /// <returns></returns>
        protected IList<T> ReadFromDatabase<T>(string whereStatement = null) where T : class, new()
        {
            if (ReadDepth == 0) ReadOpID = DateTime.Now.Ticks;
            ReadDepth++;
            KeepDatabaseOpen = true;
            try
            {
                SQLTableInfo senderTable = GetTableInfo(typeof(T));
                return ReadFromDatabaseInt<T>(senderTable, whereStatement);
            }
            finally
            {
                ReadDepth--;
                if (ReadDepth == 0)
                {
                    KeepDatabaseOpen = false;
                    Disconnect();
                }
            }
        }

        protected IList<T> ReadFromDatabaseInt<T>(SQLTableInfo senderTable, string whereStatement = null) where T : class, new()
        {
            List<T> results = new List<T>();
            string tableName = senderTable.TableName;
            SQLTableItem[] properties = senderTable.Columns;
            SQLTableItem primaryKey = senderTable.PrimaryKey;
            if (primaryKey != null) properties = properties.Concat(new[] { primaryKey }).ToArray();

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
                IndexTable indexTable = GetIndexTable(i.IndexTableName);
                foreach (var r in results)
                {
                    string value = indexTable.GetValueById(Convert.ToInt32(i.Property.GetValue(r)));
                    i.SetDBValue(r, value);
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
                    SQLTableItem parentKey = childTableInfo.ParentKeys.FirstOrDefault(n => n.ParentType == senderTable.TableType)
                        ?? throw new FormatException("Parent key not defined for class " + childTableInfo.Name);

                    //Read from child table by parent key ID.
                    MethodInfo ptrReadMethod = this.GetType()
                        .GetMethod("ReadFromDatabaseInt", BindingFlags.Instance | BindingFlags.NonPublic)
                        .MakeGenericMethod(childType);

                    string dbBackup = SetSecondaryDBPath(r);
                    try
                    {
                        foreach (T i in results)
                        {
                            int pKey = (int)senderTable.PrimaryKey.GetDBValue(i);
                            //Recursive call to ReadFromDatabse method.
                            object childItems = ptrReadMethod.Invoke(this, new object[] { childTableInfo, $"WHERE {parentKey.Name} == {pKey}" });
                            r.Property.SetValue(i, childItems);
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
                    IndexTable tb = GetIndexTable(p.IndexTableName);
                    if (tb.LastReadID == ReadOpID) continue;
                    tb.LastReadID = ReadOpID;

                    tb.Items.Clear();
                    string query = $"SELECT ID, Name from {p.IndexTableName}";
                    ExecuteQuery(query, processQueryResults: r =>
                    {
                        while (r.Read())
                        {
                            IndexTableItem t = new IndexTableItem()
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
        /// Writes an array of objects to a specified database table.
        /// The column names and type in the table should match the properties of the class.
        /// Only properties with get and set are serialized.
        /// Use <see cref="IgnoreSQLColumnAttribute"/> to skip selected properties.
        /// </summary>
        /// <remarks>DateTime shall stored as TEXT. This method works well with <see cref="ExecuteTransaction(Action)"/></remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="senders"></param>
        protected void WriteToDatabase<T>(params T[] senders) where T : class
        {
            bool keepDBOpenFlag = KeepDatabaseOpen;
            KeepDatabaseOpen = true; //Overwrite keepdatabase open
            try
            {
                WriteToDatabaseInt(senders);

                //update index table with new items.
                foreach (IndexTable i in IndexTables)
                {
                    if (i.NewItems.Count == 0) continue;
                    string query = $"INSERT OR REPLACE INTO {i.Name} (ID, Name) VALUES (@ID, @Name)";
                    foreach (IndexTableItem m in i.NewItems)
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

        private void WriteToDatabaseInt<T>(params T[] senders) where T : class
        {
            //IMPORTANT:
            //Connection to database shall not be close throughout the complete execution,
            //otherwise GetLastInsertedRowID will return null;

            //IMPORTANT:
            //DO NOT Use transaction as multiple read and write query are used.

            Type senderType = senders.First().GetType();
            SQLTableInfo senderTable = GetTableInfo(senderType);
            string tableName = senderTable.TableName;

            // Execute query for each element in the array.
            // Each query update a row in table.
            foreach (T item in senders)
            {
                SQLTableItem[] arguments = senderTable.Columns;
                SQLTableItem primaryKey = senderTable.PrimaryKey;
                bool assignPrimaryKey = false;
                int pKeyID = -1;
                if (primaryKey != null)
                {
                    // Include primary keys in query if value is not 0
                    pKeyID = (int)primaryKey.Property.GetValue(item);
                    if (pKeyID != 0) arguments = arguments.Concat(new[] { primaryKey }).ToArray();
                    else assignPrimaryKey = true;
                }

                // Create SQL query for insertion
                string query = $"INSERT OR REPLACE INTO {tableName} " +
                    $"({string.Join(", ", arguments.Select(p => p.SQLName))}) VALUES " +
                    $"({string.Join(", ", arguments.Select(p => "@" + p.SQLName))})";

                //Create parameter list
                List<SQLiteParameter> parameters = arguments.Except(senderTable.IndexKeys).
                        Select(p => new SQLiteParameter($"@{p.SQLName}", p.GetDBValue(item))).ToList();

                //Replace value for property marked as SQLIndex with id.
                foreach (SQLTableItem i in senderTable.IndexKeys)
                {
                    IndexTable indexTable = GetIndexTable(i.IndexTableName);
                    int id = indexTable.GetIdByName(i.Property.GetValue(item).ToString());
                    parameters.Add(new SQLiteParameter($"@{i.SQLName}", id));
                }

                // Execute SQL query for current table
                Command.Parameters.Clear();
                Command.Parameters.AddRange(parameters.ToArray());
                ExecuteNonQuery(query);

                //Assign Primary Key
                if (assignPrimaryKey)
                {
                    //Primary key value is 0, read assigned primary key value from database.
                    int lastRowID = GetLastInsertedRowID();
                    pKeyID = Convert.ToInt32(ExecuteScalar($"SELECT {primaryKey.Name} FROM {tableName} WHERE ROWID = {lastRowID}"));
                    //Update primary key value to object.
                    primaryKey.Property.SetValue(item, pKeyID);
                }

                //Assign Value and parent key for nested table.
                foreach (SQLTableItem t in senderTable.ChildTables)
                {
                    //Get child table info and identify parent key
                    SQLTableInfo childTableInfo = t.ChildTableInfo;
                    Type childType = childTableInfo.TableType;
                    SQLTableItem parentKey = childTableInfo.ParentKeys.FirstOrDefault(n => n.ParentType == senderTable.TableType)
                        ?? throw new FormatException("Parent key not defined for class " + childTableInfo.Name);

                    //Assign parent id to parent key
                    IList childs = t.Property.GetValue(item) as IList;
                    List<object> childList = new List<object>();
                    foreach (object c in childs)
                    {
                        parentKey.Property.SetValue(c, pKeyID);
                        childList.Add(c); //Convert to list which later pass as an object to method WriteToDatabase()
                    }

                    string dbBackup = SetSecondaryDBPath(t);
                    try
                    {
                        //Recusive write to child table items.
                        WriteToDatabaseInt(childList.ToArray());
                    }
                    finally { RestorePrimaryDBPath(dbBackup); }
                }
            }//for each items in senders

        }
        #endregion

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
    }
}