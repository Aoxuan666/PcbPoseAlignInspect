using System;
using System.Drawing;
using System.Windows.Forms;
using PcbPoseAlignInspect.Forms;
using PcbPoseAlignInspect.Models;
using PcbPoseAlignInspect.Processing;

namespace PcbPoseAlignInspect.Sdk
{
	public sealed class PcbPoseInspectSdk
	{
		private readonly PcbPoseInspectProcessor _processor = new PcbPoseInspectProcessor();

		public PcbPoseInspectRecipe OpenSetupDialog(Bitmap image, PcbPoseInspectRecipe currentRecipe)
		{
			using (SetupForm setupForm = new SetupForm())
			{
				setupForm.LoadRecipe((currentRecipe == null) ? new PcbPoseInspectRecipe() : currentRecipe.Clone());
				setupForm.SetStartupImage(image);
				return (setupForm.ShowDialog() == DialogResult.OK) ? setupForm.CurrentRecipe.Clone() : currentRecipe;
			}
		}

		public PcbPoseInspectResult RunInspection(Bitmap image, PcbPoseInspectRecipe recipe, double tolerancePx)
		{
			try
			{
				if (image == null)
				{
					return PcbPoseInspectResult.Invalid("输入图像为空", InspectNgReason.ParameterInvalid);
				}
				using (Bitmap image2 = new Bitmap(image))
				{
					return _processor.Inspect(image2, recipe, tolerancePx);
				}
			}
			catch (Exception ex)
			{
				return PcbPoseInspectResult.Invalid("SDK异常: " + ex.Message, InspectNgReason.AlgorithmException);
			}
		}
	}
}
