using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

	/// <summary>Lines kept in the on-screen console; older lines are trimmed from the top.</summary>
	public static int MaxConsoleLines = 5000;

	/// <summary>When false the console keeps its scroll position while new lines arrive.</summary>
	public static bool AutoScroll = true;

	/// <summary>Raised for every written line (any thread). Used by the dashboard error counter.</summary>
	public static event Action<DebugItemType, string> LineWritten;

	private static readonly object m_Lock = new object();

	// FIFO queues keep lines in the order they were written.
	private static ConcurrentQueue<string> logout = new ConcurrentQueue<string>();

	private static ConcurrentQueue<string> msg_towrite = new ConcurrentQueue<string>();

	private struct ConsoleLine
	{
		public string Text;
		public Color Color;
	}

	private static readonly ConcurrentQueue<ConsoleLine> consoleQueue = new ConcurrentQueue<ConsoleLine>();

	private static System.Windows.Forms.Timer consoleTimer;

	public static int VerboseLvl = 0;

	public static bool PacketDebugEnabled = true;

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
		StartCommon(output_to_file);
		StartConsoleTimer(src);
	}

	public static void Initialize(ref RichTextBox src, bool output_to_file = false)
	{
		rtfbox = src;
		StartCommon(output_to_file);
		StartConsoleTimer(src);
	}

	public static void Initialize(bool output_to_file = false)
	{
		StartCommon(output_to_file);
	}

	private static void StartCommon(bool output_to_file)
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

	/// <summary>The console is fed by a UI timer so network threads never wait on the UI.</summary>
	private static void StartConsoleTimer(Control host)
	{
		void create()
		{
			if (consoleTimer != null) return;
			consoleTimer = new System.Windows.Forms.Timer { Interval = 150 };
			consoleTimer.Tick += (s, e) => FlushConsole();
			consoleTimer.Start();
		}

		try
		{
			if (host == null || host.IsDisposed) return;
			if (host.InvokeRequired) host.BeginInvoke((MethodInvoker)create);
			else create();
		}
		catch { }
	}

	public static void Flush()
	{
		try
		{
			string fullPath = LogFilePath;
			EnsureLogDirectory(fullPath);
			using StreamWriter streamWriter = new StreamWriter(fullPath, append: true);
			bool any = false;
			while (msg_towrite.TryDequeue(out string item))
			{
				streamWriter.WriteLine(item);
				any = true;
			}
			if (any) streamWriter.Flush();
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
		try
		{
			consoleTimer?.Stop();
			consoleTimer?.Dispose();
		}
		catch { }
		consoleTimer = null;
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

	/// <summary>Console colour per log type, readable on the dark console background.</summary>
	private static Color ColorFor(DebugItemType type, string msg)
	{
		switch (type)
		{
		case DebugItemType.Error:
			return Color.FromArgb(252, 165, 165); // red-300
		case DebugItemType.ExpectedError:
			return Color.FromArgb(253, 186, 116); // orange-300
		case DebugItemType.Network_Light:
		case DebugItemType.Network_Heavy:
			return Color.FromArgb(134, 239, 172); // green-300
		case DebugItemType.Debug:
			return Color.FromArgb(148, 163, 184); // slate-400
		}
		if (msg != null && (msg.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return Color.FromArgb(252, 165, 165);
		}
		if (msg != null && msg.IndexOf("warn", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return Color.FromArgb(253, 224, 71); // yellow-300
		}
		return Color.FromArgb(226, 232, 240); // slate-200
	}

	public static void Write(string data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		DateTime now = DateTime.Now;
		string msg = data ?? "";
		int verboseReq = new DebugItem { Type = type }.VerboseReq;
		Emit(now, type, msg, FormatWithTimestamp(now, msg), ColorFor(type, msg), newline, verboseReq <= VerboseLvl);
	}

	public static void Write(ExceptionData data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)
	{
		if (data == null || (byte)data.Severity <= 0)
		{
			return;
		}
		DateTime now = DateTime.Now;
		string msg = new DebugItem(data).Msg ?? "";
		Emit(now, DebugItemType.Error, msg, FormatWithTimestamp(now, $"[ERROR] {msg}"), ColorFor(DebugItemType.Error, msg), newline, true);
	}

	private static void Emit(DateTime when, DebugItemType type, string msg, string formatted, Color color, bool newline, bool showInConsole)
	{
		if (showInConsole)
		{
			if ((rtfbox != null && !rtfbox.IsDisposed) || (txtbox != null && !txtbox.IsDisposed))
			{
				consoleQueue.Enqueue(new ConsoleLine { Text = newline ? formatted + "\r\n" : formatted, Color = color });
			}
			else
			{
				logout.Enqueue($"[{when:yyyy-MM-dd HH:mm:ss}] | {type} | {msg}\r\n");
			}
		}

		try { Console.WriteLine(formatted); } catch { }

		if (Outputlog)
		{
			msg_towrite.Enqueue($"[{when:yyyy-MM-dd HH:mm:ss}] | {type} | {msg}");
		}

		try { LineWritten?.Invoke(type, msg); } catch { }
	}

	#region Console flushing (UI thread)

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	private const int WM_SETREDRAW = 0x000B;
	private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
	private const int EM_LINESCROLL = 0x00B6;

	private static void FlushConsole()
	{
		if (consoleQueue.IsEmpty) return;

		// Drain everything queued since the last tick; drop the oldest if a flood exceeds the console size.
		var batch = new List<ConsoleLine>();
		while (consoleQueue.TryDequeue(out ConsoleLine line)) batch.Add(line);
		if (batch.Count > MaxConsoleLines) batch.RemoveRange(0, batch.Count - MaxConsoleLines);

		try
		{
			if (rtfbox != null && !rtfbox.IsDisposed) FlushToRichTextBox(rtfbox, batch);
			else if (txtbox != null && !txtbox.IsDisposed) FlushToTextBox(txtbox, batch);
		}
		catch { }
	}

	private static void FlushToRichTextBox(RichTextBox box, List<ConsoleLine> batch)
	{
		lock (m_Lock)
		{
			IntPtr h = box.Handle;
			int firstVisible = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
			int selStart = box.SelectionStart, selLength = box.SelectionLength;
			bool readOnly = box.ReadOnly;

			SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
			try
			{
				box.ReadOnly = false;

				// Append runs of same-coloured lines in one go.
				int i = 0;
				while (i < batch.Count)
				{
					Color c = batch[i].Color;
					var sb = new StringBuilder();
					while (i < batch.Count && batch[i].Color == c) sb.Append(batch[i++].Text);
					box.SelectionStart = box.TextLength;
					box.SelectionLength = 0;
					box.SelectionColor = c;
					box.SelectedText = sb.ToString();
				}

				// Trim old lines from the top.
				int removedLines = 0, removedChars = 0;
				int lineCount = box.GetLineFromCharIndex(box.TextLength) + 1;
				if (lineCount > MaxConsoleLines + 500)
				{
					removedLines = lineCount - MaxConsoleLines;
					removedChars = box.GetFirstCharIndexFromLine(removedLines);
					if (removedChars > 0)
					{
						box.Select(0, removedChars);
						box.SelectedText = "";
					}
				}

				if (AutoScroll)
				{
					box.SelectionStart = box.TextLength;
					box.SelectionLength = 0;
				}
				else
				{
					// Keep the reader's selection and scroll position.
					int newStart = Math.Max(0, selStart - removedChars);
					box.Select(newStart, Math.Max(0, Math.Min(selLength, box.TextLength - newStart)));
					int target = Math.Max(0, firstVisible - removedLines);
					int now = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
					SendMessage(h, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(target - now));
				}
			}
			finally
			{
				box.ReadOnly = readOnly;
				SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
				box.Invalidate();
			}

			if (AutoScroll) box.ScrollToCaret();
		}
	}

	private static void FlushToTextBox(TextBox box, List<ConsoleLine> batch)
	{
		lock (m_Lock)
		{
			var sb = new StringBuilder();
			foreach (var line in batch) sb.Append(line.Text);
			box.AppendText(sb.ToString());
			if (box.Lines.Length > MaxConsoleLines + 500)
			{
				box.Lines = box.Lines.Skip(box.Lines.Length - MaxConsoleLines).ToArray();
			}
			if (AutoScroll)
			{
				box.SelectionStart = box.TextLength;
				box.ScrollToCaret();
			}
		}
	}

	#endregion

	public static string PullLogItem()
	{
		var sb = new StringBuilder();
		int n = 0;
		while (n < 100 && logout.TryDequeue(out string item))
		{
			sb.Append(item);
			n++;
		}
		return n > 0 ? sb.ToString() : null;
	}

	/// <summary>Keeps at most <see cref="MaxlogfileCnt"/> log files in the log folder (oldest deleted first).</summary>
	private static void PruneOldLogFiles(string logDir)
	{
		try
		{
			var files = new DirectoryInfo(logDir).EnumerateFiles("wlophoenixlogFile*.txt")
				.OrderByDescending(f => f.LastWriteTime)
				.ToList();
			foreach (var old in files.Skip(MaxlogfileCnt))
			{
				try { old.Delete(); } catch { }
			}
		}
		catch { }
	}

	private static void Wrk()
	{
		DateTime lastPrune = DateTime.MinValue;
		do
		{
			try
			{
				if (!msg_towrite.IsEmpty)
				{
					string path = LogFilePath;
					string dir = Path.GetDirectoryName(path);

					if (DateTime.Now - lastPrune > TimeSpan.FromMinutes(10))
					{
						PruneOldLogFiles(dir);
						lastPrune = DateTime.Now;
					}

					// Roll over a full file to name_N.txt
					var info = new FileInfo(path);
					if (info.Exists && info.Length >= logfileMaxsize)
					{
						string baseName = Path.GetFileNameWithoutExtension(path);
						int n = 1;
						string rolled;
						do { rolled = Path.Combine(dir, $"{baseName}_{n++}.txt"); } while (File.Exists(rolled));
						try { File.Move(path, rolled); } catch { }
					}

					using StreamWriter streamWriter = new StreamWriter(path, append: true);
					streamWriter.AutoFlush = false;
					int written = 0;
					while (written < 2000 && msg_towrite.TryDequeue(out string item))
					{
						streamWriter.WriteLine(item);
						written++;
					}
					streamWriter.Flush();
				}
			}
			catch (Exception)
			{
			}
			Thread.Sleep((!Run) ? 1 : 150);
		}
		while (Run || !msg_towrite.IsEmpty);
	}
}
