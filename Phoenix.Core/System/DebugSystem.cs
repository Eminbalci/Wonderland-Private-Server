using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Phoenix.Core.Exceptions;

namespace System;

public static class DebugSystem
{
	private const long logfileMaxsize = 2500000L;

	private const int MaxlogfileCnt = 10;

	private static Task loggerTask;

	private static string logfold = Environment.CurrentDirectory;

	private static string logfile;

	private static RichTextBox m_rtb = null;

	private static readonly object m_Lock = new object();

	private static Queue<string> msg_towrite = new Queue<string>();

	private static Dictionary<DateTime, DebugItem> Log = new Dictionary<DateTime, DebugItem>(500);

	public static int VerboseLvl = 0;

	public static event EventHandler<DebugItem> onNewLog;

	public static void Initialize(RichTextBox rtb)
	{
		logfile = "wlophoenixlogFile" + DateTime.Now.ToShortDateString().Replace("/", "") + ".txt";
		m_rtb = rtb;
		VerboseLvl = 2;
	}

	public static bool IsRefd()
	{
		return null != m_rtb;
	}

	public static void UnRef()
	{
		m_rtb = null;
	}

	public static void Write(string str, DebugItemType type = DebugItemType.Info_Light)
	{
		Write(str, null, type);
	}

	public static void Write(object data, params object[] parm)
	{
		string str = data.ToString();
		if (data is string && parm.Count() > 0)
		{
			str = string.Format(data.ToString(), parm);
		}
		if (data is string)
		{
			Write(str, DebugItemType.Info_Heavy);
		}
		else if (data is Exception)
		{
			Write(data, null, DebugItemType.Error);
		}
	}

	public static void Write(object data, DebugItemType type, params object[] parm)
	{
		string str = data.ToString();
		if (data is string && parm.Count() > 0)
		{
			str = string.Format(data.ToString(), parm);
		}
		if (data is string)
		{
			Write(str, type);
		}
		else if (data is Exception)
		{
			Write(data, null, type);
		}
	}

	public static void Write(Color? col, object data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		Write(data, col, type, newline);
	}

	public static void Write(object data, Color? col = null, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		string text = "";
		switch (type)
		{
		case DebugItemType.Info_Light:
			text = "Info: ";
			col = Color.White;
			break;
		case DebugItemType.Network_Light:
			text = "NetInfo: ";
			col = Color.Green;
			break;
		case DebugItemType.Error:
			text = "Error: ";
			col = Color.Red;
			break;
		case DebugItemType.DataBase_Heavy:
			text = "DataBaseDebug: ";
			col = Color.CadetBlue;
			break;
		case DebugItemType.Network_Heavy:
			text = "NW-Debug: ";
			col = Color.SkyBlue;
			break;
		default:
			text = "Debug: ";
			col = Color.Magenta;
			break;
		}
		DateTime key = DateTime.Now;
		string timestamp = $"[{key:yyyy-MM-dd HH:mm:ss}] ";
		DebugItem value = default(DebugItem);
		if (data is string)
		{
			value.Type = type;
			value.Msg = data.ToString();
			if (value.VerboseReq <= VerboseLvl)
			{
				WriteLine(timestamp + text + data.ToString(), col, newline);
			}
			while (true)
			{
				try
				{
					Log.Add(key, value);
				}
				catch
				{
					key = key.AddMilliseconds(1.0);
					continue;
				}
				break;
			}
		}
		else if (data is ExceptionData)
		{
			value = new DebugItem(data as ExceptionData);
			if (value.VerboseReq <= VerboseLvl)
			{
				WriteLine(timestamp + text + value.Msg, Color.Red, newline);
			}
			while (true)
			{
				try
				{
					Queue<DateTime> old = new Queue<DateTime>();
					Log.AsParallel().ForAll(delegate(KeyValuePair<DateTime, DebugItem> c)
					{
						if (DateTime.Now - c.Key > new TimeSpan(0, 5, 0))
						{
							old.Enqueue(c.Key);
						}
					});
					while (old.Count > 0)
					{
						Log.Remove(old.Dequeue());
					}
					Log.Add(key, value);
					if (DebugSystem.onNewLog != null)
					{
						DebugSystem.onNewLog(null, value);
					}
				}
				catch
				{
					key = key.AddMilliseconds(1.0);
					continue;
				}
				break;
			}
		}
		if (loggerTask == null || loggerTask.IsCompleted)
		{
			loggerTask = Task.Factory.StartNew(delegate
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(logfold);
				int num = directoryInfo.EnumerateFiles("*.txt").Count((FileInfo c) => c.Name.StartsWith("wlophoenixlogFile"));
				if (num >= 10)
				{
					List<FileInfo> list = (from c in directoryInfo.EnumerateFiles("*.txt")
						where c.Name.StartsWith("wlophoenixlogFile")
						orderby c.LastWriteTime
						select c).ToList();
					if (File.Exists(list[0].FullName))
					{
						File.Delete(list[0].FullName);
					}
				}
				int num2 = 1;
				while (File.Exists(Path.Combine(logfold, logfile)))
				{
					FileInfo fileInfo = new FileInfo(Path.Combine(logfold, logfile));
					if (fileInfo.Length >= 2500000)
					{
						try
						{
							File.Copy(fileInfo.FullName, Path.Combine(logfold, logfile.Replace(".", num2 + ".")));
						}
						catch
						{
							num2++;
							continue;
						}
						File.Delete(fileInfo.FullName);
						fileInfo = null;
					}
					break;
				}
				lock (m_Lock)
				{
					try
					{
						using StreamWriter streamWriter = new StreamWriter(Path.Combine(logfold, logfile), append: true);
						streamWriter.AutoFlush = false;
						int num3 = 350;
						while (msg_towrite.Count > 0 || num3-- > 0)
						{
							streamWriter.WriteLine(msg_towrite.Dequeue());
						}
						streamWriter.WriteLine(string.Concat($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | ", type, " | ", data));
						streamWriter.Flush();
					}
					catch
					{
						msg_towrite.Enqueue(string.Concat($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | ", type, " | ", data));
					}
				}
			});
			return;
		}
		lock (m_Lock)
		{
			msg_towrite.Enqueue(string.Concat($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | ", type, " | ", data));
		}
	}

	private static void WriteLine(string str, Color? col, bool newline)
	{
		if (null == m_rtb)
		{
			return;
		}
		if (!m_rtb.InvokeRequired)
		{
			lock (m_Lock)
			{
				m_rtb.SelectionStart = m_rtb.TextLength;
				m_rtb.SelectionLength = 0;
				if (col.HasValue)
				{
					m_rtb.SelectionColor = col.Value;
				}
				m_rtb.AppendText(str + (newline ? "\r\n" : ""));
				m_rtb.SelectionColor = m_rtb.ForeColor;
				m_rtb.SelectionStart = m_rtb.TextLength;
				m_rtb.SelectionLength = 0;
				m_rtb.ScrollToCaret();
				return;
			}
		}
		m_rtb.Invoke((MethodInvoker)delegate
		{
			WriteLine(str, col, newline);
		});
	}
}
