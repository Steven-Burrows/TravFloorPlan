using System;
using System.Windows.Forms;

namespace TravFloorPlan
{
    public partial class MainForm : Form
    {
        private void MenuZoomIn_Click(object? sender, EventArgs e)
        {
            AdjustZoom(1.1f);
        }

        private void MenuZoomOut_Click(object? sender, EventArgs e)
        {
            AdjustZoom(0.9f);
        }

        private void MenuZoomReset_Click(object? sender, EventArgs e)
        {
            _zoom = 1f;
            _pan = new System.Drawing.PointF(0, 0);
            canvasPanel.Invalidate();
        }

        private void AdjustZoom(float factor)
        {
            float oldZoom = _zoom;
            float newZoom = Math.Clamp(oldZoom * factor, 0.2f, 10f);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

            var center = new System.Drawing.Point(canvasPanel.ClientSize.Width / 2, canvasPanel.ClientSize.Height / 2);
            var worldBefore = ScreenToWorldF(center);
            _zoom = newZoom;
            _pan = new System.Drawing.PointF(
                center.X - _zoom * worldBefore.X,
                center.Y - _zoom * worldBefore.Y
            );
            canvasPanel.Invalidate();
        }
    }
}
