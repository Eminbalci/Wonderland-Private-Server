using System.Collections.Generic;
using System.Threading.Tasks;

namespace System.Threading;

public class AsyncSemaphore
{
	private static readonly Task s_completed = Task.FromResult(result: true);

	private readonly Queue<TaskCompletionSource<bool>> m_waiters = new Queue<TaskCompletionSource<bool>>();

	private int m_currentCount;

	public AsyncSemaphore(int initialCount)
	{
		if (initialCount < 0)
		{
			throw new ArgumentOutOfRangeException("initialCount");
		}
		m_currentCount = initialCount;
	}

	public Task WaitAsync()
	{
		lock (m_waiters)
		{
			if (m_currentCount > 0)
			{
				int currentCount = m_currentCount - 1;
				m_currentCount = currentCount;
				return s_completed;
			}
			TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
			m_waiters.Enqueue(taskCompletionSource);
			return taskCompletionSource.Task;
		}
	}

	public void Release()
	{
		TaskCompletionSource<bool> taskCompletionSource = null;
		lock (m_waiters)
		{
			if (m_waiters.Count > 0)
			{
				taskCompletionSource = m_waiters.Dequeue();
			}
			else
			{
				int currentCount = m_currentCount + 1;
				m_currentCount = currentCount;
			}
		}
		taskCompletionSource?.SetResult(result: true);
	}
}
