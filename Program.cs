using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MenuStudio
{
    // ================= Settings =================
    static class Settings
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MenuStudio");
        static readonly string FilePath = Path.Combine(Dir, "settings.ini");
        static Dictionary<string, string> d = new Dictionary<string, string>();
        public static string NotesPath(string id) { return Path.Combine(Dir, id == "notes" ? "notes.txt" : "notes-" + id + ".txt"); }

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch { }
        }
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                List<string> l = new List<string>();
                foreach (KeyValuePair<string, string> kv in d) l.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, l.ToArray());
            }
            catch { }
        }
        public static string Get(string k, string def) { string v; return d.TryGetValue(k, out v) ? v : def; }
        public static int GetInt(string k, int def) { int v; return int.TryParse(Get(k, ""), out v) ? v : def; }
        public static bool GetBool(string k, bool def) { string v = Get(k, ""); return v == "" ? def : v == "1"; }
        public static void Set(string k, string v) { d[k] = v; }
        public static void Set(string k, int v) { d[k] = v.ToString(CultureInfo.InvariantCulture); }
        public static void Set(string k, bool v) { d[k] = v ? "1" : "0"; }
        public static void ClearPositions()
        {
            List<string> del = new List<string>();
            foreach (string k in d.Keys) if (k.EndsWith(".x") || k.EndsWith(".y")) del.Add(k);
            foreach (string k in del) d.Remove(k);
        }
        public static void RemovePrefix(string prefix)
        {
            List<string> del = new List<string>();
            foreach (string k in d.Keys) if (k.StartsWith(prefix)) del.Add(k);
            foreach (string k in del) d.Remove(k);
        }
    }

    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMSTATUS m);
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMSTATUS
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        public const int WS_EX_TOOLWINDOW = 0x80;
        [StructLayout(LayoutKind.Sequential)] public struct PT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct SZ { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Fmt; }
        [StructLayout(LayoutKind.Sequential)] struct BB { public int Flags; public int Enable; public IntPtr Rgn; public int Trans; }
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pptSrc, int crKey, ref BLEND pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("dwmapi.dll")] static extern int DwmEnableBlurBehindWindow(IntPtr h, ref BB bb);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("shell32.dll")] public static extern void SHChangeNotify(int evt, uint flags, IntPtr a, IntPtr b);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        public static void Present(Form f, Bitmap bmp, byte alpha)
        {
            IntPtr scr = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(scr), hb = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hb = bmp.GetHbitmap(Color.FromArgb(0)); old = SelectObject(mem, hb);
                PT dst = new PT(); dst.x = f.Left; dst.y = f.Top; SZ size = new SZ(); size.cx = bmp.Width; size.cy = bmp.Height; PT src = new PT();
                BLEND bl = new BLEND(); bl.Op = 0; bl.Flags = 0; bl.Alpha = alpha; bl.Fmt = 1;
                UpdateLayeredWindow(f.Handle, scr, ref dst, ref size, mem, ref src, 0, ref bl, 2);
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(mem, old);
                if (hb != IntPtr.Zero) DeleteObject(hb);
                DeleteDC(mem); ReleaseDC(IntPtr.Zero, scr);
            }
        }
        public static void Blur(Form f, bool on, GraphicsPath shape)
        {
            try
            {
                BB bb = new BB(); IntPtr hr = IntPtr.Zero; Region rg = null;
                if (on) { rg = new Region(shape); using (Graphics g = Graphics.FromHwnd(f.Handle)) hr = rg.GetHrgn(g); bb.Flags = 3; bb.Enable = 1; bb.Rgn = hr; }
                else { bb.Flags = 1; bb.Enable = 0; }
                DwmEnableBlurBehindWindow(f.Handle, ref bb);
                if (hr != IntPtr.Zero) DeleteObject(hr);
                if (rg != null) rg.Dispose();
            }
            catch { }
        }
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public int AccentState; public int AccentFlags; public int GradientColor; public int AnimationId; }
        [StructLayout(LayoutKind.Sequential)]
        struct WinCompAttrData { public int Attribute; public IntPtr Data; public int SizeOfData; }
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WinCompAttrData data);

        // state: 0 off, 2 transparent gradient, 3 blur behind, 4 acrylic. color = ARGB
        public static void SetAccent(IntPtr hwnd, int state, Color c)
        {
            try
            {
                AccentPolicy ap = new AccentPolicy();
                ap.AccentState = state;
                ap.AccentFlags = 2;
                ap.GradientColor = (c.A << 24) | (c.B << 16) | (c.G << 8) | c.R;
                int size = Marshal.SizeOf(ap);
                IntPtr p = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(ap, p, false);
                WinCompAttrData data = new WinCompAttrData();
                data.Attribute = 19; data.Data = p; data.SizeOfData = size;
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(p);
            }
            catch { }
        }
    }

    // ================= Shapes =================
    static class Shapes
    {
        static GraphicsPath RR(RectangleF r, float tl, float tr, float br, float bl)
        {
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            if (tl > 0) p.AddArc(r.X, r.Y, tl * 2, tl * 2, 180, 90); else p.AddLine(r.X, r.Y, r.X, r.Y);
            if (tr > 0) p.AddArc(r.Right - tr * 2, r.Y, tr * 2, tr * 2, 270, 90); else p.AddLine(r.Right, r.Y, r.Right, r.Y);
            if (br > 0) p.AddArc(r.Right - br * 2, r.Bottom - br * 2, br * 2, br * 2, 0, 90); else p.AddLine(r.Right, r.Bottom, r.Right, r.Bottom);
            if (bl > 0) p.AddArc(r.X, r.Bottom - bl * 2, bl * 2, bl * 2, 90, 90); else p.AddLine(r.X, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
        public static GraphicsPath Make(int shape, int radius, RectangleF r)
        {
            float m = Math.Min(r.Width, r.Height);
            float rad = Math.Max(0, Math.Min(radius, m / 2f));
            GraphicsPath p;
            switch (shape)
            {
                case 1: return RR(r, 0, 0, 0, 0);
                case 2: rad = m / 2f; return RR(r, rad, rad, rad, rad);
                case 3: p = new GraphicsPath(); p.AddEllipse(r); return p;
                case 4:
                    p = new GraphicsPath();
                    p.AddPolygon(new PointF[] { new PointF(r.X + r.Width * 0.25f, r.Y), new PointF(r.X + r.Width * 0.75f, r.Y), new PointF(r.Right, r.Y + r.Height / 2), new PointF(r.X + r.Width * 0.75f, r.Bottom), new PointF(r.X + r.Width * 0.25f, r.Bottom), new PointF(r.X, r.Y + r.Height / 2) });
                    return p;
                case 5:
                    { float c = m * 0.29f; p = new GraphicsPath();
                      p.AddPolygon(new PointF[] { new PointF(r.X + c, r.Y), new PointF(r.Right - c, r.Y), new PointF(r.Right, r.Y + c), new PointF(r.Right, r.Bottom - c), new PointF(r.Right - c, r.Bottom), new PointF(r.X + c, r.Bottom), new PointF(r.X, r.Bottom - c), new PointF(r.X, r.Y + c) });
                      return p; }
                case 6:
                    p = new GraphicsPath();
                    p.AddPolygon(new PointF[] { new PointF(r.X + r.Width / 2, r.Y), new PointF(r.Right, r.Y + r.Height / 2), new PointF(r.X + r.Width / 2, r.Bottom), new PointF(r.X, r.Y + r.Height / 2) });
                    return p;
                case 7:
                    { float c = rad; p = new GraphicsPath();
                      p.AddPolygon(new PointF[] { new PointF(r.X + c, r.Y), new PointF(r.Right - c, r.Y), new PointF(r.Right, r.Y + c), new PointF(r.Right, r.Bottom - c), new PointF(r.Right - c, r.Bottom), new PointF(r.X + c, r.Bottom), new PointF(r.X, r.Bottom - c), new PointF(r.X, r.Y + c) });
                      return p; }
                case 8: { float big = Math.Min(m / 2f, Math.Max(rad * 2, 10)); return RR(r, big, 4, big, 4); }
                case 9: return RR(r, rad, rad, 0, 0);
                case 10: rad = m * 0.38f; return RR(r, rad, rad, rad, rad);
                case 11:
                    { float o = Math.Min(r.Width * 0.3f, r.Height * 0.3f); p = new GraphicsPath();
                      p.AddPolygon(new PointF[] { new PointF(r.X + o, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right - o, r.Bottom), new PointF(r.X, r.Bottom) });
                      return p; }
                case 12: p = new GraphicsPath(); p.AddPolygon(new PointF[] { new PointF(r.X + r.Width / 2, r.Y), new PointF(r.Right, r.Y + r.Height * 0.38f), new PointF(r.X + r.Width * 0.82f, r.Bottom), new PointF(r.X + r.Width * 0.18f, r.Bottom), new PointF(r.X, r.Y + r.Height * 0.38f) }); return p;
                case 13: p = new GraphicsPath(); p.AddPolygon(new PointF[] { new PointF(r.X + r.Width * 0.15f, r.Y), new PointF(r.Right - r.Width * 0.15f, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) }); return p;
                case 14: return RR(r, m / 2f, 0, 0, m / 2f);
                case 15: return RR(r, 0, 0, rad, rad);
                case 16:
                    { float n = Math.Min(r.Width, r.Height) * 0.12f; float my = r.Y + r.Height / 2; p = new GraphicsPath();
                      p.AddPolygon(new PointF[] { new PointF(r.X, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right, my - n), new PointF(r.Right - n, my), new PointF(r.Right, my + n), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom), new PointF(r.X, my + n), new PointF(r.X + n, my), new PointF(r.X, my - n) });
                      return p; }
                case 17:
                    p = new GraphicsPath(); p.StartFigure();
                    p.AddLine(r.X, r.Y, r.Right, r.Y); p.AddLine(r.Right, r.Y, r.Right, r.Y + r.Height * 0.55f);
                    p.AddBezier(r.Right, r.Y + r.Height * 0.55f, r.Right, r.Y + r.Height * 0.85f, r.X + r.Width * 0.7f, r.Y + r.Height * 0.95f, r.X + r.Width / 2, r.Bottom);
                    p.AddBezier(r.X + r.Width / 2, r.Bottom, r.X + r.Width * 0.3f, r.Y + r.Height * 0.95f, r.X, r.Y + r.Height * 0.85f, r.X, r.Y + r.Height * 0.55f);
                    p.CloseFigure(); return p;
                case 18:
                    { PointF[] pts = new PointF[10]; float cx = r.X + r.Width / 2, cy = r.Y + r.Height * 0.53f;
                      for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float k = i % 2 == 0 ? 1f : 0.42f; pts[i] = new PointF(cx + (float)Math.Cos(a) * r.Width / 2 * k, cy + (float)Math.Sin(a) * r.Height / 2 * 1.04f * k); }
                      p = new GraphicsPath(); p.AddPolygon(pts); return p; }
                case 19: p = new GraphicsPath(); p.AddPolygon(new PointF[] { new PointF(r.X + r.Width / 2, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) }); return p;
                case 20: return RR(r, 0, m / 2f, m / 2f, 0);
                case 21: { float big = Math.Max(rad, 6); return RR(r, big, 0, big, 0); }
                default: return RR(r, rad, rad, rad, rad);
            }
        }
    }

    // ================= Base widget =================

    // ================= Options =================
    class Prop
    {
        public string Cat, Key, Label, Kind, Def; public int Min, Max; public string[] Choices;
        public Prop(string cat, string key, string label, string kind, string def, int min, int max, string[] ch)
        { Cat = cat; Key = key; Label = label; Kind = kind; Def = def; Min = min; Max = max; Choices = ch; }
    }

    static class Cfg
    {
        public static List<Prop> All = new List<Prop>();
        static Dictionary<string, Prop> map = new Dictionary<string, Prop>();
        static Dictionary<string, Color> cc = new Dictionary<string, Color>();
        static void A(string cat, string key, string label, string kind, string def, int min, int max, string[] ch)
        { Prop p = new Prop(cat, key, label, kind, def, min, max, ch); All.Add(p); map[key] = p; }
        static void N(string cat, string key, string label, int def, int min, int max) { A(cat, key, label, "int", def.ToString(), min, max, null); }
        static void Bl(string cat, string key, string label, bool def) { A(cat, key, label, "bool", def ? "1" : "0", 0, 1, null); }
        static void K(string cat, string key, string label, string hex) { A(cat, key, label, "color", hex, 0, 0, null); }
        static void Ch(string cat, string key, string label, int def, params string[] ch) { A(cat, key, label, "choice", def.ToString(), 0, ch.Length - 1, ch); }

        public static readonly string[] AnimNames = { "None", "Fade", "Slide down", "Slide up", "Slide from left", "Slide from right", "Zoom in", "Pop (big zoom)", "Unfold vertical", "Unfold horizontal", "Drop in from above", "Flip open" };
        static Cfg()
        {
            Ch("Look", "bgmode", "Background type", 2, "Solid", "Transparent tint", "Blur glass (Windows blur)");
            N("Look", "bgalpha", "Tint strength (0 = clear, 255 = solid)", 215, 0, 255);
            Ch("Look", "grad", "Background gradient", 0, "None", "Vertical", "Horizontal", "Diagonal");
            A("Look", "shape", "Menu shape", "choice", "0", 0, Sty.ShapeNames.Length - 1, Sty.ShapeNames);
            N("Look", "radius", "Corner radius (px)", 12, 0, 120);
            N("Look", "width", "Menu width (px)", 270, 120, 800);
            N("Look", "itemh", "Item height (px)", 36, 18, 100);
            N("Look", "pad", "Inner padding top/bottom (px)", 6, 0, 60);
            N("Look", "padx", "Item left/right padding (px)", 12, 0, 80);
            N("Look", "borderw", "Border width", 1, 0, 12);
            N("Look", "shadow", "Shadow size (px, 0 = none)", 18, 0, 60);
            N("Look", "shadowa", "Shadow strength", 90, 0, 255);
            N("Look", "opacity", "Overall opacity %", 100, 10, 100);
            Ch("Look", "hoverstyle", "Hover highlight style", 0, "Filled bar", "Left accent bar + fill", "Underline", "Outline", "Full pill", "Glow");
            N("Look", "hoverradius", "Highlight corner radius", 8, 0, 60);
            N("Look", "sepinset", "Separator inset (px)", 14, 0, 100);
            N("Look", "sepw", "Separator thickness", 1, 1, 8);
            N("Look", "sepgap", "Separator height (px)", 11, 3, 40);
            Bl("Look", "showicons", "Show icons", true);
            Ch("Look", "textalign", "Text alignment", 0, "Left", "Center", "Right");
            N("Look", "offx", "Menu position offset X (px)", 0, -300, 300);
            N("Look", "offy", "Menu position offset Y (px)", 0, -300, 300);

            K("Colors", "bg", "Background", "202020");
            K("Colors", "bg2", "Gradient second color", "34344A");
            K("Colors", "hover", "Hover highlight", "FFFFFF");
            N("Colors", "hovera", "Hover highlight strength", 34, 0, 255);
            K("Colors", "text", "Text", "FFFFFF");
            K("Colors", "hovertext", "Text when hovered", "FFFFFF");
            K("Colors", "dim", "Disabled / arrow", "9A9A9A");
            K("Colors", "iconc", "Icons", "F0F0F0");
            K("Colors", "border", "Border", "FFFFFF");
            N("Colors", "bordera", "Border strength", 60, 0, 255);
            K("Colors", "sepc", "Separator lines", "FFFFFF");
            N("Colors", "sepa", "Separator strength", 45, 0, 255);
            K("Colors", "accent", "Accent (bar / outline / glow)", "60CDFF");
            K("Colors", "shadowc", "Shadow", "000000");

            A("Text", "font", "Font", "font", "Segoe UI", 0, 0, null);
            N("Text", "fsize", "Text size (px)", 15, 8, 40);
            Bl("Text", "bold", "Bold", false);
            Bl("Text", "italic", "Italic", false);
            Ch("Text", "case", "Letter case", 0, "Normal", "UPPERCASE", "lowercase");
            Ch("Text", "iconfont", "Icon font", 0, "Segoe Fluent Icons", "Segoe MDL2 Assets");
            N("Text", "isize", "Icon size (px)", 17, 8, 48);

            Ch("Animation", "anim", "Opening animation", 1, AnimNames);
            N("Animation", "dur", "Opening duration (ms)", 220, 0, 2000);
            Ch("Animation", "ease", "Easing", 1, "Linear", "Ease out", "Ease in-out", "Overshoot (bounce)", "Elastic");
            N("Animation", "dist", "Slide distance (px)", 24, 0, 300);
            N("Animation", "stagger", "Items appear one by one: delay per item (ms, 0 = off)", 22, 0, 300);
            Ch("Animation", "staggermode", "One-by-one style", 1, "Fade", "Slide from left", "Slide from below", "Slide from right");
            Ch("Animation", "hoveranim", "Highlight movement", 2, "Instant", "Fast glide", "Smooth glide", "Slow glide", "Very slow glide");
            N("Animation", "subdelay", "Submenu open delay (ms)", 180, 0, 1500);
            Ch("Animation", "closeanim", "Closing animation", 1, "None", "Fade out", "Slide away");
            N("Animation", "closedur", "Closing duration (ms)", 120, 20, 800);

            Bl("Behavior", "enable", "Replace the desktop right-click menu", true);
            Bl("Behavior", "shiftnative", "Hold Shift while right-clicking to get the normal Windows menu", true);
            Bl("Behavior", "keys", "Keyboard control (arrows, Enter, Esc) while the menu is open", true);
            Bl("Behavior", "flip", "Keep the menu inside the screen", true);
        }
        public static string S(string k) { string x = Settings.Get("p." + k, null); if (x != null) return x; Prop p; return map.TryGetValue(k, out p) ? p.Def : ""; }
        public static int I(string k) { int r; return int.TryParse(S(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : 0; }
        public static bool B(string k) { return S(k) == "1"; }
        public static Color C(string k)
        {
            try { return Color.FromArgb(255, Color.FromArgb(Convert.ToInt32(S(k), 16))); } catch { return Color.Gray; }
        }
        public static void Set(string k, string v) { Settings.Set("p." + k, v); }
        public static void SetColor(string k, Color c) { Set(k, (c.R * 65536 + c.G * 256 + c.B).ToString("X6")); }

        // name|bg|bg2|hover|text|dim|border|accent|bgmode|bgalpha|radius|grad|hoverstyle|font|anim|ease
        public static readonly string[] Themes = {
            "Windows dark|202020|34344A|FFFFFF|FFFFFF|9A9A9A|FFFFFF|60CDFF|2|215|12|0|0|Segoe UI|1|1",
            "Windows light|F3F3F3|DDDDE8|000000|1A1A1A|707070|000000|0067C0|2|225|12|0|0|Segoe UI|1|1",
            "Acrylic dark|141420|141420|FFFFFF|FFFFFF|B0B4C8|FFFFFF|78BEFF|2|150|16|0|0|Segoe UI|2|1",
            "Glass clear|000000|000000|FFFFFF|FFFFFF|D0D4E0|FFFFFF|FFFFFF|2|60|18|0|0|Segoe UI|1|1",
            "Neon|0A0A16|240038|FF00C8|00FFD2|009682|FF00C8|FFEE00|0|240|6|3|3|Consolas|7|3",
            "Terminal|000C00|001800|46FF6E|46FF6E|1EA03C|46FF6E|46FF6E|0|250|0|0|3|Consolas|8|0",
            "Dracula|282A36|44475A|BD93F9|F8F8F2|8B94BE|BD93F9|FF79C6|0|245|10|0|1|Segoe UI|3|1",
            "Nord|2E3440|3B4252|88C0D0|ECEFF4|81A1C1|88C0D0|88C0D0|0|245|10|0|0|Segoe UI|4|2",
            "Sunset|2A1434|C84632|FFAA64|FFE8D7|CDA0AA|FF7850|FF7850|0|240|20|3|4|Segoe UI|6|3",
            "Rose gold|3C262A|785050|FFBEAA|FFEBE1|E1B9AF|FFBEAA|FFBEAA|0|240|22|3|4|Segoe UI|10|3",
            "Mono black|000000|202020|FFFFFF|FFFFFF|969696|FFFFFF|FFFFFF|0|255|0|0|3|Segoe UI|1|0",
            "Ocean|062850|0082A0|78F0E6|FFFFFF|B4E1F0|78F0E6|78F0E6|0|240|18|1|5|Segoe UI|8|1",
            "Cyberpunk|140628|3C0A46|00E6FF|FFEE00|00E6FF|FF008C|FF008C|0|245|8|3|3|Consolas|9|3",
            "Gold|1A1408|3A2E10|FFD870|FFD870|C8A850|FFD870|FFD870|0|245|14|1|1|Georgia|1|1"
        };
        public static void ApplyTheme(int i)
        {
            string[] p = Themes[i].Split('|');
            Set("bg", p[1]); Set("bg2", p[2]); Set("hover", p[3]); Set("text", p[4]); Set("hovertext", p[4]); Set("dim", p[5]); Set("border", p[6]); Set("accent", p[7]);
            Set("bgmode", p[8]); Set("bgalpha", p[9]); Set("radius", p[10]); Set("grad", p[11]); Set("hoverstyle", p[12]); Set("font", p[13]); Set("anim", p[14]); Set("ease", p[15]);
            Set("iconc", p[4]); Set("sepc", p[4]);
            Settings.Save();
        }
    }

    static class Sty
    {
        public static readonly string[] ShapeNames = { "Rounded", "Rectangle", "Pill", "Ellipse / Circle", "Hexagon", "Octagon", "Diamond", "Cut corners", "Leaf", "Tab (top)", "Squircle", "Parallelogram", "Pentagon", "Trapezoid", "Pill (left)", "Tab (bottom)", "Ticket", "Shield", "Star", "Triangle", "Pill (right)", "Diagonal corners" };
    }

    // ================= Menu items =================
    class Entry
    {
        public string Text, Glyph; public Action Act; public List<Entry> Kids; public bool Sep; public RectangleF R;
        public Entry() { }
        public Entry(string t, string g, Action a) { Text = t; Glyph = g; Act = a; }
    }

    static class Actions
    {
        static string Desk { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
        public static void Run(string cmd, string args)
        {
            try { ProcessStartInfo psi = new ProcessStartInfo(cmd, args ?? ""); psi.UseShellExecute = true; Process.Start(psi); } catch { }
        }
        static IntPtr DefView()
        {
            IntPtr pm = Native.FindWindow("Progman", null);
            IntPtr dv = Native.FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero) return dv;
            IntPtr w = IntPtr.Zero;
            while ((w = Native.FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
            {
                dv = Native.FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (dv != IntPtr.Zero) return dv;
            }
            return IntPtr.Zero;
        }
        public static void Refresh()
        {
            IntPtr dv = DefView(); if (dv != IntPtr.Zero) { Native.PostMessage(dv, 0x100, (IntPtr)0x74, IntPtr.Zero); Native.PostMessage(dv, 0x101, (IntPtr)0x74, IntPtr.Zero); }
            try { Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
        }
        public static void ToggleIcons() { IntPtr dv = DefView(); if (dv != IntPtr.Zero) Native.SendMessage(dv, 0x111, (IntPtr)0x7402, IntPtr.Zero); }
        static string Unique(string baseName, string ext)
        {
            string p = Path.Combine(Desk, baseName + ext); int n = 2;
            while (File.Exists(p) || Directory.Exists(p)) { p = Path.Combine(Desk, baseName + " (" + n + ")" + ext); n++; }
            return p;
        }
        public static void NewFolder() { try { Directory.CreateDirectory(Unique("New folder", "")); } catch { } Refresh(); }
        public static void NewText() { try { using (File.Create(Unique("New Text Document", ".txt"))) { } } catch { } Refresh(); }
        public static void NewShortcutUrl() { try { File.WriteAllText(Unique("New link", ".url"), "[InternetShortcut]\r\nURL=https://"); } catch { } Refresh(); }
        public static void OpenDesktop() { Run("explorer.exe", "\"" + Desk + "\""); }
        public static void Terminal()
        {
            try { ProcessStartInfo psi = new ProcessStartInfo("wt.exe", "-d \"" + Desk + "\""); psi.UseShellExecute = true; Process.Start(psi); }
            catch { try { ProcessStartInfo psi = new ProcessStartInfo("cmd.exe"); psi.WorkingDirectory = Desk; psi.UseShellExecute = true; Process.Start(psi); } catch { } }
        }
        public static void NativeMenu()
        {
            Hook.Bypass = true;
            Native.mouse_event(0x08, 0, 0, 0, UIntPtr.Zero); Native.mouse_event(0x10, 0, 0, 0, UIntPtr.Zero);
            Hook.Bypass = false;
        }
        static Entry E(string t, string g, Action a) { return new Entry(t, g, a); }
        static Entry Sub(string t, string g, params Entry[] kids) { Entry e = new Entry(t, g, null); e.Kids = new List<Entry>(kids); return e; }
        public static Entry Builtin(string id, string label, string glyph)
        {
            switch (id)
            {
                case "refresh": return E(label, glyph, Refresh);
                case "icons": return E(label, glyph, ToggleIcons);
                case "opendesk": return E(label, glyph, OpenDesktop);
                case "display": return E(label, glyph, delegate { Run("ms-settings:display", ""); });
                case "terminal": return E(label, glyph, Terminal);
                case "native": return E(label, glyph, NativeMenu);
                case "new":
                    return Sub(label, glyph, E("Folder", "\uE8B7", NewFolder), E("Text document", "\uE8A5", NewText), E("Internet shortcut", "\uE71B", NewShortcutUrl));
                case "personalize":
                    return Sub(label, glyph, E("Background", "\uE91B", delegate { Run("ms-settings:personalization-background", ""); }), E("Colors", "\uE790", delegate { Run("ms-settings:colors", ""); }),
                        E("Themes", "\uE771", delegate { Run("ms-settings:themes", ""); }), E("Lock screen", "\uEE3F", delegate { Run("ms-settings:lockscreen", ""); }), E("Taskbar", "\uE90E", delegate { Run("ms-settings:taskbar", ""); }));
                case "system":
                    return Sub(label, glyph, E("Task Manager", "\uE9D9", delegate { Run("taskmgr.exe", ""); }), E("Settings", "\uE713", delegate { Run("ms-settings:", ""); }),
                        E("Control Panel", "\uE9F5", delegate { Run("control.exe", ""); }), E("Command Prompt", "\uE756", delegate { Run("cmd.exe", ""); }),
                        E("PowerShell", "\uE756", delegate { Run("powershell.exe", ""); }), E("Run...", "\uE7C4", delegate { Run("explorer.exe", "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}"); }));
            }
            return E(label, glyph, delegate { });
        }
    }

    // spec: kind \u0001 id \u0001 label \u0001 glyph \u0001 cmd \u0001 args \u0001 visible
    class Spec
    {
        public string Kind, Id, Label, Glyph, Cmd, Args; public bool Vis = true;
        public string Enc() { return string.Join("\u0001", new[] { Kind, Id, Label, Glyph, Cmd, Args, Vis ? "1" : "0" }); }
        public static Spec Dec(string s)
        {
            string[] p = s.Split('\u0001'); if (p.Length < 7) return null;
            Spec x = new Spec(); x.Kind = p[0]; x.Id = p[1]; x.Label = p[2]; x.Glyph = p[3]; x.Cmd = p[4]; x.Args = p[5]; x.Vis = p[6] == "1"; return x;
        }
        public static Spec B(string id, string label, string glyph) { Spec s = new Spec(); s.Kind = "builtin"; s.Id = id; s.Label = label; s.Glyph = glyph; s.Cmd = ""; s.Args = ""; return s; }
        public static Spec Sep() { Spec s = new Spec(); s.Kind = "sep"; s.Id = ""; s.Label = ""; s.Glyph = ""; s.Cmd = ""; s.Args = ""; return s; }
        public static Spec Custom(string label, string glyph, string cmd, string args) { Spec s = new Spec(); s.Kind = "custom"; s.Id = ""; s.Label = label; s.Glyph = glyph; s.Cmd = cmd; s.Args = args; return s; }
        public static List<Spec> Defaults()
        {
            List<Spec> l = new List<Spec>();
            l.Add(B("refresh", "Refresh", "\uE72C")); l.Add(B("new", "New", "\uE710")); l.Add(Sep());
            l.Add(B("icons", "Show / hide desktop icons", "\uE890")); l.Add(B("opendesk", "Open Desktop folder", "\uE838")); l.Add(Sep());
            l.Add(B("display", "Display settings", "\uE7F4")); l.Add(B("personalize", "Personalize", "\uE771")); l.Add(Sep());
            l.Add(B("terminal", "Open in Terminal", "\uE756")); l.Add(B("system", "System tools", "\uE713")); l.Add(Sep());
            l.Add(B("native", "Show more options (Windows menu)", "\uE712"));
            return l;
        }
        public static List<Spec> Load()
        {
            string raw = Settings.Get("menu", ""); List<Spec> l = new List<Spec>();
            if (raw != "") foreach (string s in raw.Split('\u0002')) { Spec x = Dec(s); if (x != null) l.Add(x); }
            if (l.Count == 0) l = Defaults();
            return l;
        }
        public static void Save(List<Spec> l)
        {
            List<string> p = new List<string>(); foreach (Spec s in l) p.Add(s.Enc());
            Settings.Set("menu", string.Join("\u0002", p.ToArray())); Settings.Save();
        }
        public static List<Entry> Build()
        {
            List<Entry> r = new List<Entry>(); bool lastSep = true;
            foreach (Spec s in Load())
            {
                if (!s.Vis) continue;
                if (s.Kind == "sep") { if (lastSep) continue; Entry e = new Entry(); e.Sep = true; r.Add(e); lastSep = true; continue; }
                Entry en;
                if (s.Kind == "custom") { string c = s.Cmd, a = s.Args; en = new Entry(s.Label, s.Glyph, delegate { Actions.Run(c, a); }); }
                else en = Actions.Builtin(s.Id, s.Label, s.Glyph);
                r.Add(en); lastSep = false;
            }
            while (r.Count > 0 && r[r.Count - 1].Sep) r.RemoveAt(r.Count - 1);
            return r;
        }
    }

    // ================= Menu window =================
    class MenuForm : Form
    {
        public static List<MenuForm> Open = new List<MenuForm>();
        List<Entry> items; MenuForm parent, child; int hover = -1, kbd = -1; int M;
        float hx, hy, hw, hh, ha; bool hInit;
        Stopwatch sw = Stopwatch.StartNew(); Timer tm = new Timer(); Timer subT = new Timer();
        bool closing; long closeStart; int closeKind;
        SizeF box; int W, Hh;
        public Rectangle MenuRect { get { return new Rectangle(Left + M, Top + M, W, Hh); } }

        public MenuForm(List<Entry> list, MenuForm par)
        {
            items = list; parent = par;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            M = Cfg.I("shadow") + 4;
            W = Cfg.I("width"); int ih = Cfg.I("itemh"), sg = Cfg.I("sepgap"), pad = Cfg.I("pad");
            float y = pad;
            foreach (Entry e in items) { float h = e.Sep ? sg : ih; e.R = new RectangleF(0, y, W, h); y += h; }
            Hh = (int)(y + pad);
            ClientSize = new Size(W + 2 * M, Hh + 2 * M);
            tm.Interval = 15; tm.Tick += delegate { Step(); };
            subT.Tick += delegate { subT.Stop(); OpenChild(hover); };
        }
        protected override CreateParams CreateParams
        { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x80000 | 0x8 | 0x08000000; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }

        public static bool AnyOpen { get { return Open.Count > 0; } }
        public static bool Contains(Point p) { foreach (MenuForm m in Open) if (!m.closing && m.MenuRect.Contains(p)) return true; return false; }
        public static void CloseAll()
        {
            foreach (MenuForm m in new List<MenuForm>(Open)) m.BeginClose();
        }
        public static void ShowAt(List<Entry> list, Point pt, MenuForm parent, bool sub)
        {
            MenuForm f = new MenuForm(list, parent);
            int ox = sub ? 0 : Cfg.I("offx"), oy = sub ? 0 : Cfg.I("offy");
            int x = pt.X + ox - f.M, y = pt.Y + oy - f.M;
            if (Cfg.B("flip"))
            {
                Rectangle wa = Screen.FromPoint(pt).WorkingArea;
                if (x + f.M + f.W > wa.Right) x = sub && parent != null ? parent.MenuRect.Left - f.W - f.M + 4 : pt.X - f.W - f.M;
                if (y + f.M + f.Hh > wa.Bottom) y = Math.Max(wa.Top - f.M, wa.Bottom - f.Hh - f.M);
                if (x + f.M < wa.Left) x = wa.Left - f.M;
                if (y + f.M < wa.Top) y = wa.Top - f.M;
            }
            f.Location = new Point(x, y);
            Open.Add(f); f.Show(); f.UpdateBlur(); f.sw.Restart(); f.tm.Start(); f.Step();
            Hook.KeysOn(Cfg.B("keys"));
        }
        void UpdateBlur()
        {
            using (GraphicsPath p = Shapes.Make(Cfg.I("shape"), Cfg.I("radius"), new RectangleF(M, M, W, Hh))) Native.Blur(this, Cfg.I("bgmode") >= 2, p);
        }

        void BeginClose()
        {
            if (closing) return;
            if (child != null) child.BeginClose();
            int ck = Cfg.I("closeanim");
            if (ck == 0) { Kill(); return; }
            closing = true; closeKind = ck; closeStart = sw.ElapsedMilliseconds; subT.Stop();
        }
        void Kill()
        {
            tm.Stop(); subT.Stop(); Open.Remove(this); if (Open.Count == 0) Hook.KeysOn(false);
            try { Hide(); Dispose(); } catch { }
        }

        public static float Ease(int type, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            switch (type)
            {
                case 0: return t;
                case 1: return 1 - (1 - t) * (1 - t) * (1 - t);
                case 2: return t < 0.5f ? 4 * t * t * t : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2;
                case 3: { float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2); }
                default: if (t <= 0 || t >= 1) return t; return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
            }
        }
        static float Clamp(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        void Step()
        {
            if (closing && sw.ElapsedMilliseconds - closeStart >= Cfg.I("closedur")) { Kill(); return; }
            // highlight glide
            float[] ks = { 1f, 0.55f, 0.28f, 0.14f, 0.07f }; float k = ks[Math.Max(0, Math.Min(4, Cfg.I("hoveranim")))];
            int target = kbd >= 0 ? kbd : hover;
            if (target >= 0 && target < items.Count && !items[target].Sep)
            {
                RectangleF r = items[target].R;
                if (!hInit) { hx = r.X; hy = r.Y; hw = r.Width; hh = r.Height; hInit = true; }
                hx += (r.X - hx) * k; hy += (r.Y - hy) * k; hw += (r.Width - hw) * k; hh += (r.Height - hh) * k; ha += (1 - ha) * Math.Max(k, 0.25f);
            }
            else ha += (0 - ha) * Math.Max(k, 0.25f);
            Render();
        }

        void Render()
        {
            if (!IsHandleCreated || !Visible || IsDisposed) return;
            long ms = sw.ElapsedMilliseconds; int dur = Math.Max(1, Cfg.I("dur")); int anim = Cfg.I("anim");
            float t = anim == 0 || Cfg.I("dur") == 0 ? 1f : Clamp(ms / (float)dur); float e = Ease(Cfg.I("ease"), t);
            float alpha = 1f, tx = 0, ty = 0, sx = 1, sy = 1; RectangleF? clip = null; float dist = Cfg.I("dist");
            switch (anim)
            {
                case 1: alpha = Clamp(e); break;
                case 2: ty = -dist * (1 - e); alpha = Clamp(e * 1.3f); break;
                case 3: ty = dist * (1 - e); alpha = Clamp(e * 1.3f); break;
                case 4: tx = -dist * (1 - e); alpha = Clamp(e * 1.3f); break;
                case 5: tx = dist * (1 - e); alpha = Clamp(e * 1.3f); break;
                case 6: sx = sy = 0.8f + 0.2f * e; alpha = Clamp(e * 1.3f); break;
                case 7: sx = sy = 0.4f + 0.6f * e; alpha = Clamp(e * 1.5f); break;
                case 8: clip = new RectangleF(0, 0, 100000, (M + Hh + M) * Clamp(e)); break;
                case 9: clip = new RectangleF(0, 0, (M + W + M) * Clamp(e), 100000); break;
                case 10: ty = -dist * 3 * (1 - e); alpha = Clamp(e * 1.3f); break;
                case 11: sy = Math.Max(0.02f, e); alpha = Clamp(e * 1.5f); break;
            }
            if (closing)
            {
                float ct = Clamp((ms - closeStart) / (float)Math.Max(1, Cfg.I("closedur"))); alpha *= 1 - ct;
                if (closeKind == 2) ty += dist * ct * 0.8f;
            }
            using (Bitmap bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.Clear(Color.Transparent);
                g.TranslateTransform(M + tx, M + ty); g.ScaleTransform(sx, sy); g.TranslateTransform(-M, -M);
                if (clip.HasValue) g.SetClip(clip.Value);
                Paint(g, ms);
                int ga = (int)(255 * Clamp(alpha) * Cfg.I("opacity") / 100f);
                Native.Present(this, bmp, (byte)ga);
            }
        }

        void Paint(Graphics g, long ms)
        {
            int rad = Cfg.I("radius"), shape = Cfg.I("shape"), mode = Cfg.I("bgmode"); RectangleF mr = new RectangleF(M, M, W, Hh);
            int S = Cfg.I("shadow");
            if (S > 0)
            {
                Color sc = Cfg.C("shadowc"); int sa = Cfg.I("shadowa");
                for (int i = S; i >= 1; i--)
                {
                    float f = 1f - i / (float)(S + 1); int a = (int)(sa * f * f / Math.Max(3, S / 3f)); if (a < 1) continue;
                    RectangleF r = RectangleF.Inflate(mr, i, i); r.Offset(0, S * 0.25f);
                    using (GraphicsPath sp = Shapes.Make(shape, rad + i, r)) using (SolidBrush sb = new SolidBrush(Color.FromArgb(Math.Min(255, a), sc))) g.FillPath(sb, sp);
                }
            }
            using (GraphicsPath path = Shapes.Make(shape, rad, mr))
            {
                int ba = mode == 0 ? 255 : Cfg.I("bgalpha"); Color bg = Cfg.C("bg"), bg2 = Cfg.C("bg2"); int gr = Cfg.I("grad");
                if (gr > 0)
                {
                    float ang = gr == 1 ? 90f : (gr == 2 ? 0f : 45f);
                    using (LinearGradientBrush gb = new LinearGradientBrush(Rectangle.Round(RectangleF.Inflate(mr, 1, 1)), Color.FromArgb(ba, bg), Color.FromArgb(ba, bg2), ang)) g.FillPath(gb, path);
                }
                else using (SolidBrush sb = new SolidBrush(Color.FromArgb(ba, bg))) g.FillPath(sb, path);
                int bw = Cfg.I("borderw");
                if (bw > 0)
                {
                    float hwb = bw / 2f;
                    using (GraphicsPath bp = Shapes.Make(shape, rad, new RectangleF(M + hwb, M + hwb, W - bw, Hh - bw)))
                    using (Pen pen = new Pen(Color.FromArgb(Cfg.I("bordera"), Cfg.C("border")), bw)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, bp); }
                }
            }
            // highlight
            int hs = Cfg.I("hoverstyle"); int hr = Cfg.I("hoverradius"); Color hc = Cfg.C("hover"), ac = Cfg.C("accent");
            float inset = 4;
            if (ha > 0.01f && hInit)
            {
                RectangleF r = new RectangleF(M + hx + inset, M + hy + 1, hw - 2 * inset, hh - 2);
                int a = (int)(Cfg.I("hovera") * Clamp(ha));
                using (GraphicsPath hp = Shapes.Make(0, hs == 4 ? (int)(r.Height / 2) : hr, r))
                {
                    if (hs == 0 || hs == 1 || hs == 4) using (SolidBrush b = new SolidBrush(Color.FromArgb(a, hc))) g.FillPath(b, hp);
                    if (hs == 1) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * Clamp(ha)), ac))) g.FillRectangle(b, r.X, r.Y + r.Height * 0.2f, 3, r.Height * 0.6f);
                    if (hs == 2) using (Pen p = new Pen(Color.FromArgb((int)(255 * Clamp(ha)), ac), 2)) g.DrawLine(p, r.X + 6, r.Bottom - 1, r.Right - 6, r.Bottom - 1);
                    if (hs == 3) using (Pen p = new Pen(Color.FromArgb((int)(255 * Clamp(ha)), ac), 1.5f)) g.DrawPath(p, hp);
                    if (hs == 5)
                    {
                        for (int i = 3; i >= 1; i--) using (GraphicsPath gp = Shapes.Make(0, hr + i * 2, RectangleF.Inflate(r, i * 2, i * 2))) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(Clamp(ha) * 28), ac))) g.FillPath(b, gp);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, hc))) g.FillPath(b, hp);
                    }
                }
            }
            // items
            string ff = Cfg.S("font"); FontStyle fs = FontStyle.Regular; if (Cfg.B("bold")) fs |= FontStyle.Bold; if (Cfg.B("italic")) fs |= FontStyle.Italic;
            string icf = Cfg.I("iconfont") == 0 ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            Font font = null, ifont = null;
            try { font = new Font(ff, Cfg.I("fsize"), fs, GraphicsUnit.Pixel); } catch { font = new Font("Segoe UI", Cfg.I("fsize"), fs, GraphicsUnit.Pixel); }
            try { ifont = new Font(icf, Cfg.I("isize"), FontStyle.Regular, GraphicsUnit.Pixel); } catch { ifont = new Font("Segoe MDL2 Assets", Cfg.I("isize"), FontStyle.Regular, GraphicsUnit.Pixel); }
            int stag = Cfg.I("stagger"), smode = Cfg.I("staggermode"), padx = Cfg.I("padx"); bool icons = Cfg.B("showicons"); int ta = Cfg.I("textalign"), cs = Cfg.I("case");
            Color tc = Cfg.C("text"), htc = Cfg.C("hovertext"), dc = Cfg.C("dim"), icc = Cfg.C("iconc");
            StringFormat sf = new StringFormat(StringFormat.GenericTypographic); sf.LineAlignment = StringAlignment.Center; sf.FormatFlags |= StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
            StringFormat gf = new StringFormat(sf); gf.Alignment = StringAlignment.Center;
            int target = kbd >= 0 ? kbd : hover;
            for (int i = 0; i < items.Count; i++)
            {
                Entry en = items[i]; RectangleF r = new RectangleF(M + en.R.X, M + en.R.Y, W, en.R.Height);
                float ti = 1f; if (stag > 0) ti = Clamp((ms - i * stag) / Math.Max(60f, Cfg.I("dur") * 0.7f)); float ei = Ease(1, ti);
                float ox = 0, oy = 0; if (smode == 1) ox = -22 * (1 - ei); else if (smode == 2) oy = 14 * (1 - ei); else if (smode == 3) ox = 22 * (1 - ei);
                float ia = ei;
                if (en.Sep)
                {
                    float yy = r.Y + r.Height / 2; int inx = Cfg.I("sepinset");
                    using (Pen p = new Pen(Color.FromArgb((int)(Cfg.I("sepa") * ia), Cfg.C("sepc")), Cfg.I("sepw"))) g.DrawLine(p, r.X + inx + ox, yy, r.Right - inx + ox, yy);
                    continue;
                }
                bool hv = i == target && ha > 0.3f;
                Color col = Color.FromArgb((int)(255 * ia), hv ? htc : tc);
                float x0 = r.X + padx + ox; float y0 = r.Y + oy;
                if (icons && en.Glyph != null && en.Glyph != "")
                {
                    int isz = Cfg.I("isize");
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * ia), icc))) g.DrawString(en.Glyph, ifont, b, new RectangleF(x0, y0, isz + 6, r.Height), gf);
                    x0 += isz + 14;
                }
                string txt = en.Text ?? ""; txt = cs == 1 ? txt.ToUpper() : (cs == 2 ? txt.ToLower() : txt);
                float rightPad = en.Kids != null ? 28 : 0;
                RectangleF tr = new RectangleF(x0, y0, r.Right - padx - x0 + ox - rightPad, r.Height);
                StringFormat tf = new StringFormat(sf); tf.Alignment = ta == 0 ? StringAlignment.Near : (ta == 1 ? StringAlignment.Center : StringAlignment.Far);
                using (SolidBrush b = new SolidBrush(col)) g.DrawString(txt, font, b, tr, tf);
                if (en.Kids != null)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * ia), dc))) g.DrawString("\uE76C", ifont, b, new RectangleF(r.Right - padx - 18 + ox, y0, 20, r.Height), gf);
            }
            font.Dispose(); ifont.Dispose();
        }

        // ---- interaction ----
        int IndexAt(Point cl)
        {
            for (int i = 0; i < items.Count; i++) { RectangleF r = items[i].R; if (cl.Y - M >= r.Y && cl.Y - M < r.Bottom && cl.X >= M && cl.X < M + W) return i; }
            return -1;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location); if (i >= 0 && items[i].Sep) i = -1;
            if (i != hover)
            {
                hover = i; kbd = -1; subT.Stop();
                if (i >= 0 && items[i].Kids != null) { subT.Interval = Math.Max(1, Cfg.I("subdelay")); subT.Start(); }
                else if (child != null) { child.BeginClose(); child = null; }
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { if (child == null) hover = -1; base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !closing)
            {
                int i = IndexAt(e.Location); if (i >= 0 && !items[i].Sep) ActivateIndex(i);
            }
            base.OnMouseUp(e);
        }
        void OpenChild(int i)
        {
            if (i < 0 || i >= items.Count || items[i].Kids == null || closing) return;
            if (child != null) { child.BeginClose(); child = null; }
            Rectangle mr = MenuRect; RectangleF r = items[i].R;
            MenuForm.ShowAt(items[i].Kids, new Point(mr.Right - 4, mr.Top + (int)r.Y - Cfg.I("pad")), this, true);
            child = Open[Open.Count - 1];
        }
        void ActivateIndex(int i)
        {
            Entry en = items[i];
            if (en.Kids != null) { OpenChild(i); return; }
            Action a = en.Act; CloseAll();
            if (a != null) Hook.Post(a);
        }
        // keyboard from hook
        public static MenuForm TopMenu() { for (int i = Open.Count - 1; i >= 0; i--) if (!Open[i].closing) return Open[i]; return null; }
        public void Key(int vk)
        {
            if (vk == 27) { if (parent != null) { parent.child = null; BeginClose(); } else CloseAll(); return; }
            int n = items.Count; if (n == 0) return;
            if (vk == 40 || vk == 38)
            {
                int cur = kbd >= 0 ? kbd : hover; int d = vk == 40 ? 1 : -1; int i = cur;
                for (int c = 0; c < n; c++) { i = ((i < 0 ? (d > 0 ? -1 : n) : i) + d + n) % n; if (!items[i].Sep) break; }
                kbd = i; subT.Stop();
            }
            else if (vk == 13 || vk == 39)
            {
                int cur = kbd >= 0 ? kbd : hover; if (cur >= 0 && !items[cur].Sep) { if (vk == 39 && items[cur].Kids == null) return; ActivateIndex(cur); if (child != null) child.kbd = 0; }
            }
            else if (vk == 37 && parent != null) { parent.child = null; BeginClose(); }
        }
    }

    // ================= Hooks =================
    static class Hook
    {
        delegate IntPtr HookProc(int n, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] struct MSLL { public int x, y; public uint data, flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct KBLL { public uint vk, scan, flags, time; public IntPtr extra; }
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc cb, IntPtr mod, uint tid);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
        [DllImport("user32.dll")] static extern short GetKeyState(int vk);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.PT p);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(Native.PT p, [MarshalAs(UnmanagedType.IUnknown)] out object acc, out object child);

        static HookProc mouseProc = MouseCb, keyProc = KeyCb; static IntPtr mh = IntPtr.Zero, kh = IntPtr.Zero;
        public static bool Bypass, Enabled = true; static bool swallowUp; static Control sync;
        static HashSet<uint> swallowedKeys = new HashSet<uint>();

        public static void Install(Control syncCtl)
        {
            sync = syncCtl;
            mh = SetWindowsHookEx(14, mouseProc, GetModuleHandle(null), 0);
        }
        public static void Remove()
        {
            if (mh != IntPtr.Zero) { UnhookWindowsHookEx(mh); mh = IntPtr.Zero; }
            KeysOn(false);
        }
        public static void KeysOn(bool on)
        {
            if (on && kh == IntPtr.Zero) kh = SetWindowsHookEx(13, keyProc, GetModuleHandle(null), 0);
            else if (!on && kh != IntPtr.Zero) { UnhookWindowsHookEx(kh); kh = IntPtr.Zero; swallowedKeys.Clear(); }
        }
        public static void Post(Action a) { try { sync.BeginInvoke(a); } catch { } }

        static string Cls(IntPtr h) { StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }
        static bool EmptyDesktop(Native.PT pt)
        {
            try
            {
                IntPtr w = WindowFromPoint(pt); if (w == IntPtr.Zero) return false;
                string c = Cls(w);
                if (c == "SysListView32")
                {
                    IntPtr p = GetParent(w); if (p == IntPtr.Zero || Cls(p) != "SHELLDLL_DefView") return false;
                    object acc, child;
                    if (AccessibleObjectFromPoint(pt, out acc, out child) != 0 || acc == null) return false;
                    int cid = 0; try { cid = Convert.ToInt32(child); } catch { return false; }
                    return cid == 0;
                }
                if (c == "Progman" || c == "WorkerW") return true;
                return false;
            }
            catch { return false; }
        }
        static IntPtr MouseCb(int n, IntPtr w, IntPtr l)
        {
            try
            {
                if (n >= 0)
                {
                    int msg = (int)w; MSLL ms = (MSLL)Marshal.PtrToStructure(l, typeof(MSLL));
                    bool inj = (ms.flags & 1) != 0;
                    if (!inj && !Bypass)
                    {
                        if (MenuForm.AnyOpen && (msg == 0x201 || msg == 0x204 || msg == 0x207))
                        {
                            Point p = new Point(ms.x, ms.y);
                            if (!MenuForm.Contains(p)) Post(MenuForm.CloseAll);
                        }
                        if (msg == 0x204 && Enabled && !MenuForm.AnyOpen)
                        {
                            bool shift = (GetKeyState(0x10) & 0x8000) != 0;
                            if (!(shift && Cfg.B("shiftnative")))
                            {
                                Native.PT pt = new Native.PT(); pt.x = ms.x; pt.y = ms.y;
                                if (EmptyDesktop(pt)) { swallowUp = true; Point pp = new Point(ms.x, ms.y); Post(delegate { MenuForm.ShowAt(Spec.Build(), pp, null, false); }); return (IntPtr)1; }
                            }
                        }
                        else if (msg == 0x205 && swallowUp) { swallowUp = false; return (IntPtr)1; }
                    }
                }
            }
            catch { }
            return CallNextHookEx(mh, n, w, l);
        }
        static IntPtr KeyCb(int n, IntPtr w, IntPtr l)
        {
            try
            {
                if (n >= 0 && MenuForm.AnyOpen)
                {
                    KBLL k = (KBLL)Marshal.PtrToStructure(l, typeof(KBLL)); int msg = (int)w;
                    bool down = msg == 0x100 || msg == 0x104;
                    if (k.vk == 27 || k.vk == 38 || k.vk == 40 || k.vk == 13 || k.vk == 37 || k.vk == 39)
                    {
                        if (down) { swallowedKeys.Add(k.vk); uint vk = k.vk; Post(delegate { MenuForm t = MenuForm.TopMenu(); if (t != null) t.Key((int)vk); }); return (IntPtr)1; }
                        else if (swallowedKeys.Remove(k.vk)) return (IntPtr)1;
                    }
                }
            }
            catch { }
            return CallNextHookEx(kh, n, w, l);
        }
    }

    // ================= Settings window =================
    class SettingsForm : Form
    {
        static SettingsForm inst; TabControl tabs; CheckedListBox lst; List<Spec> specs; bool building;
        TextBox tLabel, tGlyph, tCmd, tArgs;
        public static void ShowIt() { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new SettingsForm(); inst.Show(); }
        public SettingsForm()
        {
            Text = "Menu Studio - customize the desktop menu"; Width = 600; Height = 780; StartPosition = FormStartPosition.CenterScreen;
            Build();
        }
        void Build()
        {
            building = true; SuspendLayout(); Controls.Clear();
            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 74;
            Label l1 = new Label(); l1.Text = "Theme:"; l1.Left = 8; l1.Top = 12; l1.AutoSize = true; top.Controls.Add(l1);
            ComboBox th = new ComboBox(); th.DropDownStyle = ComboBoxStyle.DropDownList; th.Left = 60; th.Top = 8; th.Width = 200;
            foreach (string t in Cfg.Themes) th.Items.Add(t.Split('|')[0]);
            th.SelectedIndexChanged += delegate { if (building) return; Cfg.ApplyTheme(th.SelectedIndex); Build(); };
            top.Controls.Add(th);
            Button pv = new Button(); pv.Text = "Preview menu now"; pv.Left = 280; pv.Top = 6; pv.Width = 140; pv.Height = 28;
            pv.Click += delegate { Point c = new Point(Left + Width + 20, Top + 120); Screen s = Screen.FromControl(this); if (c.X + 300 > s.WorkingArea.Right) c.X = Left - 300; MenuForm.CloseAll(); MenuForm.ShowAt(Spec.Build(), c, null, false); };
            top.Controls.Add(pv);
            Button rs = new Button(); rs.Text = "Reset everything"; rs.Left = 430; rs.Top = 6; rs.Width = 140; rs.Height = 28;
            rs.Click += delegate { if (MessageBox.Show("Reset all menu options and items to defaults?", "Menu Studio", MessageBoxButtons.YesNo) == DialogResult.Yes) { Settings.RemovePrefix("p."); Settings.Set("menu", ""); Settings.Save(); Build(); } };
            top.Controls.Add(rs);
            Label hint = new Label(); hint.Text = "Tip: hold Shift while right-clicking the desktop to open the normal Windows menu."; hint.Left = 8; hint.Top = 44; hint.AutoSize = true; top.Controls.Add(hint);
            tabs = new TabControl(); tabs.Dock = DockStyle.Fill; Controls.Add(tabs); Controls.Add(top);
            List<string> cats = new List<string>(); foreach (Prop p in Cfg.All) if (!cats.Contains(p.Cat)) cats.Add(p.Cat);
            foreach (string cat in cats)
            {
                TabPage tp = new TabPage(cat); tp.AutoScroll = true;
                TableLayoutPanel tl = new TableLayoutPanel(); tl.AutoSize = true; tl.ColumnCount = 2; tl.Padding = new Padding(8); tl.Dock = DockStyle.Top; tp.Controls.Add(tl);
                foreach (Prop p in Cfg.All) if (p.Cat == cat) AddRow(tl, p);
                if (cat == "Behavior")
                {
                    CheckBox au = new CheckBox(); au.Text = "Start with Windows"; au.Width = 300; au.Checked = Program.IsAutostart(); au.CheckedChanged += delegate { if (!building) Program.SetAutostart(au.Checked); };
                    int r = tl.RowCount++; tl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tl.Controls.Add(au, 1, r);
                }
                tabs.TabPages.Add(tp);
            }
            tabs.TabPages.Add(BuildItems());
            ResumeLayout(); building = false;
        }
        TabPage BuildItems()
        {
            TabPage tp = new TabPage("Menu items"); specs = Spec.Load();
            lst = new CheckedListBox(); lst.Left = 8; lst.Top = 8; lst.Width = 330; lst.Height = 380; lst.CheckOnClick = true;
            tp.Controls.Add(lst); RefillList(0);
            lst.ItemCheck += delegate (object s, ItemCheckEventArgs e) { if (building || e.Index < 0 || e.Index >= specs.Count) return; specs[e.Index].Vis = e.NewValue == CheckState.Checked; Spec.Save(specs); };
            lst.SelectedIndexChanged += delegate { LoadSel(); };
            int bx = 350; int by = 8;
            Func<string, Action, Button> mk = delegate (string t, Action a) { Button b = new Button(); b.Text = t; b.Left = bx; b.Top = by; b.Width = 200; b.Height = 28; by += 32; b.Click += delegate { a(); }; tp.Controls.Add(b); return b; };
            mk("Move up", delegate { Move(-1); }); mk("Move down", delegate { Move(1); });
            mk("Add separator line", delegate { Insert(Spec.Sep()); });
            mk("Add your own item", delegate { Insert(Spec.Custom("My item", "\uE768", "notepad.exe", "")); });
            mk("Delete selected (custom / line)", delegate { int i = lst.SelectedIndex; if (i >= 0 && specs[i].Kind != "builtin") { specs.RemoveAt(i); Spec.Save(specs); RefillList(Math.Max(0, i - 1)); } });
            mk("Restore default items", delegate { specs = Spec.Defaults(); Spec.Save(specs); RefillList(0); });
            int y = 400;
            Func<string, TextBox> tb = delegate (string lab) { Label l = new Label(); l.Text = lab; l.Left = 8; l.Top = y + 4; l.AutoSize = true; tp.Controls.Add(l); TextBox t = new TextBox(); t.Left = 130; t.Top = y; t.Width = 420; tp.Controls.Add(t); y += 30; return t; };
            tLabel = tb("Text"); tGlyph = tb("Icon (glyph char)"); tCmd = tb("Program / link"); tArgs = tb("Arguments");
            Label note = new Label(); note.Text = "Check = show in menu. Custom items run any program or link (for example notepad.exe, or https://...). Built-in items keep their action; you can rename them and change their icon."; note.Left = 8; note.Top = y + 6; note.Width = 540; note.Height = 60; tp.Controls.Add(note);
            EventHandler save = delegate { if (building) return; int i = lst.SelectedIndex; if (i < 0 || i >= specs.Count || specs[i].Kind == "sep") return; specs[i].Label = tLabel.Text; specs[i].Glyph = tGlyph.Text; if (specs[i].Kind == "custom") { specs[i].Cmd = tCmd.Text; specs[i].Args = tArgs.Text; } Spec.Save(specs); };
            tLabel.TextChanged += save; tGlyph.TextChanged += save; tCmd.TextChanged += save; tArgs.TextChanged += save;
            return tp;
        }
        string Disp(Spec s) { return s.Kind == "sep" ? "------ separator ------" : (s.Label + (s.Kind == "custom" ? "   (yours)" : "")); }
        void RefillList(int sel)
        {
            bool b = building; building = true; lst.Items.Clear();
            foreach (Spec s in specs) lst.Items.Add(Disp(s), s.Vis);
            if (lst.Items.Count > 0) lst.SelectedIndex = Math.Max(0, Math.Min(lst.Items.Count - 1, sel));
            building = b; LoadSel();
        }
        void LoadSel()
        {
            bool b = building; building = true; int i = lst.SelectedIndex;
            if (i >= 0 && i < specs.Count) { Spec s = specs[i]; tLabel.Text = s.Label; tGlyph.Text = s.Glyph; tCmd.Text = s.Cmd; tArgs.Text = s.Args; tLabel.Enabled = tGlyph.Enabled = s.Kind != "sep"; tCmd.Enabled = tArgs.Enabled = s.Kind == "custom"; }
            building = b;
        }
        void Move(int d)
        {
            int i = lst.SelectedIndex, j = i + d; if (i < 0 || j < 0 || j >= specs.Count) return;
            Spec t = specs[i]; specs[i] = specs[j]; specs[j] = t; Spec.Save(specs); RefillList(j);
        }
        void Insert(Spec s) { int i = lst.SelectedIndex < 0 ? specs.Count : lst.SelectedIndex + 1; specs.Insert(i, s); Spec.Save(specs); RefillList(i); }
        void AddRow(TableLayoutPanel tl, Prop p)
        {
            int r = tl.RowCount++; tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label l = new Label(); l.Text = p.Label; l.AutoSize = true; l.MaximumSize = new Size(260, 0); l.Margin = new Padding(0, 6, 8, 0); tl.Controls.Add(l, 0, r);
            Control c = null; string key = p.Key;
            if (p.Kind == "bool") { CheckBox cb = new CheckBox(); cb.Checked = Cfg.B(key); cb.CheckedChanged += delegate { if (building) return; Cfg.Set(key, cb.Checked ? "1" : "0"); Settings.Save(); if (key == "enable") Hook.Enabled = cb.Checked; }; c = cb; }
            else if (p.Kind == "int") { NumericUpDown nu = new NumericUpDown(); nu.Minimum = p.Min; nu.Maximum = p.Max; nu.Value = Math.Max(p.Min, Math.Min(p.Max, Cfg.I(key))); nu.Width = 100; nu.ValueChanged += delegate { if (building) return; Cfg.Set(key, ((int)nu.Value).ToString()); Settings.Save(); }; c = nu; }
            else if (p.Kind == "color")
            {
                Button b = new Button(); b.Width = 110; b.Height = 24; b.FlatStyle = FlatStyle.Flat; b.BackColor = Cfg.C(key);
                b.Click += delegate { using (ColorDialog cd = new ColorDialog()) { cd.Color = b.BackColor; cd.FullOpen = true; if (cd.ShowDialog(this) == DialogResult.OK) { b.BackColor = cd.Color; Cfg.SetColor(key, cd.Color); Settings.Save(); } } }; c = b;
            }
            else if (p.Kind == "choice") { ComboBox cb = new ComboBox(); cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.Width = 260; cb.Items.AddRange(p.Choices); cb.SelectedIndex = Math.Max(0, Math.Min(p.Choices.Length - 1, Cfg.I(key))); cb.SelectedIndexChanged += delegate { if (building) return; Cfg.Set(key, cb.SelectedIndex.ToString()); Settings.Save(); }; c = cb; }
            else if (p.Kind == "font")
            {
                ComboBox cb = new ComboBox(); cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.Width = 260; foreach (FontFamily ff in FontFamily.Families) cb.Items.Add(ff.Name);
                int fi = cb.Items.IndexOf(Cfg.S(key)); if (fi >= 0) cb.SelectedIndex = fi; cb.SelectedIndexChanged += delegate { if (building) return; Cfg.Set(key, (string)cb.SelectedItem); Settings.Save(); }; c = cb;
            }
            if (c != null) { c.Margin = new Padding(0, 3, 0, 3); tl.Controls.Add(c, 1, r); }
        }
    }

    // ================= Program =================
    static class Program
    {
        static NotifyIcon tray; static Control sync;
        [STAThread]
        static void Main()
        {
            bool created;
            System.Threading.Mutex mx = new System.Threading.Mutex(true, "MenuStudio_single_instance", out created);
            if (!created) return;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Settings.Load();
            bool first = !Settings.GetBool("autostart_init", false);
            if (first) { SetAutostart(true); Settings.Set("autostart_init", true); Settings.Save(); } else if (IsAutostart()) SetAutostart(true);
            sync = new Control(); sync.CreateControl(); IntPtr h = sync.Handle;
            Hook.Enabled = Cfg.B("enable"); Hook.Install(sync);
            tray = new NotifyIcon(); tray.Icon = SystemIcons.Application; tray.Text = "Menu Studio"; tray.Visible = true;
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Customize menu...", null, delegate { SettingsForm.ShowIt(); });
            m.Items.Add("Preview menu here", null, delegate { MenuForm.CloseAll(); MenuForm.ShowAt(Spec.Build(), Cursor.Position, null, false); });
            ToolStripMenuItem en = new ToolStripMenuItem("Replace desktop right-click menu"); en.CheckOnClick = true; en.Checked = Cfg.B("enable");
            en.Click += delegate { Cfg.Set("enable", en.Checked ? "1" : "0"); Settings.Save(); Hook.Enabled = en.Checked; };
            m.Items.Add(en);
            ToolStripMenuItem au = new ToolStripMenuItem("Start with Windows"); au.Checked = IsAutostart(); au.Click += delegate { SetAutostart(!IsAutostart()); au.Checked = IsAutostart(); };
            m.Items.Add(au);
            m.Items.Add("Exit (restores the normal menu)", null, delegate { Exit(); });
            tray.ContextMenuStrip = m;
            tray.MouseClick += delegate (object so, MouseEventArgs me) { if (me.Button == MouseButtons.Left) SettingsForm.ShowIt(); };
            if (first) { tray.ShowBalloonTip(8000, "Menu Studio is running", "Right-click an empty spot on the desktop. Hold Shift for the normal Windows menu. Click this icon to customize.", ToolTipIcon.Info); SettingsForm.ShowIt(); }
            Application.ApplicationExit += delegate { Hook.Remove(); };
            Application.Run();
            mx.ReleaseMutex();
        }
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool IsAutostart() { try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("MenuStudio") != null; } catch { return false; } }
        public static void SetAutostart(bool on)
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) { if (on) k.SetValue("MenuStudio", "\"" + Application.ExecutablePath + "\""); else k.DeleteValue("MenuStudio", false); } } catch { }
        }
        static void Exit() { Hook.Remove(); MenuForm.CloseAll(); tray.Visible = false; Settings.Save(); Application.Exit(); }
    }
}
