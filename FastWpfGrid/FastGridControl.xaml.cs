﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FastWpfGrid
{
    /// <summary>
    /// Interaction logic for FastGridControl.xaml
    /// </summary>
    public partial class FastGridControl : UserControl, IFastGridView
    {
        //private double _lastvscroll;
        private IFastGridModel _model;



        private FastGridCellAddress _currentCell;
        private HashSet<FastGridCellAddress> _selectedCells = new HashSet<FastGridCellAddress>();
        private FastGridCellAddress _dragStartCell;
        private FastGridCellAddress _mouseOverCell;
        private bool _mouseOverCellIsTrimmed;
        private int? _mouseOverRow;
        private int? _mouseOverRowHeader;
        private int? _mouseOverColumnHeader;
        private FastGridCellAddress _inplaceEditorCell;

        private int _headerHeight;
        private int _headerWidth;
        private Dictionary<Tuple<bool, bool>, GlyphFont> _glyphFonts = new Dictionary<Tuple<bool, bool>, GlyphFont>();
        private Dictionary<Color, Brush> _solidBrushes = new Dictionary<Color, Brush>();
        private int _rowHeightReserve = 5;
        //private Color _headerBackground = Color.FromRgb(0xDD, 0xDD, 0xDD);
        private WriteableBitmap _drawBuffer;

        private bool _isTransposed;

        private int? _resizingColumn;
        private Point? _resizingColumnOrigin;
        private int? _resizingColumnStartSize;

        private static Dictionary<string, WriteableBitmap> _imageCache = new Dictionary<string, WriteableBitmap>();

        public FastGridControl()
        {
            InitializeComponent();
            //gridCore.Grid = this;
            CellFontSize = 11;
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
            int rowHeight = GetFont(false, false).TextHeight + CellPaddingVertical*2 + 2 + RowHeightReserve;
            int columnWidth = rowHeight*4;

            _rowSizes.DefaultSize = rowHeight;
            _columnSizes.DefaultSize = columnWidth;

            HeaderWidth = GetTextWidth("0000000", false, false);
            HeaderHeight = rowHeight;

            if (IsTransposed) CountTransposedHeaderWidth();

            InvalidateAll();
        }

        private void CountTransposedHeaderWidth()
        {
            int maxw = 0;
            for (int col = 0; col < _modelColumnCount; col++)
            {
                var cell = Model.GetColumnHeader(this, col);
                int width = GetCellContentWidth(cell) + 2*CellPaddingHorizontal;
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
            int rowIndex = _rowSizes.GetScrollIndexOnPosition((int) vscroll.Value);
            int columnIndex = _columnSizes.GetScrollIndexOnPosition((int) hscroll.Value);
            //FirstVisibleRow = rowIndex;
            //FirstVisibleColumn = columnIndex;
            //RenderGrid();
            ScrollContent(rowIndex, columnIndex);
            AdjustInlineEditorPosition();
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

        private void AdjustVerticalScrollBarRange()
        {
            vscroll.Maximum = _rowSizes.GetTotalScrollSizeSum() - GridScrollAreaHeight + _rowSizes.DefaultSize;
        }

        private void AdjustScrollbars()
        {
            hscroll.Minimum = 0;
            hscroll.Maximum = _columnSizes.GetTotalScrollSizeSum() - GridScrollAreaWidth + _columnSizes.DefaultSize;
            hscroll.ViewportSize = GridScrollAreaWidth;
            hscroll.SmallChange = GridScrollAreaWidth/10.0;
            hscroll.LargeChange = GridScrollAreaWidth/2.0;

            vscroll.Minimum = 0;
            AdjustVerticalScrollBarRange();
            vscroll.ViewportSize = GridScrollAreaHeight;
            vscroll.SmallChange = _rowSizes.DefaultSize;
            vscroll.LargeChange = GridScrollAreaHeight / 2.0;
        }

        private void AdjustScrollBarPositions()
        {
            hscroll.Value = _columnSizes.GetPositionByScrollIndex(FirstVisibleColumnScrollIndex); //FirstVisibleColumn* ColumnWidth;
            vscroll.Value = _rowSizes.GetPositionByScrollIndex(FirstVisibleRowScrollIndex);
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

            FixCurrentCellAndSetSelectionToCurrentCell();

            RecountColumnWidths();
            RecountRowHeights();
            AdjustScrollbars();
            InvalidateAll();
        }

        private void FixCurrentCellAndSetSelectionToCurrentCell()
        {
            int? col = _currentCell.Column;
            int? row = _currentCell.Row;

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

            _selectedCells.Clear();
            _currentCell = new FastGridCellAddress(row, col);
            if (_currentCell.IsCell) _selectedCells.Add(_currentCell);
            OnChangeSelectedCells();
        }

        private void RecountColumnWidths()
        {
            _columnSizes.Clear();
            if (_drawBuffer == null) return;
            if (GridScrollAreaWidth > 16) _columnSizes.MaxSize = GridScrollAreaWidth - 16;

            if (IsWide) return;
            if (_model == null) return;
            int rowCount = _isTransposed ? _modelColumnCount : _modelRowCount;
            int colCount = _isTransposed ? _modelRowCount : _modelColumnCount;

            for (int col = 0; col < colCount; col++)
            {
                var cell = _isTransposed ? _model.GetRowHeader(this, col) : _model.GetColumnHeader(this, col);
                _columnSizes.PutSizeOverride(col, GetCellContentWidth(cell) + 2 * CellPaddingHorizontal);
            }

            int visRows = VisibleRowCount;
            for (int row = 0; row < Math.Min(visRows, rowCount); row++)
            {
                for (int col = 0; col < colCount; col++)
                {
                    var cell = _isTransposed ? _model.GetCell(this, col, row) : _model.GetCell(this, row, col);
                    _columnSizes.PutSizeOverride(col, GetCellContentWidth(cell) + 2 * CellPaddingHorizontal);
                }
            }

            _columnSizes.BuildIndex();
        }

        private void RecountRowHeights()
        {
            _rowSizes.Clear();
            if (_drawBuffer == null) return;
            if (GridScrollAreaHeight > 16) _rowSizes.MaxSize = GridScrollAreaHeight - 16;

            CountVisibleRowHeights();
        }

        private bool CountVisibleRowHeights()
        {
            if (!FlexibleRows) return false;
            int colCount = _isTransposed ? _modelRowCount : _modelColumnCount;
            int rowCount = VisibleRowCount;
            bool changed = false;
            for (int row = FirstVisibleRowScrollIndex; row < FirstVisibleRowScrollIndex + rowCount; row++)
            {
                int modelRow = _rowSizes.RealToModel(row);
                if (_rowSizes.HasSizeOverride(modelRow)) continue;
                changed = true;
                for (int col = 0; col < colCount; col++)
                {
                    var cell = _isTransposed ? _model.GetCell(this, col, row) : _model.GetCell(this, row, col);
                    _rowSizes.PutSizeOverride(modelRow, GetCellContentHeight(cell) + 2*CellPaddingVertical + 2 + RowHeightReserve);
                }
            }
            _rowSizes.BuildIndex();
            AdjustVerticalScrollBarRange();
            return changed;
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


        private IFastGridCell GetColumnHeader(int col)
        {
            if (Model == null) return null;
            if (IsTransposed) return Model.GetRowHeader(this, _columnSizes.RealToModel(col));
            return Model.GetColumnHeader(this, _columnSizes.RealToModel(col));
        }

        private IFastGridCell GetRowHeader(int row)
        {
            if (Model == null) return null;
            if (IsTransposed) return Model.GetColumnHeader(this, _rowSizes.RealToModel(row));
            return Model.GetRowHeader(this, _rowSizes.RealToModel(row));
        }

        private IFastGridCell GetCell(int row, int col)
        {
            if (Model == null) return null;
            if (IsTransposed) return Model.GetCell(this, _columnSizes.RealToModel(col), _rowSizes.RealToModel(row));
            return Model.GetCell(this, _rowSizes.RealToModel(row), _columnSizes.RealToModel(col));
        }

        private IFastGridCell GetCell(FastGridCellAddress addr)
        {
            if (addr.IsCell) return GetCell(addr.Row.Value, addr.Column.Value);
            if (addr.IsRowHeader) return GetRowHeader(addr.Row.Value);
            if (addr.IsColumnHeader) return GetColumnHeader(addr.Column.Value);
            if (addr.IsGridHeader && _model != null) return _model.GetGridHeader(this);
            return null;
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragStartCell = new FastGridCellAddress();
            if (_resizingColumn.HasValue)
            {
                _resizingColumn = null;
                _resizingColumnOrigin = null;
                _resizingColumnStartSize = null;
                ReleaseMouseCapture();
            }
        }


        private void HideInlinEditor(bool saveCellValue = true)
        {
            using (var ctx = CreateInvalidationContext())
            {
                if (saveCellValue && _inplaceEditorCell.IsCell)
                {
                    var cell = GetCell(_inplaceEditorCell.Row.Value, _inplaceEditorCell.Column.Value);
                    cell.SetEditText(edText.Text);
                    InvalidateCell(_inplaceEditorCell);
                }
                _inplaceEditorCell = new FastGridCellAddress();
                edText.Text = "";
                edText.Visibility = Visibility.Hidden;
            }
            Keyboard.Focus(image);
        }

        private void ShowInlineEditor(FastGridCellAddress cell, string textValueOverride = null)
        {
            string text = GetCell(cell.Row.Value, cell.Column.Value).GetEditText();
            if (text == null) return;

            _inplaceEditorCell = cell;

            edText.Text = textValueOverride ?? text;
            edText.Visibility = Visibility.Visible;
            AdjustInlineEditorPosition();

            if (edText.IsFocused)
            {
                if (textValueOverride == null)
                {
                    edText.SelectAll();
                }
            }
            else
            {
                edText.Focus();
                if (textValueOverride == null)
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action) edText.SelectAll);
                }
            }

            if (textValueOverride != null)
            {
                edText.SelectionStart = textValueOverride.Length;
            }
        }

        private void AdjustInlineEditorPosition()
        {
            if (_inplaceEditorCell.IsCell)
            {
                edText.Visibility = _inplaceEditorCell.Row.Value - FirstVisibleRowScrollIndex >= 0 ? Visibility.Visible : Visibility.Hidden;
                var rect = GetCellRect(_inplaceEditorCell.Row.Value, _inplaceEditorCell.Column.Value);
                edText.Margin = new Thickness
                    {
                        Left = rect.Left,
                        Top = rect.Top,
                        Right = imageGrid.ActualWidth - rect.Right,
                        Bottom = imageGrid.ActualHeight - rect.Bottom,
                    };
            }
        }


        private void InvalidateCurrentCell()
        {
            if (_currentCell.IsCell) InvalidateCell(_currentCell);
            if (_currentCell.Column.HasValue) InvalidateColumnHeader(_currentCell.Column.Value);
            if (_currentCell.Row.HasValue) InvalidateRowHeader(_currentCell.Row.Value);
        }

        private void SetCurrentCell(FastGridCellAddress cell)
        {
            using (var ctx = CreateInvalidationContext())
            {
                InvalidateCurrentCell();
                _currentCell = cell;
                InvalidateCurrentCell();
            }
        }

        private HashSet<FastGridCellAddress> GetCellRange(FastGridCellAddress a, FastGridCellAddress b)
        {
            var res = new HashSet<FastGridCellAddress>();
            int minrow = Math.Min(a.Row.Value, b.Row.Value);
            int maxrow = Math.Max(a.Row.Value, b.Row.Value);
            int mincol = Math.Min(a.Column.Value, b.Column.Value);
            int maxcol = Math.Max(a.Column.Value, b.Column.Value);

            for (int row = minrow; row <= maxrow; row++)
            {
                for (int col = mincol; col <= maxcol; col++)
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
            bool wasEmpty = _drawBuffer == null;
            int width = (int) imageGrid.ActualWidth - 2;
            int height = (int) imageGrid.ActualHeight - 2;
            if (width > 0 && height > 0)
            {
                _drawBuffer = BitmapFactory.New(width, height);
            }
            else
            {
                _drawBuffer = null;
            }
            image.Source = _drawBuffer;
            image.Width = Math.Max(0, width);
            image.Height = Math.Max(0, height);

            if (wasEmpty && _drawBuffer != null)
            {
                RecountColumnWidths();
                RecountRowHeights();
            }
            AdjustScrollbars();
            InvalidateAll();
        }

        private void MoveCurrentCell(int? row, int? col, KeyEventArgs e = null)
        {
            if (e != null) e.Handled = true;
            _selectedCells.ToList().ForEach(InvalidateCell);
            _selectedCells.Clear();

            InvalidateCurrentCell();

            if (row < 0) row = 0;
            if (row >= _modelRowCount) row = _modelRowCount - 1;
            if (col < 0) col = 0;
            if (col >= _modelColumnCount) col = _modelColumnCount - 1;

            _currentCell = new FastGridCellAddress(row, col);
            if (_currentCell.IsCell) _selectedCells.Add(_currentCell);
            InvalidateCurrentCell();
            ScrollCurrentCellIntoView();
            OnChangeSelectedCells();
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

        public HashSet<FastGridCellAddress> SelectedCells
        {
            get { return _selectedCells; }
        }
    }
}
