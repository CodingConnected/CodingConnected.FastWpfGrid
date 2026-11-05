using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using NUnit.Framework;

namespace CodingConnected.FastWpfGrid.Tests
{
    /// <summary>
    /// The grid used to listen to ScrollBar.Scroll, but WPF's ScrollBar handles shift+click on the
    /// track by assigning Value directly, without raising Scroll - the thumb moved and the grid
    /// did not. Listening to ValueChanged covers every way the value can change.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class ScrollBarTests
    {
        private sealed class TestModel : FastGridModelBase
        {
            public override int RowCount => 500;
            public override int ColumnCount => 100;
            public override string GetCellText(int row, int column) => $"r{row}c{column}";
        }

        private FastGridControl _grid;
        private Window _window;

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
        }

        [TearDown]
        public void TearDown()
        {
            _window?.Close();
            _window = null;
            _grid = null;
        }

        [Test]
        public void SettingTheHorizontalScrollBarValue_ScrollsTheGrid()
        {
            var hscroll = GetScrollBar("Hscroll");
            Assert.That(hscroll.Maximum, Is.GreaterThanOrEqualTo(10), "the model must be wider than the viewport");

            // What shift+click on the track does: assign Value, no Scroll event.
            hscroll.Value = 10;
            Pump(2);

            Assert.That(_grid.FirstVisibleColumnScrollIndex, Is.EqualTo(10));
        }

        [Test]
        public void SettingTheVerticalScrollBarValue_ScrollsTheGrid()
        {
            var vscroll = GetScrollBar("Vscroll");
            Assert.That(vscroll.Maximum, Is.GreaterThanOrEqualTo(10), "the model must be taller than the viewport");

            // What shift+click on the track does: assign Value, no Scroll event.
            vscroll.Value = 10;
            Pump(2);

            Assert.That(_grid.FirstVisibleRowScrollIndex, Is.EqualTo(10));
        }

        private ScrollBar GetScrollBar(string name) =>
            (ScrollBar)_grid.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(_grid);

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
