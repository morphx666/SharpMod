using Eto.Drawing;

namespace SharpModPlayerGUI {
    internal static class GraphicsExtensions {
        public static void DrawCurve(this Graphics g, Pen pen, PointF[] points) {
            DrawCurve(g, pen, points, 0.5f);
        }

        public static void DrawCurve(this Graphics g, Pen pen, PointF[] points, float tension) {
            if(points == null || points.Length < 2) return;
            if(points.Length == 2) {
                g.DrawLine(pen, points[0], points[1]);
                return;
            }

            float t = tension / 3.0f;
            int n = points.Length;
            GraphicsPath path = new();
            path.MoveTo(points[0]);

            for(int i = 0; i < n - 1; i++) {
                PointF p0 = i == 0 ? points[0] : points[i - 1];
                PointF p1 = points[i];
                PointF p2 = points[i + 1];
                PointF p3 = i + 2 < n ? points[i + 2] : points[i + 1];

                PointF c1 = new(p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
                PointF c2 = new(p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);

                path.AddBezier(p1, c1, c2, p2);
            }

            g.DrawPath(pen, path);
        }
    }
}
