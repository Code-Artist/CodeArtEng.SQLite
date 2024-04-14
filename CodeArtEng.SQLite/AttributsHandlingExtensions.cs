using System;
using System.Reflection;

namespace CodeArtEng.SQLite
{
    internal static class AttributsHandlingExtensions
    {
        public static string SQLName(this Type sender)
        {
            SQLNameAttribute attr =
                sender.GetCustomAttribute(typeof(SQLNameAttribute)) as SQLNameAttribute;
            if (attr != null) return attr.Name;
            return sender.Name;
        }
    }
}
