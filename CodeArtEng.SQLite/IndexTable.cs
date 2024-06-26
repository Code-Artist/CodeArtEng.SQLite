﻿using System.Collections.Generic;
using System.Linq;

namespace CodeArtEng.SQLite
{
    /// <summary>
    /// Key value index table.
    /// </summary>
    internal class IndexTable
    {
        /// <summary>
        /// Last read index to optimize <see cref="SQLiteHelper.ReadFromDatabase{T}(string)"/>
        /// operation. 
        /// </summary>
        public long LastReadID { get; set; } = 0;
        public IndexTable(string name) => Name = name;
        /// <summary>
        /// Table name
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Table's Items
        /// </summary>
        public List<IndexTableItem> Items { get; set; } = new List<IndexTableItem>();
        /// <summary>
        /// New items, pending update to database.
        /// </summary>
        public List<IndexTableItem> NewItems { get; set; } = new List<IndexTableItem>();
        /// <summary>
        /// Get next unique ID.
        /// </summary>
        /// <returns></returns>
        public int GetNextID() => (Items.Count == 0) ? 1 : Items.Max(n => n.ID) + 1;
        /// <summary>
        /// Get value by ID.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetIdByName(string name)
        {
            //IF item not exist, register new id to index table.
            //item added to new item list in index table.
            //id in index table auto generated by indextable class

            IndexTableItem ptrItem = Items.FirstOrDefault(n => n.Name == name);
            if (ptrItem == null)
            {
                ptrItem = new IndexTableItem() { ID = GetNextID(), Name = name };
                Items.Add(ptrItem);
                NewItems.Add(ptrItem);
            }
            return ptrItem.ID;
        }
        /// <summary>
        /// Get ID by value.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetValueById(int id) => Items.FirstOrDefault(n => n.ID == id)?.Name;
    }

    /// <summary>
    /// Item for Key Value Index table.
    /// </summary>
    internal class IndexTableItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }

    internal class IndexTableTemplate
    {
        [PrimaryKey]
        public int ID { get; set; } 
        public string Name { get; set; }
    }
}
