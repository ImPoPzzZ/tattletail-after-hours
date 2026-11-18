using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AfterHoursInstaller
{
    /// <summary>
    /// The installer window, drawn by hand: a night-purple kaleidoscope background with drifting
    /// lights, the hand-drawn logo, bubbly 3D letters and buttons. Screens: pick your game ->
    /// installing -> all set (or uh oh). Store buttons for anyone who doesn't have Tattletail yet.
    /// Everything is laid out in 900x660 "design pixels" and scaled for the screen's DPI.
    /// </summary>
    public class InstallerForm : Form
    {
        private const float W = 900f, H = 660f;
        private const string SteamUrl = "https://store.steampowered.com/app/568090/Tattletail/";
        private const string ItchUrl = "https://waygetter-games.itch.io/tattletail";
        private const string ModPageUrl = "https://impopzzz.itch.io/tattletail-after-hours";
        private const string KofiUrl = "https://ko-fi.com/impopzzz";
        private const string VersionLabel = "4.0";

        // Palette.
        private static readonly Color Yellow = Color.FromArgb(255, 222, 70), Gold = Color.FromArgb(255, 150, 20), Brown = Color.FromArgb(120, 55, 10);
        private static readonly Color Pink = Color.FromArgb(255, 80, 170), Cyan = Color.FromArgb(90, 225, 255), Purple = Color.FromArgb(150, 90, 255);

        private enum Screen { Main, Installing, Done, Error }
        private Screen _screen = Screen.Main;
        private bool _confirmUninstall;
        private float _scale = 1f, _screenStart, _last;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private float T => (float)_clock.Elapsed.TotalSeconds;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Image _logo;
        private readonly FontFamily _bubbly, _body;

        private List<GameInstall> _games = new List<GameInstall>();
        private int _selected = -1;
        private GameInstall _installed;
        private float _progress, _shownProgress;
        private string _status = "";
        private string _wineNote;     // Linux: what was set up for Proton (shown when it is done)
        private string _errorTitle = "", _errorText = "";
        private bool _errorAdmin;
        private string _toast;
        private float _toastUntil;

        // Mouse.
        private class Hot { public RectangleF R; public string Id; }
        private readonly List<Hot> _hots = new List<Hot>();
        private string _hover, _pressed;
        private readonly Dictionary<string, float> _hoverAnim = new Dictionary<string, float>();

        // Decoration.
        private class Light { public float X, Y, R, Vx, Vy, Phase; public Color C; public bool Star; }
        private readonly List<Light> _lights = new List<Light>();
        private class Bit { public float X, Y, Vx, Vy, Rot, Vr, S; public Color C; }
        private readonly List<Bit> _confetti = new List<Bit>();
        private readonly Random _rng = new Random();

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public InstallerForm()
        {
            Text = "Tattletail: After Hours - Installer";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            using (var g = CreateGraphics()) _scale = g.DpiX / 96f;
            ClientSize = new Size((int)(W * _scale), (int)(H * _scale));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            _logo = LoadImage("logo.png");
            _bubbly = Family("Segoe UI Black", "Arial Black", "Arial");
            _body = Family("Segoe UI", "Arial");
            MakeLights();
            _games = GameFinder.FindAll();
            _selected = _games.Count > 0 ? 0 : -1;
            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (s, e) => { Step(); Invalidate(); };
            _timer.Start();
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // a soft drop shadow
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            using (var p = RoundRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 26 * _scale)) Region = new Region(p);
        }

        private GameInstall Selected => _selected >= 0 && _selected < _games.Count ? _games[_selected] : null;

        // ── Animation ────────────────────────────────────────────────────────
        private void MakeLights()
        {
            Color[] pal = { Pink, Cyan, Yellow, Purple };
            for (int i = 0; i < 34; i++)
            {
                bool star = i % 3 == 0;
                _lights.Add(new Light
                {
                    X = (float)_rng.NextDouble() * W, Y = (float)_rng.NextDouble() * H,
                    R = star ? 3f + (float)_rng.NextDouble() * 4f : 8f + (float)_rng.NextDouble() * 46f,
                    Vx = ((float)_rng.NextDouble() - 0.5f) * 8f, Vy = -6f - (float)_rng.NextDouble() * 16f,
                    Phase = (float)_rng.NextDouble() * 6.28f,
                    C = Color.FromArgb(star ? 220 : 20 + _rng.Next(22), pal[_rng.Next(pal.Length)]), Star = star,
                });
            }
        }

        private void Step()
        {
            float t = T, dt = Math.Min(0.05f, t - _last);
            _last = t;
            foreach (var l in _lights)
            {
                l.X += l.Vx * dt; l.Y += l.Vy * dt;
                if (l.Y < -l.R) { l.Y = H + l.R; l.X = (float)_rng.NextDouble() * W; }
                if (l.X < -l.R) l.X = W + l.R; else if (l.X > W + l.R) l.X = -l.R;
            }
            for (int i = _confetti.Count - 1; i >= 0; i--)
            {
                var c = _confetti[i];
                c.Vy += 520f * dt; c.Vx *= 0.99f;
                c.X += c.Vx * dt; c.Y += c.Vy * dt; c.Rot += c.Vr * dt;
                if (c.Y > H + 30) _confetti.RemoveAt(i);
            }
            if (_hover != null && !_hoverAnim.ContainsKey(_hover)) _hoverAnim[_hover] = 0f;
            foreach (var k in _hoverAnim.Keys.ToList())
                _hoverAnim[k] += ((k == _hover ? 1f : 0f) - _hoverAnim[k]) * Math.Min(1f, dt * 14f);
            _shownProgress += (_progress - _shownProgress) * Math.Min(1f, dt * 8f);
        }

        private float Anim(string id) { float v; return _hoverAnim.TryGetValue(id, out v) ? v : 0f; }

        private void Burst()
        {
            Color[] pal = { Pink, Cyan, Yellow, Purple, Color.White, Color.FromArgb(120, 255, 140) };
            for (int i = 0; i < 160; i++)
            {
                float a = (float)(_rng.NextDouble() * Math.PI * 2);
                float sp = 220f + (float)_rng.NextDouble() * 420f;
                _confetti.Add(new Bit
                {
                    X = W / 2 + ((float)_rng.NextDouble() - 0.5f) * 200f, Y = 250f,
                    Vx = (float)Math.Cos(a) * sp, Vy = (float)Math.Sin(a) * sp - 320f,
                    Rot = (float)_rng.NextDouble() * 360f, Vr = ((float)_rng.NextDouble() - 0.5f) * 720f,
                    S = 6f + (float)_rng.NextDouble() * 7f, C = pal[_rng.Next(pal.Length)],
                });
            }
        }

        // ── Drawing ──────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(_scale, _scale);
            _hots.Clear();

            DrawBackground(g);
            switch (_screen)
            {
                case Screen.Main: DrawMain(g); break;
                case Screen.Installing: DrawInstalling(g); break;
                case Screen.Done: DrawDone(g); break;
                case Screen.Error: DrawError(g); break;
            }
            foreach (var c in _confetti)
            {
                var st = g.Save();
                g.TranslateTransform(c.X, c.Y);
                g.RotateTransform(c.Rot);
                using (var br = new SolidBrush(c.C)) g.FillRectangle(br, -c.S / 2, -c.S / 4, c.S, c.S / 2);
                g.Restore(st);
            }
            if (_confirmUninstall) DrawConfirm(g);
            DrawToast(g);
            DrawWindowButtons(g);
        }

        private void DrawBackground(Graphics g)
        {
            using (var br = new LinearGradientBrush(new RectangleF(0, 0, W, H), Color.FromArgb(24, 7, 44), Color.FromArgb(72, 14, 86), 65f))
                g.FillRectangle(br, 0, 0, W, H);
            Glow(g, 130, 110, 330, Color.FromArgb(70, 255, 70, 190));
            Glow(g, 800, 560, 380, Color.FromArgb(60, 40, 200, 255));
            Glow(g, 820, 60, 230, Color.FromArgb(45, 255, 210, 60));
            // A slowly turning kaleidoscope wheel behind the logo.
            var st = g.Save();
            g.TranslateTransform(W / 2, 140);
            g.RotateTransform(T * 6f);
            Color[] wheel = { Pink, Cyan, Yellow, Purple };
            for (int i = 0; i < 12; i++)
            {
                using (var p = new GraphicsPath())
                {
                    p.AddPie(-330, -330, 660, 660, i * 30f, 15f);
                    using (var br = new SolidBrush(Color.FromArgb(14, wheel[i % 4]))) g.FillPath(br, p);
                }
            }
            g.Restore(st);
            foreach (var l in _lights)
            {
                float tw = 0.55f + 0.45f * (float)Math.Sin(T * (l.Star ? 3.1f : 1.2f) + l.Phase);
                var c = Color.FromArgb((int)(l.C.A * tw), l.C);
                if (l.Star) Sparkle(g, l.X, l.Y, l.R * (0.7f + 0.5f * tw), c);
                else using (var br = new SolidBrush(c)) g.FillEllipse(br, l.X - l.R, l.Y - l.R, l.R * 2, l.R * 2);
            }
            using (var pen = new Pen(Color.FromArgb(150, 255, 120, 220), 3f))
            using (var p = RoundRect(new RectangleF(1.5f, 1.5f, W - 3, H - 3), 26)) g.DrawPath(pen, p);
        }

        private void DrawMain(Graphics g)
        {
            // The logo, bobbing gently.
            float lh = 236f, lw = lh * _logo.Width / _logo.Height, bob = (float)Math.Sin(T * 1.6) * 4f;
            g.DrawImage(_logo, W / 2 - lw / 2, 10 + bob, lw, lh);
            Pill(g, new RectangleF(W / 2 - 170, 250, 340, 30), Color.FromArgb(55, 0, 0, 0), "INSTALLER  ·  v" + VersionLabel, 11.5f, Cyan);

            // 1. Pick your game.
            var card = new RectangleF(50, 292, 800, 186);
            Card(g, card);
            Bubbly(g, "1. PICK YOUR GAME", 21, card.X + 24, card.Y + 26, 0f, Color.White, Color.FromArgb(255, 235, 170), Pink, Color.FromArgb(90, 20, 60));
            if (_games.Count == 0)
            {
                Say(g, "We couldn't find Tattletail on this PC.", 17, FontStyle.Bold, Color.White, new RectangleF(card.X + 26, card.Y + 58, card.Width - 52, 28));
                Say(g, "Installed it somewhere else? Point us to it below.  Don't have it yet? Grab it at the bottom!", 13.5f, FontStyle.Regular,
                     Color.FromArgb(215, 230, 220, 255), new RectangleF(card.X + 26, card.Y + 88, card.Width - 52, 44));
            }
            for (int i = 0; i < Math.Min(3, _games.Count); i++) GameRow(g, _games[i], i, new RectangleF(card.X + 16, card.Y + 50 + i * 38, card.Width - 32, 34));
            LinkText(g, "browse", "Not on the list? Find tattletailWindows.exe myself", new PointF(card.X + 26, card.Bottom - 30), 13f, Cyan);

            // The big button.
            var sel = Selected;
            string label = sel == null ? "INSTALL!" : sel.Modded ? (sel.SameVersion ? "REINSTALL" : "UPDATE!") : "INSTALL!";
            BigButton(g, "go", new RectangleF(W / 2 - 165, 492, 330, 70), label, sel != null, Yellow, Gold, Brown);
            if (sel != null && sel.Modded) LinkText(g, "uninstall", "Uninstall", new PointF(W / 2 + 200, 518), 13f, Color.FromArgb(200, 255, 170, 190));

            Say(g, "A free fan-made mod.  Not affiliated with Waygetter Electronics.", 11.5f, FontStyle.Regular, Color.FromArgb(150, 230, 220, 255),
                 new RectangleF(0, 574, W, 18), StringAlignment.Center);
            StoreBand(g);
        }

        private void GameRow(Graphics g, GameInstall game, int i, RectangleF r)
        {
            string id = "row" + i;
            bool sel = i == _selected;
            float h = Anim(id);
            using (var p = RoundRect(r, 12))
            {
                using (var br = new SolidBrush(Color.FromArgb(sel ? 70 : (int)(20 + 25 * h), 255, 255, 255))) g.FillPath(br, p);
                if (sel) using (var pen = new Pen(Yellow, 2.5f)) g.DrawPath(pen, p);
            }
            // Radio dot.
            float cy = r.Y + r.Height / 2;
            using (var pen = new Pen(Color.White, 2.5f)) g.DrawEllipse(pen, r.X + 12, cy - 9, 18, 18);
            if (sel) using (var br = new SolidBrush(Yellow)) g.FillEllipse(br, r.X + 16, cy - 5, 10, 10);
            // Where it's from.
            bool steam = game.Source == "STEAM", itch = game.Source == "ITCH.IO";
            Color bTop = steam ? Color.FromArgb(42, 71, 94) : itch ? Color.FromArgb(250, 92, 92) : Color.FromArgb(110, 90, 160);
            Color bText = steam ? Color.FromArgb(102, 192, 244) : Color.White;
            var badge = new RectangleF(r.X + 40, cy - 12, 84, 24);
            using (var p = RoundRect(badge, 12)) using (var br = new SolidBrush(bTop)) g.FillPath(br, p);
            Say(g, steam ? "STEAM" : itch ? "ITCH.IO" : "GAME", 11.5f, FontStyle.Bold, bText, badge, StringAlignment.Center, _bubbly);
            // The folder.
            using (var f = new Font(_body, 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(Color.White))
                g.DrawString(game.Folder, f, br, new RectangleF(r.X + 134, r.Y, r.Width - 134 - 190, r.Height), sf);
            // Status.
            string st = game.Modded ? (game.SameVersion ? "AFTER HOURS " + VersionLabel : "OLDER AFTER HOURS") : "NOT MODDED YET";
            Color sc = game.Modded ? (game.SameVersion ? Color.FromArgb(120, 255, 150) : Yellow) : Color.FromArgb(200, 210, 200, 230);
            Say(g, st, 11.5f, FontStyle.Bold, sc, new RectangleF(r.Right - 185, r.Y, 175, r.Height), StringAlignment.Far, _bubbly);
            _hots.Add(new Hot { R = r, Id = id });
        }

        private void StoreBand(Graphics g)
        {
            var band = new RectangleF(0, 598, W, 62);
            using (var br = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(br, band);
            using (var pen = new Pen(Color.FromArgb(80, 255, 120, 220), 1.5f)) g.DrawLine(pen, 0, band.Y, W, band.Y);
            Bubbly(g, "DON'T HAVE TATTLETAIL YET?", 17, 30, band.Y + band.Height / 2, 0f, Color.White, Color.FromArgb(255, 225, 245), Pink, Color.FromArgb(80, 10, 50));
            PillButton(g, "steam", new RectangleF(398, band.Y + 13, 208, 38), "BUY IT ON STEAM", Color.FromArgb(70, 140, 220), Color.FromArgb(28, 70, 130));
            PillButton(g, "itch", new RectangleF(618, band.Y + 13, 262, 38), "DOWNLOAD FREE ON ITCH.IO", Color.FromArgb(255, 110, 110), Color.FromArgb(200, 40, 70));
        }

        private void DrawInstalling(Graphics g)
        {
            float lh = 170f, lw = lh * _logo.Width / _logo.Height, bob = (float)Math.Sin(T * 3f) * 5f;
            g.DrawImage(_logo, W / 2 - lw / 2, 34 + bob, lw, lh);
            string dots = new string('.', 1 + (int)(T * 2.5f) % 3);
            Bubbly(g, "INSTALLING" + dots, 44, W / 2, 262, 0.5f, Color.FromArgb(255, 245, 160), Gold, Color.White, Brown);

            // The bar: candy stripes sliding along.
            var bar = new RectangleF(130, 322, 640, 42);
            using (var p = RoundRect(bar, 21))
            {
                using (var br = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(br, p);
                using (var pen = new Pen(Color.White, 3f)) g.DrawPath(pen, p);
            }
            float fw = Math.Max(42f, bar.Width * _shownProgress);
            var fill = new RectangleF(bar.X + 4, bar.Y + 4, fw - 8, bar.Height - 8);
            using (var p = RoundRect(fill, fill.Height / 2))
            {
                using (var br = new LinearGradientBrush(fill, Pink, Yellow, 0f)) g.FillPath(br, p);
                var st = g.Save();
                g.SetClip(p);
                using (var br = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                    for (float x = fill.X - 60 + (T * 60f) % 40f; x < fill.Right + 40; x += 40)
                        g.FillPolygon(br, new[] { new PointF(x, fill.Bottom), new PointF(x + 18, fill.Bottom), new PointF(x + 18 + 30, fill.Y), new PointF(x + 30, fill.Y) });
                g.Restore(st);
            }
            Bubbly(g, (int)Math.Round(_shownProgress * 100) + "%", 20, W / 2, bar.Y + bar.Height / 2 + 1, 0.5f, Color.White, Color.White, Color.FromArgb(160, 30, 90), Color.FromArgb(90, 10, 50));
            Say(g, _status, 15f, FontStyle.Bold, Color.FromArgb(235, 255, 255, 255), new RectangleF(0, 382, W, 24), StringAlignment.Center);
            string[] tips = { "Waking up the Arcade...", "Charging Tattletail...", "Stocking Mama's smoothie bar...", "Hiding Mama in the closet...", "Polishing the Gold fur...", "Almost there!" };
            Say(g, tips[Math.Min(tips.Length - 1, (int)(_shownProgress * tips.Length))], 14f, FontStyle.Italic, Color.FromArgb(170, 220, 200, 255), new RectangleF(0, 416, W, 22), StringAlignment.Center);
        }

        private void DrawDone(Graphics g)
        {
            float lh = 196f, lw = lh * _logo.Width / _logo.Height, bob = (float)Math.Sin(T * 1.8) * 5f;
            g.DrawImage(_logo, W / 2 - lw / 2, 22 + bob, lw, lh);
            float since = T - _screenStart;
            float pop = since < 0.5f ? Bounce(since / 0.5f) : 1f + (float)Math.Sin(T * 3f) * 0.015f;
            var st = g.Save();
            g.TranslateTransform(W / 2, 282);
            g.ScaleTransform(pop, pop);
            Bubbly(g, "YOU'RE ALL SET!", 58, 0, 0, 0.5f, Color.FromArgb(255, 248, 170), Gold, Color.White, Brown);
            g.Restore(st);
            Say(g, "Tattletail: After Hours is installed. Launch the game and you'll wake up in the Arcade!", 16f, FontStyle.Bold, Color.White,
                 new RectangleF(60, 336, W - 120, 26), StringAlignment.Center);
            Say(g, "Updating later?  Just run the new installer.   Your saves always stay safe.", 13.5f, FontStyle.Regular, Color.FromArgb(190, 220, 210, 255),
                 new RectangleF(60, 366, W - 120, 22), StringAlignment.Center);
            if (!string.IsNullOrEmpty(_wineNote))
                Say(g, _wineNote, 11.5f, FontStyle.Regular, Color.FromArgb(215, 255, 235, 170), new RectangleF(40, 538, W - 80, 52), StringAlignment.Center);
            BigButton(g, "play", new RectangleF(W / 2 - 165, 418, 330, 72), "PLAY NOW!", true, Color.FromArgb(140, 255, 150), Color.FromArgb(40, 190, 90), Color.FromArgb(15, 90, 40));
            LinkText(g, "close2", "Close", new PointF(W / 2 - 22, 512), 15f, Color.FromArgb(220, 255, 255, 255));
            Say(g, "Love it?  Tell a friend, or support the mod on Ko-fi", 12.5f, FontStyle.Regular, Color.FromArgb(150, 230, 220, 255), new RectangleF(0, 600, W, 20), StringAlignment.Center);
            LinkText(g, "kofi", "ko-fi.com/impopzzz", new PointF(W / 2 - 64, 622), 13f, Pink);
        }

        private void DrawError(Graphics g)
        {
            float wob = (float)Math.Sin(T * 7f) * 2f;
            var st = g.Save();
            g.TranslateTransform(W / 2, 170);
            g.RotateTransform(wob);
            Bubbly(g, "UH OH!", 76, 0, 0, 0.5f, Color.FromArgb(255, 150, 150), Color.FromArgb(220, 30, 60), Color.White, Color.FromArgb(90, 0, 20));
            g.Restore(st);
            Say(g, _errorTitle, 20f, FontStyle.Bold, Color.White, new RectangleF(80, 250, W - 160, 30), StringAlignment.Center);
            Say(g, _errorText, 15f, FontStyle.Regular, Color.FromArgb(220, 235, 225, 255), new RectangleF(120, 292, W - 240, 110), StringAlignment.Center);
            if (_errorAdmin)
            {
                BigButton(g, "admin", new RectangleF(W / 2 - 330, 430, 320, 68), "RUN AS ADMIN", true, Yellow, Gold, Brown);
                BigButton(g, "back", new RectangleF(W / 2 + 10, 430, 320, 68), "GO BACK", true, Color.FromArgb(200, 170, 255), Purple, Color.FromArgb(60, 30, 120));
            }
            else BigButton(g, "back", new RectangleF(W / 2 - 165, 430, 330, 68), "TRY AGAIN", true, Yellow, Gold, Brown);
        }

        private void DrawConfirm(Graphics g)
        {
            using (var br = new SolidBrush(Color.FromArgb(170, 8, 2, 16))) g.FillRectangle(br, 0, 0, W, H);
            var card = new RectangleF(W / 2 - 230, 200, 460, 240);
            using (var p = RoundRect(card, 22))
            {
                using (var br = new LinearGradientBrush(card, Color.FromArgb(70, 20, 90), Color.FromArgb(35, 8, 55), 90f)) g.FillPath(br, p);
                using (var pen = new Pen(Pink, 3f)) g.DrawPath(pen, p);
            }
            Bubbly(g, "REMOVE THE MOD?", 30, W / 2, card.Y + 50, 0.5f, Color.White, Color.FromArgb(255, 210, 235), Pink, Color.FromArgb(80, 10, 50));
            Say(g, "Tattletail goes back to normal.\nYour saves stay safe in Documents\\Tattletail After Hours.", 14.5f, FontStyle.Regular, Color.White,
                 new RectangleF(card.X + 20, card.Y + 86, card.Width - 40, 60), StringAlignment.Center);
            PillButton(g, "unYes", new RectangleF(card.X + 40, card.Bottom - 70, 180, 44), "YES, REMOVE", Color.FromArgb(255, 110, 110), Color.FromArgb(200, 40, 70));
            PillButton(g, "unNo", new RectangleF(card.Right - 220, card.Bottom - 70, 180, 44), "KEEP IT", Color.FromArgb(140, 255, 150), Color.FromArgb(40, 190, 90));
        }

        private void DrawToast(Graphics g)
        {
            if (_toast == null || T > _toastUntil) return;
            float a = Math.Min(1f, (_toastUntil - T) / 0.4f);
            using (var f = new Font(_body, 15f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                var size = g.MeasureString(_toast, f);
                var r = new RectangleF(W / 2 - size.Width / 2 - 22, 540, size.Width + 44, 40);
                using (var p = RoundRect(r, 20))
                using (var br = new SolidBrush(Color.FromArgb((int)(230 * a), 30, 10, 45)))
                using (var pen = new Pen(Color.FromArgb((int)(255 * a), Yellow), 2.5f))
                { g.FillPath(br, p); g.DrawPath(pen, p); }
                Say(g, _toast, 15f, FontStyle.Bold, Color.FromArgb((int)(255 * a), 255, 255, 255), r, StringAlignment.Center);
            }
        }

        private void DrawWindowButtons(Graphics g)
        {
            if (_screen == Screen.Installing) return; // no closing halfway through
            WindowButton(g, "min", new PointF(W - 78, 26), "–");
            WindowButton(g, "close", new PointF(W - 36, 26), "×");
        }

        private void WindowButton(Graphics g, string id, PointF c, string glyph)
        {
            float h = Anim(id), r = 15f;
            Color fill = id == "close" ? Color.FromArgb((int)(60 + 150 * h), 255, 70, 110) : Color.FromArgb((int)(50 + 90 * h), 255, 255, 255);
            using (var br = new SolidBrush(fill)) g.FillEllipse(br, c.X - r, c.Y - r, r * 2, r * 2);
            Say(g, glyph, 20f, FontStyle.Bold, Color.White, new RectangleF(c.X - r, c.Y - r - 1, r * 2, r * 2), StringAlignment.Center);
            _hots.Add(new Hot { R = new RectangleF(c.X - r, c.Y - r, r * 2, r * 2), Id = id });
        }

        // ── Pieces ───────────────────────────────────────────────────────────
        /// <summary>Chunky cartoon letters: a thick outline, a stepped dark 3D side, a top-to-bottom gradient and a shine. anchor 0 = left edge at x, 0.5 = centered.</summary>
        private void Bubbly(Graphics g, string text, float size, float x, float y, float anchor, Color top, Color bottom, Color outline, Color side)
        {
            using (var path = new GraphicsPath())
            {
                path.AddString(text, _bubbly, (int)FontStyle.Regular, size, PointF.Empty, StringFormat.GenericTypographic);
                var b = path.GetBounds();
                using (var m = new Matrix())
                {
                    m.Translate(x - b.X - b.Width * anchor, y - b.Y - b.Height / 2);
                    path.Transform(m);
                }
                b = path.GetBounds();
                float ow = Math.Max(2.5f, size * 0.16f);
                int depth = Math.Max(2, (int)(size * 0.11f));
                using (var pen = new Pen(side, ow * 2) { LineJoin = LineJoin.Round })
                using (var br = new SolidBrush(side))
                    for (int i = depth; i >= 1; i--)
                    {
                        var st = g.Save();
                        g.TranslateTransform(i * 0.45f, i);
                        g.DrawPath(pen, path);
                        g.FillPath(br, path);
                        g.Restore(st);
                    }
                using (var pen = new Pen(outline, ow * 2) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                using (var br = new LinearGradientBrush(new RectangleF(b.X, b.Y - 1, b.Width, b.Height + 2), top, bottom, LinearGradientMode.Vertical)) g.FillPath(br, path);
                var clip = g.Save();
                g.SetClip(path);
                using (var br = new SolidBrush(Color.FromArgb(80, 255, 255, 255))) g.FillRectangle(br, b.X, b.Y, b.Width, b.Height * 0.36f);
                g.Restore(clip);
            }
        }

        private void BigButton(Graphics g, string id, RectangleF r, string text, bool enabled, Color top, Color bottom, Color side)
        {
            float h = enabled ? Anim(id) : 0f;
            bool down = enabled && _pressed == id && _hover == id;
            float s = 1f + 0.05f * h - (down ? 0.03f : 0f) + (enabled ? (float)Math.Sin(T * 3.2f) * 0.012f : 0f);
            float lift = down ? 1f : 7f;
            var st = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
            g.ScaleTransform(s, s);
            var rr = new RectangleF(-r.Width / 2, -r.Height / 2 + (down ? 5 : 0), r.Width, r.Height);
            Color faceTop = enabled ? top : Color.FromArgb(150, 150, 165), faceBot = enabled ? bottom : Color.FromArgb(105, 105, 120), sideC = enabled ? side : Color.FromArgb(60, 60, 72);
            using (var p = RoundRect(new RectangleF(rr.X, rr.Y + lift, rr.Width, rr.Height), rr.Height / 2)) using (var br = new SolidBrush(sideC)) g.FillPath(br, p);
            using (var p = RoundRect(rr, rr.Height / 2))
            {
                using (var br = new LinearGradientBrush(rr, faceTop, faceBot, LinearGradientMode.Vertical)) g.FillPath(br, p);
                if (h > 0.01f) using (var pen = new Pen(Color.FromArgb((int)(120 * h), 255, 255, 255), 10f)) g.DrawPath(pen, p); // hover glow
                using (var pen = new Pen(Color.White, 4f)) g.DrawPath(pen, p);
            }
            using (var p = RoundRect(new RectangleF(rr.X + 18, rr.Y + 7, rr.Width - 36, rr.Height * 0.36f), rr.Height * 0.18f))
            using (var br = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillPath(br, p);
            Bubbly(g, text, 31, 0, rr.Y + rr.Height / 2 + 1, 0.5f, Color.White, Color.FromArgb(255, 240, 225), sideC, Color.FromArgb(Math.Max(0, sideC.R - 30), Math.Max(0, sideC.G - 30), Math.Max(0, sideC.B - 30)));
            g.Restore(st);
            if (enabled) _hots.Add(new Hot { R = r, Id = id });
        }

        private void PillButton(Graphics g, string id, RectangleF r, string text, Color top, Color bottom)
        {
            float h = Anim(id);
            var rr = new RectangleF(r.X, r.Y - 3f * h, r.Width, r.Height);
            using (var p = RoundRect(new RectangleF(rr.X, rr.Y + 4, rr.Width, rr.Height), rr.Height / 2)) using (var br = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(br, p);
            using (var p = RoundRect(rr, rr.Height / 2))
            {
                using (var br = new LinearGradientBrush(rr, Lighten(top, 0.15f * h), bottom, LinearGradientMode.Vertical)) g.FillPath(br, p);
                using (var pen = new Pen(Color.White, 2.5f)) g.DrawPath(pen, p);
            }
            using (var p = RoundRect(new RectangleF(rr.X + 10, rr.Y + 4, rr.Width - 20, rr.Height * 0.36f), rr.Height * 0.18f))
            using (var br = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(br, p);
            Say(g, text, 14f, FontStyle.Regular, Color.White, rr, StringAlignment.Center, _bubbly, true);
            _hots.Add(new Hot { R = r, Id = id });
        }

        private void Pill(Graphics g, RectangleF r, Color fill, string text, float size, Color textColor)
        {
            using (var p = RoundRect(r, r.Height / 2))
            {
                using (var br = new SolidBrush(fill)) g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb(90, textColor), 1.5f)) g.DrawPath(pen, p);
            }
            Say(g, text, size, FontStyle.Regular, textColor, r, StringAlignment.Center, _bubbly);
        }

        private void Card(Graphics g, RectangleF r)
        {
            using (var p = RoundRect(r, 22))
            {
                using (var br = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2f)) g.DrawPath(pen, p);
            }
        }

        private void LinkText(Graphics g, string id, string text, PointF at, float size, Color color)
        {
            float h = Anim(id);
            using (var f = new Font(_body, size, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Pixel))
            {
                var sz = g.MeasureString(text, f);
                using (var br = new SolidBrush(Lighten(color, 0.4f * h))) g.DrawString(text, f, br, at);
                _hots.Add(new Hot { R = new RectangleF(at.X, at.Y, sz.Width, sz.Height), Id = id });
            }
        }

        private void Say(Graphics g, string text, float size, FontStyle style, Color color, RectangleF r, StringAlignment align = StringAlignment.Near, FontFamily family = null, bool shadow = false)
        {
            using (var f = new Font(family ?? _body, size, style, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center })
            {
                if (shadow) using (var sb = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.DrawString(text, f, sb, new RectangleF(r.X + 1.5f, r.Y + 1.5f, r.Width, r.Height), sf);
                using (var br = new SolidBrush(color)) g.DrawString(text, f, br, r, sf);
            }
        }

        private static void Sparkle(Graphics g, float x, float y, float r, Color c)
        {
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                float rad = i % 2 == 0 ? r * 2.2f : r * 0.45f;
                pts[i] = new PointF(x + (float)Math.Cos(a) * rad, y + (float)Math.Sin(a) * rad);
            }
            using (var br = new SolidBrush(c)) g.FillPolygon(br, pts);
        }

        private static void Glow(Graphics g, float cx, float cy, float r, Color c)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (var br = new PathGradientBrush(path) { CenterColor = c, SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(br, path);
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * k), (int)(c.G + (255 - c.G) * k), (int)(c.B + (255 - c.B) * k));
        }

        private static float Bounce(float x)
        {
            const float C1 = 1.70158f, C3 = C1 + 1f;
            return 1f + C3 * (float)Math.Pow(x - 1, 3) + C1 * (float)Math.Pow(x - 1, 2);
        }

        private static FontFamily Family(params string[] names)
        {
            foreach (var n in names) { try { return new FontFamily(n); } catch { } }
            return FontFamily.GenericSansSerif;
        }

        private static Image LoadImage(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) return new Bitmap(s);
        }

        // ── Input ────────────────────────────────────────────────────────────
        private PointF Logical(MouseEventArgs e) { return new PointF(e.X / _scale, e.Y / _scale); }

        private string HitTest(PointF p)
        {
            for (int i = _hots.Count - 1; i >= 0; i--)
            {
                var h = _hots[i];
                if (!h.R.Contains(p)) continue;
                if (_confirmUninstall && !h.Id.StartsWith("un") && h.Id != "close" && h.Id != "min") continue;
                return h.Id;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _hover = HitTest(Logical(e));
            Cursor = _hover != null ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = null; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            string id = HitTest(Logical(e));
            if (id == null)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0); // drag the window
                return;
            }
            _pressed = id;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            string id = HitTest(Logical(e));
            string pressed = _pressed;
            _pressed = null;
            if (id != null && id == pressed) { Sounds.Pop(); OnClick(id); }
        }

        private void OnClick(string id)
        {
            if (id.StartsWith("row")) { _selected = int.Parse(id.Substring(3)); return; }
            switch (id)
            {
                case "close": case "close2": Close(); break;
                case "min": WindowState = FormWindowState.Minimized; break;
                case "browse": Browse(); break;
                case "go": StartInstall(); break;
                case "uninstall": _confirmUninstall = true; break;
                case "unNo": _confirmUninstall = false; break;
                case "unYes": _confirmUninstall = false; DoUninstall(); break;
                case "steam": Open(SteamUrl); break;
                case "itch": Open(ItchUrl); break;
                case "kofi": Open(KofiUrl); break;
                case "modpage": Open(ModPageUrl); break;
                case "play": Launch(); break;
                case "back": _screen = Screen.Main; Rescan(); break;
                case "admin": RestartAsAdmin(); break;
            }
        }

        // ── Actions ──────────────────────────────────────────────────────────
        private void Rescan()
        {
            string keep = Selected?.Folder;
            _games = GameFinder.FindAll();
            _selected = _games.FindIndex(x => string.Equals(x.Folder, keep, StringComparison.OrdinalIgnoreCase));
            if (_selected < 0) _selected = _games.Count > 0 ? 0 : -1;
        }

        private void Browse()
        {
            using (var d = new OpenFileDialog { Title = "Find tattletailWindows.exe (in your Tattletail folder)", Filter = "Tattletail|tattletailWindows.exe|Programs (*.exe)|*.exe", FileName = GameFinder.ExeName })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var game = GameFinder.Inspect(Path.GetDirectoryName(d.FileName));
                if (game == null) { Toast("That's not Tattletail: look for tattletailWindows.exe"); return; }
                int i = _games.FindIndex(x => string.Equals(x.Folder, game.Folder, StringComparison.OrdinalIgnoreCase));
                if (i < 0) { _games.Insert(0, game); i = 0; }
                _selected = i;
                Toast("Found it!");
            }
        }

        private void StartInstall()
        {
            var game = Selected;
            if (game == null) return;
            if (GameFinder.IsRunning()) { Toast("Close Tattletail first, then try again!"); return; }
            _screen = Screen.Installing;
            _screenStart = T;
            _progress = _shownProgress = 0f;
            _status = "Getting ready...";
            var worker = new Thread(() =>
            {
                try
                {
                    ModInstaller.Install(game, (f, msg) => BeginInvoke((Action)(() => { _progress = f; _status = msg; })));
                    _wineNote = WineSupport.Enable(game);   // (Linux only: the mod loader needs one Wine setting)
                    Thread.Sleep(500);
                    BeginInvoke((Action)(() => { _installed = game; _screen = Screen.Done; _screenStart = T; Burst(); Sounds.Yay(); }));
                }
                catch (UnauthorizedAccessException)
                {
                    Fail("Windows wouldn't let us write to the game folder.", "Run the installer as administrator and it'll go through.", true);
                }
                catch (IOException ex)
                {
                    Fail("A file in the game folder is busy.", "Close Tattletail (and anything else using its folder), then try again.\n(" + ex.Message + ")", false);
                }
                catch (Exception ex)
                {
                    Fail("Something went wrong while installing.", ex.Message, false);
                }
            }) { IsBackground = true };
            worker.Start();
        }

        private void Fail(string title, string text, bool admin)
        {
            BeginInvoke((Action)(() => { _errorTitle = title; _errorText = text; _errorAdmin = admin; _screen = Screen.Error; _screenStart = T; }));
        }

        private void DoUninstall()
        {
            var game = Selected;
            if (game == null) return;
            if (GameFinder.IsRunning()) { Toast("Close Tattletail first, then try again!"); return; }
            try
            {
                ModInstaller.Uninstall(game);
                WineSupport.Disable(game);
                Rescan();
                Toast("Removed. Tattletail is back to normal.");
            }
            catch (UnauthorizedAccessException) { _errorTitle = "Windows wouldn't let us remove the files."; _errorText = "Run the installer as administrator and try again."; _errorAdmin = true; _screen = Screen.Error; }
            catch (Exception ex) { _errorTitle = "Couldn't remove everything."; _errorText = ex.Message; _errorAdmin = false; _screen = Screen.Error; }
        }

        private void Launch()
        {
            var game = _installed;
            if (game == null) return;
            try
            {
                if (game.Source == "STEAM") Open("steam://rungameid/568090");
                else Process.Start(new ProcessStartInfo(Path.Combine(game.Folder, GameFinder.ExeName)) { WorkingDirectory = game.Folder, UseShellExecute = true });
                Close();
            }
            catch (Exception ex) { Toast("Couldn't start the game: " + ex.Message); }
        }

        private void RestartAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas", UseShellExecute = true });
                Close();
            }
            catch { Toast("No problem, maybe next time!"); }
        }

        private void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { Toast("Couldn't open the browser."); }
        }

        private void Toast(string text)
        {
            _toast = text;
            _toastUntil = T + 2.6f;
        }
    }
}
