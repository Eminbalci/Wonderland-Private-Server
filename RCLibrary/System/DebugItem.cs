using System.Drawing;
using System.Runtime.CompilerServices;
using Phoenix.Core.Controls;

namespace System;

public class DebugItem : IUpdatableControl
{
	private string msg;

	private DebugItemType Dtype;

	private ExceptionData DError;

	private Color DCol;

	private DateTime Dwhen;

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

	public string Msg
	{
		get
		{
			return (Error == null) ? msg : Error.Message;
		}
		set
		{
			SetField(ref msg, value, "Msg");
		}
	}

	public Color Col
	{
		get
		{
			return DCol;
		}
		set
		{
			SetField(ref DCol, value, "Col");
		}
	}

	public ExceptionData Error
	{
		get
		{
			return DError;
		}
		set
		{
			SetField(ref DError, value, "Error");
		}
	}

	public DateTime When
	{
		get
		{
			return Dwhen;
		}
		set
		{
			SetField(ref Dwhen, value, "When");
		}
	}

	public DebugItemType Type
	{
		get
		{
			return Dtype;
		}
		set
		{
			SetField(ref Dtype, value, "Type");
		}
	}

	public DebugItem(ExceptionData exception)
	{
		Dtype = DebugItemType.Error;
		Error = exception;
		Col = Color.Red;
		Dwhen = DateTime.Now;
	}

	public DebugItem([CallerMemberName] string mn = "", [CallerFilePath] string fp = "", [CallerLineNumber] int ln = 0)
	{
		Msg = "";
		Error = null;
		Dtype = DebugItemType.Info_Light;
		DCol = Color.White;
		Dwhen = DateTime.Now;
	}

	public DebugItem(ExceptionData exception, [CallerMemberName] string mn = "", [CallerFilePath] string fp = "", [CallerLineNumber] int ln = 0)
	{
		Dtype = DebugItemType.Error;
		Error = exception;
		Col = Color.Red;
		Dwhen = DateTime.Now;
	}
}
