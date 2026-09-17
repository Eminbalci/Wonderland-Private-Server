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

	private static string logfold = AppDomain.CurrentDomain.BaseDirectory;

	private static string logfile;

	private const long logfileMaxsize = 5500000L;

	private const int MaxlogfileCnt = 10;

	private static readonly object m_Lock = new object();

	private static ConcurrentStack<string> logout = new ConcurrentStack<string>();

	private static ConcurrentStack<string> msg_towrite = new ConcurrentStack<string>();

	public static int VerboseLvl = 0;

	public static string LogFilePath
	{
		get
		{
			string folder = logfold;
			if (string.IsNullOrEmpty(folder)) folder = AppDomain.CurrentDomain.BaseDirectory;
			string file = logfile;
			if (string.IsNullOrEmpty(file)) file = Path.Combine("Logs", $"wlophoenixlogFile_{DateTime.Now:yyyyMMdd}.txt");
			string full = Path.GetFullPath(Path.Combine(folder, file));
			EnsureLogDirectory(full);
			return full;
		}
	}

	public static void EnsureLogDirectory(string filePath = null)
	{
		try
		{
			string target = filePath ?? Path.Combine(logfold ?? AppDomain.CurrentDomain.BaseDirectory, logfile ?? "Logs\\server_log.txt");
			string dir = Path.GetDirectoryName(target);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
		}
		catch { }
	}

	public static void Initialize(ref TextBox src, bool output_to_file = false)
	{
		txtbox = src;
		Run = true;
		Outputlog = output_to_file;
		logfold = AppDomain.CurrentDomain.BaseDirectory;
		logfile = Path.Combine("Logs", $"wlophoenixlogFile_{DateTime.Now:yyyyMMdd}.txt");
		VerboseLvl = 0;
		if (Outputlog)
		{
			EnsureLogDirectory();
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
		logfold = AppDomain.CurrentDomain.BaseDirectory;
		logfile = Path.Combine("Logs", $"wlophoenixlogFile_{DateTime.Now:yyyyMMdd}.txt");
		VerboseLvl = 0;
		if (Outputlog)
		{
			EnsureLogDirectory();
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
		logfold = AppDomain.CurrentDomain.BaseDirectory;
		logfile = Path.Combine("Logs", $"wlophoenixlogFile_{DateTime.Now:yyyyMMdd}.txt");
		VerboseLvl = 0;
		if (Outputlog)
		{
			EnsureLogDirectory();
			loggerTask = Task.Factory.StartNew(delegate
			{
				Wrk();
			});
		}
	}

	public static void Flush()
	{
		try
		{
			string fullPath = LogFilePath;
			EnsureLogDirectory(fullPath);
			using StreamWriter streamWriter = new StreamWriter(fullPath, append: true);
			string[] array = new string[msg_towrite.Count + 10];
			int count = msg_towrite.TryPopRange(array);
			if (count > 0)
			{
				foreach (string item in array.Where(c => c != null))
				{
					streamWriter.WriteLine(item);
				}
				streamWriter.Flush();
			}
		}
		catch { }
	}

	public static void EndIntialize()
	{
		Run = false;
		Outputlog = false;
		if (loggerTask != null)
		{
			try { loggerTask.Wait(3000); } catch { }
		}
		Flush();
		rtfbox = null;
		txtbox = null;
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

	private static string FormatWithTimestamp(DateTime when, string message)
	{
		string timestamp = $"[{when:yyyy-MM-dd HH:mm:ss}]";
		if (string.IsNullOrEmpty(message))
		{
			return timestamp;
		}

		// Prevent duplicate timestamp if already prefixed with [YYYY-MM-DD HH:mm:ss]
		if (message.Length >= 21 && message.StartsWith("[") && message[11] == ' ' && message[20] == ']')
		{
			return message;
		}

		if (message.Contains("\n"))
		{
			string[] lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				if (i > 0) sb.Append("\r\n");
				string line = lines[i];
				if (!string.IsNullOrEmpty(line))
				{
					if (line.Length >= 21 && line.StartsWith("[") && line[11] == ' ' && line[20] == ']')
					{
						sb.Append(line);
					}
					else
					{
						sb.Append(timestamp).Append(" ").Append(line);
					}
				}
			}
			return sb.ToString();
		}

		return $"{timestamp} {message}";
	}

	public static void Write(string data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		DateTime now = DateTime.Now;
		DebugItem debugItem = new DebugItem("Write", "DebugSystem.cs", 159);
		debugItem.Type = type;
		debugItem.When = now;
		debugItem.Msg = data != null ? data.ToString() : "";
		string formattedMsg = FormatWithTimestamp(debugItem.When, debugItem.Msg);

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
						rtfbox.AppendText(formattedMsg);
						if (newline) rtfbox.AppendText("\r\n");
						rtfbox.SelectionColor = rtfbox.ForeColor;
						rtfbox.SelectionStart = rtfbox.TextLength;
						rtfbox.SelectionLength = 0;
						rtfbox.ScrollToCaret();
					}
				}
				else
				{
					rtfbox.BeginInvoke((MethodInvoker)delegate
					{
						if (rtfbox != null && !rtfbox.IsDisposed)
						{
							lock (m_Lock)
							{
								rtfbox.SelectionStart = rtfbox.TextLength;
								rtfbox.SelectionLength = 0;
								rtfbox.SelectionColor = debugItem.Col;
								rtfbox.AppendText(formattedMsg);
								if (newline) rtfbox.AppendText("\r\n");
								rtfbox.SelectionColor = rtfbox.ForeColor;
								rtfbox.SelectionStart = rtfbox.TextLength;
								rtfbox.SelectionLength = 0;
								rtfbox.ScrollToCaret();
							}
						}
					});
				}
			}
			else if (txtbox != null && !txtbox.IsDisposed)
			{
				if (!txtbox.InvokeRequired)
				{
					lock (m_Lock)
					{
						txtbox.SelectionStart = txtbox.TextLength;
						txtbox.SelectionLength = 0;
						txtbox.AppendText(formattedMsg);
						if (newline) txtbox.AppendText("\r\n");
						txtbox.SelectionStart = txtbox.TextLength;
						txtbox.SelectionLength = 0;
						txtbox.ScrollToCaret();
					}
				}
				else
				{
					txtbox.BeginInvoke((MethodInvoker)delegate
					{
						if (txtbox != null && !txtbox.IsDisposed)
						{
							lock (m_Lock)
							{
								txtbox.SelectionStart = txtbox.TextLength;
								txtbox.SelectionLength = 0;
								txtbox.AppendText(formattedMsg);
								if (newline) txtbox.AppendText("\r\n");
								txtbox.SelectionStart = txtbox.TextLength;
								txtbox.SelectionLength = 0;
								txtbox.ScrollToCaret();
							}
						}
					});
				}
			}
			else
			{
				logout.Push($"[{debugItem.When:yyyy-MM-dd HH:mm:ss}] | {debugItem.Type} | {debugItem.Msg}\r\n");
			}
		}

		if (rtfbox == null && txtbox == null)
		{
			try { Console.WriteLine(formattedMsg); } catch { }
		}

		if (Outputlog)
		{
			msg_towrite.Push($"[{debugItem.When:yyyy-MM-dd HH:mm:ss}] | {debugItem.Type} | {debugItem.Msg}\r\n");
		}
	}

	public static void Write(ExceptionData data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		DateTime now = DateTime.Now;
		DebugItem debugItem = new DebugItem("Write", "DebugSystem.cs", 213);
		debugItem.Type = type;
		debugItem.When = now;
		debugItem.Col = Color.Red;
		debugItem.Error = data;
		if ((byte)data.Severity <= 0)
		{
			return;
		}

		string formattedMsg = FormatWithTimestamp(debugItem.When, $"[ERROR] {debugItem.Msg}");

		if (rtfbox != null && !rtfbox.IsDisposed)
		{
			if (!rtfbox.InvokeRequired)
			{
				lock (m_Lock)
				{
					rtfbox.SelectionStart = rtfbox.TextLength;
					rtfbox.SelectionLength = 0;
					rtfbox.SelectionColor = debugItem.Col;
					rtfbox.AppendText(formattedMsg);
					if (newline) rtfbox.AppendText("\r\n");
					rtfbox.SelectionColor = rtfbox.ForeColor;
					rtfbox.SelectionStart = rtfbox.TextLength;
					rtfbox.SelectionLength = 0;
					rtfbox.ScrollToCaret();
				}
			}
			else
			{
				rtfbox.BeginInvoke((MethodInvoker)delegate
				{
					if (rtfbox != null && !rtfbox.IsDisposed)
					{
						lock (m_Lock)
						{
							rtfbox.SelectionStart = rtfbox.TextLength;
							rtfbox.SelectionLength = 0;
							rtfbox.SelectionColor = debugItem.Col;
							rtfbox.AppendText(formattedMsg);
							if (newline) rtfbox.AppendText("\r\n");
							rtfbox.SelectionColor = rtfbox.ForeColor;
							rtfbox.SelectionStart = rtfbox.TextLength;
							rtfbox.SelectionLength = 0;
							rtfbox.ScrollToCaret();
						}
					}
				});
			}
		}
		else if (txtbox != null && !txtbox.IsDisposed)
		{
			if (!txtbox.InvokeRequired)
			{
				lock (m_Lock)
				{
					txtbox.SelectionStart = txtbox.TextLength;
					txtbox.SelectionLength = 0;
					txtbox.AppendText(formattedMsg);
					if (newline) txtbox.AppendText("\r\n");
					txtbox.SelectionStart = txtbox.TextLength;
					txtbox.SelectionLength = 0;
					txtbox.ScrollToCaret();
				}
			}
			else
			{
				txtbox.BeginInvoke((MethodInvoker)delegate
				{
					if (txtbox != null && !txtbox.IsDisposed)
					{
						lock (m_Lock)
						{
							txtbox.SelectionStart = txtbox.TextLength;
							txtbox.SelectionLength = 0;
							txtbox.AppendText(formattedMsg);
							if (newline) txtbox.AppendText("\r\n");
							txtbox.SelectionStart = txtbox.TextLength;
							txtbox.SelectionLength = 0;
							txtbox.ScrollToCaret();
						}
					}
				});
			}
		}
		else
		{
			logout.Push($"[{debugItem.When:yyyy-MM-dd HH:mm:ss}] | {debugItem.Type} | {debugItem.Msg}\r\n");
		}

		if (rtfbox == null && txtbox == null)
		{
			try { Console.WriteLine(formattedMsg); } catch { }
		}

		if (Outputlog)
		{
			msg_towrite.Push($"[{debugItem.When:yyyy-MM-dd HH:mm:ss}] | {debugItem.Type} | {debugItem.Msg}\r\n");
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
					using StreamWriter streamWriter = new StreamWriter(LogFilePath, append: true);
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
