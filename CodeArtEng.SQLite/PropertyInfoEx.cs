namespace System.Reflection
{
    internal static class PropertyInfoEx
    {
        public static void SetValueEx(this PropertyInfo sender, object obj, object value)
        {
            if(sender.PropertyType == value.GetType())
            {
                sender.SetValue(obj, value);
                return;
            }
            object convertedValue = Convert.ChangeType(value, sender.PropertyType);
            sender.SetValue(obj, convertedValue);
        }
    }
}
