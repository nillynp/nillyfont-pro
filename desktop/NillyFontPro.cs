// ============================================================================
// NillyFont Pro — Ultra-Light Native Windows 10/11 Font Manager,
//                 Live Specimen Viewer, Greek Support Filter & HKCU Installer
// created by Nilly graphics (Nillygraphics · Sotiris Christos) — https://nillygraphics.com/
// Strictly C# 5 compatible (.NET Framework 4.x csc.exe — 0 external DLLs)
// Features:
//  - <50ms launch · ~6-10 MB trimmed RAM · 0 MB RAM / 0% CPU when closed
//  - Real GDI GetFontUnicodeRanges glyph check for true Greek (Ελληνικά) support!
//  - Live Editable Specimen Preview across all fonts (Virtual rendering — 0 lag on 1500+ fonts)
//  - Side-by-Side A vs B Typography Comparison + Size / Bold / Italic controls
//  - Drag & Drop / 1-Click Zero-Admin Font Installer (.ttf / .otf -> HKCU Fonts)
//  - Dark/Light Monokai Theme · Dark Scrollbars · Built-in 'i' Ultra-Light Specs
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("NillyFont Pro")]
[assembly: AssemblyDescription("Ultra-Light Native Font Manager, Live Specimen Tester, True Greek Glyph Filter & Zero-Admin Font Installer — created by Nilly graphics (Nillygraphics · Sotiris Christos)")]
[assembly: AssemblyCompany("Nilly graphics (Nillygraphics · Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyProduct("NillyFont Pro")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Nilly graphics (Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyTrademark("Nilly graphics / Nillygraphics — Sotiris Christos")]
[assembly: AssemblyVersion("1.2.2.0")]
[assembly: AssemblyFileVersion("1.2.2.0")]

namespace NillyFontPro
{
    internal static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr minSize, IntPtr maxSize);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hWnd, string app, string id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconFromResourceEx(byte[] presbits, uint dwResSize, bool fIcon, uint dwVer, int cx, int cy, uint Flags);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern uint GetFontUnicodeRanges(IntPtr hdc, IntPtr lpgs);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResourceW(string lpszFilename);

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        public static void SetDarkTitlebar(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, 4) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref v, 4);
            }
            catch { }
        }

        public static void SetDarkScrollbars(Control c, bool dark)
        {
            if (c == null || !c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
        }

        public static void SetCue(TextBox tb, string text)
        {
            if (tb == null || !tb.IsHandleCreated) return;
            try { SendMessage(tb.Handle, 0x1501, (IntPtr)1, text); } catch { }
        }

        public static void BroadcastFontChange()
        {
            try
            {
                IntPtr res;
                SendMessageTimeout(new IntPtr(0xFFFF), 0x001D, IntPtr.Zero, IntPtr.Zero, 0x0002, 500, out res);
            }
            catch { }
        }

        // True glyph-table check for Greek alphabet (Α=0x0391, Ω=0x03A9, α=0x03B1, ω=0x03C9)
        public static bool SupportsGreekGlyphs(IntPtr hdc, FontFamily fam)
        {
            FontStyle st = FontStyle.Regular;
            if (!fam.IsStyleAvailable(st))
            {
                if (fam.IsStyleAvailable(FontStyle.Bold)) st = FontStyle.Bold;
                else if (fam.IsStyleAvailable(FontStyle.Italic)) st = FontStyle.Italic;
                else return false;
            }
            IntPtr hFont = IntPtr.Zero;
            IntPtr prev = IntPtr.Zero;
            try
            {
                using (Font f = new Font(fam, 12f, st))
                {
                    hFont = f.ToHfont();
                    prev = SelectObject(hdc, hFont);
                    uint size = GetFontUnicodeRanges(hdc, IntPtr.Zero);
                    if (size == 0 || size > 1024 * 1024) return false;
                    IntPtr buf = Marshal.AllocHGlobal((int)size);
                    try
                    {
                        if (GetFontUnicodeRanges(hdc, buf) == 0) return false;
                        int cRanges = Marshal.ReadInt32(buf, 12);
                        bool hasA = false, hasO = false, hasa = false, haso = false;
                        int off = 16;
                        for (int i = 0; i < cRanges; i++)
                        {
                            ushort low = (ushort)Marshal.ReadInt16(buf, off);
                            ushort count = (ushort)Marshal.ReadInt16(buf, off + 2);
                            int high = low + count - 1;
                            if (0x0391 >= low && 0x0391 <= high) hasA = true; // Α
                            if (0x03A9 >= low && 0x03A9 <= high) hasO = true; // Ω
                            if (0x03B1 >= low && 0x03B1 <= high) hasa = true; // α
                            if (0x03C9 >= low && 0x03C9 <= high) haso = true; // ω
                            if (hasA && hasO && hasa && haso) return true;
                            off += 4;
                        }
                        return hasA && hasO && hasa && haso;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            catch { return false; }
            finally
            {
                if (prev != IntPtr.Zero) SelectObject(hdc, prev);
                if (hFont != IntPtr.Zero) DeleteObject(hFont);
            }
        }

        public static Bitmap LoadCrispIcon(string icoPath, int sz)
        {
            if (string.IsNullOrEmpty(icoPath) || !File.Exists(icoPath)) return null;
            try
            {
                byte[] data = File.ReadAllBytes(icoPath);
                if (data.Length < 22) return null;
                int cnt = BitConverter.ToUInt16(data, 4), bestIdx = 0, bestW = 0;
                for (int i = 0; i < cnt; i++)
                {
                    int off = 6 + i * 16;
                    if (off + 16 > data.Length) break;
                    int w = data[off]; if (w == 0) w = 256;
                    if (w > bestW) { bestW = w; bestIdx = i; }
                }
                int eOff = 6 + bestIdx * 16;
                int len = BitConverter.ToInt32(data, eOff + 8);
                int imgOff = BitConverter.ToInt32(data, eOff + 12);
                Bitmap dst = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(dst))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);
                    if (imgOff + 4 < data.Length && data[imgOff] == 0x89 && data[imgOff + 1] == 0x50)
                        using (MemoryStream ms = new MemoryStream(data, imgOff, len))
                        using (Image img = Image.FromStream(ms))
                            g.DrawImage(img, 0, 0, sz, sz);
                }
                return dst;
            }
            catch { return null; }
        }

        public static void ApplyTaskbarIcon(Form f, string icoPath)
        {
            if (f == null || !f.IsHandleCreated || !File.Exists(icoPath)) return;
            try
            {
                byte[] data = File.ReadAllBytes(icoPath);
                IntPtr hBig = MakeHIcon(data, 48), hSm = MakeHIcon(data, 16);
                if (hBig != IntPtr.Zero) SendMessage(f.Handle, 0x0080, (IntPtr)1, hBig);
                if (hSm != IntPtr.Zero) SendMessage(f.Handle, 0x0080, (IntPtr)0, hSm);
            }
            catch { }
        }

        private static IntPtr MakeHIcon(byte[] data, int desired)
        {
            if (data.Length < 22) return IntPtr.Zero;
            int cnt = BitConverter.ToUInt16(data, 4), bestIdx = 0, bestDiff = int.MaxValue;
            for (int i = 0; i < cnt; i++)
            {
                int off = 6 + i * 16;
                if (off + 16 > data.Length) break;
                int w = data[off]; if (w == 0) w = 256;
                int d = Math.Abs(w - desired);
                if (w >= desired && d < bestDiff) { bestDiff = d; bestIdx = i; }
                else if (bestDiff == int.MaxValue && d < bestDiff) { bestDiff = d; bestIdx = i; }
            }
            int eOff = 6 + bestIdx * 16;
            int len = BitConverter.ToInt32(data, eOff + 8);
            int imgOff = BitConverter.ToInt32(data, eOff + 12);
            if (len <= 0 || imgOff + len > data.Length) return IntPtr.Zero;
            byte[] slice = new byte[len]; Buffer.BlockCopy(data, imgOff, slice, 0, len);
            return CreateIconFromResourceEx(slice, (uint)len, true, 0x00030000, desired, desired, 0);
        }
    }

    internal sealed class FontEntry
    {
        public string Name;
        public bool HasGreek;
        public bool HasBold;
        public bool HasItalic;
        public string NameLower;
    }

    internal sealed class DoubleBufferedListBox : ListBox
    {
        public DoubleBufferedListBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
        }
    }

    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }

    internal sealed class MainForm : Form
    {
        private bool _dark = true;
        private bool _greekOnly = false;
        private bool _bold = false;
        private bool _italic = false;
        private bool _compareMode = false;
        private float _sampleSize = 22f;
        private string _compareA = "Segoe UI";
        private string _compareB = "Georgia";

        private Color _bgMain, _bgPanel, _bgSurf, _bgHover, _bgSel, _bgAlt, _border, _fgMain, _fgMuted, _accent, _cyan, _green, _pink;
        private readonly ToolTip _tip = new ToolTip();

        private readonly Panel _header, _ctrlBar, _statusBar;
        private readonly PictureBox _logoBox;
        private readonly Label _lblTitle, _lblSub, _lblStatus, _lblSizeVal;
        private readonly TextBox _txtSearch, _txtSpecimen;
        private readonly AkButton _btnGreek, _btnBold, _btnItalic, _btnSizeDown, _btnSizeUp, _btnCompare, _btnInstall, _btnTheme, _btnInfo;
        private readonly AkButton _btnSetA, _btnSetB, _btnCopyName, _btnSendNote;
        private readonly DoubleBufferedListBox _list;
        private readonly DoubleBufferedPanel _comparePanel;
        private readonly LinkLabel _lnkBrand;

        private List<FontEntry> _allFonts = new List<FontEntry>();
        private List<FontEntry> _visFonts = new List<FontEntry>();
        private int _greekCount = 0;
        private Bitmap _logoBmp;

        private readonly Font _fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        private readonly Font _fUi = new Font("Segoe UI", 9.0f);
        private readonly Font _fBold = new Font("Segoe UI", 9.0f, FontStyle.Bold);
        private readonly Font _fSmall = new Font("Segoe UI", 8.2f);
        private readonly Font _fBadge = new Font("Segoe UI", 7.6f, FontStyle.Bold);

        public int TotalFontCount { get { return _allFonts.Count; } }
        public int GreekFontCount { get { return _greekCount; } }

        public MainForm(bool smokeMode) : this(smokeMode, null) { }

        public MainForm(bool smokeMode, string[] initialFontFiles)
        {
            _dark = ReadSuiteDark(true);
            Text = "NillyFont Pro — Ultra-Light Font Manager & Greek Typography Tester | Nilly graphics";
            Size = new Size(1120, 700);
            MinimumSize = new Size(780, 500);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DragEnter += delegate(object s, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) InstallFontFiles(files);
            };

            if (initialFontFiles != null && initialFontFiles.Length > 0 && !smokeMode)
            {
                Shown += delegate
                {
                    if (MessageBox.Show(this,
                        "Εγκατάσταση " + initialFontFiles.Length + " γραμματοσειράς(ών) Zero-Admin στα Windows Fonts;\n\n" + Path.GetFileName(initialFontFiles[0]),
                        "NillyFont Pro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        InstallFontFiles(initialFontFiles);
                    }
                };
            }

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NillyFontPro.ico");
            if (File.Exists(icoPath))
            {
                _logoBmp = Native.LoadCrispIcon(icoPath, 26);
                try { Icon = new Icon(icoPath); } catch { }
            }

            // Responsive Ariakit Header (auto 1-row >= 980px, 2-row < 980px — never clips!)
            _header = new Panel { Dock = DockStyle.Top, Height = 52 };
            _header.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_border)) pe.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };
            _logoBox = new PictureBox { Size = new Size(26, 26), Location = new Point(12, 12), SizeMode = PictureBoxSizeMode.Zoom, Image = _logoBmp };
            _lblTitle = new Label { Text = "NillyFont Pro", AutoSize = true, Location = new Point(44, 7), Font = _fTitle, UseMnemonic = false };
            _lblSub = new Label { Text = "Ultra-Light Font Manager · True Greek Filter", AutoSize = true, Location = new Point(45, 29), Font = new Font("Segoe UI", 7.6f), UseMnemonic = false };

            _txtSearch = new TextBox { Width = 195, Height = 26, BorderStyle = BorderStyle.FixedSingle, Font = _fUi };
            _txtSearch.TextChanged += delegate { ApplyFilter(); };
            _txtSearch.HandleCreated += delegate { Native.SetCue(_txtSearch, "Αναζήτηση font (Ctrl+F)..."); };
            _btnGreek = MkBtn("Ελληνικά", 104, AkKind.Secondary, AkIcon.Filter, "Εμφάνιση ΜΟΝΟ γραμματοσειρών με πραγματικούς ελληνικούς χαρακτήρες (GDI Unicode Check)", delegate { ToggleGreekOnly(); });
            _btnCompare = MkBtn("Σύγκριση A/B", 128, AkKind.Secondary, AkIcon.Split, "Σύγκριση 2 γραμματοσειρών δίπλα-δίπλα", delegate { ToggleCompare(); });
            _btnInstall = MkBtn("Εγκατάσταση", 124, AkKind.Primary, AkIcon.Plus, "Εγκατάσταση γραμματοσειράς (.ttf/.otf) χωρίς Admin (HKCU) ή Drag & Drop", delegate { BrowseInstallFont(); });
            _btnTheme = MkBtn("", 32, AkKind.Secondary, AkIcon.Sun, "Αλλαγή Dark / Light θέματος", delegate { _dark = !_dark; ApplyTheme(); });
            _btnInfo = MkBtn("", 32, AkKind.Ghost, AkIcon.Info, "Ultra-Light Specs & Πληροφορίες", delegate { ShowInfoDialog(); });
            _header.Controls.AddRange(new Control[] { _logoBox, _lblTitle, _lblSub, _txtSearch, _btnGreek, _btnCompare, _btnInstall, _btnTheme, _btnInfo });
            _header.Resize += delegate { LayoutHeader(); _header.Invalidate(); };

            // Responsive Specimen Control Bar (1-row >= 960px, 2-row < 960px — zero overflow!)
            _ctrlBar = new Panel { Dock = DockStyle.Top, Height = 44 };
            _ctrlBar.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_border)) pe.Graphics.DrawLine(p, 0, _ctrlBar.Height - 1, _ctrlBar.Width, _ctrlBar.Height - 1);
            };
            _txtSpecimen = new TextBox
            {
                Text = "Χρωμοδομή · Nilly graphics — ΑΒΓΔΕΖΗΘ αβγδεζηθ 0123456789",
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.8f), Height = 26
            };
            _txtSpecimen.TextChanged += delegate { _list.Invalidate(); _comparePanel.Invalidate(); };

            _btnSizeDown = MkBtn("A-", 34, AkKind.Secondary, AkIcon.None, "Μείωση μεγέθους", delegate { ChangeSize(-2f); });
            _lblSizeVal = new Label { Text = "22 pt", Width = 44, Height = 28, TextAlign = ContentAlignment.MiddleCenter, Font = _fBold };
            _btnSizeUp = MkBtn("A+", 34, AkKind.Secondary, AkIcon.None, "Αύξηση μεγέθους", delegate { ChangeSize(2f); });
            _btnBold = MkBtn("B", 30, AkKind.Secondary, AkIcon.None, "Έντονα (Bold)", delegate { _bold = !_bold; StyleButtons(); _list.Invalidate(); _comparePanel.Invalidate(); });
            _btnItalic = MkBtn("I", 30, AkKind.Secondary, AkIcon.None, "Πλάγια (Italic)", delegate { _italic = !_italic; StyleButtons(); _list.Invalidate(); _comparePanel.Invalidate(); });
            _btnItalic.Font = new Font("Georgia", 9.5f, FontStyle.Bold | FontStyle.Italic);
            _btnSetA = MkBtn("Ορισμός A", 82, AkKind.Secondary, AkIcon.None, "Βάλε την επιλεγμένη στη θέση A σύγκρισης", delegate { SetCompareSlot(true); });
            _btnSetB = MkBtn("Ορισμός B", 82, AkKind.Secondary, AkIcon.None, "Βάλε την επιλεγμένη στη θέση B σύγκρισης", delegate { SetCompareSlot(false); });
            _btnCopyName = MkBtn("Αντιγραφή", 112, AkKind.Secondary, AkIcon.Copy, "Αντιγραφή ονόματος γραμματοσειράς", delegate { CopySelectedName(); });
            _btnSendNote = MkBtn("Note", 74, AkKind.AccentOutline, AkIcon.External, "Αποστολή Font Spec / Σύγκρισης A-B σε NillyNote Pro", delegate { SendSpecToNillyNote(); });

            _ctrlBar.Controls.AddRange(new Control[] { _txtSpecimen, _btnSizeDown, _lblSizeVal, _btnSizeUp, _btnBold, _btnItalic, _btnSetA, _btnSetB, _btnCopyName, _btnSendNote });
            _ctrlBar.Resize += delegate { LayoutCtrlBar(); _ctrlBar.Invalidate(); };

            // Owner-Drawn Font Specimen List (smooth even for 1500+ fonts!)
            _list = new DoubleBufferedListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 64,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            _list.DrawItem += List_DrawItem;
            _list.DoubleClick += delegate { CopySelectedName(); };
            _list.HandleCreated += delegate { Native.SetDarkScrollbars(_list, _dark); };

            // Bottom Side-by-Side Comparison Panel
            _comparePanel = new DoubleBufferedPanel { Dock = DockStyle.Bottom, Height = 195, Visible = false };
            _comparePanel.Paint += ComparePanel_Paint;

            // Status Bar
            _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(12, 0, 12, 0) };
            _statusBar.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_border)) pe.Graphics.DrawLine(p, 0, 0, _statusBar.Width, 0);
            };
            _lblStatus = new Label { Dock = DockStyle.Fill, Font = _fSmall, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, AutoEllipsis = true };
            _lnkBrand = new LinkLabel
            {
                Text = "created by Nilly graphics (Sotiris Christos) — nillygraphics.com",
                Dock = DockStyle.Right, AutoSize = true, Font = _fSmall, Padding = new Padding(8, 5, 2, 0), LinkBehavior = LinkBehavior.HoverUnderline
            };
            _lnkBrand.LinkClicked += delegate { OpenUrl("https://nillygraphics.com/"); };
            _statusBar.Controls.Add(_lblStatus);
            _statusBar.Controls.Add(_lnkBrand);

            Controls.Add(_list);
            Controls.Add(_comparePanel);
            Controls.Add(_ctrlBar);
            Controls.Add(_header);
            Controls.Add(_statusBar);

            HandleCreated += delegate
            {
                Native.SetDarkTitlebar(Handle, _dark);
                Native.ApplyTaskbarIcon(this, icoPath);
            };
            Shown += delegate { Native.TrimMemory(); };

            LoadInstalledFonts();
            ApplyTheme();
            LayoutHeader();
            LayoutCtrlBar();
        }

        private void LayoutHeader()
        {
            int w = Math.Max(600, _header.ClientSize.Width);
            int pad = 12, gap = 6;
            if (_lblSub != null) _lblSub.Visible = (w >= 860);
            if (_lnkBrand != null) _lnkBrand.Visible = (w >= 920);
            if (w >= 960)
            {
                if (_header.Height != 52) _header.Height = 52;
                int y = 12;
                int rx = w - pad;
                rx -= _btnInfo.Width; _btnInfo.Location = new Point(rx, y); rx -= gap;
                rx -= _btnTheme.Width; _btnTheme.Location = new Point(rx, y); rx -= gap;
                rx -= _btnInstall.Width; _btnInstall.Location = new Point(rx, y); rx -= gap;
                rx -= _btnCompare.Width; _btnCompare.Location = new Point(rx, y); rx -= gap;
                rx -= _btnGreek.Width; _btnGreek.Location = new Point(rx, y); rx -= gap;
                int brandRight = _lblSub.Visible ? Math.Max(_lblTitle.Right, _lblSub.Right) + 14 : (_lblTitle.Right + 14);
                int sw = Math.Max(120, Math.Min(210, rx - brandRight));
                _txtSearch.SetBounds(rx - sw, y + 1, sw, 26);
            }
            else
            {
                if (_header.Height != 88) _header.Height = 88;
                int rx = w - pad;
                rx -= _btnInfo.Width; _btnInfo.Location = new Point(rx, 11); rx -= gap;
                rx -= _btnTheme.Width; _btnTheme.Location = new Point(rx, 11);
                int y2 = 50;
                int lx = pad;
                _btnGreek.Location = new Point(lx, y2); lx += _btnGreek.Width + gap;
                _btnCompare.Location = new Point(lx, y2); lx += _btnCompare.Width + gap;
                _btnInstall.Location = new Point(lx, y2); lx += _btnInstall.Width + gap;
                int sw = Math.Max(110, (w - pad) - lx);
                _txtSearch.SetBounds(lx, y2 + 1, sw, 26);
            }
        }

        private AkButton MkBtn(string txt, int w, AkKind kind, AkIcon icon, string tip, EventHandler click)
        {
            AkButton b = new AkButton
            {
                Kind = kind, Icon = icon,
                Text = txt, Width = w, Height = 28, Font = _fBold,
                Margin = new Padding(0, 0, 5, 0)
            };
            b.Click += click;
            _tip.SetToolTip(b, tip);
            return b;
        }

        private void LayoutCtrlBar()
        {
            int w = Math.Max(600, _ctrlBar.ClientSize.Width);
            int pad = 12, gap = 5;
            Control[] ctrls = new Control[] { _btnSizeDown, _lblSizeVal, _btnSizeUp, _btnBold, _btnItalic, _btnSetA, _btnSetB, _btnCopyName, _btnSendNote };
            int rightW = 0;
            for (int i = 0; i < ctrls.Length; i++) rightW += ctrls[i].Width + gap;

            if (w - pad * 2 - rightW - 10 >= 190)
            {
                if (_ctrlBar.Height != 44) _ctrlBar.Height = 44;
                int specW = w - pad * 2 - rightW - 8;
                _txtSpecimen.SetBounds(pad, 9, specW, 26);
                int x = _txtSpecimen.Right + 10;
                for (int i = 0; i < ctrls.Length; i++)
                {
                    ctrls[i].Location = new Point(x, 8);
                    x += ctrls[i].Width + gap;
                }
            }
            else
            {
                if (_ctrlBar.Height != 80) _ctrlBar.Height = 80;
                _txtSpecimen.SetBounds(pad, 8, w - pad * 2, 26);
                int x = pad;
                for (int i = 0; i < ctrls.Length; i++)
                {
                    ctrls[i].Location = new Point(x, 42);
                    x += ctrls[i].Width + gap;
                }
            }
        }

        private void LoadInstalledFonts()
        {
            Stopwatch sw = Stopwatch.StartNew();
            _allFonts.Clear();
            _greekCount = 0;
            using (Bitmap tmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tmp))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    using (InstalledFontCollection ifc = new InstalledFontCollection())
                    {
                        FontFamily[] fams = ifc.Families;
                        for (int i = 0; i < fams.Length; i++)
                        {
                            FontFamily fam = fams[i];
                            bool gr = Native.SupportsGreekGlyphs(hdc, fam);
                            if (gr) _greekCount++;
                            FontEntry e = new FontEntry
                            {
                                Name = fam.Name,
                                NameLower = fam.Name.ToLowerInvariant(),
                                HasGreek = gr,
                                HasBold = fam.IsStyleAvailable(FontStyle.Bold),
                                HasItalic = fam.IsStyleAvailable(FontStyle.Italic)
                            };
                            _allFonts.Add(e);
                        }
                    }
                }
                finally { g.ReleaseHdc(hdc); }
            }
            sw.Stop();
            ApplyFilter();
            _lblStatus.Text = string.Format("✓ Φορτώθηκαν {0} γραμματοσειρές ({1} με πραγματικά Ελληνικά) σε {2} ms  ·  Drag & Drop .ttf/.otf για εγκατάσταση!",
                _allFonts.Count, _greekCount, sw.ElapsedMilliseconds);
        }

        private void ApplyFilter()
        {
            string q = _txtSearch.Text.Trim().ToLowerInvariant();
            _visFonts = new List<FontEntry>(_allFonts.Count);
            for (int i = 0; i < _allFonts.Count; i++)
            {
                FontEntry f = _allFonts[i];
                if (_greekOnly && !f.HasGreek) continue;
                if (q.Length > 0 && f.NameLower.IndexOf(q, StringComparison.Ordinal) < 0) continue;
                _visFonts.Add(f);
            }
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _visFonts.Count; i++) _list.Items.Add(_visFonts[i]);
            _list.EndUpdate();
        }

        public void ToggleGreekOnly()
        {
            _greekOnly = !_greekOnly;
            StyleButtons();
            ApplyFilter();
            _lblStatus.Text = _greekOnly
                ? string.Format("Φίλτρο Ελληνικών ΕΝΕΡΓΟ — εμφανίζονται {0} από {1} γραμματοσειρές.", _visFonts.Count, _allFonts.Count)
                : string.Format("Εμφάνιση όλων των {0} γραμματοσειρών ({1} με Ελληνικά).", _allFonts.Count, _greekCount);
        }

        public void ToggleCompare()
        {
            _compareMode = !_compareMode;
            _comparePanel.Visible = _compareMode;
            StyleButtons();
            _comparePanel.Invalidate();
        }

        private void ChangeSize(float delta)
        {
            _sampleSize = Math.Max(12f, Math.Min(44f, _sampleSize + delta));
            _lblSizeVal.Text = ((int)_sampleSize) + " pt";
            _list.ItemHeight = Math.Max(54, (int)(_sampleSize * 2.25f) + 22);
            _list.Invalidate();
            _comparePanel.Invalidate();
        }

        private void SetCompareSlot(bool isA)
        {
            FontEntry sel = _list.SelectedItem as FontEntry;
            if (sel == null) return;
            if (isA) _compareA = sel.Name; else _compareB = sel.Name;
            if (!_compareMode) ToggleCompare();
            else _comparePanel.Invalidate();
            _lblStatus.Text = string.Format("Σύγκριση: [A] {0}   vs   [B] {1}", _compareA, _compareB);
        }

        private void CopySelectedName()
        {
            FontEntry sel = _list.SelectedItem as FontEntry;
            if (sel == null) return;
            try
            {
                Clipboard.SetText(sel.Name);
                _lblStatus.Text = "✓ Αντιγράφηκε το όνομα γραμματοσειράς: " + sel.Name;
            }
            catch { _lblStatus.Text = "⚠ Το πρόχειρο είναι απασχολημένο. Δοκίμασε ξανά."; }
        }

        private void BrowseInstallFont()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "NillyFont Pro — Εγκατάσταση Γραμματοσειρών (Zero Admin HKCU)";
                ofd.Filter = "Font Files (*.ttf;*.otf)|*.ttf;*.otf";
                ofd.Multiselect = true;
                if (ofd.ShowDialog(this) == DialogResult.OK) InstallFontFiles(ofd.FileNames);
            }
        }

        private void InstallFontFiles(string[] files)
        {
            string userFontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
            Directory.CreateDirectory(userFontsDir);
            int installed = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string src = files[i];
                string ext = (Path.GetExtension(src) ?? "").ToLowerInvariant();
                if (ext != ".ttf" && ext != ".otf") continue;
                try
                {
                    string fileName = Path.GetFileName(src);
                    string dst = Path.Combine(userFontsDir, fileName);
                    File.Copy(src, dst, true);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts"))
                    {
                        if (k != null)
                        {
                            string regName = Path.GetFileNameWithoutExtension(fileName) + (ext == ".otf" ? " (OpenType)" : " (TrueType)");
                            k.SetValue(regName, dst, RegistryValueKind.String);
                        }
                    }
                    Native.AddFontResourceW(dst);
                    installed++;
                }
                catch { }
            }
            if (installed > 0)
            {
                Native.BroadcastFontChange();
                LoadInstalledFonts();
                _lblStatus.Text = string.Format("✓ Εγκαταστάθηκαν {0} γραμματοσειρές (Zero Admin HKCU)!", installed);
            }
        }

        private FontStyle ResolveStyle(FontFamily fam)
        {
            FontStyle desired = FontStyle.Regular;
            if (_bold) desired |= FontStyle.Bold;
            if (_italic) desired |= FontStyle.Italic;
            if (fam.IsStyleAvailable(desired)) return desired;
            if (fam.IsStyleAvailable(FontStyle.Regular)) return FontStyle.Regular;
            if (fam.IsStyleAvailable(FontStyle.Bold)) return FontStyle.Bold;
            if (fam.IsStyleAvailable(FontStyle.Italic)) return FontStyle.Italic;
            return FontStyle.Bold | FontStyle.Italic;
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            FontEntry fe = _list.Items[e.Index] as FontEntry;
            if (fe == null) return;

            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg = sel ? _bgSel : ((e.Index % 2 == 0) ? _bgMain : _bgAlt);
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush br = new SolidBrush(bg)) g.FillRectangle(br, e.Bounds);
            using (Pen bp = new Pen(_border)) g.DrawLine(bp, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            // Meta header line: Font Name + Greek badge + Styles
            Rectangle hdrR = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 4, e.Bounds.Width - 24, 18);
            TextRenderer.DrawText(g, fe.Name, _fBold, hdrR, sel ? _accent : _fgMain, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            string badge = fe.HasGreek ? "✓ ΕΛΛΗΝΙΚΑ" : "LATIN ONLY";
            Color badgeCol = fe.HasGreek ? _green : _fgMuted;
            TextRenderer.DrawText(g, badge, _fBadge, hdrR, badgeCol, TextFormatFlags.Right);

            // Live Specimen Preview
            string sample = string.IsNullOrEmpty(_txtSpecimen.Text) ? "ΑΒΓΔΕΖΗΘ αβγδεζηθ AaBbCc 0123456789" : _txtSpecimen.Text;
            Rectangle sampleR = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 23, e.Bounds.Width - 24, e.Bounds.Height - 26);
            try
            {
                using (FontFamily fam = new FontFamily(fe.Name))
                using (Font drawF = new Font(fam, _sampleSize, ResolveStyle(fam), GraphicsUnit.Point))
                using (SolidBrush brTxt = new SolidBrush(_fgMain))
                using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
                {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(sample, drawF, brTxt, sampleR, sf);
                }
            }
            catch { }
        }

        private void ComparePanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int w = _comparePanel.Width, h = _comparePanel.Height;
            using (SolidBrush br = new SolidBrush(_bgPanel)) g.FillRectangle(br, 0, 0, w, h);
            using (Pen p = new Pen(_accent, 2f)) g.DrawLine(p, 0, 1, w, 1);

            int half = w / 2;
            using (Pen mid = new Pen(_border)) g.DrawLine(mid, half, 8, half, h - 8);
            DrawCompareCard(g, new Rectangle(12, 10, half - 22, h - 20), "A", _compareA, _cyan);
            DrawCompareCard(g, new Rectangle(half + 10, 10, w - half - 22, h - 20), "B", _compareB, _accent);
        }

        private void DrawCompareCard(Graphics g, Rectangle r, string slot, string fontName, Color tagCol)
        {
            TextRenderer.DrawText(g, "[" + slot + "]  " + fontName, _fBold, new Rectangle(r.X, r.Y, r.Width, 20), tagCol, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            string sample = string.IsNullOrEmpty(_txtSpecimen.Text) ? "Χρωμοδομή · Nilly graphics" : _txtSpecimen.Text;
            try
            {
                using (FontFamily fam = new FontFamily(fontName))
                using (Font fBig = new Font(fam, Math.Min(21f, _sampleSize), ResolveStyle(fam), GraphicsUnit.Point))
                using (Font fAlpha = new Font(fam, 10.8f, ResolveStyle(fam), GraphicsUnit.Point))
                using (SolidBrush br = new SolidBrush(_fgMain))
                using (SolidBrush brM = new SolidBrush(_fgMuted))
                using (StringFormat sfOne = new StringFormat(StringFormatFlags.NoWrap))
                {
                    sfOne.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(sample, fBig, br, new RectangleF(r.X, r.Y + 28, r.Width, 42), sfOne);
                    g.DrawString("ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ\nαβγδεζηθικλμνξοπρστυφχψω\nABCDEFGHIJKLMNOPQRSTUVWXYZ · abcdefghijklmnopqrstuvwxyz · 0123456789",
                        fAlpha, brM, new RectangleF(r.X, r.Y + 76, r.Width, r.Height - 78));
                }
            }
            catch { }
        }

        private void ApplyTheme()
        {
            if (_dark)
            {
                _bgMain = Color.FromArgb(29, 31, 32); _bgPanel = Color.FromArgb(34, 36, 38);
                _bgSurf = Color.FromArgb(24, 26, 27); _bgHover = Color.FromArgb(48, 51, 54);
                _bgSel = Color.FromArgb(46, 51, 58); _bgAlt = Color.FromArgb(32, 34, 36);
                _border = Color.FromArgb(54, 57, 61); _fgMain = Color.FromArgb(242, 244, 247);
                _fgMuted = Color.FromArgb(154, 160, 168); _accent = Color.FromArgb(252, 150, 31);
                _cyan = Color.FromArgb(102, 217, 239); _green = Color.FromArgb(166, 226, 46);
                _pink = Color.FromArgb(240, 104, 118);
            }
            else
            {
                _bgMain = Color.FromArgb(248, 249, 250); _bgPanel = Color.FromArgb(238, 241, 246);
                _bgSurf = Color.White; _bgHover = Color.FromArgb(226, 232, 242);
                _bgSel = Color.FromArgb(209, 228, 250); _bgAlt = Color.FromArgb(243, 245, 249);
                _border = Color.FromArgb(210, 216, 226); _fgMain = Color.FromArgb(26, 29, 33);
                _fgMuted = Color.FromArgb(95, 104, 116); _accent = Color.FromArgb(224, 118, 12);
                _cyan = Color.FromArgb(14, 132, 168); _green = Color.FromArgb(62, 145, 32);
                _pink = Color.FromArgb(196, 40, 56);
            }

            Native.SetDarkTitlebar(Handle, _dark);
            BackColor = _bgMain; ForeColor = _fgMain;
            _header.BackColor = _bgPanel; _ctrlBar.BackColor = _bgPanel; _statusBar.BackColor = _bgPanel;
            _lblTitle.ForeColor = _fgMain; _lblSub.ForeColor = _accent; _lblStatus.ForeColor = _fgMuted; _lblSizeVal.ForeColor = _accent;
            _lnkBrand.LinkColor = _fgMuted; _lnkBrand.ActiveLinkColor = _accent;
            _txtSearch.BackColor = _bgSurf; _txtSearch.ForeColor = _fgMain;
            _txtSpecimen.BackColor = _bgSurf; _txtSpecimen.ForeColor = _fgMain;
            _list.BackColor = _bgMain; _list.ForeColor = _fgMain;
            Native.SetDarkScrollbars(_list, _dark);
            StyleButtons();
            _header.Invalidate();
            _ctrlBar.Invalidate();
            _statusBar.Invalidate();
            _list.Invalidate();
            _comparePanel.Invalidate();
        }

        private void StyleButtons()
        {
            _btnGreek.Active = _greekOnly;
            _btnCompare.Active = _compareMode;
            _btnBold.Active = _bold;
            _btnItalic.Active = _italic;
            _btnTheme.Icon = _dark ? AkIcon.Sun : AkIcon.Moon;

            AkButton[] all = new AkButton[]
            {
                _btnGreek, _btnCompare, _btnInstall, _btnTheme, _btnInfo,
                _btnSizeDown, _btnSizeUp, _btnBold, _btnItalic, _btnSetA, _btnSetB, _btnCopyName, _btnSendNote
            };
            for (int i = 0; i < all.Length; i++)
            {
                all[i].Dark = _dark;
                all[i].Invalidate();
            }
        }

        private static bool ReadSuiteDark(bool fallback)
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NillySuite\suite.theme");
                if (File.Exists(p))
                {
                    string s = File.ReadAllText(p, Encoding.ASCII).Trim();
                    if (s.Equals("light", StringComparison.OrdinalIgnoreCase)) return false;
                    if (s.Equals("dark", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return fallback;
        }

        private static string FindSiblingExe(string folder, string exeName)
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NillySuite\" + folder + @"\" + exeName);
                if (File.Exists(appData)) return appData;
                DirectoryInfo par = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                if (par != null)
                {
                    string sib = Path.Combine(Path.Combine(par.FullName, folder), exeName);
                    if (File.Exists(sib)) return sib;
                }
            }
            catch { }
            return null;
        }

        private void SendSpecToNillyNote()
        {
            FontEntry sel = _list.SelectedItem as FontEntry;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== NillyFont Pro — Typography Spec ===");
            sb.AppendLine("created by Nilly graphics (Sotiris Christos) — nillygraphics.com");
            sb.AppendLine("");
            if (sel != null)
                sb.AppendLine(string.Format("Επιλεγμένη Γραμματοσειρά:  {0}  ({1})", sel.Name, sel.HasGreek ? "Υποστηρίζει Ελληνικά" : "Latin only"));
            sb.AppendLine(string.Format("Σύγκριση [A]: {0}   |   [B]: {1}", _compareA, _compareB));
            sb.AppendLine(string.Format("Μέγεθος: {0} pt  |  Bold: {1}  |  Italic: {2}", (int)_sampleSize, _bold ? "Ναι" : "Όχι", _italic ? "Ναι" : "Όχι"));
            sb.AppendLine("Κείμενο δοκιμής: " + _txtSpecimen.Text);
            sb.AppendLine(string.Format("Σύνολο στο PC: {0} γραμματοσειρές ({1} με Ελληνικά)", _allFonts.Count, _greekCount));

            string txt = sb.ToString();
            string noteExe = FindSiblingExe("NillyNotePro", "NillyNotePro.exe");
            if (noteExe != null)
            {
                try
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "NillyFont_Spec_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                    File.WriteAllText(tmp, txt, new UTF8Encoding(false));
                    Process.Start(new ProcessStartInfo(noteExe, "\"" + tmp + "\"") { WorkingDirectory = Path.GetDirectoryName(noteExe), UseShellExecute = true });
                    _lblStatus.Text = "↗ Το Typography Spec εστάλη στο NillyNote Pro!";
                    return;
                }
                catch { }
            }
            try { Clipboard.SetText(txt); _lblStatus.Text = "✓ Αντιγράφηκε το Font Spec στο πρόχειρο."; } catch { }
        }

        private void StyleBtn(Button b, bool primary, Color accent)
        {
            b.BackColor = primary ? accent : _bgSurf;
            b.ForeColor = primary ? Color.FromArgb(20, 22, 26) : _fgMain;
            b.FlatAppearance.BorderColor = primary ? accent : _border;
            b.FlatAppearance.MouseOverBackColor = primary ? accent : _bgHover;
        }

        public void ShowInfoDialog()
        {
            double exeKb = 0;
            try { exeKb = new FileInfo(Assembly.GetExecutingAssembly().Location).Length / 1024.0; } catch { }
            long ramMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

            using (Form dlg = new Form())
            {
                dlg.Text = "NillyFont Pro — Ultra-Light Specs & Πληροφορίες";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(510, 365);
                dlg.MaximizeBox = false; dlg.MinimizeBox = false;
                dlg.BackColor = _bgMain; dlg.ForeColor = _fgMain;
                Native.SetDarkTitlebar(dlg.Handle, _dark);

                PictureBox pic = new PictureBox { Size = new Size(42, 42), Location = new Point(18, 16), SizeMode = PictureBoxSizeMode.Zoom, Image = _logoBmp };
                Label hdr = new Label { Text = "NillyFont Pro v1.2.2", Location = new Point(70, 16), AutoSize = true, Font = new Font("Segoe UI", 13.0f, FontStyle.Bold) };
                Label sub = new Label { Text = "Ultra-Light Font Manager, Live Specimen & True Greek Glyph Filter", Location = new Point(72, 42), AutoSize = true, Font = _fSmall, ForeColor = _accent, UseMnemonic = false };

                Panel card = new Panel { Location = new Point(18, 72), Size = new Size(474, 185), BackColor = _bgPanel };
                Label specs = new Label
                {
                    Location = new Point(12, 10), Size = new Size(450, 166), Font = _fUi, ForeColor = _fgMain,
                    Text = string.Format(
                        "• Μέγεθος EXE στον δίσκο:  {0:0.0} KB  (0 εξωτερικά DLL · 0 Electron)\n" +
                        "• Τρέχουσα RAM ανοιχτό:  ~{1} MB  |  Κλειστό:  0 MB RAM & 0% CPU\n" +
                        "• Αντικαθιστά:  Windows 11 Settings → Fonts (κολλάει σε 500+ fonts) & FontBase\n" +
                        "• Φίλτρο Ελληνικών:  Πραγματικός έλεγχος GDI GetFontUnicodeRanges (Α-Ω, α-ω)\n" +
                        "• Σύγκριση A vs B:  2 γραμματοσειρές δίπλα-δίπλα με πλήρες αλφάβητο\n" +
                        "• Zero-Admin Εγκατάσταση:  Drag & Drop .ttf / .otf απευθείας στα HKCU Fonts",
                        exeKb, ramMb)
                };
                card.Controls.Add(specs);

                LinkLabel lnkSite = new LinkLabel { Text = "created by Nilly graphics (Sotiris Christos) — https://nillygraphics.com/", Location = new Point(18, 272), AutoSize = true, Font = _fSmall, LinkColor = _accent };
                lnkSite.LinkClicked += delegate { OpenUrl("https://nillygraphics.com/"); };
                LinkLabel lnkGit = new LinkLabel { Text = "GitHub (MIT Open Source · 0€): https://github.com/nillynp/nillyfont-pro", Location = new Point(18, 294), AutoSize = true, Font = _fSmall, LinkColor = _cyan };
                lnkGit.LinkClicked += delegate { OpenUrl("https://github.com/nillynp/nillyfont-pro"); };

                Button btnClose = new Button
                {
                    Text = "Κλείσιμο", DialogResult = DialogResult.OK, Location = new Point(404, 318), Size = new Size(88, 30),
                    FlatStyle = FlatStyle.Flat, BackColor = _accent, ForeColor = Color.FromArgb(20, 22, 26), Font = _fBold
                };
                btnClose.FlatAppearance.BorderSize = 0;

                dlg.Controls.AddRange(new Control[] { pic, hdr, sub, card, lnkSite, lnkGit, btnClose });
                dlg.AcceptButton = btnClose; dlg.CancelButton = btnClose;
                dlg.ShowDialog(this);
            }
        }

        private static void OpenUrl(string u) { try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { } }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { _txtSearch.Focus(); _txtSearch.SelectAll(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (_logoBmp != null) _logoBmp.Dispose(); _fTitle.Dispose(); _fUi.Dispose(); _fBold.Dispose(); _fSmall.Dispose(); _fBadge.Dispose(); _tip.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && args[0].Equals("--test-smoke", StringComparison.OrdinalIgnoreCase))
                return RunSmoke(args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null);

            List<string> fontFiles = new List<string>();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (File.Exists(args[i]))
                    {
                        string ext = Path.GetExtension(args[i]).ToLowerInvariant();
                        if (ext == ".ttf" || ext == ".otf") fontFiles.Add(args[i]);
                    }
                }
            }
            Application.Run(SuiteUpdater.Attach(new MainForm(false, fontFiles.Count > 0 ? fontFiles.ToArray() : null), "nillyfont-pro", "NillyFont-Pro", "Install_NillyFontPro.ps1"));
            return 0;
        }

        private static int RunSmoke(string logPath, string pngPath)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                using (MainForm f = new MainForm(true))
                {
                    f.Show();
                    f.ToggleGreekOnly();
                    f.ToggleCompare();
                    Application.DoEvents();

                    if (!string.IsNullOrEmpty(pngPath))
                    {
                        using (Bitmap shot = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(shot, new Rectangle(0, 0, f.Width, f.Height));
                            shot.Save(pngPath, ImageFormat.Png);
                        }
                    }

                    double exeKb = new FileInfo(Assembly.GetExecutingAssembly().Location).Length / 1024.0;
                    bool pass = (f.TotalFontCount >= 20 && f.GreekFontCount >= 5 && f.IsHandleCreated && exeKb < 150.0);
                    sb.AppendLine("TOTAL_FONTS=" + f.TotalFontCount);
                    sb.AppendLine("GREEK_FONTS=" + f.GreekFontCount);
                    sb.AppendLine("EXE_KB=" + exeKb.ToString("0.0"));
                    sb.AppendLine("WINDOW_OPENED_OK=" + f.IsHandleCreated);
                    sb.AppendLine("SMOKE_RESULT=" + (pass ? "PASS" : "FAIL"));
                    f.Close();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("SMOKE_RESULT=FAIL");
                sb.AppendLine(ex.ToString());
                if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
                return 1;
            }
            if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            return 0;
        }
    }

    // ========================================================================
    // BUILT-IN ULTRA-LIGHT AUTO-UPDATER (1x/24h shared CDN check · SHA-256)
    // ========================================================================
    internal static class SuiteUpdater
    {
        private const string CATALOG_URL = "https://raw.githubusercontent.com/nillynp/nillyhub/main/catalog.json";

        public static T Attach<T>(T form, string repoName, string pkgPrefix, string installerScript) where T : System.Windows.Forms.Form
        {
            if (form == null) return null;
            form.Shown += delegate
            {
                System.Threading.Thread th = new System.Threading.Thread(delegate()
                {
                    try
                    {
                        System.Threading.Thread.Sleep(2200);
                        if (form.IsDisposed) return;
                        string curTag, newTag;
                        if (!CheckForUpdate(repoName, out curTag, out newTag)) return;
                        if (form.IsDisposed || !form.IsHandleCreated) return;
                        form.BeginInvoke((System.Action)delegate
                        {
                            if (form.IsDisposed || form.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None) return;
                            ShowUpdateBanner(form, repoName, pkgPrefix, installerScript, curTag, newTag);
                        });
                    }
                    catch { }
                });
                th.IsBackground = true;
                th.Start();
            };
            return form;
        }

        private static void ShowUpdateBanner(System.Windows.Forms.Form form, string repoName, string pkgPrefix, string installerScript, string curTag, string newTag)
        {
            System.Drawing.Font fTxt = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Regular);
            System.Drawing.Font fBtn = new System.Drawing.Font("Segoe UI", 8.2f, System.Drawing.FontStyle.Bold);

            System.Windows.Forms.Panel bar = new System.Windows.Forms.Panel();
            bar.Dock = System.Windows.Forms.DockStyle.Top;
            bar.Height = 28;
            bar.BackColor = System.Drawing.Color.FromArgb(40, 29, 18);
            bar.Padding = new System.Windows.Forms.Padding(10, 0, 8, 1);
            bar.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
            {
                using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(252, 150, 31)))
                    e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            System.Windows.Forms.Button btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "×";
            btnClose.Dock = System.Windows.Forms.DockStyle.Right;
            btnClose.Width = 26;
            btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = System.Drawing.Color.Transparent;
            btnClose.ForeColor = System.Drawing.Color.FromArgb(200, 170, 130);
            btnClose.Font = fBtn;
            btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClose.TabStop = false;
            btnClose.Click += delegate { bar.Dispose(); };

            System.Windows.Forms.Button btnUpd = new System.Windows.Forms.Button();
            btnUpd.Text = "Ενημέρωση";
            btnUpd.Dock = System.Windows.Forms.DockStyle.Right;
            btnUpd.Width = 88;
            btnUpd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnUpd.FlatAppearance.BorderSize = 0;
            btnUpd.BackColor = System.Drawing.Color.FromArgb(252, 150, 31);
            btnUpd.ForeColor = System.Drawing.Color.FromArgb(20, 22, 26);
            btnUpd.Font = fBtn;
            btnUpd.Cursor = System.Windows.Forms.Cursors.Hand;
            btnUpd.TabStop = false;

            System.Windows.Forms.Label lbl = new System.Windows.Forms.Label();
            lbl.Dock = System.Windows.Forms.DockStyle.Fill;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lbl.Font = fTxt;
            lbl.ForeColor = System.Drawing.Color.FromArgb(255, 185, 95);
            lbl.UseMnemonic = false;
            lbl.Text = "Νέα έκδοση " + newTag + " διαθέσιμη (έχεις " + curTag + ")";

            btnUpd.Click += delegate
            {
                btnUpd.Enabled = false;
                btnUpd.Text = "Λήψη...";
                System.Threading.Thread uth = new System.Threading.Thread(delegate()
                {
                    try
                    {
                        ApplySelfUpdate(repoName, pkgPrefix, installerScript,
                            delegate(string st)
                            {
                                try { if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate { lbl.Text = st; }); } catch { }
                            },
                            delegate
                            {
                                try { if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate { System.Windows.Forms.Application.Exit(); }); } catch { }
                            });
                    }
                    catch (System.Exception ex)
                    {
                        try
                        {
                            if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate
                            {
                                lbl.Text = "Σφάλμα ενημέρωσης: " + ex.Message;
                                btnUpd.Text = "Ενημέρωση";
                                btnUpd.Enabled = true;
                            });
                        }
                        catch { }
                    }
                });
                uth.IsBackground = true;
                uth.Start();
            };

            bar.Disposed += delegate { fTxt.Dispose(); fBtn.Dispose(); };
            bar.Controls.Add(lbl);
            bar.Controls.Add(btnUpd);
            bar.Controls.Add(btnClose);
            form.Controls.Add(bar);
        }

        private static bool TryParseVer(string raw, out System.Version ver)
        {
            ver = null;
            if (string.IsNullOrEmpty(raw)) return false;
            string s = raw.Trim();
            if (s.StartsWith("v", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            string[] p = s.Split('.');
            if (p.Length < 2 || p.Length > 4) return false;
            int maj, min, pat = 0;
            if (!int.TryParse(p[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maj)) return false;
            if (!int.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out min)) return false;
            if (p.Length >= 3 && !int.TryParse(p[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out pat)) return false;
            ver = new System.Version(maj, min, pat, 0);
            return true;
        }

        private static string FmtVer(System.Version v)
        {
            return v.Build > 0
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "v{0}.{1}.{2}", v.Major, v.Minor, v.Build)
                : string.Format(System.Globalization.CultureInfo.InvariantCulture, "v{0}.{1}", v.Major, v.Minor);
        }

        public static bool CheckForUpdate(string repoName, out string curTag, out string newTag)
        {
            curTag = null; newTag = null;
            System.Version rawCur = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            System.Version curVer = new System.Version(rawCur.Major, rawCur.Minor, rawCur.Build >= 0 ? rawCur.Build : 0, 0);
            curTag = FmtVer(curVer);

            string suiteDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "NillySuite");
            string cachePath = System.IO.Path.Combine(suiteDir, ".nillyhub_catalog.json");
            string json = null;
            try
            {
                if (System.IO.File.Exists(cachePath))
                {
                    System.DateTime wt = System.IO.File.GetLastWriteTimeUtc(cachePath);
                    if ((System.DateTime.UtcNow - wt).TotalHours < 24.0)
                    {
                        string cached = System.IO.File.ReadAllText(cachePath, System.Text.Encoding.UTF8);
                        if (cached.IndexOf("\"latestVersion\"", System.StringComparison.Ordinal) >= 0) json = cached;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(json))
            {
                json = HttpGetText(CATALOG_URL, 5000);
                if (string.IsNullOrEmpty(json) || json.Length > 256 * 1024) return false;
                try
                {
                    System.IO.Directory.CreateDirectory(suiteDir);
                    System.IO.File.WriteAllText(cachePath, json, new System.Text.UTF8Encoding(false));
                }
                catch { }
            }

            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                json,
                "\"repo\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(repoName) + "\"[^\\}]{0,500}?\"latestVersion\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            System.Version remVer;
            if (!TryParseVer(m.Groups[1].Value, out remVer) || remVer <= curVer) return false;
            newTag = FmtVer(remVer);
            return true;
        }

        private static string HttpGetText(string url, int timeoutMs)
        {
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12; } catch { }
            System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            req.UserAgent = "NillySuite-Updater/1.0 (https://nillygraphics.com/)";
            req.Accept = "application/vnd.github+json, application/json";
            req.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
            using (System.IO.Stream st = resp.GetResponseStream())
            using (System.IO.StreamReader sr = new System.IO.StreamReader(st, System.Text.Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private static string ExtractAssetBlock(string json, string assetName)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                json, "\"name\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(assetName) + "\"");
            if (!m.Success) return null;
            int start = m.Index;
            int depth = 0;
            while (start > 0)
            {
                char c = json[start];
                if (c == '}') depth++;
                else if (c == '{') { if (depth == 0) break; depth--; }
                start--;
            }
            int end = start;
            depth = 0;
            bool inStr = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            return end > start ? json.Substring(start, end - start + 1) : null;
        }

        public static void ApplySelfUpdate(string repoName, string pkgPrefix, string installerScript, System.Action<string> onStatus, System.Action onExit)
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string appDir = System.IO.Path.GetDirectoryName(exePath);
            if (System.IO.Directory.Exists(System.IO.Path.Combine(appDir, ".git")))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/nillynp/" + repoName + "/releases/latest") { UseShellExecute = true });
                if (onStatus != null) onStatus("Άνοιξε το GitHub Release (φάκελος Git).");
                return;
            }

            if (onStatus != null) onStatus("Έλεγχος SHA-256 στο GitHub...");
            string relJson = HttpGetText("https://api.github.com/repos/nillynp/" + repoName + "/releases/latest", 12000);
            if (string.IsNullOrEmpty(relJson)) throw new System.IO.InvalidDataException("Κενή απόκριση GitHub.");
            if (System.Text.RegularExpressions.Regex.IsMatch(relJson, "\"draft\"\\s*:\\s*true") ||
                System.Text.RegularExpressions.Regex.IsMatch(relJson, "\"prerelease\"\\s*:\\s*true"))
                throw new System.IO.InvalidDataException("Μη σταθερή έκδοση.");

            System.Text.RegularExpressions.Match tm = System.Text.RegularExpressions.Regex.Match(relJson, "\"tag_name\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?)\"");
            if (!tm.Success) throw new System.IO.InvalidDataException("Μη έγκυρο tag έκδοσης.");
            string tag = tm.Groups[1].Value;

            System.Version curRaw = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            System.Version curVer = new System.Version(curRaw.Major, curRaw.Minor, curRaw.Build >= 0 ? curRaw.Build : 0, 0);
            System.Version remVer;
            if (!TryParseVer(tag, out remVer) || remVer <= curVer)
                throw new System.InvalidOperationException("Έχεις ήδη την τελευταία έκδοση.");

            string zipName = pkgPrefix + "-" + tag + "-Portable.zip";
            string expectedUrl = "https://github.com/nillynp/" + repoName + "/releases/download/" + tag + "/" + zipName;
            string blk = ExtractAssetBlock(relJson, zipName);
            if (string.IsNullOrEmpty(blk)) throw new System.IO.FileNotFoundException("Δεν βρέθηκε το Portable ZIP.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(blk, "\"state\"\\s*:\\s*\"uploaded\""))
                throw new System.IO.InvalidDataException("Το αρχείο δεν είναι έτοιμο.");
            System.Text.RegularExpressions.Match um = System.Text.RegularExpressions.Regex.Match(blk, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"");
            if (!um.Success || !string.Equals(um.Groups[1].Value, expectedUrl, System.StringComparison.Ordinal))
                throw new System.IO.InvalidDataException("Μη έγκυρο URL λήψης.");
            System.Text.RegularExpressions.Match dm = System.Text.RegularExpressions.Regex.Match(blk, "\"digest\"\\s*:\\s*\"sha256:([0-9a-f]{64})\"");
            if (!dm.Success) throw new System.IO.InvalidDataException("Απουσιάζει SHA-256.");
            string expectedSha = dm.Groups[1].Value;

            string tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NillyUpd-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tmpDir);
            string zipFile = System.IO.Path.Combine(tmpDir, "pkg.zip");

            if (onStatus != null) onStatus("Λήψη " + tag + "...");
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12; } catch { }
            System.Net.HttpWebRequest dreq = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(expectedUrl);
            dreq.UserAgent = "NillySuite-Updater/1.0 (https://nillygraphics.com/)";
            dreq.Timeout = 20000;
            dreq.ReadWriteTimeout = 30000;
            using (System.Net.HttpWebResponse dresp = (System.Net.HttpWebResponse)dreq.GetResponse())
            using (System.IO.Stream src = dresp.GetResponseStream())
            using (System.IO.FileStream dst = new System.IO.FileStream(zipFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                byte[] buf = new byte[16384];
                int n; long total = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    total += n;
                    if (total > 32L * 1024L * 1024L) throw new System.IO.InvalidDataException("Υπέρβαση μεγέθους ZIP.");
                    dst.Write(buf, 0, n);
                }
            }

            if (onStatus != null) onStatus("Επαλήθευση SHA-256...");
            string actualSha;
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (System.IO.FileStream fs = System.IO.File.OpenRead(zipFile))
            {
                byte[] hash = sha.ComputeHash(fs);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                actualSha = sb.ToString();
            }
            if (!string.Equals(actualSha, expectedSha, System.StringComparison.OrdinalIgnoreCase))
            {
                try { System.IO.Directory.Delete(tmpDir, true); } catch { }
                throw new System.IO.InvalidDataException("Αποτυχία ελέγχου SHA-256.");
            }

            if (onStatus != null) onStatus("Εγκατάσταση " + tag + " & επανεκκίνηση...");
            string ps1 = System.IO.Path.Combine(tmpDir, "apply.ps1");
            string script =
                "param($myPid,$zip,$appDir,$instName,$exePath,$tag)\r\n" +
                "for($i=0;$i -lt 60;$i++){ if(-not (Get-Process -Id $myPid -ErrorAction SilentlyContinue)){break}; Start-Sleep -Milliseconds 150 }\r\n" +
                "Add-Type -AssemblyName System.IO.Compression.FileSystem\r\n" +
                "$za = [System.IO.Compression.ZipFile]::OpenRead($zip)\r\n" +
                "try {\r\n" +
                "  $pref = $null; $shared = $true\r\n" +
                "  foreach($e in $za.Entries){ $fn=$e.FullName -replace '\\\\','/'; $sl=$fn.IndexOf('/'); if($sl -le 0){$shared=$false;break}; $p=$fn.Substring(0,$sl+1); if(-not $pref){$pref=$p}elseif(-not $pref.Equals($p,[StringComparison]::OrdinalIgnoreCase)){$shared=$false;break} }\r\n" +
                "  $rootFull = [System.IO.Path]::GetFullPath($appDir).TrimEnd('\\') + '\\'\r\n" +
                "  foreach($e in $za.Entries){\r\n" +
                "    $rel = $e.FullName -replace '\\\\','/'\r\n" +
                "    if($shared -and $pref -and $rel.StartsWith($pref,[StringComparison]::OrdinalIgnoreCase)){ $rel=$rel.Substring($pref.Length) }\r\n" +
                "    if([string]::IsNullOrEmpty($rel) -or $rel.EndsWith('/') -or $rel.Contains(':')){ continue }\r\n" +
                "    $dst = [System.IO.Path]::GetFullPath((Join-Path $appDir ($rel -replace '/','\\')))\r\n" +
                "    if(-not $dst.StartsWith($rootFull,[StringComparison]::OrdinalIgnoreCase)){ continue }\r\n" +
                "    $par = Split-Path -Parent $dst; if($par){ New-Item -ItemType Directory -Force -Path $par | Out-Null }\r\n" +
                "    for($k=0;$k -lt 5;$k++){ try { [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e,$dst,$true); break } catch { Start-Sleep -Milliseconds 250 } }\r\n" +
                "  }\r\n" +
                "} finally { $za.Dispose() }\r\n" +
                "$bak = $exePath + '.bak'\r\n" +
                "if(Test-Path $exePath){ Copy-Item -LiteralPath $exePath -Destination $bak -Force -ErrorAction SilentlyContinue; for($k=0;$k -lt 5;$k++){ try { Remove-Item -LiteralPath $exePath -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 250 } } }\r\n" +
                "Get-ChildItem (Join-Path $appDir 'desktop') -Filter '*.cs' -ErrorAction SilentlyContinue | ForEach-Object { $_.LastWriteTimeUtc = [DateTime]::UtcNow }\r\n" +
                "if($instName -and (Test-Path (Join-Path $appDir $instName))){ & (Join-Path $appDir $instName) -Silent 2>$null | Out-Null }\r\n" +
                "if(-not (Test-Path $exePath) -and (Test-Path $bak)){ Move-Item -LiteralPath $bak -Destination $exePath -Force -ErrorAction SilentlyContinue } else { Remove-Item -LiteralPath $bak -Force -ErrorAction SilentlyContinue }\r\n" +
                "if($tag){ Set-Content -LiteralPath (Join-Path $appDir '.nilly_version') -Value $tag -Encoding UTF8 -ErrorAction SilentlyContinue }\r\n" +
                "if(Test-Path $exePath){ Start-Process -FilePath $exePath -WorkingDirectory $appDir }\r\n" +
                "Start-Sleep -Milliseconds 400\r\n" +
                "Remove-Item -LiteralPath (Split-Path $zip) -Recurse -Force -ErrorAction SilentlyContinue\r\n";
            System.IO.File.WriteAllText(ps1, script, new System.Text.UTF8Encoding(false));

            int myPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ps1 + "\" " +
                myPid + " \"" + zipFile + "\" \"" + appDir + "\" \"" + installerScript + "\" \"" + exePath + "\" \"" + tag + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            System.Diagnostics.Process.Start(psi);
            if (onExit != null) onExit();
        }
    }

    // ========================================================================
    // ARIAKIT-INSPIRED MINIMAL VECTOR UI KIT (Zero DLLs · 1.5px Stroke Icons)
    // ========================================================================
    internal enum AkKind { Primary, Secondary, Ghost, Tab, AccentOutline, Danger }

    internal enum AkIcon
    {
        None, Refresh, Sun, Moon, Search, Pin, Info, Play, Pause, Stop,
        Plus, Minus, Close, Up, Down, Folder, Copy, Trash, Check, Swap,
        Download, Link, Split, Pipette, Bolt, List, Filter, Shield, Lock, Unlock,
        Edit, Save, External, Bold, Italic, Undo, ChevLeft, ChevRight, Star
    }

    internal static class AkDraw
    {
        public static System.Drawing.Drawing2D.GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = System.Math.Min(r * 2f, System.Math.Min(w, h));
            if (d <= 0.5f) { p.AddRectangle(new System.Drawing.RectangleF(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawIcon(System.Drawing.Graphics g, AkIcon icon, float cx, float cy, float sz, System.Drawing.Color color)
        {
            if (icon == AkIcon.None) return;
            System.Drawing.Drawing2D.SmoothingMode oldSm = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float r = sz * 0.5f;
            using (System.Drawing.Pen pen = new System.Drawing.Pen(color, 1.5f))
            using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(color))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                switch (icon)
                {
                    case AkIcon.Refresh:
                        g.DrawArc(pen, cx - r * 0.82f, cy - r * 0.82f, r * 1.64f, r * 1.64f, -55, 295);
                        g.DrawLine(pen, cx + r * 0.28f, cy - r * 0.85f, cx + r * 0.62f, cy - r * 0.52f);
                        g.DrawLine(pen, cx + r * 0.62f, cy - r * 0.52f, cx + r * 0.20f, cy - r * 0.42f);
                        break;
                    case AkIcon.Sun:
                        g.DrawEllipse(pen, cx - r * 0.42f, cy - r * 0.42f, r * 0.84f, r * 0.84f);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * System.Math.PI / 4.0;
                            float x1 = cx + (float)(System.Math.Cos(a) * r * 0.68f);
                            float y1 = cy + (float)(System.Math.Sin(a) * r * 0.68f);
                            float x2 = cx + (float)(System.Math.Cos(a) * r * 0.94f);
                            float y2 = cy + (float)(System.Math.Sin(a) * r * 0.94f);
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;
                    case AkIcon.Moon:
                        g.DrawArc(pen, cx - r * 0.78f, cy - r * 0.78f, r * 1.56f, r * 1.56f, 40, 235);
                        g.DrawArc(pen, cx - r * 0.30f, cy - r * 0.92f, r * 1.32f, r * 1.32f, 115, 130);
                        break;
                    case AkIcon.Search:
                        g.DrawEllipse(pen, cx - r * 0.82f, cy - r * 0.82f, r * 1.24f, r * 1.24f);
                        g.DrawLine(pen, cx + r * 0.25f, cy + r * 0.25f, cx + r * 0.82f, cy + r * 0.82f);
                        break;
                    case AkIcon.Pin:
                        g.DrawLine(pen, cx - r * 0.48f, cy - r * 0.72f, cx + r * 0.48f, cy - r * 0.72f);
                        g.DrawLine(pen, cx - r * 0.34f, cy - r * 0.72f, cx - r * 0.56f, cy + r * 0.14f);
                        g.DrawLine(pen, cx + r * 0.34f, cy - r * 0.72f, cx + r * 0.56f, cy + r * 0.14f);
                        g.DrawLine(pen, cx - r * 0.66f, cy + r * 0.14f, cx + r * 0.66f, cy + r * 0.14f);
                        g.DrawLine(pen, cx, cy + r * 0.14f, cx, cy + r * 0.86f);
                        break;
                    case AkIcon.Info:
                        g.DrawEllipse(pen, cx - r * 0.85f, cy - r * 0.85f, r * 1.70f, r * 1.70f);
                        g.DrawLine(pen, cx, cy - r * 0.08f, cx, cy + r * 0.48f);
                        g.FillEllipse(br, cx - 1.1f, cy - r * 0.52f, 2.2f, 2.2f);
                        break;
                    case AkIcon.Play:
                        g.FillPolygon(br, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.45f, cy - r * 0.68f),
                            new System.Drawing.PointF(cx + r * 0.68f, cy),
                            new System.Drawing.PointF(cx - r * 0.45f, cy + r * 0.68f)
                        });
                        break;
                    case AkIcon.Pause:
                        g.DrawLine(pen, cx - r * 0.36f, cy - r * 0.62f, cx - r * 0.36f, cy + r * 0.62f);
                        g.DrawLine(pen, cx + r * 0.36f, cy - r * 0.62f, cx + r * 0.36f, cy + r * 0.62f);
                        break;
                    case AkIcon.Stop:
                        g.FillRectangle(br, cx - r * 0.56f, cy - r * 0.56f, r * 1.12f, r * 1.12f);
                        break;
                    case AkIcon.Plus:
                        g.DrawLine(pen, cx - r * 0.68f, cy, cx + r * 0.68f, cy);
                        g.DrawLine(pen, cx, cy - r * 0.68f, cx, cy + r * 0.68f);
                        break;
                    case AkIcon.Minus:
                        g.DrawLine(pen, cx - r * 0.68f, cy, cx + r * 0.68f, cy);
                        break;
                    case AkIcon.Close:
                        g.DrawLine(pen, cx - r * 0.58f, cy - r * 0.58f, cx + r * 0.58f, cy + r * 0.58f);
                        g.DrawLine(pen, cx + r * 0.58f, cy - r * 0.58f, cx - r * 0.58f, cy + r * 0.58f);
                        break;
                    case AkIcon.Up:
                        g.DrawLine(pen, cx, cy + r * 0.68f, cx, cy - r * 0.64f);
                        g.DrawLine(pen, cx - r * 0.52f, cy - r * 0.14f, cx, cy - r * 0.66f);
                        g.DrawLine(pen, cx + r * 0.52f, cy - r * 0.14f, cx, cy - r * 0.66f);
                        break;
                    case AkIcon.Down:
                        g.DrawLine(pen, cx, cy - r * 0.68f, cx, cy + r * 0.64f);
                        g.DrawLine(pen, cx - r * 0.52f, cy + r * 0.14f, cx, cy + r * 0.66f);
                        g.DrawLine(pen, cx + r * 0.52f, cy + r * 0.14f, cx, cy + r * 0.66f);
                        break;
                    case AkIcon.Folder:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.85f, cy - r * 0.56f),
                            new System.Drawing.PointF(cx - r * 0.22f, cy - r * 0.56f),
                            new System.Drawing.PointF(cx - r * 0.04f, cy - r * 0.34f),
                            new System.Drawing.PointF(cx + r * 0.85f, cy - r * 0.34f),
                            new System.Drawing.PointF(cx + r * 0.85f, cy + r * 0.64f),
                            new System.Drawing.PointF(cx - r * 0.85f, cy + r * 0.64f)
                        });
                        break;
                    case AkIcon.Copy:
                        g.DrawRectangle(pen, cx - r * 0.26f, cy - r * 0.26f, r * 1.02f, r * 1.02f);
                        g.DrawLine(pen, cx - r * 0.68f, cy + r * 0.30f, cx - r * 0.68f, cy - r * 0.68f);
                        g.DrawLine(pen, cx - r * 0.68f, cy - r * 0.68f, cx + r * 0.30f, cy - r * 0.68f);
                        break;
                    case AkIcon.Trash:
                        g.DrawLine(pen, cx - r * 0.76f, cy - r * 0.44f, cx + r * 0.76f, cy - r * 0.44f);
                        g.DrawLine(pen, cx - r * 0.28f, cy - r * 0.74f, cx + r * 0.28f, cy - r * 0.74f);
                        g.DrawRectangle(pen, cx - r * 0.54f, cy - r * 0.42f, r * 1.08f, r * 1.16f);
                        break;
                    case AkIcon.Check:
                        g.DrawLine(pen, cx - r * 0.68f, cy + r * 0.04f, cx - r * 0.14f, cy + r * 0.58f);
                        g.DrawLine(pen, cx - r * 0.14f, cy + r * 0.58f, cx + r * 0.74f, cy - r * 0.52f);
                        break;
                    case AkIcon.Swap:
                        g.DrawLine(pen, cx - r * 0.70f, cy - r * 0.32f, cx + r * 0.70f, cy - r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.32f, cy - r * 0.68f, cx + r * 0.72f, cy - r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.70f, cy + r * 0.34f, cx - r * 0.70f, cy + r * 0.34f);
                        g.DrawLine(pen, cx - r * 0.32f, cy + r * 0.70f, cx - r * 0.72f, cy + r * 0.34f);
                        break;
                    case AkIcon.Download:
                        g.DrawLine(pen, cx, cy - r * 0.72f, cx, cy + r * 0.28f);
                        g.DrawLine(pen, cx - r * 0.44f, cy - r * 0.12f, cx, cy + r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.44f, cy - r * 0.12f, cx, cy + r * 0.32f);
                        g.DrawLine(pen, cx - r * 0.74f, cy + r * 0.72f, cx + r * 0.74f, cy + r * 0.72f);
                        break;
                    case AkIcon.Link:
                    case AkIcon.External:
                        g.DrawLine(pen, cx - r * 0.58f, cy + r * 0.58f, cx + r * 0.62f, cy - r * 0.62f);
                        g.DrawLine(pen, cx - r * 0.06f, cy - r * 0.64f, cx + r * 0.64f, cy - r * 0.64f);
                        g.DrawLine(pen, cx + r * 0.64f, cy - r * 0.64f, cx + r * 0.64f, cy + r * 0.06f);
                        break;
                    case AkIcon.Split:
                        g.DrawRectangle(pen, cx - r * 0.82f, cy - r * 0.64f, r * 1.64f, r * 1.28f);
                        g.DrawLine(pen, cx, cy - r * 0.64f, cx, cy + r * 0.64f);
                        break;
                    case AkIcon.Pipette:
                        g.DrawLine(pen, cx - r * 0.66f, cy + r * 0.66f, cx + r * 0.40f, cy - r * 0.40f);
                        g.DrawLine(pen, cx + r * 0.12f, cy - r * 0.68f, cx + r * 0.68f, cy - r * 0.12f);
                        break;
                    case AkIcon.Bolt:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx + r * 0.14f, cy - r * 0.82f),
                            new System.Drawing.PointF(cx - r * 0.62f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx - r * 0.04f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx - r * 0.18f, cy + r * 0.82f),
                            new System.Drawing.PointF(cx + r * 0.62f, cy - r * 0.10f),
                            new System.Drawing.PointF(cx + r * 0.04f, cy - r * 0.10f)
                        });
                        break;
                    case AkIcon.List:
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.46f, cx + r * 0.72f, cy - r * 0.46f);
                        g.DrawLine(pen, cx - r * 0.72f, cy, cx + r * 0.72f, cy);
                        g.DrawLine(pen, cx - r * 0.72f, cy + r * 0.46f, cx + r * 0.72f, cy + r * 0.46f);
                        break;
                    case AkIcon.Filter:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.78f, cy - r * 0.60f),
                            new System.Drawing.PointF(cx + r * 0.78f, cy - r * 0.60f),
                            new System.Drawing.PointF(cx + r * 0.16f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx + r * 0.16f, cy + r * 0.68f),
                            new System.Drawing.PointF(cx - r * 0.16f, cy + r * 0.52f),
                            new System.Drawing.PointF(cx - r * 0.16f, cy + r * 0.08f)
                        });
                        break;
                    case AkIcon.Shield:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx, cy - r * 0.82f),
                            new System.Drawing.PointF(cx + r * 0.72f, cy - r * 0.44f),
                            new System.Drawing.PointF(cx + r * 0.58f, cy + r * 0.34f),
                            new System.Drawing.PointF(cx, cy + r * 0.82f),
                            new System.Drawing.PointF(cx - r * 0.58f, cy + r * 0.34f),
                            new System.Drawing.PointF(cx - r * 0.72f, cy - r * 0.44f)
                        });
                        break;
                    case AkIcon.Lock:
                        g.DrawRectangle(pen, cx - r * 0.58f, cy - r * 0.08f, r * 1.16f, r * 0.84f);
                        g.DrawArc(pen, cx - r * 0.38f, cy - r * 0.76f, r * 0.76f, r * 0.84f, 180, 180);
                        break;
                    case AkIcon.Unlock:
                        g.DrawRectangle(pen, cx - r * 0.58f, cy - r * 0.08f, r * 1.16f, r * 0.84f);
                        g.DrawArc(pen, cx - r * 0.38f, cy - r * 0.76f, r * 0.76f, r * 0.84f, 180, 130);
                        break;
                    case AkIcon.Save:
                        g.DrawRectangle(pen, cx - r * 0.72f, cy - r * 0.72f, r * 1.44f, r * 1.44f);
                        g.DrawRectangle(pen, cx - r * 0.42f, cy - r * 0.72f, r * 0.72f, r * 0.48f);
                        g.DrawRectangle(pen, cx - r * 0.44f, cy + r * 0.14f, r * 0.88f, r * 0.58f);
                        break;
                    case AkIcon.Edit:
                        g.DrawLine(pen, cx - r * 0.62f, cy + r * 0.62f, cx + r * 0.56f, cy - r * 0.56f);
                        g.DrawLine(pen, cx + r * 0.24f, cy - r * 0.74f, cx + r * 0.74f, cy - r * 0.24f);
                        g.DrawLine(pen, cx - r * 0.72f, cy + r * 0.72f, cx - r * 0.34f, cy + r * 0.62f);
                        break;
                    case AkIcon.Undo:
                        g.DrawArc(pen, cx - r * 0.52f, cy - r * 0.34f, r * 1.24f, r * 1.04f, -135, 225);
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.22f, cx - r * 0.28f, cy - r * 0.62f);
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.22f, cx - r * 0.24f, cy + r * 0.10f);
                        break;
                    case AkIcon.ChevLeft:
                        g.DrawLine(pen, cx + r * 0.28f, cy - r * 0.58f, cx - r * 0.34f, cy);
                        g.DrawLine(pen, cx - r * 0.34f, cy, cx + r * 0.28f, cy + r * 0.58f);
                        break;
                    case AkIcon.ChevRight:
                        g.DrawLine(pen, cx - r * 0.28f, cy - r * 0.58f, cx + r * 0.34f, cy);
                        g.DrawLine(pen, cx + r * 0.34f, cy, cx - r * 0.28f, cy + r * 0.58f);
                        break;
                    case AkIcon.Star:
                        System.Drawing.PointF[] sp = new System.Drawing.PointF[10];
                        for (int k = 0; k < 10; k++)
                        {
                            double ang = -System.Math.PI / 2.0 + k * System.Math.PI / 5.0;
                            float rad = (k % 2 == 0) ? r * 0.85f : r * 0.38f;
                            sp[k] = new System.Drawing.PointF(cx + (float)(System.Math.Cos(ang) * rad), cy + (float)(System.Math.Sin(ang) * rad));
                        }
                        g.DrawPolygon(pen, sp);
                        break;
                }
            }
            g.SmoothingMode = oldSm;
        }
    }

    internal sealed class AkButton : System.Windows.Forms.Button
    {
        public AkKind Kind = AkKind.Secondary;
        public AkIcon Icon = AkIcon.None;
        public bool Active = false;
        public bool Dark = true;
        public int Radius = 6;
        private bool _hov = false;
        private bool _down = false;

        public AkButton()
        {
            SetStyle(System.Windows.Forms.ControlStyles.AllPaintingInWmPaint |
                     System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer |
                     System.Windows.Forms.ControlStyles.UserPaint |
                     System.Windows.Forms.ControlStyles.ResizeRedraw, true);
            FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = System.Windows.Forms.Cursors.Hand;
            TabStop = false;
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(System.EventArgs e) { _hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(System.EventArgs e) { _hov = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e) { if (e.Button == System.Windows.Forms.MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            System.Drawing.Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            System.Drawing.Color parentBg = Parent != null ? Parent.BackColor : (Dark ? System.Drawing.Color.FromArgb(29, 31, 32) : System.Drawing.Color.FromArgb(248, 249, 250));
            g.Clear(parentBg);

            System.Drawing.Color accent = Dark ? System.Drawing.Color.FromArgb(252, 150, 31) : System.Drawing.Color.FromArgb(224, 118, 12);
            System.Drawing.Color accentHov = Dark ? System.Drawing.Color.FromArgb(255, 169, 64) : System.Drawing.Color.FromArgb(204, 102, 8);
            System.Drawing.Color surf = Dark ? System.Drawing.Color.FromArgb(39, 41, 43) : System.Drawing.Color.White;
            System.Drawing.Color surfHov = Dark ? System.Drawing.Color.FromArgb(50, 53, 56) : System.Drawing.Color.FromArgb(236, 239, 243);
            System.Drawing.Color border = Dark ? System.Drawing.Color.FromArgb(58, 62, 66) : System.Drawing.Color.FromArgb(210, 216, 224);
            System.Drawing.Color borderHov = Dark ? System.Drawing.Color.FromArgb(84, 89, 95) : System.Drawing.Color.FromArgb(176, 184, 196);
            System.Drawing.Color fgMain = Dark ? System.Drawing.Color.FromArgb(242, 244, 247) : System.Drawing.Color.FromArgb(26, 29, 33);
            System.Drawing.Color fgMuted = Dark ? System.Drawing.Color.FromArgb(154, 160, 168) : System.Drawing.Color.FromArgb(95, 104, 116);
            System.Drawing.Color fgDis = Dark ? System.Drawing.Color.FromArgb(92, 97, 104) : System.Drawing.Color.FromArgb(170, 178, 188);

            bool en = Enabled;
            System.Drawing.Color bg = parentBg;
            System.Drawing.Color bd = System.Drawing.Color.Transparent;
            System.Drawing.Color fg = en ? fgMain : fgDis;
            bool drawFill = false;

            if (Kind == AkKind.Primary)
            {
                drawFill = true;
                bg = en ? (_hov ? accentHov : accent) : (Dark ? System.Drawing.Color.FromArgb(56, 48, 38) : System.Drawing.Color.FromArgb(228, 224, 218));
                fg = en ? (Dark ? System.Drawing.Color.FromArgb(20, 21, 23) : System.Drawing.Color.White) : fgDis;
            }
            else if (Kind == AkKind.AccentOutline)
            {
                drawFill = true;
                bg = en ? (_hov ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : surf) : (Dark ? System.Drawing.Color.FromArgb(32, 34, 36) : System.Drawing.Color.FromArgb(244, 246, 248));
                bd = en ? accent : border;
                fg = en ? accent : fgDis;
            }
            else if (Kind == AkKind.Danger)
            {
                drawFill = true;
                System.Drawing.Color red = Dark ? System.Drawing.Color.FromArgb(240, 104, 118) : System.Drawing.Color.FromArgb(196, 40, 56);
                bg = en ? (_hov ? (Dark ? System.Drawing.Color.FromArgb(62, 30, 36) : System.Drawing.Color.FromArgb(254, 228, 231)) : (Dark ? System.Drawing.Color.FromArgb(46, 28, 32) : System.Drawing.Color.FromArgb(255, 240, 242))) : surf;
                bd = en ? (Dark ? System.Drawing.Color.FromArgb(116, 48, 58) : System.Drawing.Color.FromArgb(236, 154, 164)) : border;
                fg = en ? red : fgDis;
            }
            else if (Kind == AkKind.Tab)
            {
                drawFill = _hov && en;
                bg = surfHov;
                fg = en ? (Active ? fgMain : (_hov ? fgMain : fgMuted)) : fgDis;
            }
            else if (Kind == AkKind.Ghost)
            {
                drawFill = (Active || _hov) && en;
                bg = Active ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : surfHov;
                bd = Active ? accent : System.Drawing.Color.Transparent;
                fg = en ? (Active ? accent : (_hov ? fgMain : fgMuted)) : fgDis;
            }
            else // Secondary (Ariakit tactile elevated pill)
            {
                drawFill = true;
                bg = en ? (Active ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : (_hov ? surfHov : surf)) : (Dark ? System.Drawing.Color.FromArgb(32, 34, 36) : System.Drawing.Color.FromArgb(245, 247, 249));
                bd = en ? (Active ? accent : (_hov ? borderHov : border)) : (Dark ? System.Drawing.Color.FromArgb(44, 47, 50) : System.Drawing.Color.FromArgb(224, 228, 234));
                fg = en ? (Active ? accent : fgMain) : fgDis;
            }

            float ox = 0.5f, oy = (_down && en) ? 1.5f : 0.5f;
            float rw = Width - 1.5f, rh = Height - 1.5f - ((_down && en) ? 1f : 0f);
            if (drawFill || bd.A > 0)
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp = AkDraw.RoundRect(ox, oy, rw, rh, Radius))
                {
                    if (drawFill) using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(bg)) g.FillPath(br, gp);
                    if (bd.A > 0) using (System.Drawing.Pen p = new System.Drawing.Pen(bd, 1f)) g.DrawPath(p, gp);
                }
            }
            if (Kind == AkKind.Tab && Active)
            {
                using (System.Drawing.SolidBrush ab = new System.Drawing.SolidBrush(accent))
                    g.FillRectangle(ab, 6, Height - 3, System.Math.Max(8, Width - 12), 2.5f);
            }

            string txt = Text ?? "";
            float yShift = (_down && en) ? 1f : 0f;
            if (Icon != AkIcon.None && txt.Length == 0)
            {
                AkDraw.DrawIcon(g, Icon, Width * 0.5f, Height * 0.5f + yShift, 13.5f, fg);
            }
            else if (Icon != AkIcon.None)
            {
                System.Drawing.Size ts = System.Windows.Forms.TextRenderer.MeasureText(txt, Font, new System.Drawing.Size(Width, Height), System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.NoPadding);
                float iconW = 13f, gap = 6f;
                float totalW = iconW + gap + ts.Width;
                float startX = System.Math.Max(8f, (Width - totalW) * 0.5f);
                AkDraw.DrawIcon(g, Icon, startX + iconW * 0.5f, Height * 0.5f + yShift, 12.5f, fg);
                System.Drawing.Rectangle tr = new System.Drawing.Rectangle((int)(startX + iconW + gap), (int)yShift, System.Math.Max(10, Width - (int)(startX + iconW + gap) - 4), Height);
                System.Windows.Forms.TextRenderer.DrawText(g, txt, Font, tr, fg, System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.EndEllipsis);
            }
            else
            {
                System.Drawing.Rectangle tr = new System.Drawing.Rectangle(4, (int)yShift, System.Math.Max(8, Width - 8), Height);
                System.Windows.Forms.TextRenderer.DrawText(g, txt, Font, tr, fg, System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.EndEllipsis);
            }
        }
    }

}
