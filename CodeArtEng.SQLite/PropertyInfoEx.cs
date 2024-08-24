using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace System.Reflection
{
    internal static class PropertyInfoEx
    {
        public static void SetValueEx(this PropertyInfo sender, object obj, object value)
        {
            object convertedValue = Convert.ChangeType(value, sender.PropertyType);
            sender.SetValue(obj, convertedValue);
        }
    }
}
