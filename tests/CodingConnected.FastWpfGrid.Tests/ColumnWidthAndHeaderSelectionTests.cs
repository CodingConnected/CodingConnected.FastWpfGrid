using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NUnit.Framework;

namespace CodingConnected.FastWpfGrid.Tests
{
    /// <summary>
    /// Two defects found while investigating GitHub issue #5, both in the column-header path.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class ColumnWidthAndHeaderSelectionTests
    {
        private const int ModelColumnCount = 12;
        private const int ModelRowCount = 60;

        private sealed class TestModel : FastGridModelBase
        {
            public override int RowCount => ModelRowCount;
            public override int ColumnCount => ModelColumnCount;
            public override string GetCellText(int row, int column) => $"r{row}c{column}";
        }

        private FastGridControl _grid;
        private Window _window;
        private object _columnSizes;

        [SetUp]
        public void SetUp()
        {
            _grid = new FastGridControl
            {
                IsReadOnly = true,
                CellPaddingHorizontal = 5,
                MinColumnWidthOverride = 35,
            };
            _grid.Model = new TestModel();
            _window = new Window
            {
                Width = 900,
                Height = 650,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = new Grid { Children = { _grid } },
            };
            _window.Show();
            Pump(6);
            _columnSizes = GetField(_grid, "_columnSizes");
        }

        [TearDown]
        public void TearDown()
        {
            _window?.Close();
            _window = null;
            _grid = null;
        }

        [Test]
        public void ColumnWidthSetByDragging_SurvivesRecalculatingTheColumnWidths()
        {
            ResizeColumn(2, 400);
            Assert.That(GetColumnSize(2), Is.EqualTo(400));

            // A model refresh, a font size change or AdjustColumnSizes all land here.
            Invoke(_grid, "RecountColumnWidths");
            Pump(2);

            Assert.That(GetColumnSize(2), Is.EqualTo(400), "a width the user dragged must not be recalculated away");
        }

        [Test]
        public void DoubleClickAutoFit_ReplacesTheDraggedColumnWidth()
        {
            ResizeColumn(2, 400);
            // OnMouseDoubleClick removes the override before fitting the content.
            _columnSizes.GetType().GetMethod("RemoveSizeOverride").Invoke(_columnSizes, new object[] { 2 });

            Invoke(_grid, "RecountColumnWidths");
            Pump(2);

            Assert.That(GetColumnSize(2), Is.Not.EqualTo(400), "auto-fit must be able to override a dragged width");
        }

        [Test]
        public void SelectingAColumnHeader_SelectsEveryRowExactly()
        {
            var realRowCount = (int)GetField(_grid, "_rowSizes").GetType().GetProperty("RealCount").GetValue(GetField(_grid, "_rowSizes"));
            Assert.That(realRowCount, Is.EqualTo(ModelRowCount));

            var headerCell = new FastGridCellAddress(null, 3);
            var range = (IEnumerable<FastGridCellAddress>)Invoke(_grid, "GetCellRange", headerCell, headerCell);
            var cells = range.ToList();

            Assert.That(cells.Count, Is.EqualTo(ModelRowCount), "a column header selects every row, no more");
            Assert.That(cells.All(c => c.Column == 3), Is.True);
            Assert.That(cells.Max(c => c.Row.Value), Is.EqualTo(ModelRowCount - 1), "no row past the last one");
        }

        private int GetColumnSize(int realIndex) =>
            (int)_columnSizes.GetType().GetMethod("GetSizeByRealIndex").Invoke(_columnSizes, new object[] { realIndex });

        private void ResizeColumn(int realIndex, int size)
        {
            _columnSizes.GetType().GetMethod("Resize").Invoke(_columnSizes, new object[] { realIndex, size });
            Invoke(_grid, "AdjustScrollbars");
            Invoke(_grid, "InvalidateAll");
            Pump(2);
        }

        private static object GetField(object instance, string name) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        private static object Invoke(object instance, string name, params object[] args) =>
            instance.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(instance, args);

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
