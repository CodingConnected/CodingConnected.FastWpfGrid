using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NUnit.Framework;

namespace CodingConnected.FastWpfGrid.Tests
{
    /// <summary>
    /// Regression tests for the render path. WPF requires an STA thread (thread affinity + a
    /// Dispatcher message pump), which is what <see cref="ApartmentAttribute"/> provides.
    /// The tests need a desktop session: they show an off-screen Window.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class RenderRecoveryTests
    {
        private sealed class TestModel : FastGridModelBase
        {
            public override int ColumnCount => 12;
            public override int RowCount => 60;
            public override string GetCellText(int row, int column) => (row * column).ToString();
        }

        private Window _window;
        private FastGridControl _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new FastGridControl
            {
                IsReadOnly = true,
                IsTransposed = true,
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
            Pump(5);
        }

        [TearDown]
        public void TearDown()
        {
            _window?.Close();
            _window = null;
            _grid = null;
        }

        [Test]
        public void Rendering_AfterResizes_UsesAnUnfrozenBuffer()
        {
            for (var i = 0; i < 40; i++)
            {
                _window.Width = 500 + (i * 37) % 600;
                _window.Height = 350 + (i * 23) % 400;
                Pump(1);
            }
            Pump(4);

            var buffer = GetDrawBuffer(_grid);
            Assert.That(buffer, Is.Not.Null, "the grid should have created a draw buffer");
            Assert.That(buffer.IsFrozen, Is.False, "the live draw buffer must never be frozen");
            Assert.That(buffer.PixelWidth, Is.GreaterThan(0));
            Assert.That(buffer.PixelHeight, Is.GreaterThan(0));
        }

        [Test]
        public void RenderFailure_IsRecovered_AndRaisesRenderFailed()
        {
            var original = GetDrawBuffer(_grid);
            Assert.That(original, Is.Not.Null);

            Exception reported = null;
            _grid.RenderFailed += ex => reported = ex;

            // Simulate the customer crash: the live buffer lost its native surface. Freezing a
            // WriteableBitmap disposes and nulls the native double-buffered bitmap, after which
            // Lock() throws instead of rendering.
            original.Freeze();

            _grid.InvalidateAll();
            Pump(8);

            var replacement = GetDrawBuffer(_grid);
            Assert.That(reported, Is.Not.Null, "RenderFailed should have been raised for the unusable buffer");
            Assert.That(replacement, Is.Not.Null, "the grid should have recreated the draw buffer");
            Assert.That(ReferenceEquals(replacement, original), Is.False, "the broken buffer must be replaced");
            Assert.That(replacement.IsFrozen, Is.False);
        }

        private static WriteableBitmap GetDrawBuffer(FastGridControl grid)
        {
            return (WriteableBitmap)typeof(FastGridControl)
                .GetField("_drawBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(grid);
        }

        /// <summary>Runs the Dispatcher until queued work (including Render-priority invalidations) is done.</summary>
        private static void Pump(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    (Action)(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
        }
    }
}
