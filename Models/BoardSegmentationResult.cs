using System;
using System.Drawing;

namespace PcbPoseAlignInspect.Models
{
	[Serializable]
	public sealed class BoardSegmentationResult
	{
		public bool Success { get; set; }

		public string Message { get; set; }

		public PointF[] Contour { get; set; }

		public int CandidateCount { get; set; }

		public double SelectedArea { get; set; }

		public PointF Center { get; set; }

		public double AngleDeg { get; set; }

		public RectangleF BoundingBox { get; set; }

		public double ElapsedMs { get; set; }

		public int ContourPointCount
		{
			get
			{
				return Contour == null ? 0 : Contour.Length;
			}
		}

		public BoardSegmentationResult()
		{
			Message = string.Empty;
			Contour = new PointF[0];
			BoundingBox = RectangleF.Empty;
		}

		public static BoardSegmentationResult Invalid(string message)
		{
			return new BoardSegmentationResult
			{
				Success = false,
				Message = message ?? string.Empty
			};
		}
	}
}
