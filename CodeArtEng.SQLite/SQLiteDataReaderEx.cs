using System.Data.SQLite;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Extension class for <see cref="SQLiteDataReader"/>
    /// </summary>
    public static class SQLiteDataReaderEx
    {
        /// <summary>
        /// Retrieve value as string with null check.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static string GetStringEx(this SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index)) return string.Empty;
            return reader.GetString(index).Trim();
        }

        /// <summary>
        /// Retrieve value as Int32 with null check.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int GetInt32Ex(this SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index)) return 0;
            return reader.GetInt32(index);
        }

        /// <summary>
        /// Retrieve value as Int32 with null check.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static long GetInt64Ex(this SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index)) return 0;
            return reader.GetInt64(index);
        }

    }
}
