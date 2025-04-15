namespace CodeArtEng.SQLite
{
    public enum SQLWriteMode
    {
        /// <summary>
        /// Update all rows in both parent and child tables including all index tables
        /// </summary>
        All,
        /// <summary>
        /// Update rows in parent table and parent index tables only.
        /// </summary>
        ParentOnly,
        /// <summary>
        /// Update rows in child table and child index tables only.
        /// </summary>
        ChildsOnly
    }

    /// <summary>
    /// Write options used by WriteToDatabase.
    /// </summary>
    public class SQLiteWriteOptions
    {
        /// <summary>
        /// Create table when table not exists in database.
        /// (Default = true)
        /// </summary>
        public bool CreateTable { get; set; } = true;
        /// <summary>
        /// Write operation mode.
        /// </summary>
        public SQLWriteMode WriteMode { get; set; } = SQLWriteMode.All;
    }
}
