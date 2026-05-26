using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PcbPoseAlignInspect.Controls;
using PcbPoseAlignInspect.Models;
using PcbPoseAlignInspect.Processing;

namespace PcbPoseAlignInspect.Forms
{
	public sealed class SetupForm : Form
	{
		private const int MaxPreviewSide = 1000;
		private const int RightPanelWidth = 430;

		private readonly PcbPoseInspectProcessor _processor = new PcbPoseInspectProcessor();
		private readonly SplitContainer _splitContainer;
		private readonly PoseInspectCanvas _canvas;
		private readonly Timer _previewTimer;

		private Bitmap _startupImage;
		private Bitmap _currentImage;
		private Bitmap _previewImage;
		private float _previewScale = 1f;
		private bool _binding;
		private bool _contourRunning;
		private bool _contourRefreshPending;
		private bool _inspectionRunning;
		private int _workVersion;

		private TextBox _txtImagePath;
		private Button _btnUseStartupImage;
		private Button _btnExtractContour;
		private Button _btnTeachBoard;
		private Button _btnSaveFeatureTemplate;
		private Button _btnRun;
		private NumericUpDown _numTolerance;
		private NumericUpDown _numHueMin;
		private NumericUpDown _numHueMax;
		private NumericUpDown _numSatMin;
		private NumericUpDown _numSatMax;
		private NumericUpDown _numValMin;
		private NumericUpDown _numValMax;
		private NumericUpDown _numOpenRadius;
		private NumericUpDown _numCloseRadius;
		private NumericUpDown _numBoardMinArea;
		private NumericUpDown _numGreenRedDiff;
		private NumericUpDown _numGreenBlueDiff;
		private NumericUpDown _numRedMax;
		private NumericUpDown _numFeatureMinScore;
		private CheckBox _chkFillUp;
		private CheckBox _chkUseConvexHull;
		private CheckBox _chkBoardRoi;
		private CheckBox _chkAutoPreview;
		private CheckBox _chkFeatureMatch;
		private ComboBox _cmbFeatureShape;
		private Label _lblStatus;
		private Label _lblDx;
		private Label _lblDy;
		private Label _lblAngle;
		private Label _lblScore;
		private Label _lblFeatureScore;
		private Label _lblElapsed;

		public PcbPoseInspectRecipe CurrentRecipe { get; private set; }

		public SetupForm()
		{
			CurrentRecipe = new PcbPoseInspectRecipe();
			Text = "PcbPoseAlignInspect - PCB摆盘检测配置";
			Width = 1360;
			Height = 820;
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(1024, 700);
			Font = new Font("Microsoft YaHei UI", 9f);

			_splitContainer = new SplitContainer();
			_splitContainer.Dock = DockStyle.Fill;
			_splitContainer.FixedPanel = FixedPanel.Panel2;
			_splitContainer.SplitterWidth = 6;
			Controls.Add(_splitContainer);

			_canvas = new PoseInspectCanvas();
			_canvas.Dock = DockStyle.Fill;
			_canvas.RoiChanged += CanvasOnRoiChanged;
			_splitContainer.Panel1.Controls.Add(_canvas);

			Panel rightPanel = new Panel();
			rightPanel.Dock = DockStyle.Fill;
			rightPanel.AutoScroll = false;
			rightPanel.Padding = new Padding(10);
			_splitContainer.Panel2.Controls.Add(rightPanel);
			BuildRightPanel(rightPanel);
			Shown += delegate { BeginInvoke(new Action(ApplyInitialSplitterDistance)); };

			_previewTimer = new Timer();
			_previewTimer.Interval = 260;
			_previewTimer.Tick += PreviewTimerOnTick;

			BindRecipeToUi();
			SetStatus("未加载图像", Color.DimGray);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			_previewTimer.Stop();
			_previewTimer.Dispose();
			DisposeBitmap(ref _startupImage);
			DisposeBitmap(ref _currentImage);
			DisposeBitmap(ref _previewImage);
			base.OnFormClosed(e);
		}

		public void SetStartupImage(Bitmap image)
		{
			DisposeBitmap(ref _startupImage);
			if (image != null)
			{
				_startupImage = new Bitmap(image);
			}
			if (_btnUseStartupImage != null)
			{
				_btnUseStartupImage.Enabled = _startupImage != null;
			}
		}

		public void LoadRecipe(PcbPoseInspectRecipe recipe)
		{
			CurrentRecipe = recipe == null ? new PcbPoseInspectRecipe() : recipe.Clone();
			BindRecipeToUi();
		}

		public void LoadImage(Bitmap image)
		{
			LoadImageInternal(image, "(传入图像)", true);
		}

		private void BuildRightPanel(Control parent)
		{
			TabControl tabs = new TabControl();
			tabs.Dock = DockStyle.Fill;
			parent.Controls.Add(tabs);

			TabPage boardPage = new TabPage("图像与分割");
			TabPage resultPage = new TabPage("特征与结果");
			tabs.TabPages.Add(boardPage);
			tabs.TabPages.Add(resultPage);

			int y = 8;
			GroupBox imageGroup = AddGroup(boardPage, "1. 图像与视图", y, 150);
			AddButton(imageGroup, "加载图片", 12, 28, 122, 30, BtnLoadImageOnClick);
			_btnUseStartupImage = AddButton(imageGroup, "使用传入图像", 146, 28, 122, 30, BtnUseStartupImageOnClick);
			_btnUseStartupImage.Enabled = _startupImage != null;
			AddButton(imageGroup, "重置视图", 280, 28, 122, 30, delegate { _canvas.ResetView(); });
			AddButton(imageGroup, "清空ROI/结果", 12, 68, 122, 30, delegate
			{
				_canvas.ClearRois();
				UpdateResult(null);
				SetStatus("已清空", Color.DimGray);
			});
			AddButton(imageGroup, "画板体ROI", 146, 68, 122, 30, delegate
			{
				RequireImageThenSetMode(PoseInspectCanvas.CanvasTeachMode.DrawInspectRoi);
			});
			_btnExtractContour = AddButton(imageGroup, "提取轮廓", 280, 68, 122, 30, BtnExtractContourOnClick);
			_txtImagePath = new TextBox();
			_txtImagePath.ReadOnly = true;
			_txtImagePath.SetBounds(12, 110, 390, 24);
			imageGroup.Controls.Add(_txtImagePath);

			y += 158;
			GroupBox boardGroup = AddGroup(boardPage, "2. PCB板体分割", y, 390);
			AddRangeRow(boardGroup, "H 范围", 28, out _numHueMin, out _numHueMax);
			AddRangeRow(boardGroup, "S 范围", 62, out _numSatMin, out _numSatMax);
			AddRangeRow(boardGroup, "V 范围", 96, out _numValMin, out _numValMax);
			_numGreenRedDiff = AddSingleNumericRow(boardGroup, "G-R 最小差", 130, 0m, 255m, 0);
			_numGreenBlueDiff = AddSingleNumericRow(boardGroup, "G-B 最小差", 164, 0m, 255m, 0);
			_numRedMax = AddSingleNumericRow(boardGroup, "R 最大值", 198, 0m, 255m, 0);
			_numOpenRadius = AddDoubleNumericRow(boardGroup, "开运算", "闭运算", 232, 0m, 120m, 1, out _numCloseRadius);
			_numBoardMinArea = AddSingleNumericRow(boardGroup, "最小面积", 266, 1m, 20000000m, 0);
			_chkFillUp = AddCheckBox(boardGroup, "填洞 FillUp", 12, 302, 120);
			_chkUseConvexHull = AddCheckBox(boardGroup, "凸包补边", 142, 302, 100);
			_chkBoardRoi = AddCheckBox(boardGroup, "启用板体ROI", 250, 302, 120);
			_chkAutoPreview = AddCheckBox(boardGroup, "自动预览", 12, 332, 100);
			_chkAutoPreview.Checked = true;
			AddButton(boardGroup, "推荐PCB参数", 142, 328, 126, 30, BtnApplyPcbPresetOnClick);
			_btnTeachBoard = AddButton(boardGroup, "保存板体示教", 280, 328, 122, 30, BtnTeachBoardOnClick);

			y = 8;
			GroupBox featureGroup = AddGroup(resultPage, "3. 特征模板定位", y, 210);
			AddButton(featureGroup, "画搜索ROI", 12, 28, 122, 30, delegate
			{
				RequireImageThenSetMode(PoseInspectCanvas.CanvasTeachMode.DrawFeatureSearchRoi);
			});
			AddButton(featureGroup, "画模板ROI", 146, 28, 122, 30, delegate
			{
				RequireImageThenSetMode(PoseInspectCanvas.CanvasTeachMode.DrawFeatureTemplateRoi);
			});
			_btnSaveFeatureTemplate = AddButton(featureGroup, "保存特征模板", 280, 28, 122, 30, BtnSaveFeatureTemplateOnClick);
			_chkFeatureMatch = AddCheckBox(featureGroup, "启用特征模板定位", 12, 72, 170);
			AddLabel(featureGroup, "ROI形状", 12, 106, 118);
			_cmbFeatureShape = new ComboBox();
			_cmbFeatureShape.DropDownStyle = ComboBoxStyle.DropDownList;
			_cmbFeatureShape.SetBounds(132, 102, 136, 26);
			_cmbFeatureShape.Items.Add(FeatureRoiShape.Rectangle);
			_cmbFeatureShape.Items.Add(FeatureRoiShape.Circle);
			featureGroup.Controls.Add(_cmbFeatureShape);
			_numFeatureMinScore = AddSingleNumericRow(featureGroup, "最小分数", 140, 0m, 1m, 2);

			y += 218;
			GroupBox resultGroup = AddGroup(resultPage, "4. 运行结果", y, 220);
			AddLabel(resultGroup, "统一公差(px)", 12, 30, 118);
			_numTolerance = AddNumeric(resultGroup, 132, 26, 136, 0m, 9999m, 2);
			_btnRun = AddButton(resultGroup, "运行检测", 280, 25, 122, 30, BtnRunOnClick);
			_lblStatus = AddResultLabel(resultGroup, "状态", 12, 68, "未加载图像");
			_lblDx = AddResultLabel(resultGroup, "dx(px)", 12, 96, "-");
			_lblDy = AddResultLabel(resultGroup, "dy(px)", 12, 124, "-");
			_lblAngle = AddResultLabel(resultGroup, "dAngle(deg)", 12, 152, "-");
			_lblScore = AddResultLabel(resultGroup, "score(px)", 12, 180, "-");
			_lblFeatureScore = AddResultLabel(resultGroup, "feature", 214, 96, "-");
			_lblElapsed = AddResultLabel(resultGroup, "耗时(ms)", 214, 124, "-");
			y += 228;
			AddButton(resultPage, "确认配置", 10, y, 194, 36, BtnOkOnClick);
			AddButton(resultPage, "取消", 218, y, 194, 36, delegate
			{
				DialogResult = DialogResult.Cancel;
				Close();
			});

			HookParameterChanged();
		}

		private void ApplyInitialSplitterDistance()
		{
			int availableWidth = _splitContainer.ClientSize.Width - _splitContainer.SplitterWidth;
			if (availableWidth <= RightPanelWidth + 320)
			{
				return;
			}

			int rightWidth = Math.Min(RightPanelWidth, availableWidth - 320);
			int splitterDistance = availableWidth - rightWidth;
			if (splitterDistance > 0 && splitterDistance < _splitContainer.ClientSize.Width)
			{
				_splitContainer.SplitterDistance = splitterDistance;
			}
		}

		private void BindRecipeToUi()
		{
			if (_numTolerance == null)
			{
				return;
			}

			_binding = true;
			try
			{
				_numTolerance.Value = ToDecimal(CurrentRecipe.UnifiedTolerancePx, _numTolerance.Minimum, _numTolerance.Maximum);
				_numHueMin.Value = ToDecimal(CurrentRecipe.BoardHueMin, _numHueMin.Minimum, _numHueMin.Maximum);
				_numHueMax.Value = ToDecimal(CurrentRecipe.BoardHueMax, _numHueMax.Minimum, _numHueMax.Maximum);
				_numSatMin.Value = ToDecimal(CurrentRecipe.BoardSatMin, _numSatMin.Minimum, _numSatMin.Maximum);
				_numSatMax.Value = ToDecimal(CurrentRecipe.BoardSatMax, _numSatMax.Minimum, _numSatMax.Maximum);
				_numValMin.Value = ToDecimal(CurrentRecipe.BoardValMin, _numValMin.Minimum, _numValMin.Maximum);
				_numValMax.Value = ToDecimal(CurrentRecipe.BoardValMax, _numValMax.Minimum, _numValMax.Maximum);
				_numGreenRedDiff.Value = ToDecimal(CurrentRecipe.BoardGreenRedDiffMin, _numGreenRedDiff.Minimum, _numGreenRedDiff.Maximum);
				_numGreenBlueDiff.Value = ToDecimal(CurrentRecipe.BoardGreenBlueDiffMin, _numGreenBlueDiff.Minimum, _numGreenBlueDiff.Maximum);
				_numRedMax.Value = ToDecimal(CurrentRecipe.BoardRedMax, _numRedMax.Minimum, _numRedMax.Maximum);
				_numOpenRadius.Value = ToDecimal(CurrentRecipe.BoardOpenRadius, _numOpenRadius.Minimum, _numOpenRadius.Maximum);
				_numCloseRadius.Value = ToDecimal(CurrentRecipe.BoardCloseRadius, _numCloseRadius.Minimum, _numCloseRadius.Maximum);
				_numBoardMinArea.Value = ToDecimal(CurrentRecipe.BoardMinArea, _numBoardMinArea.Minimum, _numBoardMinArea.Maximum);
				_chkFillUp.Checked = CurrentRecipe.BoardFillUp;
				_chkUseConvexHull.Checked = CurrentRecipe.BoardUseConvexHull;
				_chkBoardRoi.Checked = CurrentRecipe.EnableBoardSearchRoi;
				_chkFeatureMatch.Checked = CurrentRecipe.EnableFeatureTemplateMatch;
				if (_cmbFeatureShape.Items.Contains(CurrentRecipe.FeatureRoiShape))
				{
					_cmbFeatureShape.SelectedItem = CurrentRecipe.FeatureRoiShape;
				}
				else
				{
					_cmbFeatureShape.SelectedIndex = 0;
				}
				_numFeatureMinScore.Value = ToDecimal(CurrentRecipe.FeatureMatchMinScore, _numFeatureMinScore.Minimum, _numFeatureMinScore.Maximum);
				_canvas.BoardSearchRoi = CurrentRecipe.BoardSearchRoi;
				_canvas.EnableBoardSearchRoi = CurrentRecipe.EnableBoardSearchRoi;
				_canvas.FeatureSearchRoi = CurrentRecipe.FeatureSearchRoi;
				_canvas.FeatureTemplateRoi = CurrentRecipe.FeatureTemplateRoi;
				_canvas.EnableFeatureSearchRoi = !CurrentRecipe.FeatureSearchRoi.IsEmpty;
				_canvas.FeatureRoiShape = CurrentRecipe.FeatureRoiShape;
				_canvas.ClearOverlay();
				_canvas.Invalidate();
			}
			finally
			{
				_binding = false;
			}
		}

		private void ApplyUiToRecipe()
		{
			CurrentRecipe.UnifiedTolerancePx = (double)_numTolerance.Value;
			CurrentRecipe.BoardHueMin = Math.Min((int)_numHueMin.Value, (int)_numHueMax.Value);
			CurrentRecipe.BoardHueMax = Math.Max((int)_numHueMin.Value, (int)_numHueMax.Value);
			CurrentRecipe.BoardSatMin = Math.Min((int)_numSatMin.Value, (int)_numSatMax.Value);
			CurrentRecipe.BoardSatMax = Math.Max((int)_numSatMin.Value, (int)_numSatMax.Value);
			CurrentRecipe.BoardValMin = Math.Min((int)_numValMin.Value, (int)_numValMax.Value);
			CurrentRecipe.BoardValMax = Math.Max((int)_numValMin.Value, (int)_numValMax.Value);
			CurrentRecipe.BoardGreenRedDiffMin = (int)_numGreenRedDiff.Value;
			CurrentRecipe.BoardGreenBlueDiffMin = (int)_numGreenBlueDiff.Value;
			CurrentRecipe.BoardRedMax = (int)_numRedMax.Value;
			CurrentRecipe.BoardOpenRadius = (double)_numOpenRadius.Value;
			CurrentRecipe.BoardCloseRadius = (double)_numCloseRadius.Value;
			CurrentRecipe.BoardFillUp = _chkFillUp.Checked;
			CurrentRecipe.BoardUseConvexHull = _chkUseConvexHull.Checked;
			CurrentRecipe.BoardMinArea = (int)_numBoardMinArea.Value;
			CurrentRecipe.EnableBoardSearchRoi = _chkBoardRoi.Checked;
			CurrentRecipe.BoardSearchRoi = _canvas.BoardSearchRoi;
			CurrentRecipe.EnableFeatureTemplateMatch = _chkFeatureMatch.Checked;
			CurrentRecipe.FeatureRoiShape = _cmbFeatureShape.SelectedItem is FeatureRoiShape ? (FeatureRoiShape)_cmbFeatureShape.SelectedItem : FeatureRoiShape.Rectangle;
			CurrentRecipe.FeatureMatchMinScore = (double)_numFeatureMinScore.Value;
			CurrentRecipe.FeatureSearchRoi = _canvas.FeatureSearchRoi;
			CurrentRecipe.FeatureTemplateRoi = _canvas.FeatureTemplateRoi;
			_canvas.FeatureRoiShape = CurrentRecipe.FeatureRoiShape;
			_canvas.EnableBoardSearchRoi = CurrentRecipe.EnableBoardSearchRoi;
			_canvas.EnableFeatureSearchRoi = !CurrentRecipe.FeatureSearchRoi.IsEmpty;
		}

		private void CanvasOnRoiChanged(object sender, EventArgs e)
		{
			if (_binding)
			{
				return;
			}

			CurrentRecipe.BoardSearchRoi = _canvas.BoardSearchRoi;
			CurrentRecipe.EnableBoardSearchRoi = _canvas.EnableBoardSearchRoi;
			CurrentRecipe.FeatureSearchRoi = _canvas.FeatureSearchRoi;
			CurrentRecipe.FeatureTemplateRoi = _canvas.FeatureTemplateRoi;
			_binding = true;
			_chkBoardRoi.Checked = _canvas.EnableBoardSearchRoi;
			_binding = false;
			MarkContourDirty("ROI已修改，正在刷新轮廓预览");
		}

		private void BtnLoadImageOnClick(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff";
				openFileDialog.Title = "选择检测图像";
				if (openFileDialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}

				try
				{
					using (Bitmap image = LoadBitmapUnlocked(openFileDialog.FileName))
					{
						LoadImageInternal(image, openFileDialog.FileName, true);
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "加载图片失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void BtnUseStartupImageOnClick(object sender, EventArgs e)
		{
			if (_startupImage == null)
			{
				MessageBox.Show(this, "当前没有传入图像。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			LoadImageInternal(_startupImage, "(传入图像)", true);
		}

		private void BtnExtractContourOnClick(object sender, EventArgs e)
		{
			if (CheckImageLoaded())
			{
				RunContourExtractionAsync();
			}
		}

		private void BtnApplyPcbPresetOnClick(object sender, EventArgs e)
		{
			_binding = true;
			try
			{
				_numHueMin.Value = 50m;
				_numHueMax.Value = 122m;
				_numSatMin.Value = 24m;
				_numSatMax.Value = 147m;
				_numValMin.Value = 20m;
				_numValMax.Value = 245m;
				_numGreenRedDiff.Value = 2m;
				_numGreenBlueDiff.Value = 6m;
				_numRedMax.Value = 245m;
				_numOpenRadius.Value = 5.3m;
				_numCloseRadius.Value = 15.9m;
				_numBoardMinArea.Value = 20000m;
				_chkFillUp.Checked = true;
				_chkUseConvexHull.Checked = true;
			}
			finally
			{
				_binding = false;
			}

			MarkContourDirty("已应用推荐PCB参数，正在刷新轮廓预览");
		}

		private void BtnTeachBoardOnClick(object sender, EventArgs e)
		{
			if (!CheckImageLoaded())
			{
				return;
			}

			ApplyUiToRecipe();
			PcbPoseInspectRecipe teachRecipe = CurrentRecipe.Clone();
			teachRecipe.EnableFeatureTemplateMatch = false;
			Bitmap imageCopy = new Bitmap(_currentImage);
			SetBusy(true, "板体示教中...");
			Task.Run(delegate
			{
				try
				{
					return _processor.Teach(imageCopy, teachRecipe);
				}
				finally
				{
					imageCopy.Dispose();
				}
			}).ContinueWith(delegate(Task<PcbPoseInspectResult> task)
			{
				SetBusy(false, null);
				if (IsDisposed || Disposing)
				{
					return;
				}

				if (task.Status != TaskStatus.RanToCompletion)
				{
					SetStatus("示教异常", Color.Firebrick);
					MessageBox.Show(this, "示教异常: " + GetTaskError(task), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				PcbPoseInspectResult result = task.Result;
				_canvas.Result = result;
				UpdateResult(result);
				if (!result.Success)
				{
					MessageBox.Show(this, result.Message, "示教失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				CurrentRecipe.TeachBoardCenter = teachRecipe.TeachBoardCenter;
				CurrentRecipe.TeachBoardAngleDeg = teachRecipe.TeachBoardAngleDeg;
				CurrentRecipe.AngleRadiusPx = teachRecipe.AngleRadiusPx;
				CurrentRecipe.IsTaught = true;
				SetStatus("板体示教成功", Color.SeaGreen);
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void BtnSaveFeatureTemplateOnClick(object sender, EventArgs e)
		{
			if (!CheckImageLoaded())
			{
				return;
			}

			ApplyUiToRecipe();
			if (CurrentRecipe.FeatureTemplateRoi.Width < 4f || CurrentRecipe.FeatureTemplateRoi.Height < 4f)
			{
				MessageBox.Show(this, "请先画特征模板ROI。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			PcbPoseInspectRecipe teachRecipe = CurrentRecipe.Clone();
			teachRecipe.EnableFeatureTemplateMatch = false;
			Bitmap imageCopy = new Bitmap(_currentImage);
			SetBusy(true, "保存特征模板中...");
			Task.Run(delegate
			{
				try
				{
					return _processor.Teach(imageCopy, teachRecipe);
				}
				finally
				{
					imageCopy.Dispose();
				}
			}).ContinueWith(delegate(Task<PcbPoseInspectResult> task)
			{
				SetBusy(false, null);
				if (IsDisposed || Disposing)
				{
					return;
				}

				if (task.Status != TaskStatus.RanToCompletion)
				{
					SetStatus("保存模板异常", Color.Firebrick);
					MessageBox.Show(this, "保存模板异常: " + GetTaskError(task), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				PcbPoseInspectResult result = task.Result;
				if (!result.Success || !result.BoardDetectOk || result.RuntimeBoardCenter.IsEmpty)
				{
					SetStatus("模板保存失败", Color.Firebrick);
					MessageBox.Show(this, "板体中心提取不稳定，不能保存特征模板。\r\n" + result.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				using (Bitmap image = CropBitmap(_currentImage, CurrentRecipe.FeatureTemplateRoi))
				{
					using (Bitmap mask = CreateFeatureTemplateMask(CurrentRecipe.FeatureTemplateRoi, CurrentRecipe.FeatureRoiShape))
					{
						CurrentRecipe.SetFeatureTemplate(image, mask, CurrentRecipe.FeatureTemplateRoi, CurrentRecipe.FeatureRoiShape, result.RuntimeBoardCenter);
					}
				}

				CurrentRecipe.TeachBoardAngleDeg = teachRecipe.TeachBoardAngleDeg;
				CurrentRecipe.AngleRadiusPx = Math.Max(20.0, teachRecipe.AngleRadiusPx);
				CurrentRecipe.IsTaught = true;
				CurrentRecipe.EnableFeatureTemplateMatch = true;
				_binding = true;
				_chkFeatureMatch.Checked = true;
				_binding = false;
				_canvas.FeatureTemplateRoi = CurrentRecipe.FeatureTemplateRoi;
				_canvas.FeatureRoiShape = CurrentRecipe.FeatureRoiShape;
				_canvas.Result = result;
				UpdateResult(result);
				SetStatus("特征模板已保存", Color.SeaGreen);
				MessageBox.Show(this, "特征模板已保存，并记录了特征中心到板中心的偏移关系。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void BtnRunOnClick(object sender, EventArgs e)
		{
			if (!CheckImageLoaded())
			{
				return;
			}

			ApplyUiToRecipe();
			PcbPoseInspectRecipe recipe = CurrentRecipe.Clone();
			double tolerance = (double)_numTolerance.Value;
			Bitmap imageCopy = new Bitmap(_currentImage);
			SetBusy(true, "检测运行中...");
			Task.Run(delegate
			{
				try
				{
					return _processor.Inspect(imageCopy, recipe, tolerance);
				}
				finally
				{
					imageCopy.Dispose();
				}
			}).ContinueWith(delegate(Task<PcbPoseInspectResult> task)
			{
				SetBusy(false, null);
				if (IsDisposed || Disposing)
				{
					return;
				}

				if (task.Status != TaskStatus.RanToCompletion)
				{
					SetStatus("检测异常", Color.Firebrick);
					MessageBox.Show(this, "检测任务异常: " + GetTaskError(task), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				PcbPoseInspectResult result = task.Result;
				_canvas.Result = result;
				UpdateResult(result);
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void BtnOkOnClick(object sender, EventArgs e)
		{
			ApplyUiToRecipe();
			DialogResult = DialogResult.OK;
			Close();
		}

		private void RunContourExtractionAsync()
		{
			if (_currentImage == null)
			{
				return;
			}
			if (_contourRunning)
			{
				_contourRefreshPending = true;
				return;
			}

			ApplyUiToRecipe();
			PcbPoseInspectRecipe previewRecipe = CurrentRecipe.Clone();
			ScalePreviewRecipe(previewRecipe, _previewScale);
			Bitmap previewImage = new Bitmap(_previewImage ?? _currentImage);
			float scale = _previewScale;
			int version = ++_workVersion;
			_contourRunning = true;
			_contourRefreshPending = false;
			_btnExtractContour.Enabled = false;
			SetStatus("轮廓提取中...", Color.DimGray);
			Task.Run(delegate
			{
				try
				{
					PointF[] contour = _processor.ExtractBoardContour(previewImage, previewRecipe);
					return ScaleContourToSource(contour, scale);
				}
				finally
				{
					previewImage.Dispose();
				}
			}).ContinueWith(delegate(Task<PointF[]> task)
			{
				_contourRunning = false;
				_btnExtractContour.Enabled = !_inspectionRunning;
				if (!IsDisposed && !Disposing && version == _workVersion)
				{
					if (task.Status != TaskStatus.RanToCompletion)
					{
						_canvas.BoardContour = new PointF[0];
						SetStatus("轮廓提取异常", Color.Firebrick);
					}
					else
					{
						PointF[] contour = task.Result ?? new PointF[0];
						_canvas.BoardContour = contour;
						SetStatus(contour.Length > 1 ? "轮廓已提取" : "未提取到有效轮廓", contour.Length > 1 ? Color.SeaGreen : Color.Firebrick);
					}
				}
				if (_contourRefreshPending && !IsDisposed && !Disposing)
				{
					_contourRefreshPending = false;
					BeginInvoke(new Action(RunContourExtractionAsync));
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void LoadImageInternal(Bitmap image, string pathText, bool clearOverlay)
		{
			DisposeBitmap(ref _currentImage);
			_currentImage = image == null ? null : new Bitmap(image);
			RebuildPreviewImage();
			_canvas.Image = _currentImage;
			if (clearOverlay)
			{
				_canvas.ClearOverlay();
				UpdateResult(null);
			}
			_txtImagePath.Text = pathText ?? string.Empty;
			if (_currentImage == null)
			{
				SetStatus("未加载图像", Color.DimGray);
				return;
			}

			SetStatus("图像已加载，正在预览轮廓", Color.DimGray);
			ScheduleContourRefresh();
		}

		private void RequireImageThenSetMode(PoseInspectCanvas.CanvasTeachMode mode)
		{
			if (CheckImageLoaded())
			{
				_canvas.Mode = mode;
			}
		}

		private bool CheckImageLoaded()
		{
			if (_currentImage != null)
			{
				return true;
			}
			MessageBox.Show(this, "请先加载图像。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return false;
		}

		private void HookParameterChanged()
		{
			EventHandler boardChanged = delegate
			{
				MarkContourDirty("分割参数已修改，正在刷新轮廓预览");
			};
			EventHandler featureChanged = delegate
			{
				if (_binding)
				{
					return;
				}
				ApplyUiToRecipe();
				_canvas.Result = null;
				_canvas.Invalidate();
				if (_currentImage != null)
				{
					SetStatus("特征参数已修改", Color.DimGray);
				}
			};
			EventHandler toleranceChanged = delegate
			{
				if (!_binding)
				{
					ApplyUiToRecipe();
				}
			};

			_numHueMin.ValueChanged += boardChanged;
			_numHueMax.ValueChanged += boardChanged;
			_numSatMin.ValueChanged += boardChanged;
			_numSatMax.ValueChanged += boardChanged;
			_numValMin.ValueChanged += boardChanged;
			_numValMax.ValueChanged += boardChanged;
			_numGreenRedDiff.ValueChanged += boardChanged;
			_numGreenBlueDiff.ValueChanged += boardChanged;
			_numRedMax.ValueChanged += boardChanged;
			_numOpenRadius.ValueChanged += boardChanged;
			_numCloseRadius.ValueChanged += boardChanged;
			_numBoardMinArea.ValueChanged += boardChanged;
			_chkFillUp.CheckedChanged += boardChanged;
			_chkUseConvexHull.CheckedChanged += boardChanged;
			_chkBoardRoi.CheckedChanged += boardChanged;
			_chkAutoPreview.CheckedChanged += delegate
			{
				if (_chkAutoPreview.Checked)
				{
					ScheduleContourRefresh();
				}
			};
			_chkFeatureMatch.CheckedChanged += featureChanged;
			_cmbFeatureShape.SelectedIndexChanged += featureChanged;
			_numFeatureMinScore.ValueChanged += featureChanged;
			_numTolerance.ValueChanged += toleranceChanged;
		}

		private void PreviewTimerOnTick(object sender, EventArgs e)
		{
			_previewTimer.Stop();
			if (_currentImage == null || _inspectionRunning || !_chkAutoPreview.Checked)
			{
				return;
			}
			RunContourExtractionAsync();
		}

		private void ScheduleContourRefresh()
		{
			if (_binding || _currentImage == null || !_chkAutoPreview.Checked)
			{
				return;
			}
			_previewTimer.Stop();
			_previewTimer.Start();
		}

		private void MarkContourDirty(string text)
		{
			if (_binding)
			{
				return;
			}

			ApplyUiToRecipe();
			_canvas.Result = null;
			if (!_chkAutoPreview.Checked)
			{
				_canvas.BoardContour = new PointF[0];
			}
			if (_currentImage != null)
			{
				SetStatus(text, Color.DimGray);
				ScheduleContourRefresh();
			}
		}

		private void UpdateResult(PcbPoseInspectResult result)
		{
			if (result == null)
			{
				_lblDx.Text = "dx(px): -";
				_lblDy.Text = "dy(px): -";
				_lblAngle.Text = "dAngle(deg): -";
				_lblScore.Text = "score(px): -";
				_lblFeatureScore.Text = "feature: -";
				_lblElapsed.Text = "耗时(ms): -";
				return;
			}

			SetStatus(result.Success ? "OK" : "NG - " + result.NgReasonText, result.Success ? Color.SeaGreen : Color.Firebrick);
			_lblDx.Text = "dx(px): " + result.DxPx.ToString("F3");
			_lblDy.Text = "dy(px): " + result.DyPx.ToString("F3");
			_lblAngle.Text = "dAngle(deg): " + result.AngleDeltaDeg.ToString("F4");
			_lblScore.Text = "score(px): " + result.ScorePx.ToString("F3") + " / tol " + result.UsedTolerancePx.ToString("F3");
			_lblFeatureScore.Text = result.FeatureMatchOk ? "feature: " + result.FeatureMatchScore.ToString("F3") : "feature: NG";
			_lblElapsed.Text = "耗时(ms): " + result.ElapsedMs.ToString("F1");
		}

		private void SetStatus(string text, Color backColor)
		{
			if (_lblStatus == null)
			{
				return;
			}
			_lblStatus.Text = "状态: " + text;
			_lblStatus.BackColor = backColor;
			_lblStatus.ForeColor = Color.White;
		}

		private void SetBusy(bool busy, string status)
		{
			_inspectionRunning = busy;
			_btnRun.Enabled = !busy;
			_btnTeachBoard.Enabled = !busy;
			_btnSaveFeatureTemplate.Enabled = !busy;
			_btnExtractContour.Enabled = !busy && !_contourRunning;
			if (!string.IsNullOrEmpty(status))
			{
				SetStatus(status, Color.DimGray);
			}
		}

		private void RebuildPreviewImage()
		{
			DisposeBitmap(ref _previewImage);
			_previewScale = 1f;
			if (_currentImage == null)
			{
				return;
			}

			int maxSide = Math.Max(_currentImage.Width, _currentImage.Height);
			if (maxSide <= MaxPreviewSide)
			{
				_previewImage = new Bitmap(_currentImage);
				return;
			}

			_previewScale = (float)MaxPreviewSide / maxSide;
			int width = Math.Max(1, (int)Math.Round(_currentImage.Width * _previewScale));
			int height = Math.Max(1, (int)Math.Round(_currentImage.Height * _previewScale));
			_previewImage = new Bitmap(width, height);
			using (Graphics graphics = Graphics.FromImage(_previewImage))
			{
				graphics.InterpolationMode = InterpolationMode.Bilinear;
				graphics.PixelOffsetMode = PixelOffsetMode.Half;
				graphics.DrawImage(_currentImage, new Rectangle(0, 0, width, height), new Rectangle(0, 0, _currentImage.Width, _currentImage.Height), GraphicsUnit.Pixel);
			}
		}

		private static void ScalePreviewRecipe(PcbPoseInspectRecipe recipe, float scale)
		{
			if (recipe == null || Math.Abs(scale - 1f) < 0.001f)
			{
				return;
			}

			recipe.BoardSearchRoi = ScaleRect(recipe.BoardSearchRoi, scale);
			recipe.FeatureSearchRoi = ScaleRect(recipe.FeatureSearchRoi, scale);
			recipe.FeatureTemplateRoi = ScaleRect(recipe.FeatureTemplateRoi, scale);
			recipe.BoardMinArea = Math.Max(1, (int)Math.Round(recipe.BoardMinArea * scale * scale));
			recipe.BoardOpenRadius = Math.Max(0.0, recipe.BoardOpenRadius * scale);
			recipe.BoardCloseRadius = Math.Max(0.0, recipe.BoardCloseRadius * scale);
		}

		private static RectangleF ScaleRect(RectangleF rect, float scale)
		{
			if (rect.IsEmpty)
			{
				return RectangleF.Empty;
			}
			return new RectangleF(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
		}

		private static PointF[] ScaleContourToSource(PointF[] contour, float scale)
		{
			if (contour == null || contour.Length == 0 || Math.Abs(scale - 1f) < 0.001f)
			{
				return contour ?? new PointF[0];
			}

			PointF[] scaled = new PointF[contour.Length];
			float factor = 1f / scale;
			for (int i = 0; i < contour.Length; i++)
			{
				scaled[i] = new PointF(contour[i].X * factor, contour[i].Y * factor);
			}
			return scaled;
		}

		private static Bitmap CropBitmap(Bitmap source, RectangleF roiF)
		{
			Rectangle srcRect = Rectangle.Round(roiF);
			srcRect.Intersect(new Rectangle(0, 0, source.Width, source.Height));
			if (srcRect.Width <= 0 || srcRect.Height <= 0)
			{
				return new Bitmap(1, 1);
			}

			Bitmap bitmap = new Bitmap(srcRect.Width, srcRect.Height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.DrawImage(source, new Rectangle(0, 0, srcRect.Width, srcRect.Height), srcRect, GraphicsUnit.Pixel);
			}
			return bitmap;
		}

		private static Bitmap CreateFeatureTemplateMask(RectangleF roi, FeatureRoiShape shape)
		{
			int width = Math.Max(1, (int)Math.Round(roi.Width));
			int height = Math.Max(1, (int)Math.Round(roi.Height));
			Bitmap bitmap = new Bitmap(width, height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.Black);
				using (Brush brush = new SolidBrush(Color.White))
				{
					if (shape == FeatureRoiShape.Circle)
					{
						int diameter = Math.Max(1, Math.Min(width, height) - 1);
						int x = (width - diameter) / 2;
						int y = (height - diameter) / 2;
						graphics.FillEllipse(brush, x, y, diameter, diameter);
					}
					else
					{
						graphics.FillRectangle(brush, 0, 0, width, height);
					}
				}
			}
			return bitmap;
		}

		private static Bitmap LoadBitmapUnlocked(string path)
		{
			using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				using (Bitmap original = new Bitmap(stream))
				{
					return new Bitmap(original);
				}
			}
		}

		private static void DisposeBitmap(ref Bitmap bitmap)
		{
			if (bitmap != null)
			{
				bitmap.Dispose();
				bitmap = null;
			}
		}

		private static GroupBox AddGroup(Control parent, string text, int y, int height)
		{
			GroupBox group = new GroupBox();
			group.Text = text;
			group.SetBounds(10, y, 414, height);
			parent.Controls.Add(group);
			return group;
		}

		private static Button AddButton(Control parent, string text, int x, int y, int w, int h, EventHandler onClick)
		{
			Button button = new Button();
			button.Text = text;
			button.SetBounds(x, y, w, h);
			button.Click += onClick;
			button.TabStop = false;
			parent.Controls.Add(button);
			return button;
		}

		private static Label AddLabel(Control parent, string text, int x, int y, int w)
		{
			Label label = new Label();
			label.Text = text;
			label.SetBounds(x, y, w, 24);
			label.TextAlign = ContentAlignment.MiddleLeft;
			parent.Controls.Add(label);
			return label;
		}

		private static CheckBox AddCheckBox(Control parent, string text, int x, int y, int w)
		{
			CheckBox checkBox = new CheckBox();
			checkBox.Text = text;
			checkBox.SetBounds(x, y, w, 24);
			parent.Controls.Add(checkBox);
			return checkBox;
		}

		private static void AddRangeRow(Control parent, string label, int y, out NumericUpDown min, out NumericUpDown max)
		{
			AddLabel(parent, label, 12, y + 4, 112);
			min = AddNumeric(parent, 132, y, 72, 0m, 255m, 0);
			max = AddNumeric(parent, 214, y, 72, 0m, 255m, 0);
			AddLabel(parent, "min / max", 296, y + 4, 84);
		}

		private static NumericUpDown AddSingleNumericRow(Control parent, string label, int y, decimal min, decimal max, int decimals)
		{
			AddLabel(parent, label, 12, y + 4, 112);
			return AddNumeric(parent, 132, y, 136, min, max, decimals);
		}

		private static NumericUpDown AddDoubleNumericRow(Control parent, string leftLabel, string rightLabel, int y, decimal min, decimal max, int decimals, out NumericUpDown rightNumeric)
		{
			AddLabel(parent, leftLabel, 12, y + 4, 70);
			NumericUpDown leftNumeric = AddNumeric(parent, 82, y, 72, min, max, decimals);
			AddLabel(parent, rightLabel, 172, y + 4, 70);
			rightNumeric = AddNumeric(parent, 242, y, 72, min, max, decimals);
			return leftNumeric;
		}

		private static NumericUpDown AddNumeric(Control parent, int x, int y, int w, decimal min, decimal max, int decimals)
		{
			NumericUpDown numeric = new NumericUpDown();
			numeric.SetBounds(x, y, w, 24);
			numeric.Minimum = min;
			numeric.Maximum = max;
			numeric.DecimalPlaces = decimals;
			numeric.ThousandsSeparator = false;
			numeric.Increment = decimals > 0 ? 0.1m : 1m;
			parent.Controls.Add(numeric);
			return numeric;
		}

		private static Label AddResultLabel(Control parent, string name, int x, int y, string initText)
		{
			Label label = new Label();
			label.Text = name + ": " + initText;
			label.SetBounds(x, y, 190, 24);
			label.TextAlign = ContentAlignment.MiddleLeft;
			parent.Controls.Add(label);
			return label;
		}

		private static decimal ToDecimal(double value, decimal min, decimal max)
		{
			decimal decimalValue = (decimal)value;
			if (decimalValue < min)
			{
				return min;
			}
			if (decimalValue > max)
			{
				return max;
			}
			return decimalValue;
		}

		private static string GetTaskError(Task task)
		{
			if (task != null && task.Exception != null && task.Exception.GetBaseException() != null)
			{
				return task.Exception.GetBaseException().Message;
			}
			return "未知错误";
		}
	}
}
