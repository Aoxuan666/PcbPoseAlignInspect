using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PcbPoseAlignInspect.Models
{
	[Serializable]
	public class PcbPoseInspectRecipe
	{
		public string RecipeName { get; set; }

		public RectangleF BoardSearchRoi { get; set; }

		public bool EnableBoardSearchRoi { get; set; }

		public int BoardHueMin { get; set; }

		public int BoardHueMax { get; set; }

		public int BoardSatMin { get; set; }

		public int BoardSatMax { get; set; }

		public int BoardValMin { get; set; }

		public int BoardValMax { get; set; }

		public double BoardOpenRadius { get; set; }

		public double BoardCloseRadius { get; set; }

		public bool BoardFillUp { get; set; }

		public int BoardMinArea { get; set; }

		public double UnifiedTolerancePx { get; set; }

		public bool EnableFeatureTemplateMatch { get; set; }

		public int BoardGreenRedDiffMin { get; set; }

		public int BoardGreenBlueDiffMin { get; set; }

		public int BoardRedMax { get; set; }

		public bool BoardUseConvexHull { get; set; }

		public RectangleF FeatureSearchRoi { get; set; }

		public RectangleF FeatureTemplateRoi { get; set; }

		public FeatureRoiShape FeatureRoiShape { get; set; }

		public double FeatureMatchMinScore { get; set; }

		public PointF TeachFeatureCenter { get; set; }

		public PointF FeatureToBoardOffset { get; set; }

		public byte[] FeatureTemplateImagePng { get; set; }

		public byte[] FeatureTemplateMaskPng { get; set; }

		public PointF TeachBoardCenter { get; set; }

		public double TeachBoardAngleDeg { get; set; }

		public double AngleRadiusPx { get; set; }

		public bool IsTaught { get; set; }

		public PcbPoseInspectRecipe()
		{
			RecipeName = "Default";
			BoardSearchRoi = RectangleF.Empty;
			EnableBoardSearchRoi = false;
			BoardHueMin = 50;
			BoardHueMax = 122;
			BoardSatMin = 24;
			BoardSatMax = 147;
			BoardValMin = 20;
			BoardValMax = 245;
			BoardOpenRadius = 5.3;
			BoardCloseRadius = 15.9;
			BoardFillUp = true;
			BoardMinArea = 20000;
			UnifiedTolerancePx = 6.0;
			EnableFeatureTemplateMatch = false;
			FeatureSearchRoi = RectangleF.Empty;
			FeatureTemplateRoi = RectangleF.Empty;
			FeatureRoiShape = FeatureRoiShape.Rectangle;
			FeatureMatchMinScore = 0.45;
			TeachFeatureCenter = PointF.Empty;
			FeatureToBoardOffset = PointF.Empty;
			FeatureTemplateImagePng = null;
			FeatureTemplateMaskPng = null;
			TeachBoardCenter = PointF.Empty;
			TeachBoardAngleDeg = 0.0;
			AngleRadiusPx = 80.0;
			IsTaught = false;
			BoardGreenRedDiffMin = 2;
			BoardGreenBlueDiffMin = 6;
			BoardRedMax = 245;
			BoardUseConvexHull = true;
		}

		public PcbPoseInspectRecipe Clone()
		{
			return new PcbPoseInspectRecipe
			{
				RecipeName = RecipeName,
				BoardSearchRoi = BoardSearchRoi,
				EnableBoardSearchRoi = EnableBoardSearchRoi,
				BoardHueMin = BoardHueMin,
				BoardHueMax = BoardHueMax,
				BoardSatMin = BoardSatMin,
				BoardSatMax = BoardSatMax,
				BoardValMin = BoardValMin,
				BoardValMax = BoardValMax,
				BoardGreenRedDiffMin = BoardGreenRedDiffMin,
				BoardGreenBlueDiffMin = BoardGreenBlueDiffMin,
				BoardRedMax = BoardRedMax,
				BoardUseConvexHull = BoardUseConvexHull,
				BoardOpenRadius = BoardOpenRadius,
				BoardCloseRadius = BoardCloseRadius,
				BoardFillUp = BoardFillUp,
				BoardMinArea = BoardMinArea,
				UnifiedTolerancePx = UnifiedTolerancePx,
				EnableFeatureTemplateMatch = EnableFeatureTemplateMatch,
				FeatureSearchRoi = FeatureSearchRoi,
				FeatureTemplateRoi = FeatureTemplateRoi,
				FeatureRoiShape = FeatureRoiShape,
				FeatureMatchMinScore = FeatureMatchMinScore,
				TeachFeatureCenter = TeachFeatureCenter,
				FeatureToBoardOffset = FeatureToBoardOffset,
				FeatureTemplateImagePng = CloneBytes(FeatureTemplateImagePng),
				FeatureTemplateMaskPng = CloneBytes(FeatureTemplateMaskPng),
				TeachBoardCenter = TeachBoardCenter,
				TeachBoardAngleDeg = TeachBoardAngleDeg,
				AngleRadiusPx = AngleRadiusPx,
				IsTaught = IsTaught
			};
		}

		public bool HasFeatureTemplate()
		{
			return FeatureTemplateImagePng != null && FeatureTemplateImagePng.Length != 0;
		}

		public Bitmap CreateFeatureTemplateImage()
		{
			return CreateBitmap(FeatureTemplateImagePng);
		}

		public Bitmap CreateFeatureTemplateMask()
		{
			return CreateBitmap(FeatureTemplateMaskPng);
		}

		public void SetFeatureTemplate(Bitmap image, Bitmap mask, RectangleF templateRoi, FeatureRoiShape roiShape, PointF boardCenter)
		{
			FeatureTemplateRoi = templateRoi;
			FeatureRoiShape = roiShape;
			FeatureTemplateImagePng = ToPngBytes(image);
			FeatureTemplateMaskPng = ToPngBytes(mask);
			TeachFeatureCenter = new PointF(templateRoi.Left + templateRoi.Width / 2f, templateRoi.Top + templateRoi.Height / 2f);
			FeatureToBoardOffset = new PointF(boardCenter.X - TeachFeatureCenter.X, boardCenter.Y - TeachFeatureCenter.Y);
			TeachBoardCenter = boardCenter;
		}

		private static byte[] CloneBytes(byte[] bytes)
		{
			if (bytes == null)
			{
				return null;
			}
			byte[] array = new byte[bytes.Length];
			Array.Copy(bytes, array, bytes.Length);
			return array;
		}

		private static Bitmap CreateBitmap(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return null;
			}
			using (MemoryStream stream = new MemoryStream(bytes))
			{
				using (Bitmap original = new Bitmap(stream))
				{
					return new Bitmap(original);
				}
			}
		}

		private static byte[] ToPngBytes(Bitmap bitmap)
		{
			if (bitmap == null)
			{
				return null;
			}
			using (MemoryStream memoryStream = new MemoryStream())
			{
				bitmap.Save(memoryStream, ImageFormat.Png);
				return memoryStream.ToArray();
			}
		}
	}
}
