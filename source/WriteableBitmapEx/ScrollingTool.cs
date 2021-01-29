using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace System.Windows.Media.Imaging
{
    public static class ScrollingTool
    {
        /// <summary>
        /// scrolls content of given rectangle
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="dy">if greater than 0, scrolls down, else scrolls up</param>
        /// <param name="rect"></param>
        public static unsafe void ScrollY(this WriteableBitmap bmp, int dy, IntRect rect, Color? background = null)
        {
            var bgcolor = WriteableBitmapExtensions.ConvertColor(background ?? Colors.White);

            using (var context = bmp.GetBitmapContext())
            {
                // Use refs for faster access (really important!) speeds up a lot!
                var w = context.Width;
                var h = context.Height;
                var pixels = context.Pixels;
                var xmin = rect.Left;
                var ymin = rect.Top;
                var xmax = rect.Right;
                var ymax = rect.Bottom;

                if (xmin < 0) xmin = 0;
                if (ymin < 0) ymin = 0;
                if (xmax >= w) xmax = w - 1;
                if (ymax >= h) ymax = h - 1;
                var xcnt = xmax - xmin + 1;
                var ycnt = ymax - ymin + 1;
                if (xcnt <= 0) return;

                if (dy > 0)
                {
                    for (var y = ymax; y >= ymin + dy; y--)
                    {
                        var ydstidex = y;
                        var ysrcindex = y - dy;
                        if (ysrcindex < ymin || ysrcindex > ymax) continue;

                        NativeMethods.memcpy(pixels + ydstidex*w + xmin, pixels + ysrcindex*w + xmin, xcnt*4);
                    }
                }
                if (dy < 0)
                {
                    for (var y = ymin; y <= ymax - dy; y++)
                    {
                        var ysrcindex = y - dy;
                        var ydstidex = y;
                        if (ysrcindex < ymin || ysrcindex > ymax) continue;
                        NativeMethods.memcpy(pixels + ydstidex*w + xmin, pixels + ysrcindex*w + xmin, xcnt*4);
                    }
                }

                if (dy < 0)
                {
                    bmp.FillRectangle(xmin, ymax + dy + 1, xmax, ymax, bgcolor);
                }
                if (dy > 0)
                {
                    bmp.FillRectangle(xmin, ymin, xmax, ymin + dy - 1, bgcolor);
                }
            }
        }

        /// <summary>
        /// scrolls content of given rectangle
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="dx">if greater than 0, scrolls right, else scrolls left</param>
        /// <param name="rect"></param>
        public static unsafe void ScrollX(this WriteableBitmap bmp, int dx, IntRect rect, Color? background = null)
        {
            var bgcolor = WriteableBitmapExtensions.ConvertColor(background ?? Colors.White);
            using (var context = bmp.GetBitmapContext())
            {
                // Use refs for faster access (really important!) speeds up a lot!
                var w = context.Width;
                var h = context.Height;
                var pixels = context.Pixels;
                var xmin = rect.Left;
                var ymin = rect.Top;
                var xmax = rect.Right;
                var ymax = rect.Bottom;

                if (xmin < 0) xmin = 0;
                if (ymin < 0) ymin = 0;
                if (xmax >= w) xmax = w - 1;
                if (ymax >= h) ymax = h - 1;
                var xcnt = xmax - xmin + 1;
                var ycnt = ymax - ymin + 1;

                int srcx = xmin, dstx = xmin;
                if (dx < 0)
                {
                    xcnt += dx;
                    dstx = xmin;
                    srcx = xmin - dx;
                }
                if (dx > 0)
                {
                    xcnt -= dx;
                    srcx = xmin;
                    dstx = xmin + dx;
                }

                if (xcnt <= 0) return;

                var yptr = pixels + w*ymin;
                for (var y = ymin; y <= ymax; y++, yptr += w)
                {
                    NativeMethods.memcpy(yptr + dstx, yptr + srcx, xcnt*4);
                }

                if (dx < 0)
                {
                    bmp.FillRectangle(xmax + dx + 1, ymin, xmax, ymax, bgcolor);
                }
                if (dx > 0)
                {
                    bmp.FillRectangle(xmin, ymin, xmin + dx - 1, ymax, bgcolor);
                }
            }
        }
    }
}

