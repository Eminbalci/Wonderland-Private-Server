namespace System.Threading;

public static class ThreadExt
{
	public static void Init(this Thread src, object parm = null)
	{
		if (parm != null)
		{
			src.Start(parm);
		}
		else
		{
			src.Start();
		}
		ThreadManager.RecordThread(src);
	}

	public static void Kill(this Thread src)
	{
		src.Abort();
		ThreadManager.UnRecordThread(src);
	}
}
