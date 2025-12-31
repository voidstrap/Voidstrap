using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Voidstrap;

namespace RobloxLightingOverlay
{
    public static class OverlayManager
    {
        public static FxUI UI;
        public static FxOverlay Overlay;

        public static void Start()
        {
            if (UI != null) return;
            UI = new FxUI(); UI.Show();
            Overlay = new FxOverlay(); Overlay.Show();
        }
    }

    public static class RobloxWindow
    {
        public static bool TryGet(out RECT r)
        {
            r = new RECT();
            var p = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
            if (p == null || p.MainWindowHandle == IntPtr.Zero) return false;

            GetWindowRect(p.MainWindowHandle, out r);
            var s = System.Windows.Forms.Screen.FromHandle(p.MainWindowHandle);

            if (Math.Abs((r.Right - r.Left) - s.Bounds.Width) < 6 &&
                Math.Abs((r.Bottom - r.Top) - s.Bounds.Height) < 6)
            {
                r.Left = s.Bounds.Left;
                r.Top = s.Bounds.Top;
                r.Right = s.Bounds.Right;
                r.Bottom = s.Bounds.Bottom;
            }
            return true;
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    }

    public class FxSettings
    {
        public bool Glow, Vignette, AutoExposure;
        public bool FilmGrain, FXAA = true;
        public bool PerformanceMode, DynamicResolution = true;

        public int MinScale = 1;
        public int MaxScale = 3;

        public double GlowAmt = 1.2;
        public double VigAmt = 0.9;

        public double ExposureMin = 0.8;
        public double ExposureMax = 2.2;

        public double FilmGrainStrength = 0.12;
        public double GameBrightness = 1.0;
        public double FpsSmoothStrength = 0.12;
    }

    public class FxOverlay : Window
    {
        WriteableBitmap bmp;
        Image img;

        double exposure = 1.0;
        double smoothMotion, prevLum;
        int dynamicScale = 1;

        static readonly Random rng = new Random();
        Stopwatch timer = Stopwatch.StartNew();
        double last;

        public FxOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            IsHitTestVisible = false;

            img = new Image { Stretch = Stretch.Fill };
            Content = img;

            Loaded += (_, __) =>
            {
                ClickThrough();
                CompositionTarget.Rendering += OnRender;
            };
        }

        void OnRender(object sender, EventArgs e)
        {
            double now = timer.Elapsed.TotalSeconds;
            double dt = now - last;
            last = now;
            if (dt <= 0 || dt > 0.1) dt = 1.0 / 60.0;
            RenderFrame(dt);
        }

        void UpdateDynamicResolution(FxSettings s)
        {
            if (!s.DynamicResolution)
            {
                dynamicScale = s.PerformanceMode ? s.MaxScale : 1;
                return;
            }

            if (smoothMotion > 0.25 && dynamicScale < s.MaxScale)
                dynamicScale++;
            else if (smoothMotion < 0.05 && dynamicScale > s.MinScale)
                dynamicScale--;

            dynamicScale = Math.Clamp(dynamicScale, s.MinScale, s.MaxScale);
        }

        unsafe void RenderFrame(double dt)
        {
            var s = OverlayManager.UI?.Settings;
            if (s == null || !RobloxWindow.TryGet(out var r)) return;

            Left = r.Left; Top = r.Top;
            Width = r.Right - r.Left;
            Height = r.Bottom - r.Top;

            UpdateDynamicResolution(s);

            int scale = dynamicScale;
            int w = Math.Max(1, (int)Width / scale);
            int h = Math.Max(1, (int)Height / scale);

            if (bmp == null || bmp.PixelWidth != w || bmp.PixelHeight != h)
            {
                bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                img.Source = bmp;
            }

            bmp.Lock();
            byte* p = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;

            double avgLum = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double nx = x / (double)w * 2 - 1;
                    double ny = y / (double)h * 2 - 1;
                    double dist = Math.Sqrt(nx * nx + ny * ny);

                    double light = 26 * Math.Clamp(exposure, 0.8, 2.2) * s.GameBrightness;

                    if (s.Glow)
                        light += Math.Max(0, 1 - dist * 2) * 110 * s.GlowAmt;

                    byte v = (byte)Math.Clamp(light, 0, 235);

                    if (s.Vignette)
                    {
                        double vig = Math.Clamp(1 - Math.Pow(dist, 2.2) * s.VigAmt, 0, 1);
                        v = (byte)(v * vig);
                    }

                    byte rC = v, gC = v, bC = v;

                    if (s.FilmGrain)
                    {
                        int grain = (int)((rng.NextDouble() - 0.5) * 255 * s.FilmGrainStrength);
                        rC = (byte)Math.Clamp(rC + grain, 0, 255);
                        gC = (byte)Math.Clamp(gC + grain, 0, 255);
                        bC = (byte)Math.Clamp(bC + grain, 0, 255);
                    }

                    byte alpha = 0;
                    if (s.Glow) alpha = Math.Max(alpha, (byte)40);
                    if (s.Vignette) alpha = Math.Max(alpha, (byte)30);
                    if (s.FilmGrain) alpha = Math.Max(alpha, (byte)25);

                    int i = y * stride + x * 4;
                    p[i] = bC;
                    p[i + 1] = gC;
                    p[i + 2] = rC;
                    p[i + 3] = alpha;

                    avgLum += v;
                }

            avgLum /= (w * h);
            smoothMotion = smoothMotion * 0.9 + Math.Abs(avgLum - prevLum) / 255.0 * 0.1;
            prevLum = avgLum;

            if (s.AutoExposure)
            {
                exposure = Math.Clamp(
                    exposure * (1 - s.FpsSmoothStrength) +
                    (0.6 / Math.Max(0.08, avgLum / 255.0)) * s.FpsSmoothStrength,
                    s.ExposureMin, s.ExposureMax);
            }

            if (s.FXAA && scale <= 2)
                ApplyFXAA(bmp, w, h);

            bmp.AddDirtyRect(new Int32Rect(0, 0, w, h));
            bmp.Unlock();
        }

        unsafe void ApplyFXAA(WriteableBitmap bmp, int w, int h)
        {
            byte* p = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;

            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * stride + x * 4;
                    int edge =
                        Math.Abs(p[i] - p[i + 4]) +
                        Math.Abs(p[i] - p[i + stride]);

                    if (edge > 48)
                    {
                        byte b = (byte)((p[i] + p[i - 4] + p[i + 4] + p[i - stride] + p[i + stride]) / 5);
                        byte g = (byte)((p[i + 1] + p[i - 3] + p[i + 5] + p[i - stride + 1] + p[i + stride + 1]) / 5);
                        byte r = (byte)((p[i + 2] + p[i - 2] + p[i + 6] + p[i - stride + 2] + p[i + stride + 2]) / 5);

                        p[i] = b;
                        p[i + 1] = g;
                        p[i + 2] = r;
                    }
                }
        }

        void ClickThrough()
        {
            var h = new WindowInteropHelper(this).Handle;
            long style = GetWindowLongPtr(h, -20).ToInt64();
            style |= 0x20;     // WS_EX_TRANSPARENT
            style |= 0x80000;  // WS_EX_LAYERED
            SetWindowLongPtr(h, -20, new IntPtr(style));
        }

        [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr val);
    }

    public class FxUI : Window
    {
        public FxSettings Settings = new FxSettings();
        static readonly string SAVE = Path.Combine(Paths.Base, "FXSettings.json");

        public FxUI()
        {
            Title = "Voidstrap Lighting FX";
            Width = 520;
            Height = 700;
            Background = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            Load();
            Content = Build();
            Closed += (_, __) => Save();
        }

        UIElement Build()
        {
            var s = new StackPanel { Margin = new Thickness(16) };

            Section(s, "Lighting");
            Toggle(s, "Glow", () => Settings.Glow, v => Settings.Glow = v);
            Toggle(s, "Vignette", () => Settings.Vignette, v => Settings.Vignette = v);
            Toggle(s, "Auto Exposure", () => Settings.AutoExposure, v => Settings.AutoExposure = v);

            Section(s, "Advanced");
            Toggle(s, "Film Grain", () => Settings.FilmGrain, v => Settings.FilmGrain = v);
            Toggle(s, "FXAA", () => Settings.FXAA, v => Settings.FXAA = v);

            Section(s, "Performance");
            Toggle(s, "Performance Mode", () => Settings.PerformanceMode, v => Settings.PerformanceMode = v);
            Toggle(s, "Dynamic Resolution", () => Settings.DynamicResolution, v => Settings.DynamicResolution = v);

            return new ScrollViewer { Content = s };
        }

        void Section(Panel p, string t) =>
            p.Children.Add(new TextBlock { Text = t, Foreground = Brushes.LightGray, Margin = new Thickness(0, 14, 0, 6) });

        void Toggle(Panel p, string t, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox { Content = t, Foreground = Brushes.White, IsChecked = get() };
            c.Checked += (_, __) => set(true);
            c.Unchecked += (_, __) => set(false);
            p.Children.Add(c);
        }

        void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SAVE));
            File.WriteAllText(SAVE, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        void Load()
        {
            try
            {
                if (File.Exists(SAVE))
                    Settings = JsonSerializer.Deserialize<FxSettings>(File.ReadAllText(SAVE)) ?? new FxSettings();
            }
            catch { Settings = new FxSettings(); }
        }
    }

    public struct RECT { public int Left, Top, Right, Bottom; }
}
