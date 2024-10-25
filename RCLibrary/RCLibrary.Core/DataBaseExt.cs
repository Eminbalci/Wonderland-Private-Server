using System;
using System.Data;
using System.Globalization;

namespace RCLibrary.Core;

public static class DataBaseExt
{
	public static T GetField<T>(this DataRow src, string columnname)
	{
		if (!src.Table.Columns.Contains(columnname))
		{
			return default(T);
		}
		if (typeof(T).IsValueType)
		{
			try
			{
				return (T)Convert.ChangeType(src[columnname], typeof(T), CultureInfo.InvariantCulture);
			}
			catch
			{
				return default(T);
			}
		}
		return (src[columnname] is DBNull) ? default(T) : src.Field<T>(columnname);
	}
}
