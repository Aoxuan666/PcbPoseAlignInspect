using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using HalconDotNet;
using PcbPoseAlignInspect.Models;

namespace PcbPoseAlignInspect.Processing
{
	public sealed class PcbPoseInspectProcessor
	{
		private sealed class DetectedPose
		{
			public PointF BoardCenter;

			public double BoardAngleDeg;

			public RectangleF BoardBox;

			public double AngleRadiusPx;

			public bool FeatureMatchOk;

			public double FeatureMatchScore;

			public PointF FeatureCenter;

			public RectangleF FeatureBounds;
		}

		private sealed class FeatureMatch
		{
			public bool Ok;

			public double Score;

			public PointF Center;

			public RectangleF Bounds;
		}

		public PcbPoseInspectResult Teach(Bitmap image, PcbPoseInspectRecipe recipe)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			if (image == null)
			{
				return PcbPoseInspectResult.Invalid("输入图像为空", InspectNgReason.ParameterInvalid);
			}
			if (recipe == null)
			{
				return PcbPoseInspectResult.Invalid("配方为空", InspectNgReason.ParameterInvalid);
			}
			try
			{
				if (!TryDetectPose(image, recipe, inspectMode: false, out var pose, out var error))
				{
					return PcbPoseInspectResult.Invalid("示教失败: " + error, InspectNgReason.BoardDetectFailed);
				}
				recipe.TeachBoardCenter = pose.BoardCenter;
				recipe.TeachBoardAngleDeg = pose.BoardAngleDeg;
				recipe.AngleRadiusPx = Math.Max(20.0, pose.AngleRadiusPx);
				recipe.IsTaught = true;
				return new PcbPoseInspectResult
				{
					Success = true,
					Message = "示教成功",
					NgReasons = InspectNgReason.None,
					BoardDetectOk = true,
					FeatureMatchOk = pose.FeatureMatchOk,
					FeatureMatchScore = pose.FeatureMatchScore,
					TeachBoardCenter = recipe.TeachBoardCenter,
					RuntimeBoardCenter = pose.BoardCenter,
					TeachBoardAngleDeg = recipe.TeachBoardAngleDeg,
					RuntimeBoardAngleDeg = pose.BoardAngleDeg,
					RuntimeBoardBoundingBox = pose.BoardBox,
					TeachFeatureCenter = recipe.TeachFeatureCenter,
					RuntimeFeatureCenter = pose.FeatureCenter,
					RuntimeFeatureBounds = pose.FeatureBounds,
					FeatureSearchRoi = recipe.FeatureSearchRoi,
					FeatureTemplateRoi = recipe.FeatureTemplateRoi,
					FeatureRoiShape = recipe.FeatureRoiShape,
					ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
				};
			}
			catch (Exception ex)
			{
				return PcbPoseInspectResult.Invalid("示教异常: " + ex.Message, InspectNgReason.AlgorithmException);
			}
		}

		public PcbPoseInspectResult Inspect(Bitmap image, PcbPoseInspectRecipe recipe, double? toleranceOverridePx)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			if (image == null)
			{
				return PcbPoseInspectResult.Invalid("输入图像为空", InspectNgReason.ParameterInvalid);
			}
			if (recipe == null)
			{
				return PcbPoseInspectResult.Invalid("配方为空", InspectNgReason.ParameterInvalid);
			}
			if (!recipe.IsTaught)
			{
				return PcbPoseInspectResult.Invalid("配方未示教，请先示教", InspectNgReason.ParameterInvalid);
			}
			try
			{
				if (!TryDetectPose(image, recipe, inspectMode: true, out var pose, out var error))
				{
					return PcbPoseInspectResult.Invalid("检测失败: " + error, InspectNgReason.BoardDetectFailed);
				}
				PointF boardCenter = pose.BoardCenter;
				double boardAngleDeg = pose.BoardAngleDeg;
				double num = boardCenter.X - recipe.TeachBoardCenter.X;
				double num2 = boardCenter.Y - recipe.TeachBoardCenter.Y;
				double num3 = NormalizeAngleDelta(boardAngleDeg - recipe.TeachBoardAngleDeg);
				double num4 = Math.Abs(num3) * Math.PI / 180.0 * Math.Max(1.0, recipe.AngleRadiusPx);
				double num5 = Math.Max(Math.Max(Math.Abs(num), Math.Abs(num2)), num4);
				double num6 = (toleranceOverridePx.HasValue ? toleranceOverridePx.Value : recipe.UnifiedTolerancePx);
				InspectNgReason inspectNgReason = InspectNgReason.None;
				if (Math.Abs(num) > num6)
				{
					inspectNgReason |= InspectNgReason.XOutOfTolerance;
				}
				if (Math.Abs(num2) > num6)
				{
					inspectNgReason |= InspectNgReason.YOutOfTolerance;
				}
				if (num4 > num6)
				{
					inspectNgReason |= InspectNgReason.AngleOutOfTolerance;
				}
				if (num5 > num6)
				{
					inspectNgReason |= InspectNgReason.UnifiedToleranceOut;
				}
				bool flag = inspectNgReason == InspectNgReason.None;
				return new PcbPoseInspectResult
				{
					Success = flag,
					Message = (flag ? "OK" : ("NG: " + inspectNgReason)),
					NgReasons = inspectNgReason,
					BoardDetectOk = true,
					FeatureMatchOk = pose.FeatureMatchOk,
					FeatureMatchScore = pose.FeatureMatchScore,
					TeachBoardCenter = recipe.TeachBoardCenter,
					RuntimeBoardCenter = boardCenter,
					TeachBoardAngleDeg = recipe.TeachBoardAngleDeg,
					RuntimeBoardAngleDeg = boardAngleDeg,
					RuntimeBoardBoundingBox = pose.BoardBox,
					TeachFeatureCenter = recipe.TeachFeatureCenter,
					RuntimeFeatureCenter = pose.FeatureCenter,
					RuntimeFeatureBounds = pose.FeatureBounds,
					FeatureSearchRoi = recipe.FeatureSearchRoi,
					FeatureTemplateRoi = recipe.FeatureTemplateRoi,
					FeatureRoiShape = recipe.FeatureRoiShape,
					DxPx = num,
					DyPx = num2,
					AngleDeltaDeg = num3,
					AngleEquivalentPx = num4,
					ScorePx = num5,
					UsedTolerancePx = num6,
					ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
				};
			}
			catch (Exception ex)
			{
				return PcbPoseInspectResult.Invalid("检测异常: " + ex.Message, InspectNgReason.AlgorithmException);
			}
		}

		public PointF[] ExtractBoardContour(Bitmap image, PcbPoseInspectRecipe recipe)
		{
			return ExtractBoardSegmentation(image, recipe).Contour;
		}

		public BoardSegmentationResult ExtractBoardSegmentation(Bitmap image, PcbPoseInspectRecipe recipe)
		{
			if (image == null || recipe == null)
			{
				return BoardSegmentationResult.Invalid(image == null ? "输入图像为空" : "配方为空");
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			using (Bitmap bitmap = To24bpp(image))
			{
				HObject hObject = null;
				HObject selectedRegion = null;
				try
				{
					hObject = CreateHalconImageFromBitmap(bitmap);
					if (!TrySegmentBoardRegion(hObject, bitmap.Width, bitmap.Height, recipe, out selectedRegion, out var error, out var selectedArea, out var candidateCount))
					{
						return new BoardSegmentationResult
						{
							Success = false,
							Message = error,
							CandidateCount = candidateCount,
							SelectedArea = selectedArea,
							ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
						};
					}
					PointF[] contour = RegionToContourPoints(selectedRegion, 1800);
					HOperatorSet.AreaCenter(selectedRegion, out var area, out var row, out var column);
					HOperatorSet.SmallestRectangle2(selectedRegion, out var _, out var _, out var phi, out var _, out var _);
					HOperatorSet.SmallestRectangle1(selectedRegion, out var row1, out var column1, out var row2, out var column2);
					double finalArea = area.D > 0.0 ? area.D : selectedArea;
					return new BoardSegmentationResult
					{
						Success = contour.Length > 1,
						Message = contour.Length > 1 ? "轮廓已提取" : "未提取到有效轮廓",
						Contour = contour,
						CandidateCount = candidateCount,
						SelectedArea = finalArea,
						Center = new PointF((float)column.D, (float)row.D),
						AngleDeg = NormalizeAngleDeg(phi.D * 180.0 / Math.PI),
						BoundingBox = RectangleF.FromLTRB((float)column1.D, (float)row1.D, (float)column2.D, (float)row2.D),
						ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
					};
				}
				catch
				{
					return BoardSegmentationResult.Invalid("轮廓提取异常");
				}
				finally
				{
					DisposeObj(selectedRegion);
					DisposeObj(hObject);
				}
			}
		}

		private static bool TryDetectPose(Bitmap bitmap, PcbPoseInspectRecipe recipe, bool inspectMode, out DetectedPose pose, out string error)
		{
			pose = null;
			error = string.Empty;
			if (inspectMode && recipe.EnableFeatureTemplateMatch && !recipe.HasFeatureTemplate())
			{
				error = "已启用特征模板定位，但尚未保存特征模板";
				return false;
			}
			using (Bitmap bitmap2 = To24bpp(bitmap))
			{
				HObject hObject = null;
				try
				{
					hObject = CreateHalconImageFromBitmap(bitmap2);
					if (!TryDetectBoard(hObject, bitmap2.Width, bitmap2.Height, recipe, out var center, out var angleDeg, out var boardBox, out var angleRadiusPx, out var error2))
					{
						error = error2;
						return false;
					}
					FeatureMatch featureMatch = null;
					if (recipe.EnableFeatureTemplateMatch && recipe.HasFeatureTemplate())
					{
						featureMatch = MatchFeatureTemplate(hObject, bitmap2.Width, bitmap2.Height, recipe);
						if (!featureMatch.Ok)
						{
							error = "特征模板匹配失败，请检查特征搜索ROI、模板ROI、最小分数或图像清晰度";
							return false;
						}
						center = new PointF(featureMatch.Center.X + recipe.FeatureToBoardOffset.X, featureMatch.Center.Y + recipe.FeatureToBoardOffset.Y);
					}
					pose = new DetectedPose
					{
						BoardCenter = center,
						BoardAngleDeg = angleDeg,
						BoardBox = boardBox,
						AngleRadiusPx = angleRadiusPx,
						FeatureMatchOk = (featureMatch?.Ok ?? true),
						FeatureMatchScore = (featureMatch?.Score ?? 1.0),
						FeatureCenter = (featureMatch?.Center ?? PointF.Empty),
						FeatureBounds = (featureMatch?.Bounds ?? RectangleF.Empty)
					};
					return true;
				}
				finally
				{
					DisposeObj(hObject);
				}
			}
		}

		private static bool TryDetectBoard(HObject hoImage, int width, int height, PcbPoseInspectRecipe recipe, out PointF center, out double angleDeg, out RectangleF boardBox, out double angleRadiusPx, out string error)
		{
			center = PointF.Empty;
			angleDeg = 0.0;
			boardBox = RectangleF.Empty;
			angleRadiusPx = 0.0;
			error = string.Empty;
			HObject selectedRegion = null;
			try
			{
				if (!TrySegmentBoardRegion(hoImage, width, height, recipe, out selectedRegion, out error, out var selectedArea, out var _))
				{
					return false;
				}
				if (selectedRegion == null)
				{
					error = "未找到有效PCB区域";
					return false;
				}
				if (selectedArea < (double)Math.Max(10, recipe.BoardMinArea))
				{
					error = "PCB区域面积小于最小面积，当前面积: " + selectedArea.ToString("F0");
					return false;
				}
				HOperatorSet.SmallestRectangle2(selectedRegion, out var row, out var column, out var phi, out var length, out var length2);
				HOperatorSet.SmallestRectangle1(selectedRegion, out var row2, out var column2, out var row3, out var column3);
				center = new PointF((float)column.D, (float)row.D);
				angleDeg = NormalizeAngleDeg(phi.D * 180.0 / Math.PI);
				boardBox = RectangleF.FromLTRB((float)column2.D, (float)row2.D, (float)column3.D, (float)row3.D);
				angleRadiusPx = Math.Sqrt(length.D * length.D + length2.D * length2.D);
				return true;
			}
			catch (Exception ex)
			{
				error = "板体检测异常: " + ex.Message;
				return false;
			}
			finally
			{
				DisposeObj(selectedRegion);
			}
		}

		private static bool TrySegmentBoardRegion(HObject hoImage, int width, int height, PcbPoseInspectRecipe recipe, out HObject selectedRegion, out string error, out double selectedArea, out int candidateCount)
		{
			selectedRegion = null;
			error = string.Empty;
			selectedArea = 0.0;
			candidateCount = 0;
			HObject image = null;
			HObject image2 = null;
			HObject image3 = null;
			HObject imageResult = null;
			HObject imageResult2 = null;
			HObject imageResult3 = null;
			HObject region = null;
			HObject region2 = null;
			HObject region3 = null;
			HObject regionIntersection = null;
			HObject regionIntersection2 = null;
			HObject imageSub = null;
			HObject imageSub2 = null;
			HObject region4 = null;
			HObject region5 = null;
			HObject region6 = null;
			HObject regionIntersection3 = null;
			HObject regionIntersection4 = null;
			HObject regionIntersection5 = null;
			HObject rectangle = null;
			HObject objectsSelected = null;
			HObject objectsSelected2 = null;
			HObject objectsSelected3 = null;
			HObject objectsSelected4 = null;
			HObject objectsSelected5 = null;
			HObject connectedRegions = null;
			try
			{
				HOperatorSet.Decompose3(hoImage, out image, out image2, out image3);
				HOperatorSet.TransFromRgb(image, image2, image3, out imageResult, out imageResult2, out imageResult3, "hsv");
				ThresholdHue(imageResult, out region, recipe.BoardHueMin, recipe.BoardHueMax);
				HOperatorSet.Threshold(imageResult2, out region2, ClampByte(recipe.BoardSatMin), ClampByte(recipe.BoardSatMax));
				HOperatorSet.Threshold(imageResult3, out region3, ClampByte(recipe.BoardValMin), ClampByte(recipe.BoardValMax));
				HOperatorSet.Intersection(region, region2, out regionIntersection);
				HOperatorSet.Intersection(regionIntersection, region3, out regionIntersection2);
				HOperatorSet.SubImage(image2, image, out imageSub, 1.0, 128.0);
				HOperatorSet.SubImage(image2, image3, out imageSub2, 1.0, 128.0);
				int v = 128 + Math.Max(0, recipe.BoardGreenRedDiffMin);
				int v2 = 128 + Math.Max(0, recipe.BoardGreenBlueDiffMin);
				HOperatorSet.Threshold(imageSub, out region4, ClampByte(v), 255);
				HOperatorSet.Threshold(imageSub2, out region5, ClampByte(v2), 255);
				HOperatorSet.Threshold(image, out region6, 0, ClampByte(recipe.BoardRedMax));
				HOperatorSet.Intersection(regionIntersection2, region4, out regionIntersection3);
				HOperatorSet.Intersection(regionIntersection3, region5, out regionIntersection4);
				HOperatorSet.Intersection(regionIntersection4, region6, out regionIntersection5);
				if (recipe.EnableBoardSearchRoi && recipe.BoardSearchRoi.Width > 2f && recipe.BoardSearchRoi.Height > 2f)
				{
					RectangleF rectangleF = ClipRect(recipe.BoardSearchRoi, width, height);
					HOperatorSet.GenRectangle1(out rectangle, rectangleF.Top, rectangleF.Left, Math.Max(rectangleF.Top, rectangleF.Bottom - 1f), Math.Max(rectangleF.Left, rectangleF.Right - 1f));
					HOperatorSet.Intersection(regionIntersection5, rectangle, out objectsSelected);
				}
				else
				{
					HOperatorSet.CopyObj(regionIntersection5, out objectsSelected, 1, 1);
				}
				double num = Math.Max(0.0, recipe.BoardOpenRadius);
				double num2 = Math.Max(0.0, recipe.BoardCloseRadius);
				if (num > 0.05)
				{
					HOperatorSet.OpeningCircle(objectsSelected, out objectsSelected2, num);
				}
				else
				{
					HOperatorSet.CopyObj(objectsSelected, out objectsSelected2, 1, 1);
				}
				if (num2 > 0.05)
				{
					HOperatorSet.ClosingCircle(objectsSelected2, out objectsSelected3, num2);
				}
				else
				{
					HOperatorSet.CopyObj(objectsSelected2, out objectsSelected3, 1, 1);
				}
				if (recipe.BoardFillUp)
				{
					HOperatorSet.FillUp(objectsSelected3, out objectsSelected4);
				}
				else
				{
					HOperatorSet.CopyObj(objectsSelected3, out objectsSelected4, 1, 1);
				}
				HOperatorSet.Connection(objectsSelected4, out connectedRegions);
				selectedRegion = SelectLargestRegionByArea(connectedRegions, Math.Max(10, recipe.BoardMinArea), out selectedArea, out candidateCount);
				if (selectedRegion == null)
				{
					error = "未提取到有效PCB区域。建议画一个只覆盖PCB的板体ROI，并从推荐PCB参数开始微调。";
					return false;
				}
				if (recipe.BoardUseConvexHull)
				{
					HOperatorSet.ShapeTrans(selectedRegion, out objectsSelected5, "convex");
					DisposeObj(selectedRegion);
					selectedRegion = objectsSelected5;
					objectsSelected5 = null;
					HOperatorSet.AreaCenter(selectedRegion, out var area, out var _, out var _);
					selectedArea = area.D;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = "HSV+RGB分割异常: " + ex.Message;
				return false;
			}
			finally
			{
				DisposeObj(image);
				DisposeObj(image2);
				DisposeObj(image3);
				DisposeObj(imageResult);
				DisposeObj(imageResult2);
				DisposeObj(imageResult3);
				DisposeObj(region);
				DisposeObj(region2);
				DisposeObj(region3);
				DisposeObj(regionIntersection);
				DisposeObj(regionIntersection2);
				DisposeObj(imageSub);
				DisposeObj(imageSub2);
				DisposeObj(region4);
				DisposeObj(region5);
				DisposeObj(region6);
				DisposeObj(regionIntersection3);
				DisposeObj(regionIntersection4);
				DisposeObj(regionIntersection5);
				DisposeObj(rectangle);
				DisposeObj(objectsSelected);
				DisposeObj(objectsSelected2);
				DisposeObj(objectsSelected3);
				DisposeObj(objectsSelected4);
				DisposeObj(objectsSelected5);
				DisposeObj(connectedRegions);
			}
		}

		private static void ThresholdHue(HObject hoH, out HObject region, int hueMin, int hueMax)
		{
			int num = ClampByte(hueMin);
			int num2 = ClampByte(hueMax);
			if (num <= num2)
			{
				HOperatorSet.Threshold(hoH, out region, num, num2);
				return;
			}
			HObject region2 = null;
			HObject region3 = null;
			try
			{
				HOperatorSet.Threshold(hoH, out region2, num, 255);
				HOperatorSet.Threshold(hoH, out region3, 0, num2);
				HOperatorSet.Union2(region2, region3, out region);
			}
			finally
			{
				DisposeObj(region2);
				DisposeObj(region3);
			}
		}

		private static FeatureMatch MatchFeatureTemplate(HObject hoImage, int width, int height, PcbPoseInspectRecipe recipe)
		{
			if (hoImage == null || recipe == null || !recipe.HasFeatureTemplate())
			{
				return new FeatureMatch
				{
					Ok = false,
					Score = 0.0
				};
			}
			Rectangle rectangle = ToClippedRectangle(recipe.FeatureSearchRoi, width, height);
			if (rectangle.Width <= 1 || rectangle.Height <= 1)
			{
				rectangle = new Rectangle(0, 0, width, height);
			}
			using (Bitmap bitmap = recipe.CreateFeatureTemplateImage())
			{
				if (bitmap == null || bitmap.Width <= 1 || bitmap.Height <= 1)
				{
					return new FeatureMatch
					{
						Ok = false,
						Score = 0.0
					};
				}
				using (Bitmap bitmap2 = To24bpp(bitmap))
				{
					HObject hObject = null;
					HObject grayImage = null;
					HObject region = null;
					HObject imageReduced = null;
					HObject grayImage2 = null;
					HObject searchRegion = null;
					HObject imageReduced2 = null;
					HTuple modelID = null;
					try
					{
						hObject = CreateHalconImageFromBitmap(bitmap2);
						HOperatorSet.Rgb1ToGray(hObject, out grayImage);
						CreateFeatureRegion(out region, new RectangleF(0f, 0f, bitmap2.Width, bitmap2.Height), recipe.FeatureRoiShape);
						HOperatorSet.ReduceDomain(grayImage, region, out imageReduced);
						HOperatorSet.CreateNccModel(imageReduced, "auto", -0.523599, 1.047198, "auto", "use_polarity", out modelID);
						HOperatorSet.Rgb1ToGray(hoImage, out grayImage2);
						CreateFeatureRegion(out searchRegion, new RectangleF(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height), recipe.FeatureRoiShape);
						HOperatorSet.ReduceDomain(grayImage2, searchRegion, out imageReduced2);
						HOperatorSet.FindNccModel(imageReduced2, modelID, -0.523599, 1.047198, Math.Max(0.0, Math.Min(1.0, recipe.FeatureMatchMinScore)), 1, 0.5, "true", 0, out var row, out var column, out var _, out var score);
						if (score == null || score.Length <= 0)
						{
							return new FeatureMatch
							{
								Ok = false,
								Score = 0.0
							};
						}
						PointF center = new PointF((float)column.D, (float)row.D);
						RectangleF bounds = new RectangleF(center.X - (float)bitmap2.Width / 2f, center.Y - (float)bitmap2.Height / 2f, bitmap2.Width, bitmap2.Height);
						return new FeatureMatch
						{
							Ok = (score.D >= recipe.FeatureMatchMinScore),
							Score = score.D,
							Center = center,
							Bounds = bounds
						};
					}
					catch
					{
						return new FeatureMatch
						{
							Ok = false,
							Score = 0.0
						};
					}
					finally
					{
						if (modelID != null)
						{
							try
							{
								HOperatorSet.ClearNccModel(modelID);
							}
							catch
							{
							}
						}
						DisposeObj(hObject);
						DisposeObj(grayImage);
						DisposeObj(region);
						DisposeObj(imageReduced);
						DisposeObj(grayImage2);
						DisposeObj(searchRegion);
						DisposeObj(imageReduced2);
					}
				}
			}
		}

		private static void CreateFeatureRegion(out HObject region, RectangleF roi, FeatureRoiShape shape)
		{
			if (shape == FeatureRoiShape.Circle)
			{
				double num = (double)roi.Top + (double)roi.Height / 2.0;
				double num2 = (double)roi.Left + (double)roi.Width / 2.0;
				HOperatorSet.GenEllipse(out region, num, num2, 0.0, Math.Max(1.0, (double)roi.Height / 2.0), Math.Max(1.0, (double)roi.Width / 2.0));
			}
			else
			{
				HOperatorSet.GenRectangle1(out region, roi.Top, roi.Left, Math.Max(roi.Top, roi.Bottom - 1f), Math.Max(roi.Left, roi.Right - 1f));
			}
		}

		private static HObject SelectLargestRegionByArea(HObject regions, double minArea, out double selectedArea, out int candidateCount)
		{
			selectedArea = 0.0;
			candidateCount = 0;
			if (regions == null)
			{
				return null;
			}
			HOperatorSet.CountObj(regions, out var number);
			if (number.I <= 0)
			{
				return null;
			}
			HObject objectsSelected = null;
			for (int i = 1; i <= number.I; i++)
			{
				HObject objectSelected = null;
				try
				{
					HOperatorSet.SelectObj(regions, out objectSelected, i);
					HOperatorSet.AreaCenter(objectSelected, out var area, out var _, out var _);
					double d = area.D;
					if (d >= minArea)
					{
						candidateCount++;
						if (d > selectedArea)
						{
							selectedArea = d;
							DisposeObj(objectsSelected);
							objectsSelected = null;
							HOperatorSet.CopyObj(objectSelected, out objectsSelected, 1, 1);
						}
					}
				}
				finally
				{
					DisposeObj(objectSelected);
				}
			}
			return objectsSelected;
		}

		private static PointF[] RegionToContourPoints(HObject region, int maxPoints)
		{
			if (region == null)
			{
				return new PointF[0];
			}
			HObject contours = null;
			HObject objectSelected = null;
			try
			{
				HOperatorSet.GenContourRegionXld(region, out contours, "border");
				HOperatorSet.CountObj(contours, out var number);
				if (number.I <= 0)
				{
					return new PointF[0];
				}
				HOperatorSet.SelectObj(contours, out objectSelected, 1);
				HOperatorSet.GetContourXld(objectSelected, out var row, out var col);
				int num = Math.Min(row.Length, col.Length);
				if (num <= 1)
				{
					return new PointF[0];
				}
				int num2 = Math.Max(1, num / Math.Max(100, maxPoints));
				List<PointF> list = new List<PointF>();
				for (int i = 0; i < num; i += num2)
				{
					list.Add(new PointF((float)col[i].D, (float)row[i].D));
				}
				if (list.Count > 0)
				{
					list.Add(list[0]);
				}
				return list.ToArray();
			}
			catch
			{
				return new PointF[0];
			}
			finally
			{
				DisposeObj(contours);
				DisposeObj(objectSelected);
			}
		}

		private static Rectangle ToClippedRectangle(RectangleF roi, int width, int height)
		{
			int num = Math.Max(0, (int)Math.Floor(roi.Left));
			int num2 = Math.Max(0, (int)Math.Floor(roi.Top));
			int num3 = Math.Min(width, (int)Math.Ceiling(roi.Right));
			int num4 = Math.Min(height, (int)Math.Ceiling(roi.Bottom));
			if (num3 <= num || num4 <= num2)
			{
				return Rectangle.Empty;
			}
			return Rectangle.FromLTRB(num, num2, num3, num4);
		}

		private static RectangleF ClipRect(RectangleF r, int width, int height)
		{
			float num = Math.Max(0f, r.Left);
			float num2 = Math.Max(0f, r.Top);
			float num3 = Math.Min(width, r.Right);
			float num4 = Math.Min(height, r.Bottom);
			if (num3 <= num || num4 <= num2)
			{
				return RectangleF.Empty;
			}
			return RectangleF.FromLTRB(num, num2, num3, num4);
		}

		private static int ClampByte(int v)
		{
			if (v < 0)
			{
				return 0;
			}
			if (v > 255)
			{
				return 255;
			}
			return v;
		}

		private static double NormalizeAngleDelta(double a)
		{
			double num;
			for (num = a; num > 180.0; num -= 360.0)
			{
			}
			for (; num < -180.0; num += 360.0)
			{
			}
			return num;
		}

		private static double NormalizeAngleDeg(double a)
		{
			double num;
			for (num = a; num > 90.0; num -= 180.0)
			{
			}
			for (; num < -90.0; num += 180.0)
			{
			}
			return num;
		}

		private static Bitmap To24bpp(Bitmap src)
		{
			if (src.PixelFormat == PixelFormat.Format24bppRgb)
			{
				return new Bitmap(src);
			}
			Bitmap bitmap = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.DrawImage(src, 0, 0, src.Width, src.Height);
			}
			return bitmap;
		}

		private static HObject CreateHalconImageFromBitmap(Bitmap bmp)
		{
			BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
			try
			{
				HOperatorSet.GenImageInterleaved(out var imageRGB, bitmapData.Scan0, "bgr", bmp.Width, bmp.Height, -1, "byte", bmp.Width, bmp.Height, 0, 0, -1, 0);
				return imageRGB;
			}
			finally
			{
				bmp.UnlockBits(bitmapData);
			}
		}

		private static void DisposeObj(HObject obj)
		{
			obj?.Dispose();
		}
	}
}
