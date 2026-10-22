using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace iGCRGB.Core
{
    /// <summary>运行期绘制程序/托盘图标（免去外部 ico 文件）。</summary>
    public static class IconFactory
    {
        private static Bitmap _bitmap;
        private static Icon _trayIcon;
        private static ImageSource _windowIcon;

        public static Icon TrayIcon
        {
            get
            {
                if (_trayIcon == null) Build();
                return _trayIcon;
            }
        }

        public static ImageSource WindowIcon
        {
            get
            {
                if (_windowIcon == null) Build();
                return _windowIcon;
            }
        }

        private static void Build()
        {
            _bitmap = Render(64);
            IntPtr handle = _bitmap.GetHicon();
            _trayIcon = Icon.FromHandle(handle);
            _windowIcon = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        private static Bitmap Render(int size)
        {
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(System.Drawing.Color.Transparent);

                Rectangle rect = new Rectangle(2, 2, size - 4, size - 4);
                using (GraphicsPath path = RoundedRect(rect, size * 0.28f))
                using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new PointF(0, 0), new PointF(size, size),
                    System.Drawing.Color.FromArgb(255, 154, 123, 255),
                    System.Drawing.Color.FromArgb(255, 74, 140, 255)))
                {
                    g.FillPath(brush, path);
                }

                // 内圈高光
                using (GraphicsPath path2 = RoundedRect(new Rectangle(4, 4, size - 8, size - 8), size * 0.24f))
                using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 255, 255, 255), Math.Max(1f, size / 32f)))
                {
                    g.DrawPath(pen, path2);
                }

                using (Font font = new Font("Segoe UI", size * 0.36f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("iG", font, System.Drawing.Brushes.White, new RectangleF(0, size * 0.02f, size, size), sf);
                }
            }
            return bmp;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
