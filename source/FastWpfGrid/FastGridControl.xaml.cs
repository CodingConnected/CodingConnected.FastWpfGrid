using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodingConnected.FastWpfGrid
{
    /// <summary>
    /// Interaction logic for FastGridControl.xaml
    /// </summary>
    public partial class FastGridControl : UserControl, IFastGridView
    {
        //private double _lastvscroll;
        private IFastGridModel _model;

        private FastGridCellAddress _currentCell;

        private int _headerHeight;
        private int _headerWidth;
        private readonly Dictionary<Tuple<bool, bool>, GlyphFont> _glyphFonts = new Dictionary<Tuple<bool, bool>, GlyphFont>();
        private readonly Dictionary<Color, Brush> _solidBrushes = new Dictionary<Color, Brush>();
        private int _rowHeightReserve = 5;
        //private Color _headerBackground = Color.FromRgb(0xDD, 0xDD, 0xDD);
        private WriteableBitmap _drawBuffer;

        private bool _isTransposed;

        private bool _isReadOnly;

        private static readonly Dictionary<string, ImageHolder> _imageCache = new Dictionary<string, ImageHolder>();


        public FastGridModelBase ModelSource
        {
            get => (FastGridModelBase)GetValue(ModelSourceProperty);
            set => SetValue(ModelSourceProperty, value);
        }

        public static readonly DependencyProperty ModelSourceProperty =
            DependencyProperty.Register(nameof(ModelSource), typeof(FastGridModelBase), typeof(FastGridControl), new PropertyMetadata(null, OnModelSourceChanged));

        public static void OnModelSourceChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var ct = (FastGridControl)sender;
            if (ct != null)
            {
                ct.Model = (FastGridModelBase)e.NewValue;
            }
        }



        public FastGridControl()
        {
            InitializeComponent();
            //gridCore.Grid = this;
            CellFontSize = 11;
            _dragTimer = new DispatcherTimer
            {
                IsEnabled = false,
                Interval = TimeSpan.FromSeconds(0.05)
            };
            _dragTimer.Tick += _dragTimer_Tick;
            AllowSelectAll = true;
            Loaded += (sender, args) =>
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    _scaleX = 96.0 * source.CompositionTarget.TransformToDevice.M11 / 96.0;
                    _scaleY = 96.0 * source.CompositionTarget.TransformToDevice.M22 / 96.0;
                }
                SetScrollbarMargin();
            };
        }

        private double _scaleX = 1.0;
        private double _scaleY = 1.0;

        public bool AllowSelectAll { get; set; }

        /// <summary>
        /// Prevents the inline editor from being used if the control is in a read-only state.
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => _isReadOnly = value;
        }

        public GlyphFont GetFont(bool isBold, bool isItalic)
        {
            var key = Tuple.Create(isBold, isItalic);
            if (!_glyphFonts.ContainsKey(key))
            {
                var font = LetterGlyphTool.GetFont(new PortableFontDesc(CellFontName, CellFontSize, isBold, isItalic, UseClearType));
                _glyphFonts[key] = font;
            }
            return _glyphFonts[key];
        }

        public void ClearCaches()
        {
            _glyphFonts.Clear();
        }

        public int GetTextWidth(string text, bool isBold, bool isItalic)
        {
            return GetFont(isBold, isItalic).GetTextWidth(text);
            //double size = CellFontSize;
            //int totalWidth = 0;
            //var glyphTypeface = GetFont(isBold, isItalic);

            //for (int n = 0; n < text.Length; n++)
            //{
            //    ushort glyphIndex = glyphTypeface.CharacterToGlyphMap[text[n]];
            //    double width = Math.Round(glyphTypeface.AdvanceWidths[glyphIndex] * size);
            //    totalWidth += (int)width;
            //}
            //return totalWidth;
        }

        private void RecalculateDefaultCellSize()
        {
            ClearCaches();
            var rowHeight = GetFont(false, false).TextHeight + CellPaddingVertical*2 + 2 + RowHeightReserve;
            var columnWidth = MinColumnWidthOverride ?? rowHeight*4;

            _rowSizes.DefaultSize = rowHeight;
            _columnSizes.DefaultSize = columnWidth;

            RecalculateHeaderSize();

            InvalidateAll();
        }

        private void RecalculateHeaderSize()
        {
            HeaderWidth = GetTextWidth("0000000", false, false);
            HeaderHeight = _rowSizes.DefaultSize;

            if (IsTransposed) CountTransposedHeaderWidth();
            if (Model != null)
            {
                var width = GetCellContentWidth(Model.GetGridHeader(this));
                if (width + 2 * CellPaddingHorizontal > HeaderWidth) 
                    HeaderWidth = width + 2 * CellPaddingHorizontal;
                //var hWidth = 0.0;
                //for (int i = 0; i < Model.RowCount; i++)
                //{
                //    var header = Model.GetRowHeader(this, i);
                //    var w = GetTextWidth(header.GetBlock(0).TextData, false, false);
                //    if (w > hWidth) hWidth = w;
                //}
                //HeaderWidth = (int)hWidth;
                //HeaderHeight = _rowSizes.DefaultSize;
            }
        }

        private void CountTransposedHeaderWidth()
        {
            var maxw = 0;
            for (var col = 0; col < _modelColumnCount; col++)
            {
                var cell = Model.GetColumnHeader(this, col);
                var width = GetCellContentWidth(cell) + 2*CellPaddingHorizontal;
                if (width > maxw) maxw = width;
            }
            HeaderWidth = maxw;
        }

        //public int RowHeight
        //{
        //    get { return _rowHeight; }
        //}

        //public int ColumnWidth
        //{
        //    get { return _columnWidth; }
        //}

        private void ScrollChanged()
        {
            //int rowIndex = _rowSizes.GetScrollIndexOnPosition((int) vscroll.Value);
            //int columnIndex = _columnSizes.GetScrollIndexOnPosition((int) hscroll.Value);

            var rowIndex = (int) Math.Round(Vscroll.Value);
            var columnIndex = (int) Math.Round(Hscroll.Value);

            //FirstVisibleRow = rowIndex;
            //FirstVisibleColumn = columnIndex;
            //RenderGrid();
            ScrollContent(rowIndex, columnIndex);
            AdjustInlineEditorPosition();
            AdjustSelectionMenuPosition();
        }


        public Color GetAlternateBackground(int row)
        {
            return _alternatingColors[row%_alternatingColors.Length];
        }

        private void hscroll_Scroll(object sender, ScrollEventArgs e)
        {
            ScrollChanged();
        }

        private void vscroll_Scroll(object sender, ScrollEventArgs e)
        {
            ScrollChanged();
        }

        private void OnModelPropertyChanged()
        {
            if (_model != null) _model.DetachView(this);
            _model = Model;
            if (_model != null) _model.AttachView(this);
            NotifyRefresh();
        }


        public int GridScrollAreaWidth
        {
            get
            {
                if (_drawBuffer == null) return 1;
                return _drawBuffer.PixelWidth - HeaderWidth - FrozenWidth;
            }
        }

        public int GridScrollAreaHeight
        {
            get
            {
                if (_drawBuffer == null) return 1;
                return _drawBuffer.PixelHeight - HeaderHeight - FrozenHeight;
            }
        }

        //private void AdjustVerticalScrollBarRange()
        //{
        //    //vscroll.Maximum = _rowSizes.GetTotalScrollSizeSum() - GridScrollAreaHeight + _rowSizes.DefaultSize;

        //}

        private void AdjustScrollbars()
        {
            Hscroll.Minimum = 0;
            //hscroll.Maximum = _columnSizes.GetTotalScrollSizeSum() - GridScrollAreaWidth + _columnSizes.DefaultSize;

            // hscroll.Maximum = _columnSizes.ScrollCount - 1;

            Hscroll.Maximum = Math.Min(
                _columnSizes.ScrollCount - 1,
                _columnSizes.ScrollCount - _columnSizes.GetVisibleScrollCountReversed(_columnSizes.ScrollCount - 1, GridScrollAreaWidth) + 1);
            Hscroll.ViewportSize = VisibleColumnCount; //GridScrollAreaWidth;
            Hscroll.SmallChange = 1; // GridScrollAreaWidth / 10.0;
            Hscroll.LargeChange = 3; // GridScrollAreaWidth / 2.0;

            Hscroll.Visibility = VisibleColumnCount - _columnSizes.ScrollCount >= Hscroll.Maximum ? Visibility.Collapsed : Visibility.Visible;
            
            Vscroll.Minimum = 0;
            if (FlexibleRows)
            {
                Vscroll.Maximum = _rowSizes.ScrollCount - 1;
            }
            else
            {
                Vscroll.Maximum = _rowSizes.ScrollCount - (GridScrollAreaHeight/(_rowSizes.DefaultSize + 1)) + 1;
            }
            Vscroll.ViewportSize = VisibleRowCount;
            Vscroll.SmallChange = 1;
            Vscroll.LargeChange = 10;
            
            Vscroll.Visibility = VisibleRowCount - _rowSizes.ScrollCount >= Vscroll.Maximum ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AdjustScrollBarPositions()
        {
            //hscroll.Value = _columnSizes.GetPositionByScrollIndex(FirstVisibleColumnScrollIndex); //FirstVisibleColumn* ColumnWidth;
            //vscroll.Value = _rowSizes.GetPositionByScrollIndex(FirstVisibleRowScrollIndex);
            Hscroll.Value = FirstVisibleColumnScrollIndex;
            Vscroll.Value = FirstVisibleRowScrollIndex;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            AdjustScrollbars();
        }

        public void NotifyRefresh()
        {
            _modelRowCount = 0;
            _modelColumnCount = 0;
            if (_model != null)
            {
                _modelRowCount = _model.RowCount;
                _modelColumnCount = _model.ColumnCount;
            }

            UpdateSeriesCounts();
            RecalculateHeaderSize();
            FixCurrentCellAndSetSelectionToCurrentCell();

            RecountColumnWidths();
            RecountRowHeights();
            AdjustScrollbars();
            SetScrollbarMargin();
            FixScrollPosition();
            InvalidateAll();
        }

        private void FixCurrentCellAndSetSelectionToCurrentCell()
        {
            var col = _currentCell.Column;
            var row = _currentCell.Row;

            if (col.HasValue)
            {
                if (col >= _modelColumnCount) col = _modelColumnCount - 1;
                if (col < 0) col = null;
            }

            if (row.HasValue)
            {
                if (row >= _modelRowCount) row = _modelRowCount - 1;
                if (row < 0) row = null;
            }

            ClearSelectedCells();
            _currentCell = new FastGridCellAddress(row, col);
            if (_currentCell.IsCell) AddSelectedCell(_currentCell);
            OnChangeSelectedCells(false);
        }

        public void NotifyAddedRows()
        {
            NotifyRefresh();
        }

        public Brush GetSolidBrush(Color color)
        {
            if (!_solidBrushes.ContainsKey(color))
            {
                _solidBrushes[color] = new SolidColorBrush(color);
            }
            return _solidBrushes[color];
        }


        private IFastGridCell GetModelRowHeader(int row)
        {
            if (_model == null) return null;
            if (row < 0 || row >= _modelRowCount) return null;
            return _model.GetRowHeader(this, row);
        }

        private IFastGridCell GetModelColumnHeader(int col)
        {
            if (_model == null) return null;
            if (col < 0 || col >= _modelColumnCount) return null;
            return _model.GetColumnHeader(this, col);
        }

        private IFastGridCell GetModelCell(int row, int col)
        {
            if (_model == null) return null;
            if (row < 0 || row >= _modelRowCount) return null;
            if (col < 0 || col >= _modelColumnCount) return null;
            return _model.GetCell(this, row, col);
        }

        private IFastGridCell GetColumnHeader(int col)
        {
            if (IsTransposed) return GetModelRowHeader(_columnSizes.RealToModel(col));
            return GetModelColumnHeader(_columnSizes.RealToModel(col));
        }

        private IFastGridCell GetRowHeader(int row)
        {
            if (IsTransposed) return GetModelColumnHeader(_rowSizes.RealToModel(row));
            return GetModelRowHeader(_rowSizes.RealToModel(row));
        }

        private IFastGridCell GetCell(int row, int col)
        {
            if (IsTransposed) return GetModelCell(_columnSizes.RealToModel(col), _rowSizes.RealToModel(row));
            return GetModelCell(_rowSizes.RealToModel(row), _columnSizes.RealToModel(col));
        }

        private IFastGridCell GetCell(FastGridCellAddress addr)
        {
            if (addr.IsCell) return GetCell(addr.Row.Value, addr.Column.Value);
            if (addr.IsRowHeader) return GetRowHeader(addr.Row.Value);
            if (addr.IsColumnHeader) return GetColumnHeader(addr.Column.Value);
            if (addr.IsGridHeader && _model != null) return _model.GetGridHeader(this);
            return null;
        }

        public void HideInlineEditor(bool saveCellValue = true)
        {
            using (var ctx = CreateInvalidationContext())
            {
                if (saveCellValue && _inplaceEditorCell.IsCell && _inlineTextChanged)
                {
                    var cell = GetCell(_inplaceEditorCell.Row.Value, _inplaceEditorCell.Column.Value);
                    cell.SetEditText(EdText.Text);
                    InvalidateCell(_inplaceEditorCell);
                }
                _inplaceEditorCell = new FastGridCellAddress();
                EdText.Text = "";
                EdText.Visibility = Visibility.Hidden;
            }
            Keyboard.Focus(Image);
        }

        private void ShowInlineEditor(FastGridCellAddress cell, string textValueOverride = null)
        {
            if (_isReadOnly) return;
            if (!cell.IsCell) return;
            var cellObj = GetCell(cell.Row.Value, cell.Column.Value);
            if (cellObj == null) return;
            var text = cellObj.GetEditText();
            if (text == null) return;

            _inplaceEditorCell = cell;

            EdText.Text = textValueOverride ?? text;
            EdText.Visibility = Visibility.Visible;
            AdjustInlineEditorPosition();

            if (EdText.IsFocused)
            {
                if (textValueOverride == null)
                {
                    EdText.SelectAll();
                }
            }
            else
            {
                EdText.Focus();
                if (textValueOverride == null)
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action) EdText.SelectAll);
                }
            }

            if (textValueOverride != null)
            {
                EdText.SelectionStart = textValueOverride.Length;
            }

            _inlineTextChanged = !String.IsNullOrEmpty(textValueOverride);
        }

        private void AdjustInlineEditorPosition()
        {
            if (_inplaceEditorCell.IsCell)
            {
                var visible = _rowSizes.IsVisible(_inplaceEditorCell.Row.Value, FirstVisibleRowScrollIndex, GridScrollAreaHeight)
                              && _columnSizes.IsVisible(_inplaceEditorCell.Column.Value, FirstVisibleColumnScrollIndex, GridScrollAreaWidth);
                EdText.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
                var rect = GetCellRect(_inplaceEditorCell.Row.Value, _inplaceEditorCell.Column.Value);

                EdText.Margin = new Thickness
                    {
                        Left = rect.Left / DpiDetector.DpiXKoef,
                        Top = rect.Top / DpiDetector.DpiYKoef,
                        Right = ImageGrid.ActualWidth - rect.Right / DpiDetector.DpiXKoef,
                        Bottom = ImageGrid.ActualHeight - rect.Bottom / DpiDetector.DpiYKoef,
                    };
            }
        }

        private void AdjustSelectionMenuPosition()
        {
            var maxaddr = FastGridCellAddress.Empty;

            foreach(var addr in _selectedCells)
            {
                if (!addr.IsCell) continue;
                if (!maxaddr.IsCell) maxaddr = addr;
                if (addr.Row.Value + addr.Column.Value > maxaddr.Row.Value + maxaddr.Column.Value) maxaddr = addr;
            }

            if (!maxaddr.IsCell) return;

            var left = GetColumnLeft(maxaddr.Column.Value);
            var top = GetRowTop(maxaddr.Row.Value + 1);

            MenuSelection.Margin = new Thickness
            {
                Left = left / DpiDetector.DpiXKoef,
                Top = top / DpiDetector.DpiYKoef,
            };
        }

        private void InvalidateCurrentCell()
        {
            if (_currentCell.IsCell) InvalidateCell(_currentCell);
            if (_currentCell.Column.HasValue) InvalidateColumnHeader(_currentCell.Column.Value);
            if (_currentCell.Row.HasValue) InvalidateRowHeader(_currentCell.Row.Value);
        }

        private void SetCurrentCell(FastGridCellAddress cell)
        {
            if (cell.IsRowHeader && _currentCell.IsCell) cell = new FastGridCellAddress(cell.Row, _currentCell.Column);
            if (cell.IsColumnHeader && _currentCell.IsCell) cell = new FastGridCellAddress(_currentCell.Row, cell.Column);

            using (var ctx = CreateInvalidationContext())
            {
                InvalidateCurrentCell();
                _currentCell = cell;
                InvalidateCurrentCell();
            }
        }

        /// returns cell range. 
        /// if a is row header, returns full rows.
        /// if a is column header, returns full columns
        private HashSet<FastGridCellAddress> GetCellRange(FastGridCellAddress a, FastGridCellAddress b)
        {
            var res = new HashSet<FastGridCellAddress>();

            int minrow;
            int maxrow;
            int mincol;
            int maxcol;

            if (a.IsRowHeader)
            {
                mincol = 0;
                maxcol = _columnSizes.RealCount - 1;
            }
            else
            {
                if (a.Column == null || b.Column == null) return res;
                mincol = Math.Min(a.Column.Value, b.Column.Value);
                maxcol = Math.Max(a.Column.Value, b.Column.Value);
            }

            if (a.IsColumnHeader)
            {
                minrow = 0;
                maxrow = _rowSizes.RealCount;
            }
            else
            {
                if (a.Row == null || b.Row == null) return res;
                minrow = Math.Min(a.Row.Value, b.Row.Value);
                maxrow = Math.Max(a.Row.Value, b.Row.Value);
            }

            const int LIMIT_RESERVE = 3;
            if (SelectedRealRowCountLimit.HasValue && maxrow - minrow > SelectedRealRowCountLimit.Value + LIMIT_RESERVE)
            {
                maxrow = minrow + SelectedRealRowCountLimit.Value + LIMIT_RESERVE;
            }
            if (SelectedRealColumnCountLimit.HasValue && maxcol - mincol > SelectedRealColumnCountLimit.Value + LIMIT_RESERVE)
            {
                maxcol = mincol + SelectedRealColumnCountLimit.Value + LIMIT_RESERVE;
            }

            for (var row = minrow; row <= maxrow; row++)
            {
                for (var col = mincol; col <= maxcol; col++)
                {
                    res.Add(new FastGridCellAddress(row, col));
                }
            }
            return res;
        }
        

        private void SetHoverRow(int? row)
        {
            if (row == _mouseOverRow) return;
            using (var ctx = CreateInvalidationContext())
            {
                if (_mouseOverRow.HasValue) InvalidateRow(_mouseOverRow.Value);
                _mouseOverRow = row;
                if (_mouseOverRow.HasValue) InvalidateRow(_mouseOverRow.Value);
            }
        }

        private void SetHoverColumn(int? col)
        {
            if (col == _mouseOverColumn) return;
            using (var ctx = CreateInvalidationContext())
            {
                if (_mouseOverColumn.HasValue) InvalidateColumn(_mouseOverColumn.Value);
                _mouseOverColumn = col;
                if (_mouseOverColumn.HasValue) InvalidateColumn(_mouseOverColumn.Value);
            }
        }

        private void SetHoverRowHeader(int? row)
        {
            if (row == _mouseOverRowHeader) return;
            using (var ctx = CreateInvalidationContext())
            {
                if (_mouseOverRowHeader.HasValue) InvalidateRowHeader(_mouseOverRowHeader.Value);
                _mouseOverRowHeader = row;
                if (_mouseOverRowHeader.HasValue) InvalidateRow(_mouseOverRowHeader.Value);
            }
        }

        private void SetHoverColumnHeader(int? column)
        {
            if (column == _mouseOverColumnHeader) return;
            using (var ctx = CreateInvalidationContext())
            {
                if (_mouseOverColumnHeader.HasValue) InvalidateColumnHeader(_mouseOverColumnHeader.Value);
                _mouseOverColumnHeader = column;
                if (_mouseOverColumnHeader.HasValue) InvalidateColumn(_mouseOverColumnHeader.Value);
            }
        }

        private void SetHoverCell(FastGridCellAddress cell)
        {
            if (cell == _mouseOverCell) return;
            using (var ctx = CreateInvalidationContext())
            {
                if (!_mouseOverCell.IsEmpty) InvalidateCell(_mouseOverCell);
                _mouseOverCell = cell.IsEmpty ? FastGridCellAddress.Empty : cell;
                if (!_mouseOverCell.IsEmpty) InvalidateCell(_mouseOverCell);
            }
        }

        private void imageGridResized(object sender, SizeChangedEventArgs e)
        {
            var wasEmpty = _drawBuffer == null;
            var width = (int) ImageGrid.ActualWidth - 2;
            var height = (int) ImageGrid.ActualHeight - 2;
            if (width > 0 && height > 0)
            {
				//To avoid flicker (blank image) while resizing, crop the current buffer and set it as the image source instead of using a new one.
				//This will be shown during the refresh.
				var pixelWidth = (int) Math.Ceiling(width*DpiDetector.DpiXKoef);
	            var pixelHeight = (int) Math.Ceiling(height*DpiDetector.DpiYKoef);
	            if (_drawBuffer == null)
	            {
		            _drawBuffer = BitmapFactory.New(pixelWidth, pixelHeight);
	            }
	            else
	            {
		            var oldBuffer = _drawBuffer;
		            _drawBuffer = oldBuffer.Crop(0, 0, pixelWidth, pixelHeight);
					
					//The unmanaged memory when crating new WritableBitmaps doesn't reliably garbage collect and can still cause out of memory exceptions
					//Profiling revealed handles on the object that aren't able to be collected.
					//Freezing the object removes all handles and should help in garbage collection.
					oldBuffer.Freeze();
	            }
            }
            else
            {
				_drawBuffer = null;
            }
            Image.Source = _drawBuffer;
            Image.Margin = new Thickness(0);
            Image.Width = Math.Max(0, width);
            Image.Height = Math.Max(0, height);

            //var screenPos = imageGrid.PointToScreen(new Point(0, 0));
            //double fracX = screenPos.X - Math.Truncate(screenPos.X);
            //double fracY = screenPos.Y - Math.Truncate(screenPos.Y);
            //double dleft = 1 - fracX;
            //double dtop = 1 - fracY;
            //if (fracX == 0) dleft = 0;
            //if (fracY == 0) dtop = 0;
            //image.Margin = new Thickness(dleft, dtop, imageGrid.ActualWidth - width - dleft - 1, imageGrid.ActualHeight - height - dtop - 25);

            if (wasEmpty && _drawBuffer != null)
            {
                RecountColumnWidths();
                RecountRowHeights();
            }
            AdjustScrollbars();
            InvalidateAll();
        }

        private bool MoveCurrentCell(int? row, int? col, KeyEventArgs e = null)
        {
            if (e != null) e.Handled = true;
            if (!ShiftPressed)
            {
                _selectedCells.ToList().ForEach(InvalidateCell);
                ClearSelectedCells();
            }

            InvalidateCurrentCell();

            if (row < 0) row = 0;
            if (row >= _realRowCount) row = _realRowCount - 1;
            if (col < 0) col = 0;
            if (col >= _realColumnCount) col = _realColumnCount - 1;

            _currentCell = new FastGridCellAddress(row, col);
            if (_currentCell.IsCell) AddSelectedCell(_currentCell);
            InvalidateCurrentCell();
            ScrollCurrentCellIntoView();
            OnChangeSelectedCells(true);
            return true;
        }

        private void RenderChanged()
        {
            InvalidateAll();
        }

        private void OnUseClearTypePropertyChanged()
        {
            ClearCaches();
            RecalculateDefaultCellSize();
            RenderChanged();
        }

        private bool IsModelCellInValidRange(FastGridCellAddress cell)
        {
            if (cell.Row.HasValue && (cell.Row.Value < 0 || cell.Row.Value >= _modelRowCount)) return false;
            if (cell.Column.HasValue && (cell.Column.Value < 0 || cell.Column.Value >= _modelColumnCount)) return false;
            return true;
        }

        public HashSet<FastGridCellAddress> GetSelectedModelCells()
        {
            var res = new HashSet<FastGridCellAddress>();
            foreach (var cell in _selectedCells)
            {
                var cellModel = RealToModel(cell);
                if (cellModel.IsCell && IsModelCellInValidRange(cellModel)) res.Add(cellModel);
            }
            return res;
        }

        public FastGridCellAddress CurrentModelCell
        {
            get => RealToModel(CurrentCell);
            set => CurrentCell = ModelToReal(value);
        }

        public void ShowSelectionMenu(IEnumerable<string> commands)
        {
            if (commands == null)
            {
                MenuSelection.ItemsSource = null;
                MenuSelection.Visibility = Visibility.Hidden;
            }
            else
            {
                MenuSelection.ItemsSource = commands.Select(x => new SelectionQuickCommand(Model, x)).ToList();
                MenuSelection.Visibility = Visibility.Visible;
                AdjustSelectionMenuPosition();
            }
        }

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.C && Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                var view = (IFastGridView)sender;
                var fastGridControl = (FastGridControl)sender;
                var selection = fastGridControl.GetSelectedModelCells();
                if (selection.Count > 1)
                {
                    var minRow = selection.Min(x => x.Row);
                    var maxRow = selection.Max(x => x.Row);
                    if (!minRow.HasValue || !maxRow.HasValue) return;
                    var selectionRowCount = maxRow.Value - minRow.Value + 1;
                    var minCol = selection.Min(x => x.Column);
                    var maxCol = selection.Max(x => x.Column);
                    if (!minCol.HasValue || !maxCol.HasValue) return;
                    var selectionColCount = maxCol.Value - minCol.Value + 1;

                    var id1 = IsTransposed ? selectionColCount + 1 : selectionRowCount + 1;
                    var id2 = IsTransposed ? selectionRowCount + 1 : selectionColCount + 1;
                    
                    var data = new string[id1, id2];

                    var sb = new StringBuilder();
                    var model = fastGridControl.Model;
                    for (var c = minCol.Value; c <= maxCol.Value; c++)
                    {
                        if (IsTransposed) data[c - minCol.Value + 1, 0] = _model.GetColumnHeader(this, c).GetBlock(0).TextData;
                        else data[0, c - minCol.Value + 1] = _model.GetColumnHeader(this, c).GetBlock(0).TextData;
                    }
                    for (var r = minRow.Value; r <= maxRow.Value; r++)
                    {
                        if (IsTransposed) data[0, r - minRow.Value + 1] = _model.GetRowHeader(this, r).GetBlock(0).TextData;
                        else data[r - minRow.Value + 1, 0] = _model.GetRowHeader(this, r).GetBlock(0).TextData;
                    }
                    foreach (var cell in selection)
                    {
                        if (!cell.Row.HasValue || !cell.Column.HasValue) continue;
                        var da = model.GetCell(view, cell.Row.Value, cell.Column.Value);
                        if (IsTransposed)
                        {
                            data[cell.Column.Value - minCol.Value + 1, cell.Row.Value - minRow.Value + 1] = da.GetBlock(0).TextData;
                        }
                        else
                        {
                            data[cell.Row.Value - minRow.Value + 1, cell.Column.Value - minCol.Value + 1] = da.GetBlock(0).TextData;
                        }
                    }

                    for (var i = 0; i < data.GetLength(0); i++)
                    {
                        for (var j = 0; j < data.GetLength(1); j++)
                        {
                            if (j != 0) sb.Append("\t");
                            sb.Append(data[i, j]);
                        }

                        sb.AppendLine();
                    }

                    Clipboard.SetText(sb.ToString());
                }
            }
        }
    }
}
