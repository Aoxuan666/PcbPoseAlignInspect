using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PcbPoseAlignInspect.Forms;

namespace PcbPoseAlignInspect
{
	internal static class Program
	{
		private static int _showingException;

		[STAThread]
		private static void Main()
		{
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += ApplicationOnThreadException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				using (SetupForm setupForm = new SetupForm())
				{
					Application.Run(setupForm);
				}
			}
			catch (Exception ex)
			{
				ShowException("程序启动或运行异常", ex);
			}
		}

		private static void ApplicationOnThreadException(object sender, ThreadExceptionEventArgs e)
		{
			ShowException("界面操作异常", e.Exception);
		}

		private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception exception = e.ExceptionObject as Exception;
			ShowException(e.IsTerminating ? "程序发生严重异常，即将退出" : "程序发生未处理异常", exception);
		}

		private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			e.SetObserved();
			ShowException("后台任务异常", e.Exception.GetBaseException());
		}

		private static void ShowException(string title, Exception exception)
		{
			if (Interlocked.Exchange(ref _showingException, 1) == 1)
			{
				return;
			}

			try
			{
				string message = exception == null ? "发生未知异常。" : exception.Message;
				if (exception != null && !string.IsNullOrWhiteSpace(exception.StackTrace))
				{
					message += "\r\n\r\n详细信息:\r\n" + exception.StackTrace;
				}
				MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				Interlocked.Exchange(ref _showingException, 0);
			}
		}
	}
}
