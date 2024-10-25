using System.Diagnostics;

namespace System.IO;

public static class Get
{
	public static string FileVersion(string file)
	{
		return FileVersionInfo.GetVersionInfo(file).FileVersion;
	}
}
