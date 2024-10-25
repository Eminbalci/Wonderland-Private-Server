using System.Net.Sockets;
using System.Threading;

namespace RCLibrary.Core.Networking;

public abstract class TcpServer
{
	protected Socket m_Socket;

	protected bool m_bKeepAlive = false;

	protected Thread m_ThreadHandle = null;

	protected readonly object m_Lock = new object();

	public void Initialize()
	{
		lock (m_Lock)
		{
			m_ThreadHandle = new Thread(ListenThread);
			m_ThreadHandle.Start();
			m_bKeepAlive = true;
		}
	}

	public abstract void ListenThread();

	public bool IsRunning()
	{
		return !m_bKeepAlive || m_ThreadHandle == null || m_ThreadHandle.IsAlive;
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
		try
		{
			m_Socket.Close();
			m_ThreadHandle.Join(3000);
			m_ThreadHandle.Abort();
		}
		catch
		{
		}
	}
}
