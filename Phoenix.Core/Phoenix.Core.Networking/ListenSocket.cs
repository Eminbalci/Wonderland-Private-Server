using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Phoenix.Core.Networking;

public abstract class ListenSocket
{
	protected Socket m_Socket;

	protected bool m_bKeepAlive = false;

	protected Task m_ThreadHandle = null;

	protected readonly object m_Lock = new object();

	protected CancellationTokenSource cancelthrd = new CancellationTokenSource();

	public void Initialize()
	{
		lock (m_Lock)
		{
			m_ThreadHandle = Task.Factory.StartNew(ListenThread, cancelthrd.Token);
			m_bKeepAlive = true;
		}
	}

	public abstract void ListenThread();

	public bool IsRunning()
	{
		return !m_bKeepAlive || m_ThreadHandle == null || m_ThreadHandle.Status == TaskStatus.Running;
	}

	public void Kill()
	{
		lock (m_Lock)
		{
			if (m_bKeepAlive)
			{
				m_bKeepAlive = false;
			}
		}
		m_ThreadHandle.Wait(1000);
		if (m_ThreadHandle.Status == TaskStatus.Running)
		{
			cancelthrd.Cancel();
			m_ThreadHandle.Dispose();
		}
	}
}
