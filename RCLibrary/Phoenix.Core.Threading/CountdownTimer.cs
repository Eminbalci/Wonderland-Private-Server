using System;
using System.Windows.Forms;

namespace Phoenix.Core.Threading;

internal class CountdownTimer
{
	private Timer timer;

	private TimeSpan starttime;

	~CountdownTimer()
	{
	}

	private void Start()
	{
		timer = new Timer();
		timer.Tick += timer_Tick;
	}

	private void Stop()
	{
		timer.Stop();
		timer.Dispose();
	}

	private void timer_Tick(object sender, EventArgs e)
	{
		throw new NotImplementedException();
	}
}
