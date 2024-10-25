namespace System;

public static class StringExtensions
{
	public static byte[] ToByteArray(this string s)
	{
		byte[] array = new byte[s.Length];
		for (int i = 0; i < s.Length; i++)
		{
			array[i] = (byte)s[i];
		}
		return array;
	}

	public static bool IsDigitsOnly(this string str)
	{
		foreach (char c in str)
		{
			if (c < '0' || c > '9')
			{
				return false;
			}
		}
		return true;
	}
}
