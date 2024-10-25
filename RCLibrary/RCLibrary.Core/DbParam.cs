namespace RCLibrary.Core;

public struct DbParam
{
	public string identifier;

	public string value;

	public DbParam(string identity, object parameter)
	{
		identifier = identity;
		value = parameter.ToString();
	}
}
