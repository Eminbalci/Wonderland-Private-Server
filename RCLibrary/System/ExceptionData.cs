using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Phoenix.Core.Exceptions.Message;

namespace System;

public class ExceptionData : Exception
{
	private ExceptionSeverity? _severity;

	public Exception Exception { get; private set; }

	public string Component { get; private set; }

	public string UserId { get; set; }

	public new string Message => $"Error: \"{Exception.Message}\" \r\n By \"{UserId}\"\r\n At [ {Component} ]\r\n";

	public ExceptionSeverity Severity
	{
		get
		{
			if (Exception.Message.StartsWith("No connection could be made because the target machine actively refused it") || Message.StartsWith("ObjectDisposedException") || Message.StartsWith("Cannot access a disposed object"))
			{
				return ExceptionSeverity.ExpectedError;
			}
			return _severity.HasValue ? _severity.Value : ExceptionSeverity.Fatal;
		}
	}

	public List<Frame> Exception_Frames { get; private set; }

	public ExceptionData(ExceptionSeverity severity, string msg, [CallerMemberName] string _mn = "", [CallerFilePath] string _fp = "", [CallerLineNumber] int _ln = 0)
	{
		_severity = severity;
		Exception = new Exception(msg);
		Component = $"<File> {_fp} \r\n <Method> {_mn}\r\n <Line> {_ln}";
		StackTrace stackTrace = new StackTrace(Exception, fNeedFileInfo: true);
		StackFrame[] frames = stackTrace.GetFrames();
		if (frames == null)
		{
			return;
		}
		Exception_Frames = new List<Frame>();
		for (int i = 0; i < frames.Length; i++)
		{
			StackFrame stackFrame = frames[i];
			MethodBase method = stackFrame.GetMethod();
			Type declaringType = method.DeclaringType;
			string fileName = stackFrame.GetFileName();
			Frame frame = new Frame
			{
				i = i,
				fn = fileName,
				ln = stackFrame.GetFileLineNumber(),
				m = method.ToString()
			};
			frame.m = frame.m.Substring(frame.m.IndexOf(' ')).Trim();
			if (declaringType != null)
			{
				frame.c = declaringType.FullName;
			}
			Exception_Frames.Add(frame);
		}
	}

	public ExceptionData(Exception ex, ExceptionSeverity severitylvl = ExceptionSeverity.Error, [CallerMemberName] string _mn = "", [CallerFilePath] string _fp = "", [CallerLineNumber] int _ln = 0)
	{
		if (ex == null)
		{
			return;
		}
		_severity = severitylvl;
		Exception = ex;
		Component = $"<File> {_fp} \r\n <Method> {_mn}\r\n <Line> {_ln}";
		StackTrace stackTrace = new StackTrace(ex, fNeedFileInfo: true);
		StackFrame[] frames = stackTrace.GetFrames();
		if (frames == null)
		{
			return;
		}
		Exception_Frames = new List<Frame>();
		for (int i = 0; i < frames.Length; i++)
		{
			StackFrame stackFrame = frames[i];
			MethodBase method = stackFrame.GetMethod();
			Type declaringType = method.DeclaringType;
			string fileName = stackFrame.GetFileName();
			Frame frame = new Frame
			{
				i = i,
				fn = fileName,
				ln = stackFrame.GetFileLineNumber(),
				m = method.ToString()
			};
			frame.m = frame.m.Substring(frame.m.IndexOf(' ')).Trim();
			if (declaringType != null)
			{
				frame.c = declaringType.FullName;
			}
			Exception_Frames.Add(frame);
		}
	}
}
