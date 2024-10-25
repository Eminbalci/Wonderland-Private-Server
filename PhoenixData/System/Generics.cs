using System.IO;
using System.Runtime.InteropServices;

namespace System;

public static class Generics
{
	public static T ConvertTo<T>(Stream fs)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(T))];
		fs.Read(array, 0, Marshal.SizeOf(typeof(T)));
		T result = default(T);
		GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
		try
		{
			result = (T)Marshal.PtrToStructure(gCHandle.AddrOfPinnedObject(), typeof(T));
			return result;
		}
		catch (Exception)
		{
		}
		finally
		{
			gCHandle.Free();
		}
		return result;
	}

	public static byte[] ConvertTo<T>(T file)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(T))];
		GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
		try
		{
			IntPtr ptr = gCHandle.AddrOfPinnedObject();
			Marshal.StructureToPtr((object)file, ptr, fDeleteOld: false);
		}
		catch (Exception)
		{
		}
		finally
		{
			gCHandle.Free();
		}
		return array;
	}
}
