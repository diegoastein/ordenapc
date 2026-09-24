using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace OrdenaPC;

static class TrayIcons
{
    /// <summary>Círculo de color con una "O" blanca; gris cuando está en pausa.</summary>
    public static Icon Make(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("O", font, Brushes.White, new RectangleF(0, 0, 32, 32), format);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
