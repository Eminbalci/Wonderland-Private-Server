using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Phoenix.Core.Exceptions.Message;

namespace Phoenix.Core.Exceptions;

public class ExceptionData
{
	public Exception Exception { get; private set; }

	public string Component { get; private set; }

	public string UserId { get; set; }

	public string Message { get; set; }

	public ExceptionSeverity Severity { get; set; }

	public List<Frame> Exception_Frames { get; private set; }

	public ExceptionData(Exception crap)
	{
	}

	public ExceptionData(ExceptionSeverity severity, Exception exception, [CallerMemberName] string mn = "", [CallerFilePath] string fp = "", [CallerLineNumber] int ln = 0)
	{
		if (exception == null)
		{
			return;
		}
		Exception = exception;
		Component = string.Format("{0} , {1}", fp, mn + " " + ln);
		StackTrace stackTrace = new StackTrace(exception, fNeedFileInfo: true);
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
