using System;
using System.Drawing;

namespace PcbPoseAlignInspect.Models
{
	[Serializable]
	public sealed class FeatureTemplatePreview
	{
		public PointF[] ActivePoints { get; set; }

		public string Message { get; set; }

		public FeatureTemplatePreview()
		{
			ActivePoints = new PointF[0];
			Message = string.Empty;
		}

		public FeatureTemplatePreview Clone()
		{
			return new FeatureTemplatePreview
			{
				ActivePoints = ActivePoints == null ? new PointF[0] : (PointF[])ActivePoints.Clone(),
				Message = Message ?? string.Empty
			};
		}
	}
}
