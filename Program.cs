using System;
using System.Windows.Forms;
using PcbPoseAlignInspect.Forms;

namespace PcbPoseAlignInspect
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			using (SetupForm setupForm = new SetupForm())
			{
				Application.Run(setupForm);
			}
		}
	}
}
