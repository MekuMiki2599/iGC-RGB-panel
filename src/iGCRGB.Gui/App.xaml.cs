using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using iGCRGB.Core;
using Forms = System.Windows.Forms;

namespace iGCRGB
{
    public partial class App : Application
    {
        public LightingConfig Config { get; private set; }
        public LightingEngine Engine { get; private set; }
        public string ConfigPath { get; private set; }
        public bool IsExiting { get; private set; }

        public event Action<string, bool> StatusChanged;
        public event Action<bool, string> DeviceStateChanged;

        private const string MutexName = @"Local\iGCRGB_Gui_Singleton";
        private const string ShowEventName = @"Local\iGCRGB_Gui_Show";

        private Mutex _mutex;
        private EventWaitHandle _showEvent;
        private Forms.NotifyIcon _tray;
        private DispatcherTimer _retryTimer;
        private bool _startHidden;
        private bool _balloonShown;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                SignalExistingInstance();
                Shutdown();
                return;
            }

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            StartShowListener();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            try { Directory.SetCurrentDirectory(baseDir); } catch { }

            AppLog.Init(Path.Combine(baseDir, "iGCRGB.log"));
            AppLog.Info("==== iGCRGB GUI 启动 ====");

            foreach (string arg in e.Args)
            {
                if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase)) _startHidden = true;
            }

            ConfigPath = ResolveConfigPath(baseDir);
            bool createdConfig;
            Config = LightingConfig.LoadOrCreate(ConfigPath, out createdConfig);
            if (createdConfig)
            {
                try { Config.Save(ConfigPath); } catch { }
            }

            Engine = new LightingEngine();
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            MainWindow window = new MainWindow();
            this.MainWindow = window;
            if (!_startHidden) window.Show();

            InitTray();
            if (_startHidden)
            {
                SetStatus("已在后台运行", false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(3000);
                    TrimWorkingSet();
                });
            }

            ApplyAsync();
        }

        private void StartShowListener()
        {
            Thread thread = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        if (_showEvent.WaitOne())
                        {
                            Dispatcher.BeginInvoke(new Action(ShowSettingsWindow));
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "iGCRGB-ShowListener";
            thread.Start();
        }

        private static void SignalExistingInstance()
        {
            try
            {
                EventWaitHandle handle;
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out handle))
                {
                    using (handle) handle.Set();
                }
            }
            catch
            {
            }
        }

        private static string ResolveConfigPath(string baseDir)
        {
            try
            {
                string probe = Path.Combine(baseDir, ".iGCRGB_write_test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return Path.Combine(baseDir, "settings.json");
            }
            catch
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(local, "iGCRGB"), "settings.json");
            }
        }

        // ---------------- 灯光应用 ----------------

        public void ApplyAsync()
        {
            SetStatus("正在应用灯效…", false);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = true;
                string error = null;
                string summary = null;
                try
                {
                    if (!Engine.IsInitialized) summary = Engine.Initialize();
                    Engine.Apply(Config);
                }
                catch (Exception ex)
                {
                    ok = false;
                    error = ex.Message;
                    AppLog.Error("应用灯效失败", ex);
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (ok)
                    {
                        StopRetry();
                        SetStatus("已应用 · " + DateTime.Now.ToString("HH:mm:ss"), false);
                        RaiseDeviceState(true, Engine.DeviceSummary ?? summary);
                        SaveConfig();
                    }
                    else
                    {
                        SetStatus("应用失败：" + error, true);
                        RaiseDeviceState(false, null);
                        StartRetry();
                    }
                }));
            });
        }

        public void Reinitialize()
        {
            try { Engine.Shutdown(); } catch { }
            ApplyAsync();
        }

        private void StartRetry()
        {
            if (_retryTimer == null)
            {
                _retryTimer = new DispatcherTimer();
                _retryTimer.Interval = TimeSpan.FromSeconds(8);
                _retryTimer.Tick += delegate
                {
                    if (Engine.IsInitialized) { StopRetry(); return; }
                    AppLog.Info("自动重试初始化灯光设备…");
                    ApplyAsync();
                };
            }
            if (!_retryTimer.IsEnabled) _retryTimer.Start();
        }

        private void StopRetry()
        {
            if (_retryTimer != null) _retryTimer.Stop();
        }

        public void SaveConfig()
        {
            try { Config.Save(ConfigPath); }
            catch (Exception ex) { AppLog.Error("保存配置失败", ex); }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            AppLog.Info("系统从睡眠唤醒，准备重新初始化灯光。");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(3000);
                Dispatcher.BeginInvoke(new Action(Reinitialize));
            });
        }

        // ---------------- 托盘 ----------------

        private void InitTray()
        {
            _tray = new Forms.NotifyIcon();
            _tray.Icon = IconFactory.TrayIcon;
            _tray.Text = "iGCRGB · 主板灯光控制";
            _tray.Visible = true;

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开设置", null, delegate { Dispatcher.Invoke(new Action(ShowSettingsWindow)); });
            menu.Items.Add("重新应用灯光", null, delegate { Dispatcher.Invoke(new Action(ApplyAsync)); });
            menu.Items.Add("立即关灯", null, delegate
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    Config.SystemOn = false;
                    MainWindow w = MainWindow as MainWindow;
                    if (w != null) w.SyncSystemSwitch();
                    ApplyAsync();
                }));
            });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Dispatcher.Invoke(new Action(ExitApp)); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { Dispatcher.Invoke(new Action(ShowSettingsWindow)); };
        }

        public void NotifyMinimizedToTray()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(1200);
                TrimWorkingSet();
            });
            if (_balloonShown || _tray == null) return;
            _balloonShown = true;
            try
            {
                _tray.ShowBalloonTip(3000, "iGCRGB 仍在后台运行",
                    "灯效会继续保持，双击托盘图标可以重新打开设置。", Forms.ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

        /// <summary>隐藏到托盘后回收内存，让后台占用更小。</summary>
        private static void TrimWorkingSet()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Process process = Process.GetCurrentProcess())
                {
                    SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1));
                }
            }
            catch
            {
            }
        }

        public void ShowSettingsWindow()
        {
            MainWindow window = MainWindow as MainWindow;
            if (window == null) return;
            window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }

        public void ExitApp()
        {
            if (IsExiting) return;
            IsExiting = true;
            AppLog.Info("程序退出");
            try { SaveConfig(); } catch { }
            try { Engine.Shutdown(); } catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { if (_mutex != null) _mutex.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }

        // ---------------- 状态广播 ----------------

        private void SetStatus(string text, bool isError)
        {
            Action<string, bool> handler = StatusChanged;
            if (handler != null) handler(text, isError);
        }

        private void RaiseDeviceState(bool connected, string summary)
        {
            Action<bool, string> handler = DeviceStateChanged;
            if (handler != null) handler(connected, summary);
        }
    }
}
