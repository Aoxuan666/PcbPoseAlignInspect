using System;
using System.Drawing;

namespace PcbPoseAlignInspect.Models
{
	[Serializable]
	public class PcbPoseInspectResult
	{
		public bool Success { get; set; }

		public string Message { get; set; }

		public InspectNgReason NgReasons { get; set; }

		public PointF RuntimeBoardCenter { get; set; }

		public PointF TeachBoardCenter { get; set; }

		public double RuntimeBoardAngleDeg { get; set; }

		public double TeachBoardAngleDeg { get; set; }

		public RectangleF RuntimeBoardBoundingBox { get; set; }

		public double DxPx { get; set; }

		public double DyPx { get; set; }

		public double AngleDeltaDeg { get; set; }

		public double AngleEquivalentPx { get; set; }

		public double ScorePx { get; set; }

		public double UsedTolerancePx { get; set; }

		public bool BoardDetectOk { get; set; }

		public bool FeatureMatchOk { get; set; }

		public double FeatureMatchScore { get; set; }

		public PointF RuntimeFeatureCenter { get; set; }

		public PointF TeachFeatureCenter { get; set; }

		public RectangleF RuntimeFeatureBounds { get; set; }

		public RectangleF FeatureSearchRoi { get; set; }

		public RectangleF FeatureTemplateRoi { get; set; }

		public FeatureRoiShape FeatureRoiShape { get; set; }

		public double ElapsedMs { get; set; }

		public string NgReasonText
		{
			get
			{
				if (NgReasons == InspectNgReason.None)
				{
					return "NONE";
				}
				return NgReasons.ToString();
			}
		}

		public PcbPoseInspectResult()
		{
			Success = false;
			Message = string.Empty;
			NgReasons = InspectNgReason.None;
			RuntimeBoardBoundingBox = RectangleF.Empty;
		}

		public static PcbPoseInspectResult Invalid(string message, InspectNgReason reason)
		{
			return new PcbPoseInspectResult
			{
				Success = false,
				Message = (message ?? string.Empty),
				NgReasons = reason
			};
		}
	}
}
