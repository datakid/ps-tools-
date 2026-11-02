using System;
using System.Drawing;

namespace PSTools
{
    static class Tok
    {
        public static int S1 { get { return UI.S(4); } }
        public static int S2 { get { return UI.S(8); } }
        public static int S3 { get { return UI.S(12); } }
        public static int S4 { get { return UI.S(16); } }
        public static int S5 { get { return UI.S(24); } }
        public static int S6 { get { return UI.S(32); } }
        public static int RadiusSmall { get { return UI.S(4); } }
        public static int RadiusLarge { get { return UI.S(8); } }
        public static int RowHeight { get { return UI.S(36); } }
        public static int HeaderRowHeight { get { return UI.S(28); } }
        public static int RowHeightCompact { get { return UI.S(32); } }
        public static int InputHeight { get { return UI.S(32); } }
        public static int ButtonHeight { get { return UI.S(32); } }

        public static Color Window { get { return UI.Back; } }
        public static Color Surface { get { return UI.Card; } }
        public static Color SurfaceAlt { get { return UI.Dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249); } }
        public static Color Stroke { get { return UI.Dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(229, 229, 229); } }
        public static Color StrokeStrong { get { return UI.Dark ? Color.FromArgb(154, 154, 154) : Color.FromArgb(138, 138, 138); } }
        public static Color InputStroke { get { return UI.Dark ? Color.FromArgb(69, 69, 69) : Color.FromArgb(209, 209, 209); } }
        public static Color Text { get { return UI.Dark ? Color.White : Color.FromArgb(27, 27, 27); } }
        public static Color TextSecondary { get { return UI.Dark ? Color.FromArgb(197, 197, 197) : Color.FromArgb(93, 93, 93); } }
        public static Color TextDisabled { get { return UI.Dark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160); } }
        public static Color Accent { get { return UI.Accent; } }
        public static Color Hover { get { return UI.Blend(SurfaceAlt, UI.Dark ? Color.White : Color.Black, 0.06f); } }
        public static Color Selected { get { return UI.Blend(SurfaceAlt, UI.Accent, UI.Dark ? 0.18f : 0.12f); } }
        public static Color RiskModifies { get { return UI.Dark ? Color.FromArgb(252, 225, 0) : Color.FromArgb(157, 93, 0); } }
        public static Color RiskDestructive { get { return UI.Dark ? Color.FromArgb(255, 153, 164) : Color.FromArgb(196, 43, 28); } }
        public static Color Input { get { return UI.Dark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(255, 255, 255); } }

        public static Font Body { get { return UI.Font; } }
        public static Font Caption { get { return UI.Small; } }
        public static Font Semibold { get { return UI.Bold; } }
        public static Font Mono { get { return UI.Mono; } }
    }
}
