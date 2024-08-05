using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
