using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NUnit.Framework;

namespace CodingConnected.FastWpfGrid.Tests
{
    /// <summary>
    /// Regression tests for GitHub issue #5: after a column is widened, clicking a column header
    /// (or a cell) to the right of it selects the wrong column.
    ///
    /// Cause: SeriesSizes.GetScrollIndexOnPosition measured from the START of the last size
    /// override instead of from its END, so the overridden (wider) size was counted as a whole
    /// number of default-sized items. Only grids that keep a sparse set of overrides are affected
    /// in practice: a wide grid (&gt; WideColumnsLimit real columns, e.g. a transposed grid) gets
    /// no per-column overrides from RecountColumnWidths, so a manual resize leaves exactly one.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class ColumnHitTestTests
    {
        private sealed class TestModel : FastGridModelBase
        {
            private readonly int _rowCount;
            private readonly int _columnCount;

            public TestModel(int rowCount, int columnCount)
            {
                _rowCount = rowCount;
                _columnCount = columnCount;
            }

            public override int RowCount => _rowCount;
            public override int ColumnCount => _columnCount;
            public override string GetCellText(int row, int column) => $"r{row}c{column}";
        }

        [TestCase(100, 1000, true, TestName = "Transposed grid (1000 rendered columns)")]
        [TestCase(300, 300, false, TestName = "300 rendered columns")]
        public void ClickingAHit_AfterWideningAColumn_SelectsTheRenderedColumn(int columns, int rows, bool transposed)
        {
            var grid = ShowGrid(rows, columns, transposed, out var window);
            try
            {
                // Premise: these grids are "wide", so only the resized column has a size override.
                Assert.That(grid.IsWide, Is.True, "this test targets the sparse-override (wide grid) case");

                // Widen an early column, leaving columns to its right on screen.
                ResizeColumn(grid, 2, 400);
                Pump(6);

                var columnSizes = GetField(grid, "_columnSizes");
                var getColumnLeft = grid.GetType().GetMethod("GetColumnLeft", BindingFlags.Instance | BindingFlags.NonPublic);
                var getSize = columnSizes.GetType().GetMethod("GetSizeByRealIndex");
                var realCount = (int)columnSizes.GetType().GetProperty("RealCount").GetValue(columnSizes);

                var checkedColumns = 0;
                for (var column = 0; column < realCount; column++)
                {
                    var left = (int)getColumnLeft.Invoke(grid, new object[] { column });
                    var size = (int)getSize.Invoke(columnSizes, new object[] { column });
                    var centerX = left + size / 2;
                    if (centerX > grid.GridScrollAreaWidth + grid.HeaderWidth) break;
                    if (size <= 0) continue;
                    checkedColumns++;

                    var headerHit = grid.GetCellAddress(new Point(centerX, grid.HeaderHeight / 2));
                    Assert.That(headerHit.Column, Is.EqualTo(column),
                        $"a point in column {column} (x={centerX}, left={left}, size={size}) must hit its column header");

                    var cellHit = grid.GetCellAddress(new Point(centerX, grid.HeaderHeight + 20));
                    Assert.That(cellHit.Column, Is.EqualTo(column),
                        $"a point in column {column} (x={centerX}, left={left}, size={size}) must hit its cells");
                }

                Assert.That(checkedColumns, Is.GreaterThan(5), "the widened column should still leave columns visible to its right");
            }
            finally
            {
                window.Close();
            }
        }

        private static void ResizeColumn(FastGridControl grid, int realIndex, int size)
        {
            // Same geometry sequence as the mouse drag in FastGridControl.OnMouseMove.
            var columnSizes = GetField(grid, "_columnSizes");
            columnSizes.GetType().GetMethod("Resize").Invoke(columnSizes, new object[] { realIndex, size });
            Invoke(grid, "AdjustScrollbars");
            Invoke(grid, "InvalidateAll");
        }

        private static FastGridControl ShowGrid(int rows, int columns, bool transposed, out Window window)
        {
            var grid = new FastGridControl
            {
                IsReadOnly = true,
                IsTransposed = transposed,
                CellPaddingHorizontal = 5,
                MinColumnWidthOverride = 35,
            };
            grid.Model = new TestModel(rows, columns);

            window = new Window
            {
                Width = 900,
                Height = 650,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = new Grid { Children = { grid } },
            };
            window.Show();
            Pump(6);
            return grid;
        }

        private static object GetField(object instance, string name) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        private static void Invoke(object instance, string name) =>
            instance.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(instance, null);

        private static void Pump(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
        }
    }
}
