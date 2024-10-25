using System.Diagnostics;

namespace Phoenix.Core;

public static class Get
{
	public static string FileVersion(string file)
	{
		return FileVersionInfo.GetVersionInfo(file).FileVersion;
	}
}
