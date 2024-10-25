using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace System;

public static class DebugSystem
{
	private static RichTextBox rtfbox;

	private static TextBox txtbox;

	private static bool Run;

	private static bool Outputlog;

	private static Task loggerTask;

	private static string logfold = Environment.CurrentDirectory;

	private static string logfile;

	private const long logfileMaxsize = 5500000L;

	private const int MaxlogfileCnt = 10;

	private static readonly object m_Lock = new object();

	private static ConcurrentStack<string> logout = new ConcurrentStack<string>();

	private static ConcurrentStack<string> msg_towrite = new ConcurrentStack<string>();

	public static int VerboseLvl = 0;

	public static void Initialize(ref TextBox src, bool output_to_file = false)
	{
		txtbox = src;
		Run = true;
		Outputlog = output_to_file;
		logfile = "Logs\\wlophoenixlogFile" + DateTime.Now.ToShortDateString().Replace("/", "") + ".txt";
		VerboseLvl = 0;
		if (Outputlog)
		{
			loggerTask = Task.Factory.StartNew(delegate
			{
				Wrk();
			});
		}
	}

	public static void Initialize(ref RichTextBox src, bool output_to_file = false)
	{
		rtfbox = src;
		Run = true;
		Outputlog = output_to_file;
		logfile = "Logs\\wlophoenixlogFile" + DateTime.Now.ToShortDateString().Replace("/", "") + ".txt";
		VerboseLvl = 0;
		if (Outputlog)
		{
			loggerTask = Task.Factory.StartNew(delegate
			{
				Wrk();
			});
		}
	}

	public static void Initialize(bool output_to_file = false)
	{
		Run = true;
		Outputlog = output_to_file;
		logfile = "wlophoenixlogFile" + DateTime.Now.ToShortDateString().Replace("/", "") + ".txt";
		VerboseLvl = 0;
		if (Outputlog)
		{
			loggerTask = Task.Factory.StartNew(delegate
			{
				Wrk();
			});
		}
	}

	public static void EndIntialize()
	{
		Run = false;
		Outputlog = false;
		rtfbox = null;
		txtbox = null;
		if (loggerTask != null)
		{
			loggerTask.Wait(7000);
		}
	}

	public static void OpenExtLog()
	{
	}

	public static void Write(string data)
	{
		Write(data, DebugItemType.DataBase_Light);
	}

	public static void Write(DebugItemType type, string data, params object[] parm)
	{
		string data2 = data.ToString();
		if (parm.Count() > 0)
		{
			data2 = string.Format(data.ToString(), parm);
		}
		Write(data2, type);
	}

	public static void Write(ExceptionData data)
	{
		Write(data, DebugItemType.Error);
	}

	public static void Write(string data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		DateTime now = DateTime.Now;
		DebugItem debugItem = new DebugItem("Write", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Debug\\DebugSystem.cs", 159);
		debugItem.Type = type;
		debugItem.When = DateTime.Now;
		debugItem.Msg = data.ToString();
		if (debugItem.VerboseReq <= VerboseLvl)
		{
			if (rtfbox != null && !rtfbox.IsDisposed)
			{
				if (!rtfbox.InvokeRequired)
				{
					lock (m_Lock)
					{
						rtfbox.SelectionStart = rtfbox.TextLength;
						rtfbox.SelectionLength = 0;
						rtfbox.SelectionColor = debugItem.Col;
						rtfbox.AppendText(debugItem.Msg);
						rtfbox.AppendText("\r\n");
						rtfbox.SelectionColor = rtfbox.ForeColor;
						rtfbox.SelectionStart = rtfbox.TextLength;
						rtfbox.SelectionLength = 0;
						rtfbox.ScrollToCaret();
					}
				}
				else
				{
					rtfbox.Invoke((MethodInvoker)delegate
					{
						Write(data, type, newline);
					});
				}
			}
			else if (txtbox != null && !txtbox.IsDisposed)
			{
				if (!txtbox.InvokeRequired)
				{
					lock (m_Lock)
					{
						txtbox.SelectionStart = rtfbox.TextLength;
						txtbox.SelectionLength = 0;
						txtbox.AppendText(debugItem.Msg);
						rtfbox.AppendText("\r\n");
						txtbox.SelectionStart = txtbox.TextLength;
						txtbox.SelectionLength = 0;
						txtbox.ScrollToCaret();
					}
				}
				else
				{
					txtbox.Invoke((MethodInvoker)delegate
					{
						Write(data, type, newline);
					});
				}
			}
			else
			{
				logout.Push(string.Concat(debugItem.When, " | ", debugItem.Type, " | ", debugItem.Msg, "\r\n"));
			}
		}
		if (Outputlog)
		{
			msg_towrite.Push(string.Concat(debugItem.When, " | ", debugItem.Type, " | ", debugItem.Msg, "\r\n"));
		}
	}

	public static void Write(ExceptionData data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		DateTime now = DateTime.Now;
		DebugItem debugItem = new DebugItem("Write", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Debug\\DebugSystem.cs", 213);
		debugItem.Type = type;
		debugItem.When = DateTime.Now;
		debugItem.Col = Color.Red;
		debugItem.Error = data;
		if ((byte)data.Severity <= 0)
		{
			return;
		}
		if (rtfbox != null && !rtfbox.IsDisposed)
		{
			if (!rtfbox.InvokeRequired)
			{
				lock (m_Lock)
				{
					rtfbox.SelectionStart = rtfbox.TextLength;
					rtfbox.SelectionLength = 0;
					rtfbox.SelectionColor = debugItem.Col;
					rtfbox.AppendText(debugItem.Msg);
					rtfbox.AppendText("\r\n");
					rtfbox.SelectionColor = rtfbox.ForeColor;
					rtfbox.SelectionStart = rtfbox.TextLength;
					rtfbox.SelectionLength = 0;
					rtfbox.ScrollToCaret();
				}
			}
			else
			{
				rtfbox.Invoke((MethodInvoker)delegate
				{
					Write(data, type, newline);
				});
			}
		}
		else if (txtbox != null && !txtbox.IsDisposed)
		{
			if (!txtbox.InvokeRequired)
			{
				lock (m_Lock)
				{
					txtbox.SelectionStart = rtfbox.TextLength;
					txtbox.SelectionLength = 0;
					txtbox.AppendText(debugItem.Msg);
					rtfbox.AppendText("\r\n");
					txtbox.SelectionStart = txtbox.TextLength;
					txtbox.SelectionLength = 0;
					txtbox.ScrollToCaret();
				}
			}
			else
			{
				txtbox.Invoke((MethodInvoker)delegate
				{
					Write(data, type, newline);
				});
			}
		}
		else
		{
			logout.Push(string.Concat(debugItem.When, " | ", debugItem.Type, " | ", debugItem.Msg, "\r\n"));
		}
		if (Outputlog)
		{
			msg_towrite.Push(string.Concat(debugItem.When, " | ", debugItem.Type, " | ", debugItem.Msg, "\r\n"));
		}
	}

	public static string PullLogItem()
	{
		string[] array = new string[100];
		if (logout.TryPopRange(array, 0, 100) > 0)
		{
			if (array.Count((string c) => c != null) < 6)
			{
				return string.Concat(array.Where((string c) => c != null));
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (string item in array.Where((string c) => c != null))
			{
				stringBuilder.Append(item);
			}
			return stringBuilder.ToString();
		}
		return null;
	}

	private static void Wrk()
	{
		do
		{
			try
			{
				string[] array = new string[250];
				if (msg_towrite.TryPopRange(array) > 0)
				{
					StringBuilder stringBuilder = new StringBuilder();
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
						if (fileInfo.Length >= 5500000)
						{
							try
							{
								File.Copy(fileInfo.FullName, Path.Combine(logfold, logfile.Replace(".", num2 + ".")));
								File.Delete(fileInfo.FullName);
							}
							catch
							{
								num2++;
								continue;
							}
							fileInfo = null;
						}
						break;
					}
					using StreamWriter streamWriter = new StreamWriter(Path.Combine(logfold, logfile), append: true);
					streamWriter.AutoFlush = false;
					foreach (string item in array.Where((string c) => c != null))
					{
						streamWriter.WriteLine(item);
					}
					streamWriter.Flush();
				}
			}
			catch (Exception)
			{
			}
			Thread.Sleep((!Run) ? 1 : 150);
		}
		while (Run || msg_towrite.Count > 0);
	}
}
