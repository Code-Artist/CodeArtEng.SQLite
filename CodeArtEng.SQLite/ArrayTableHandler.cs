namespace CodeArtEng.SQLite
{
    internal class ArrayTableHandler
    {
    }

    public class ArrayTable <T>
    {
        /// <summary>
        /// Parent table ID
        /// </summary>
        public int ID { get; set; }
        public T Value { get; set; }
    }
}
