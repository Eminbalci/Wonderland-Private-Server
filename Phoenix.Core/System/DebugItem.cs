using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Phoenix.Core.Exceptions;

namespace System;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DebugItem
{
	public string Msg;

	public DebugItemType Type;

	private ExceptionData Error;

	public int VerboseReq
	{
		get
		{
			switch (Type)
			{
			case DebugItemType.Info_Light:
			case DebugItemType.Error:
				return 0;
			case DebugItemType.DataBase_Light:
			case DebugItemType.Network_Light:
				return 1;
			case DebugItemType.Info_Heavy:
			case DebugItemType.DataBase_Heavy:
			case DebugItemType.Network_Heavy:
				return 2;
			case DebugItemType.Debug:
				return 3;
			default:
				return -1;
			}
		}
	}

	public DebugItem([CallerMemberName] string mn = "", [CallerFilePath] string fp = "", [CallerLineNumber] int ln = 0)
	{
		Msg = "";
		Error = null;
		Type = DebugItemType.Info_Light;
	}

	public DebugItem(ExceptionData exception)
	{
		Type = DebugItemType.Error;
		Error = exception;
		Msg = Error.Message;
	}
}
