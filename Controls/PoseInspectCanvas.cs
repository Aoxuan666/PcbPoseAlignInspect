using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PcbPoseAlignInspect.Models;

namespace PcbPoseAlignInspect.Controls
{
	public sealed class PoseInspectCanvas : Control
	{
		public enum CanvasTeachMode
		{
			None,
			DrawInspectRoi,
			DrawFeatureSearchRoi,
			DrawFeatureTemplateRoi
		}

		private enum RoiHandle
		{
			None,
			Move,
			Left,
			Right,
			Top,
			Bottom,
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight
		}

		private enum RoiTarget
		{
			None,
			BoardSearch,
			FeatureSearch,
			FeatureTemplate
		}

		private Bitmap _image;

		private Bitmap _displayImage;

		private CanvasTeachMode _mode;

		private PcbPoseInspectResult _result;

		private PointF[] _boardContour = new PointF[0];

		private float _zoom = 1f;

		private PointF _pan = PointF.Empty;

		private bool _panning;

		private bool _drawing;

		private bool _editing;

		private Point _lastMouse;

		private Point _drawStart;

		private PointF _editStartImage;

		private RectangleF _editStartRoi;

		private RectangleF _previewRoi;

		private RoiHandle _editHandle = RoiHandle.None;

		private RoiTarget _editTarget = RoiTarget.None;

		public Bitmap Image
		{
			get
			{
				return _image;
			}
			set
			{
				_image = value;
				RebuildDisplayImage();
				ResetView();
				Invalidate();
			}
		}

		public CanvasTeachMode Mode
		{
			get
			{
				return _mode;
			}
			set
			{
				_mode = value;
				_drawing = false;
				_editing = false;
				_previewRoi = RectangleF.Empty;
				Invalidate();
			}
		}

		public RectangleF BoardSearchRoi { get; set; }

		public bool EnableBoardSearchRoi { get; set; }

		public RectangleF FeatureSearchRoi { get; set; }

		public RectangleF FeatureTemplateRoi { get; set; }

		public bool EnableFeatureSearchRoi { get; set; }

		public FeatureRoiShape FeatureRoiShape { get; set; }

		public PointF[] BoardContour
		{
			get
			{
				return _boardContour;
			}
			set
			{
				_boardContour = value ?? new PointF[0];
				Invalidate();
			}
		}

		public PcbPoseInspectResult Result
		{
			get
			{
				return _result;
			}
			set
			{
				_result = value;
				Invalidate();
			}
		}

		public event EventHandler RoiChanged;

		public PoseInspectCanvas()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.FromArgb(22, 24, 28);
			DoubleBuffered = true;
			Mode = CanvasTeachMode.None;
			BoardSearchRoi = RectangleF.Empty;
			FeatureSearchRoi = RectangleF.Empty;
			FeatureTemplateRoi = RectangleF.Empty;
			EnableBoardSearchRoi = false;
			EnableFeatureSearchRoi = false;
			FeatureRoiShape = FeatureRoiShape.Rectangle;
			base.MouseWheel += CanvasMouseWheel;
		}

		public void ClearRois()
		{
			BoardSearchRoi = RectangleF.Empty;
			FeatureSearchRoi = RectangleF.Empty;
			FeatureTemplateRoi = RectangleF.Empty;
			EnableBoardSearchRoi = false;
			EnableFeatureSearchRoi = false;
			BoardContour = new PointF[0];
			Result = null;
			OnRoiChanged();
		}

		public void ClearOverlay()
		{
			BoardContour = new PointF[0];
			Result = null;
			Invalidate();
		}

		public void ResetView()
		{
			_zoom = 1f;
			_pan = PointF.Empty;
			Invalidate();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				DisposeDisplayImage();
			}
			base.Dispose(disposing);
		}

		protected override bool IsInputKey(Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				return true;
			}
			return base.IsInputKey(keyData);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape)
			{
				Mode = CanvasTeachMode.None;
				_drawing = false;
				_editing = false;
				_panning = false;
				Cursor = Cursors.Default;
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.Clear(BackColor);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			if (_image == null)
			{
				DrawCenterText(graphics, "未加载图像\r\n请点击右侧“加载图片”或“使用传入图像”");
				return;
			}
			RectangleF imageViewRect = GetImageViewRect();
			graphics.InterpolationMode = ((_zoom > 2f) ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear);
			graphics.PixelOffsetMode = PixelOffsetMode.Half;
			graphics.DrawImage(_displayImage ?? _image, imageViewRect);
			DrawContour(graphics);
			DrawRoi(graphics, BoardSearchRoi, EnableBoardSearchRoi, Color.FromArgb(255, 90, 220, 120), "板体检测ROI", handles: true);
			DrawFeatureRoi(graphics, FeatureSearchRoi, EnableFeatureSearchRoi, Color.FromArgb(255, 210, 80, 255), "特征搜索ROI", handles: true);
			DrawFeatureRoi(graphics, FeatureTemplateRoi, !FeatureTemplateRoi.IsEmpty, Color.FromArgb(255, 255, 170, 40), "特征模板ROI", handles: true);
			if (_drawing && !_previewRoi.IsEmpty)
			{
				DrawDrawingPreview(graphics, _previewRoi);
			}
			DrawResultOverlay(graphics, _result);
			DrawHud(graphics);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			Focus();
			if (_image == null)
			{
				return;
			}
			if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
			{
				_panning = true;
				_lastMouse = e.Location;
				Cursor = Cursors.SizeAll;
			}
			else
			{
				if (e.Button != MouseButtons.Left)
				{
					return;
				}
				if (_mode == CanvasTeachMode.DrawInspectRoi || _mode == CanvasTeachMode.DrawFeatureSearchRoi || _mode == CanvasTeachMode.DrawFeatureTemplateRoi)
				{
					_drawing = true;
					_drawStart = e.Location;
					_previewRoi = RectangleF.Empty;
					Invalidate();
					return;
				}
				RoiTarget target;
				RoiHandle roiHandle = HitTestAllRois(e.Location, out target);
				if (roiHandle != RoiHandle.None && target != RoiTarget.None)
				{
					_editing = true;
					_editTarget = target;
					_editHandle = roiHandle;
					_lastMouse = e.Location;
					_editStartImage = ScreenToImage(e.Location);
					_editStartRoi = GetRoi(target);
					Cursor = CursorForHandle(roiHandle);
				}
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (_image != null)
			{
				if (_panning)
				{
					_pan.X += e.X - _lastMouse.X;
					_pan.Y += e.Y - _lastMouse.Y;
					_lastMouse = e.Location;
					Invalidate();
				}
				else if (_drawing)
				{
					PointF a = ScreenToImage(_drawStart);
					PointF b = ScreenToImage(e.Location);
					_previewRoi = ClampRoi(NormalizeRect(a, b));
					Invalidate();
				}
				else if (_editing)
				{
					PointF curPt = ScreenToImage(e.Location);
					RectangleF roi = EditRoi(_editStartRoi, _editStartImage, curPt, _editHandle);
					SetRoi(_editTarget, roi);
					Invalidate();
				}
				else
				{
					Cursor = CursorForHandle(HitTestAllRois(e.Location, out var _));
				}
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (_panning)
			{
				_panning = false;
				Cursor = Cursors.Default;
			}
			else if (_editing)
			{
				_editing = false;
				_editHandle = RoiHandle.None;
				_editTarget = RoiTarget.None;
				Cursor = Cursors.Default;
				OnRoiChanged();
			}
			else
			{
				if (!_drawing || e.Button != MouseButtons.Left)
				{
					return;
				}
				_drawing = false;
				if (_previewRoi.Width >= 4f && _previewRoi.Height >= 4f)
				{
					if (_mode == CanvasTeachMode.DrawFeatureSearchRoi)
					{
						FeatureSearchRoi = _previewRoi;
						EnableFeatureSearchRoi = true;
					}
					else if (_mode == CanvasTeachMode.DrawFeatureTemplateRoi)
					{
						FeatureTemplateRoi = _previewRoi;
					}
					else
					{
						BoardSearchRoi = _previewRoi;
						EnableBoardSearchRoi = true;
					}
					Mode = CanvasTeachMode.None;
					OnRoiChanged();
				}
				_previewRoi = RectangleF.Empty;
				Invalidate();
			}
		}

		private void CanvasMouseWheel(object sender, MouseEventArgs e)
		{
			if (_image != null)
			{
				float zoom = _zoom;
				float num = ((e.Delta > 0) ? 1.18f : 0.84745765f);
				_zoom = Math.Max(0.08f, Math.Min(16f, _zoom * num));
				if (!(Math.Abs(_zoom - zoom) < 0.0001f))
				{
					PointF pointF = ScreenToImageUnclamped(e.Location, zoom, _pan);
					PointF pointF2 = ScreenToImageUnclamped(e.Location, _zoom, _pan);
					float num2 = GetBaseScale() * _zoom;
					_pan.X += (pointF2.X - pointF.X) * num2;
					_pan.Y += (pointF2.Y - pointF.Y) * num2;
					Invalidate();
				}
			}
		}

		private void DrawHud(Graphics g)
		{
			string arg = ((_mode == CanvasTeachMode.DrawInspectRoi) ? "绘制板体检测ROI" : ((_mode == CanvasTeachMode.DrawFeatureSearchRoi) ? "绘制特征搜索ROI" : ((_mode != CanvasTeachMode.DrawFeatureTemplateRoi) ? "浏览 / 编辑ROI" : "绘制特征模板ROI")));
			string s = $"模式: {arg}   缩放: {_zoom * 100f:F0}%   左键编辑ROI / 右键拖动画面 / 滚轮缩放 / Esc退出绘制";
			using (Brush brush = new SolidBrush(Color.FromArgb(165, 0, 0, 0)))
			{
				g.FillRectangle(brush, 8, 8, Math.Min(base.ClientSize.Width - 16, 760), 30);
			}
			using (Brush brush2 = new SolidBrush(Color.White))
			{
				g.DrawString(s, Font, brush2, 16f, 15f);
			}
		}

		private void DrawContour(Graphics g)
		{
			if (_boardContour == null || _boardContour.Length < 2 || _image == null)
			{
				return;
			}
			PointF[] array = new PointF[_boardContour.Length];
			for (int i = 0; i < _boardContour.Length; i++)
			{
				array[i] = ImageToScreen(_boardContour[i]);
			}
			using (Pen pen = new Pen(Color.FromArgb(245, 30, 255, 100), 2.2f))
			{
				g.DrawLines(pen, array);
			}
		}

		private void DrawResultOverlay(Graphics g, PcbPoseInspectResult result)
		{
			if (result != null)
			{
				DrawPoint(g, result.RuntimeBoardCenter, Color.FromArgb(255, 255, 80, 80), "C");
				if (!result.RuntimeFeatureBounds.IsEmpty)
				{
					if (result.RuntimeFeatureContour != null && result.RuntimeFeatureContour.Length > 1)
					{
						DrawFeatureContour(g, result.RuntimeFeatureContour, result.FeatureMatchOk ? Color.FromArgb(255, 255, 60, 230) : Color.FromArgb(255, 255, 120, 60), result.FeatureMatchOk ? "特征轮廓" : "低分候选");
					}
					else if (!result.FeatureMatchOk)
					{
						DrawFeatureRoi(g, result.RuntimeFeatureBounds, enabled: true, result.FeatureMatchOk ? Color.FromArgb(255, 255, 60, 230) : Color.FromArgb(255, 255, 120, 60), result.FeatureMatchOk ? "特征匹配范围" : "低分候选", handles: false);
					}
					DrawPoint(g, result.RuntimeFeatureCenter, result.FeatureMatchOk ? Color.FromArgb(255, 255, 60, 230) : Color.FromArgb(255, 255, 120, 60), "F");
				}
			}
		}

		private void DrawPoint(Graphics g, PointF imgPt, Color color, string label)
		{
			if (imgPt.IsEmpty)
			{
				return;
			}
			PointF pointF = ImageToScreen(imgPt);
			RectangleF rect = new RectangleF(pointF.X - 4f, pointF.Y - 4f, 8f, 8f);
			using (Brush brush = new SolidBrush(color))
			{
				g.FillEllipse(brush, rect);
			}
			using (Pen pen = new Pen(Color.Black, 1f))
			{
				g.DrawEllipse(pen, rect);
			}
			using (Brush brush2 = new SolidBrush(color))
			{
				g.DrawString(label, Font, brush2, pointF.X + 6f, pointF.Y - 10f);
			}
		}

		private void DrawRoi(Graphics g, RectangleF roi, bool enabled, Color color, string text, bool handles)
		{
			if (enabled && !roi.IsEmpty && _image != null)
			{
				RectangleF r = ImageRectToScreen(roi);
				using (Pen pen = new Pen(color, 2f))
				{
					g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
				}
				if (handles)
				{
					DrawHandles(g, r, color);
				}
				DrawLabel(g, text, r, color);
			}
		}

		private void DrawFeatureRoi(Graphics g, RectangleF roi, bool enabled, Color color, string text, bool handles)
		{
			if (!enabled || roi.IsEmpty || _image == null)
			{
				return;
			}
			RectangleF rectangleF = ImageRectToScreen(roi);
			using (Pen pen = new Pen(color, 2f))
			{
				if (FeatureRoiShape == FeatureRoiShape.Circle)
				{
					RectangleF circleRect = GetInnerCircleRect(rectangleF);
					g.DrawEllipse(pen, circleRect);
				}
				else
				{
					g.DrawRectangle(pen, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
				}
			}
			if (handles)
			{
				DrawHandles(g, rectangleF, color);
			}
			if (handles && text == "特征模板ROI")
			{
				PointF center = new PointF(roi.Left + roi.Width / 2f, roi.Top + roi.Height / 2f);
				DrawPoint(g, center, color, "T");
			}
			DrawLabel(g, text, rectangleF, color);
		}

		private static RectangleF GetInnerCircleRect(RectangleF rect)
		{
			float size = Math.Min(rect.Width, rect.Height);
			return new RectangleF(rect.Left + (rect.Width - size) / 2f, rect.Top + (rect.Height - size) / 2f, size, size);
		}

		private void DrawFeatureContour(Graphics g, PointF[] contour, Color color, string text)
		{
			if (contour == null || contour.Length < 2 || _image == null)
			{
				return;
			}
			PointF[] points = new PointF[contour.Length];
			for (int i = 0; i < contour.Length; i++)
			{
				points[i] = ImageToScreen(contour[i]);
			}
			using (Pen pen = new Pen(color, 2.2f))
			{
				g.DrawLines(pen, points);
			}
			RectangleF bounds = BoundsOf(points);
			if (!bounds.IsEmpty)
			{
				DrawLabel(g, text, bounds, color);
			}
		}

		private void DrawDrawingPreview(Graphics g, RectangleF roi)
		{
			if (_mode == CanvasTeachMode.DrawFeatureSearchRoi || _mode == CanvasTeachMode.DrawFeatureTemplateRoi)
			{
				DrawFeatureRoi(g, roi, enabled: true, Color.FromArgb(255, 120, 220, 255), "绘制中", handles: true);
			}
			else
			{
				DrawRoi(g, roi, enabled: true, Color.FromArgb(255, 120, 240, 140), "绘制中", handles: true);
			}
		}

		private void DrawLabel(Graphics g, string text, RectangleF r, Color color)
		{
			using (Brush brush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
			{
				using (Brush brush2 = new SolidBrush(Color.FromArgb(235, color)))
				{
					SizeF sizeF = g.MeasureString(text, Font);
					RectangleF rect = new RectangleF(r.Left + 3f, Math.Max(4f, r.Top - sizeF.Height - 2f), sizeF.Width + 8f, sizeF.Height + 2f);
					g.FillRectangle(brush, rect);
					g.DrawString(text, Font, brush2, rect.Left + 4f, rect.Top + 1f);
				}
			}
		}

		private static void DrawHandles(Graphics g, RectangleF r, Color color)
		{
			PointF[] array = new PointF[8]
			{
				new PointF(r.Left, r.Top),
				new PointF(r.Right, r.Top),
				new PointF(r.Left, r.Bottom),
				new PointF(r.Right, r.Bottom),
				new PointF((r.Left + r.Right) * 0.5f, r.Top),
				new PointF((r.Left + r.Right) * 0.5f, r.Bottom),
				new PointF(r.Left, (r.Top + r.Bottom) * 0.5f),
				new PointF(r.Right, (r.Top + r.Bottom) * 0.5f)
			};
			using (Brush brush = new SolidBrush(Color.White))
			{
				using (Pen pen = new Pen(color, 1.5f))
				{
					for (int i = 0; i < array.Length; i++)
					{
						PointF pointF = array[i];
						RectangleF rect = new RectangleF(pointF.X - 4f, pointF.Y - 4f, 8f, 8f);
						g.FillRectangle(brush, rect);
						g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
					}
				}
			}
		}

		private static RectangleF BoundsOf(PointF[] points)
		{
			if (points == null || points.Length == 0)
			{
				return RectangleF.Empty;
			}
			float minX = points[0].X;
			float minY = points[0].Y;
			float maxX = points[0].X;
			float maxY = points[0].Y;
			for (int i = 1; i < points.Length; i++)
			{
				PointF p = points[i];
				minX = Math.Min(minX, p.X);
				minY = Math.Min(minY, p.Y);
				maxX = Math.Max(maxX, p.X);
				maxY = Math.Max(maxY, p.Y);
			}
			if (maxX <= minX || maxY <= minY)
			{
				return RectangleF.Empty;
			}
			return RectangleF.FromLTRB(minX, minY, maxX, maxY);
		}

		private RoiHandle HitTestAllRois(Point p, out RoiTarget target)
		{
			target = RoiTarget.None;
			if (!FeatureTemplateRoi.IsEmpty)
			{
				RoiHandle roiHandle = HitTestRect(p, FeatureTemplateRoi);
				if (roiHandle != RoiHandle.None)
				{
					target = RoiTarget.FeatureTemplate;
					return roiHandle;
				}
			}
			if (EnableFeatureSearchRoi && !FeatureSearchRoi.IsEmpty)
			{
				RoiHandle roiHandle = HitTestRect(p, FeatureSearchRoi);
				if (roiHandle != RoiHandle.None)
				{
					target = RoiTarget.FeatureSearch;
					return roiHandle;
				}
			}
			if (EnableBoardSearchRoi && !BoardSearchRoi.IsEmpty)
			{
				RoiHandle roiHandle = HitTestRect(p, BoardSearchRoi);
				if (roiHandle != RoiHandle.None)
				{
					target = RoiTarget.BoardSearch;
					return roiHandle;
				}
			}
			return RoiHandle.None;
		}

		private RoiHandle HitTestRect(Point p, RectangleF imageRoi)
		{
			if (_image == null || imageRoi.IsEmpty)
			{
				return RoiHandle.None;
			}
			RectangleF rectangleF = ImageRectToScreen(imageRoi);
			if (Near(p, rectangleF.Left, rectangleF.Top, 7f))
			{
				return RoiHandle.TopLeft;
			}
			if (Near(p, rectangleF.Right, rectangleF.Top, 7f))
			{
				return RoiHandle.TopRight;
			}
			if (Near(p, rectangleF.Left, rectangleF.Bottom, 7f))
			{
				return RoiHandle.BottomLeft;
			}
			if (Near(p, rectangleF.Right, rectangleF.Bottom, 7f))
			{
				return RoiHandle.BottomRight;
			}
			if (Math.Abs((float)p.X - rectangleF.Left) <= 7f && (float)p.Y >= rectangleF.Top && (float)p.Y <= rectangleF.Bottom)
			{
				return RoiHandle.Left;
			}
			if (Math.Abs((float)p.X - rectangleF.Right) <= 7f && (float)p.Y >= rectangleF.Top && (float)p.Y <= rectangleF.Bottom)
			{
				return RoiHandle.Right;
			}
			if (Math.Abs((float)p.Y - rectangleF.Top) <= 7f && (float)p.X >= rectangleF.Left && (float)p.X <= rectangleF.Right)
			{
				return RoiHandle.Top;
			}
			if (Math.Abs((float)p.Y - rectangleF.Bottom) <= 7f && (float)p.X >= rectangleF.Left && (float)p.X <= rectangleF.Right)
			{
				return RoiHandle.Bottom;
			}
			if (rectangleF.Contains(p))
			{
				return RoiHandle.Move;
			}
			return RoiHandle.None;
		}

		private RectangleF GetRoi(RoiTarget target)
		{
			switch (target)
			{
			case RoiTarget.BoardSearch:
				return BoardSearchRoi;
			case RoiTarget.FeatureSearch:
				return FeatureSearchRoi;
			case RoiTarget.FeatureTemplate:
				return FeatureTemplateRoi;
			default:
				return RectangleF.Empty;
			}
		}

		private void SetRoi(RoiTarget target, RectangleF roi)
		{
			switch (target)
			{
			case RoiTarget.BoardSearch:
				BoardSearchRoi = roi;
				EnableBoardSearchRoi = true;
				break;
			case RoiTarget.FeatureSearch:
				FeatureSearchRoi = roi;
				EnableFeatureSearchRoi = true;
				break;
			case RoiTarget.FeatureTemplate:
				FeatureTemplateRoi = roi;
				break;
			}
		}

		private static bool Near(Point p, float x, float y, float distance)
		{
			return Math.Abs((float)p.X - x) <= distance && Math.Abs((float)p.Y - y) <= distance;
		}

		private static Cursor CursorForHandle(RoiHandle handle)
		{
			switch (handle)
			{
			case RoiHandle.Move:
				return Cursors.SizeAll;
			case RoiHandle.Left:
			case RoiHandle.Right:
				return Cursors.SizeWE;
			case RoiHandle.Top:
			case RoiHandle.Bottom:
				return Cursors.SizeNS;
			case RoiHandle.TopLeft:
			case RoiHandle.BottomRight:
				return Cursors.SizeNWSE;
			case RoiHandle.TopRight:
			case RoiHandle.BottomLeft:
				return Cursors.SizeNESW;
			default:
				return Cursors.Default;
			}
		}

		private RectangleF EditRoi(RectangleF start, PointF startPt, PointF curPt, RoiHandle handle)
		{
			float num = curPt.X - startPt.X;
			float num2 = curPt.Y - startPt.Y;
			RectangleF r = start;
			switch (handle)
			{
			case RoiHandle.Move:
				r.X += num;
				r.Y += num2;
				break;
			case RoiHandle.Left:
			case RoiHandle.TopLeft:
			case RoiHandle.BottomLeft:
				r.X += num;
				r.Width -= num;
				break;
			case RoiHandle.Right:
			case RoiHandle.TopRight:
			case RoiHandle.BottomRight:
				r.Width += num;
				break;
			}
			switch (handle)
			{
			case RoiHandle.Top:
			case RoiHandle.TopLeft:
			case RoiHandle.TopRight:
				r.Y += num2;
				r.Height -= num2;
				break;
			case RoiHandle.Bottom:
			case RoiHandle.BottomLeft:
			case RoiHandle.BottomRight:
				r.Height += num2;
				break;
			}
			if (r.Width < 4f)
			{
				r.Width = 4f;
			}
			if (r.Height < 4f)
			{
				r.Height = 4f;
			}
			return ClampRoi(r);
		}

		private RectangleF ClampRoi(RectangleF r)
		{
			if (_image == null)
			{
				return RectangleF.Empty;
			}
			float num = Math.Max(0f, Math.Min((float)_image.Width - 1f, r.X));
			float num2 = Math.Max(0f, Math.Min((float)_image.Height - 1f, r.Y));
			float right = Math.Max(num + 4f, Math.Min(_image.Width, r.Right));
			float bottom = Math.Max(num2 + 4f, Math.Min(_image.Height, r.Bottom));
			return RectangleF.FromLTRB(num, num2, right, bottom);
		}

		private RectangleF GetImageViewRect()
		{
			if (_image == null)
			{
				return RectangleF.Empty;
			}
			float num = GetBaseScale() * _zoom;
			float num2 = (float)_image.Width * num;
			float num3 = (float)_image.Height * num;
			float num4 = ((float)base.ClientSize.Width - num2) * 0.5f + _pan.X;
			float num5 = ((float)base.ClientSize.Height - num3) * 0.5f + _pan.Y;
			return new RectangleF(num4, num5, num2, num3);
		}

		private float GetBaseScale()
		{
			if (_image == null || base.ClientSize.Width <= 1 || base.ClientSize.Height <= 1)
			{
				return 1f;
			}
			return Math.Min((float)base.ClientSize.Width / (float)_image.Width, (float)base.ClientSize.Height / (float)_image.Height);
		}

		private void RebuildDisplayImage()
		{
			DisposeDisplayImage();
			if (_image == null)
			{
				return;
			}
			int num = Math.Max(_image.Width, _image.Height);
			if (num <= 2200)
			{
				return;
			}
			float num2 = 2200f / (float)num;
			int num3 = Math.Max(1, (int)((float)_image.Width * num2));
			int num4 = Math.Max(1, (int)((float)_image.Height * num2));
			_displayImage = new Bitmap(num3, num4);
			using (Graphics graphics = Graphics.FromImage(_displayImage))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
				graphics.PixelOffsetMode = PixelOffsetMode.Half;
				graphics.DrawImage(_image, new Rectangle(0, 0, num3, num4));
			}
		}

		private void DisposeDisplayImage()
		{
			if (_displayImage != null)
			{
				_displayImage.Dispose();
				_displayImage = null;
			}
		}

		private PointF ScreenToImage(Point p)
		{
			PointF pointF = ScreenToImageUnclamped(p, _zoom, _pan);
			if (_image == null)
			{
				return PointF.Empty;
			}
			return new PointF(Math.Max(0f, Math.Min((float)_image.Width - 1f, pointF.X)), Math.Max(0f, Math.Min((float)_image.Height - 1f, pointF.Y)));
		}

		private PointF ScreenToImageUnclamped(Point p, float zoom, PointF pan)
		{
			if (_image == null)
			{
				return PointF.Empty;
			}
			float num = GetBaseScale() * zoom;
			float num2 = ((float)base.ClientSize.Width - (float)_image.Width * num) * 0.5f + pan.X;
			float num3 = ((float)base.ClientSize.Height - (float)_image.Height * num) * 0.5f + pan.Y;
			return new PointF(((float)p.X - num2) / num, ((float)p.Y - num3) / num);
		}

		private PointF ImageToScreen(PointF p)
		{
			RectangleF imageViewRect = GetImageViewRect();
			return new PointF(imageViewRect.Left + p.X * imageViewRect.Width / (float)_image.Width, imageViewRect.Top + p.Y * imageViewRect.Height / (float)_image.Height);
		}

		private RectangleF ImageRectToScreen(RectangleF roi)
		{
			PointF pointF = ImageToScreen(new PointF(roi.Left, roi.Top));
			PointF pointF2 = ImageToScreen(new PointF(roi.Right, roi.Bottom));
			return RectangleF.FromLTRB(pointF.X, pointF.Y, pointF2.X, pointF2.Y);
		}

		private static RectangleF NormalizeRect(PointF a, PointF b)
		{
			float left = Math.Min(a.X, b.X);
			float top = Math.Min(a.Y, b.Y);
			float right = Math.Max(a.X, b.X);
			float bottom = Math.Max(a.Y, b.Y);
			return RectangleF.FromLTRB(left, top, right, bottom);
		}

		private void DrawCenterText(Graphics g, string text)
		{
			using (StringFormat stringFormat = new StringFormat())
			{
				stringFormat.Alignment = StringAlignment.Center;
				stringFormat.LineAlignment = StringAlignment.Center;
				using (Brush brush = new SolidBrush(Color.FromArgb(220, 220, 220, 220)))
				{
					g.DrawString(text, Font, brush, base.ClientRectangle, stringFormat);
				}
			}
		}

		private void OnRoiChanged()
		{
			this.RoiChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
