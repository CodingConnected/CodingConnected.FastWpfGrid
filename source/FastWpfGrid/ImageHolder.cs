using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FastWpfGrid
{
    public class ImageHolder
    {
        public WriteableBitmap Bitmap;
        public BitmapImage Image;
        public WriteableBitmapExtensions.BlendMode BlendMode;
        public Color KeyColor = Colors.White;

        public ImageHolder(WriteableBitmap bitmap, BitmapImage image)
        {
            Bitmap = bitmap;
            Image = image;

            using (var context = Bitmap.GetBitmapContext())
            {
                var w = Bitmap.PixelWidth;
                var h = Bitmap.PixelHeight;

                for (var x = 0; x < w; x++)
                {
                    for (var y = 0; y < h; y++)
                    {
                        var color = Bitmap.GetPixel(x, y);
                        if (color.A != 0xFF)
                        {
                            BlendMode = WriteableBitmapExtensions.BlendMode.Alpha;
                            return;
                        }
                    }
                }

                BlendMode = WriteableBitmapExtensions.BlendMode.ColorKeying;
                KeyColor = bitmap.GetPixel(0, 0);
            }
        }
    }
}
