using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace System.Threading;

public static class ThreadManager
{
	private static Dictionary<int, Thread> Threadlist = new Dictionary<int, Thread>();

	public static int Count()
	{
		return Threadlist.Count;
	}

	public static int RunningThreads()
	{
		return Threadlist.Count((KeyValuePair<int, Thread> c) => c.Value.IsAlive);
	}

	public static void RecordThread(Thread src)
	{
		Threadlist.Add(src.ManagedThreadId, src);
	}

	public static void UnRecordThread(Thread src)
	{
		Threadlist.Remove(src.ManagedThreadId);
	}

	public static void KillALL()
	{
		Parallel.For(0, Threadlist.Count, delegate(int c)
		{
			Threadlist.ToList()[c].Value.Abort();
		});
		Threadlist.Clear();
	}
}
