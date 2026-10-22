using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using iGCRGB.Core;

namespace iGCRGB
{
    public sealed class EffectItem
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public string Badge { get; set; }
        public string Glyph { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }

    public partial class MainWindow : Window
    {
        private readonly App _app;
        private readonly ObservableCollection<EffectItem> _items = new ObservableCollection<EffectItem>();
        private bool _loading = true;
        private DispatcherTimer _applyTimer;
        private Border[] _swatches;

        private static readonly string[] SwatchColors =
        {
            "#FF3B30", "#FF9F0A", "#FFD60A", "#34C759", "#00C7BE",
            "#32ADE6", "#5856D6", "#AF52DE", "#FF2D55", "#FFFFFF"
        };

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public MainWindow()
        {
            InitializeComponent();
            _app = (App)Application.Current;

            Icon = IconFactory.WindowIcon;
            BuildSwatches();
            BuildEffectList();

            _app.StatusChanged += OnStatusChanged;
            _app.DeviceStateChanged += OnDeviceStateChanged;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            SourceInitialized += MainWindow_SourceInitialized;
            _loading = false;
        }

        // ---------------- 初始化 ----------------

        private void BuildEffectList()
        {
            _items.Clear();
            foreach (EffectMeta meta in EffectCatalog.All)
            {
                _items.Add(new EffectItem
                {
                    Key = meta.Key,
                    Title = meta.Title,
                    Desc = meta.Desc,
                    Badge = meta.Badge,
                    Glyph = meta.Glyph
                });
            }
            EffectListBox.ItemsSource = _items;
        }

        private void BuildSwatches()
        {
            _swatches = new Border[SwatchColors.Length];
            for (int i = 0; i < SwatchColors.Length; i++)
            {
                Color color = (Color)ColorConverter.ConvertFromString(SwatchColors[i]);
                Border border = new Border();
                border.Style = (Style)FindResource("Swatch");
                border.Background = new SolidColorBrush(color);
                border.Tag = i;
                border.MouseLeftButtonDown += Swatch_Click;
                SwatchPanel.Children.Add(border);
                _swatches[i] = border;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfigIntoUi();
            ApplyDarkTitleBar();
            Dispatcher.BeginInvoke(new Action(delegate { SettingsScroll.ScrollToTop(); }));
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
                DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
                // Windows 11：直接指定标题栏/边框/文字颜色，保证与深色界面一致
                int caption = 0x0019120E;  // #0E1219
                int border = 0x0035261E;   // #1E2635
                int text = 0x00F9F1ED;     // #EDF1F9
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));
                DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));
            }
            catch
            {
            }
        }

        private void LoadConfigIntoUi()
        {
            _loading = true;
            try
            {
                SwitchSystem.IsChecked = _app.Config.SystemOn;
                AutoApplySwitch.IsChecked = _app.Config.AutoApply;
                CloseToTraySwitch.IsChecked = _app.Config.CloseToTray;
                bool autoStart = StartupManager.IsEnabled();
                _app.Config.AutoStart = autoStart;
                AutoStartSwitch.IsChecked = autoStart;

                EffectItem selected = null;
                foreach (EffectItem item in _items)
                {
                    if (string.Equals(item.Key, _app.Config.Effect, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = item;
                        break;
                    }
                }
                EffectListBox.SelectedItem = selected ?? _items[5];
            }
            finally
            {
                _loading = false;
            }
            PopulateEditor();
        }

        // ---------------- 编辑器 ----------------

        private void PopulateEditor()
        {
            EffectMeta meta = _app.Config.CurrentMeta;
            EffectData data = _app.Config.CurrentData;
            if (data.Speed < 1) data.Speed = 1;

            _loading = true;
            try
            {
                PreviewTitle.Text = meta.Title;
                PreviewDesc.Text = meta.Desc;
                PreviewBadge.Text = meta.Badge;

                ColorCard.Visibility = meta.ShowColor ? Visibility.Visible : Visibility.Collapsed;
                ColorRow.ColumnDefinitions[0].Width = meta.ShowColor
                    ? new GridLength(1.62, GridUnitType.Star)
                    : new GridLength(0, GridUnitType.Star);
                SpeedCard.Visibility = meta.ShowSpeed ? Visibility.Visible : Visibility.Collapsed;
                DirCard.Visibility = meta.ShowDirection ? Visibility.Visible : Visibility.Collapsed;
                SensCard.Visibility = meta.ShowSensitivity ? Visibility.Visible : Visibility.Collapsed;
                StatusCard.Visibility = meta.ShowTemperature ? Visibility.Visible : Visibility.Collapsed;

                RSlider.Value = data.R;
                GSlider.Value = data.G;
                BSlider.Value = data.B;
                RValue.Text = data.R.ToString();
                GValue.Text = data.G.ToString();
                BValue.Text = data.B.ToString();
                UpdateHexBox(data);
                UpdateColorPreview(data);

                BriSlider.Value = data.Bri;
                BriValue.Text = data.Bri.ToString();
                SpeedSlider.Value = data.Speed;
                SpeedValue.Text = data.Speed.ToString();
                SensSlider.Value = data.Sensitivity;
                SensValue.Text = data.Sensitivity.ToString();

                TempNowSlider.Value = _app.Config.StateTemp;
                TempNowValue.Text = _app.Config.StateTemp + " ℃";
                TempLowSlider.Value = _app.Config.StateLow;
                TempLowValue.Text = _app.Config.StateLow + " ℃";
                TempHighSlider.Value = _app.Config.StateHigh;
                TempHighValue.Text = _app.Config.StateHigh + " ℃";

                UpdateDirButtons();
                UpdateTempBaseButtons();
            }
            finally
            {
                _loading = false;
            }
            UpdatePreview();
        }

        private void UpdateHexBox(EffectData data)
        {
            string hex = "#" + data.R.ToString("X2") + data.G.ToString("X2") + data.B.ToString("X2");
            if (!string.Equals(HexBox.Text, hex, StringComparison.OrdinalIgnoreCase))
                HexBox.Text = hex;
        }

        private void UpdateColorPreview(EffectData data)
        {
            ColorPreview.Background = new SolidColorBrush(Color.FromRgb((byte)data.R, (byte)data.G, (byte)data.B));
        }

        private void UpdatePreview()
        {
            EffectMeta meta = _app.Config.CurrentMeta;
            EffectData data = _app.Config.CurrentData;
            double factor = Math.Max(0.45, data.Bri / 255.0);

            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);

            if (!_app.Config.SystemOn)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(24, 27, 36), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(11, 13, 18), 1));
                PreviewBrightness.Text = "灯光已关闭";
            }
            else if (meta.Key == "Sleep")
            {
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(28, 31, 40), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(11, 13, 18), 1));
                PreviewBrightness.Text = "灯光已关闭";
            }
            else if (meta.Key == "StatusDisplay")
            {
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(59, 130, 246), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(245, 158, 11), 0.5));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(239, 68, 68), 1));
                PreviewBrightness.Text = "当前温度 " + _app.Config.StateTemp + " ℃";
            }
            else if (meta.Key == "Rainbow" || meta.Key == "ColorCycle")
            {
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 90, 110), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 205, 90), 0.25));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(90, 230, 160), 0.5));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(90, 170, 255), 0.75));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(170, 120, 255), 1));
                PreviewBrightness.Text = "亮度 " + data.Bri + " / 255";
            }
            else if (!meta.ShowColor)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(138, 107, 255), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(74, 140, 255), 0.55));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(11, 13, 18), 1));
                PreviewBrightness.Text = "亮度 " + data.Bri + " / 255";
            }
            else
            {
                Color baseColor = Color.FromRgb((byte)data.R, (byte)data.G, (byte)data.B);
                Color dim = Color.FromRgb((byte)(data.R * factor), (byte)(data.G * factor), (byte)(data.B * factor));
                brush.GradientStops.Add(new GradientStop(WithAlpha(dim, 0.95), 0));
                brush.GradientStops.Add(new GradientStop(WithAlpha(dim, 0.55), 0.5));
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(11, 13, 18), 1));
                PreviewBrightness.Text = "亮度 " + data.Bri + " / 255";
            }

            PreviewBg.Background = brush;
        }

        private static Color WithAlpha(Color color, double alpha)
        {
            byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            return Color.FromArgb(a, color.R, color.G, color.B);
        }

        // ---------------- 事件：列表 ----------------

        private void EffectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _app == null) return;
            EffectItem item = EffectListBox.SelectedItem as EffectItem;
            if (item == null) return;
            _app.Config.Effect = item.Key;
            PopulateEditor();
            ScheduleApply();
        }

        // ---------------- 事件：颜色 ----------------

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            if (border == null) return;
            int index = (int)border.Tag;
            Color color = (Color)ColorConverter.ConvertFromString(SwatchColors[index]);

            _loading = true;
            try
            {
                RSlider.Value = color.R;
                GSlider.Value = color.G;
                BSlider.Value = color.B;
            }
            finally
            {
                _loading = false;
            }
            ApplyColorFromSliders();
        }

        private void Rgb_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || _app == null) return;
            ApplyColorFromSliders();
        }

        private void ApplyColorFromSliders()
        {
            EffectData data = _app.Config.CurrentData;
            data.R = (int)Math.Round(RSlider.Value);
            data.G = (int)Math.Round(GSlider.Value);
            data.B = (int)Math.Round(BSlider.Value);
            RValue.Text = data.R.ToString();
            GValue.Text = data.G.ToString();
            BValue.Text = data.B.ToString();
            UpdateHexBox(data);
            UpdateColorPreview(data);
            UpdatePreview();
            ScheduleApply();
        }

        private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _app == null) return;
            string text = (HexBox.Text ?? "").Trim().TrimStart('#');
            if (text.Length != 6) return;
            int value;
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return;

            int r = (value >> 16) & 0xFF;
            int g = (value >> 8) & 0xFF;
            int b = value & 0xFF;

            _loading = true;
            try
            {
                RSlider.Value = r;
                GSlider.Value = g;
                BSlider.Value = b;
            }
            finally
            {
                _loading = false;
            }
            ApplyColorFromSliders();
        }

        // ---------------- 事件：滑杆 ----------------

        private void BriSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            EffectData data = _app.Config.CurrentData;
            data.Bri = (int)Math.Round(BriSlider.Value);
            BriValue.Text = data.Bri.ToString();
            UpdatePreview();
            ScheduleApply();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            EffectData data = _app.Config.CurrentData;
            data.Speed = (int)Math.Round(SpeedSlider.Value);
            SpeedValue.Text = data.Speed.ToString();
            ScheduleApply();
        }

        private void SensSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            EffectData data = _app.Config.CurrentData;
            data.Sensitivity = (int)Math.Round(SensSlider.Value);
            SensValue.Text = data.Sensitivity.ToString();
            ScheduleApply();
        }

        private void TempNowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _app.Config.StateTemp = (int)Math.Round(TempNowSlider.Value);
            TempNowValue.Text = _app.Config.StateTemp + " ℃";
            ScheduleApply();
        }

        private void TempLowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _app.Config.StateLow = (int)Math.Round(TempLowSlider.Value);
            TempLowValue.Text = _app.Config.StateLow + " ℃";
            ScheduleApply();
        }

        private void TempHighSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _app.Config.StateHigh = (int)Math.Round(TempHighSlider.Value);
            TempHighValue.Text = _app.Config.StateHigh + " ℃";
            ScheduleApply();
        }

        // ---------------- 事件：方向 / 温度来源 ----------------

        private void Dir0Button_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.CurrentData.Dir = 0;
            UpdateDirButtons();
            ScheduleApply();
        }

        private void Dir1Button_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.CurrentData.Dir = 1;
            UpdateDirButtons();
            ScheduleApply();
        }

        private void UpdateDirButtons()
        {
            Brush accent = (Brush)FindResource("AccentBrush");
            Brush dim = (Brush)FindResource("TextMutedBrush");
            int dir = _app.Config.CurrentData.Dir;
            Dir0Button.Content = dir == 0 ? "● 正向" : "正向";
            Dir1Button.Content = dir == 1 ? "● 反向" : "反向";
            Dir0Button.Foreground = dir == 0 ? accent : dim;
            Dir1Button.Foreground = dir == 1 ? accent : dim;
        }

        private void TempCpuButton_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.StateBase = "CPU";
            UpdateTempBaseButtons();
            ScheduleApply();
        }

        private void TempGpuButton_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.StateBase = "GPU";
            UpdateTempBaseButtons();
            ScheduleApply();
        }

        private void UpdateTempBaseButtons()
        {
            Brush accent = (Brush)FindResource("AccentBrush");
            Brush dim = (Brush)FindResource("TextMutedBrush");
            bool cpu = !string.Equals(_app.Config.StateBase, "GPU", StringComparison.OrdinalIgnoreCase);
            TempCpuButton.Content = cpu ? "● CPU" : "CPU";
            TempGpuButton.Content = cpu ? "GPU" : "● GPU";
            TempCpuButton.Foreground = cpu ? accent : dim;
            TempGpuButton.Foreground = cpu ? dim : accent;
        }

        // ---------------- 事件：开关 ----------------

        private void SwitchSystem_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _app.Config.SystemOn = SwitchSystem.IsChecked == true;
            UpdatePreview();
            ApplyNow();
        }

        public void SyncSystemSwitch()
        {
            _loading = true;
            try
            {
                SwitchSystem.IsChecked = _app.Config.SystemOn;
            }
            finally
            {
                _loading = false;
            }
            UpdatePreview();
        }

        private void AutoApplySwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _app.Config.AutoApply = AutoApplySwitch.IsChecked == true;
            _app.SaveConfig();
        }

        private void AutoStartSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            bool want = AutoStartSwitch.IsChecked == true;
            string message;
            bool ok = StartupManager.SetEnabled(want, out message);
            if (!ok)
            {
                _loading = true;
                AutoStartSwitch.IsChecked = !want;
                _loading = false;
                ShowFooterMessage(message, true);
                return;
            }
            _app.Config.AutoStart = want;
            _app.SaveConfig();
            ShowFooterMessage(message, false);
        }

        private void CloseToTraySwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _app.Config.CloseToTray = CloseToTraySwitch.IsChecked == true;
            _app.SaveConfig();
        }

        // ---------------- 事件：按钮 ----------------

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyNow();
        }

        private void OffButton_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.SystemOn = false;
            SyncSystemSwitch();
            ApplyNow();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            string message;
            bool ok = _app.Config.TryImportIgc(_app.Config.IgcConfigPath, out message);
            if (ok)
            {
                LoadConfigIntoUi();
                _app.SaveConfig();
                ApplyNow();
            }
            ShowFooterMessage(message, !ok);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(this,
                "确定把灯光设置恢复为默认值吗？（开机自启等程序选项会保留）",
                "恢复默认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            LightingConfig fresh = LightingConfig.CreateDefault();
            fresh.AutoStart = StartupManager.IsEnabled();
            fresh.AutoApply = _app.Config.AutoApply;
            fresh.CloseToTray = _app.Config.CloseToTray;
            fresh.IgcConfigPath = _app.Config.IgcConfigPath;
            _app.Config.CopyFrom(fresh);
            LoadConfigIntoUi();
            _app.SaveConfig();
            ApplyNow();
        }

        // ---------------- 应用调度 ----------------

        private void ScheduleApply()
        {
            if (!_app.Config.AutoApply)
            {
                ShowFooterMessage("有未应用的修改", false);
                return;
            }
            if (_applyTimer == null)
            {
                _applyTimer = new DispatcherTimer();
                _applyTimer.Interval = TimeSpan.FromMilliseconds(380);
                _applyTimer.Tick += delegate
                {
                    _applyTimer.Stop();
                    ApplyNow();
                };
            }
            _applyTimer.Stop();
            _applyTimer.Start();
        }

        public void ApplyNow()
        {
            _app.SaveConfig();
            _app.ApplyAsync();
        }

        // ---------------- 状态显示 ----------------

        private void OnStatusChanged(string text, bool isError)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                FooterStatus.Text = text;
                FooterStatus.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "TextMutedBrush");
                FooterDot.Fill = (Brush)FindResource(isError ? "DangerBrush" : "Accent2Brush");
            }));
        }

        private void OnDeviceStateChanged(bool connected, string summary)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                StatusDot.Fill = (Brush)FindResource(connected ? "Accent2Brush" : "DangerBrush");
                TxtStatus.Text = connected
                    ? ("已连接 · " + (summary ?? "iGame 主板"))
                    : "未连接 · 请检查设备";
            }));
        }

        private void ShowFooterMessage(string text, bool isError)
        {
            FooterStatus.Text = text;
            FooterStatus.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "TextMutedBrush");
            FooterDot.Fill = (Brush)FindResource(isError ? "DangerBrush" : "Accent2Brush");
        }

        // ---------------- 关闭行为 ----------------

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_app.IsExiting) return;
            e.Cancel = true;
            if (_app.Config.CloseToTray)
            {
                Hide();
                _app.NotifyMinimizedToTray();
            }
            else
            {
                _app.ExitApp();
            }
        }
    }
}
