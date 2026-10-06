using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Forms = System.Windows.Forms;

namespace CloudMusicRemote
{
    static class Desktop
    {
        static Window window;
        static RemoteServer server;
        static MouseHook mouse;
        static Forms.NotifyIcon tray;
        static KeyboardRecorder recorder;
        static DispatcherTimer timer;
        static bool exiting;
        static int selected;
        static string recording;
        static string[] labels = { "播放 / 暂停", "上一首", "下一首", "音量增大", "音量减小" };
        static Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        static T Find<T>(string name) where T : class { return window.FindName(name) as T; }
        [STAThread] static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\CloudMusicRemote.v1", out created))
            {
                if (!created)
                {
                    IntPtr other = FindWindow(null, "随手控");
                    if (other == IntPtr.Zero) other = FindWindow(null, "网易云 · 随手控");
                    if (other != IntPtr.Zero) { ShowWindow(other, 9); SetForegroundWindow(other); }
                    return;
                }
                try
                {
                    string warning = Preferences.Load();
                    var app = new Application();
                    using (var resource = typeof(Desktop).Assembly.GetManifestResourceStream("Window.xaml")) window = (Window)XamlReader.Load(resource);
                    server = new RemoteServer();
                    selected = Preferences.Snapshot().Side;
                    for (int i = 0; i < Preferences.Actions.Length; i++)
                    {
                        string action = Preferences.Actions[i];
                        var row = new Grid { Margin = new Thickness(0, i == 0 ? 0 : 10, 0, 0) };
                        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                        row.Children.Add(new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
                        var button = new Button { Content = Preferences.Get(action).Label, FontSize = 11 };
                        Grid.SetColumn(button, 1); row.Children.Add(button); buttons[action] = button;
                        button.Click += (s, e) => Record(action); Find<StackPanel>("KeyRows").Children.Add(row);
                    }
                    Find<Button>("Upper").Click += (s, e) => Select(0, true);
                    Find<Button>("Lower").Click += (s, e) => Select(1, true);
                    Find<Button>("Disabled").Click += (s, e) => Select(2, true);
                    Find<Grid>("Segments").SizeChanged += (s, e) => Select(selected, false);
                    Find<Button>("Reset").Click += (s, e) =>
                    {
                        CancelRecord(); var data = Preferences.Defaults(); data.Side = selected;
                        try { Preferences.Save(data); RefreshKeys(); Status("已恢复默认快捷键"); } catch (Exception ex) { Status(ex.Message); }
                    };
                    Find<Button>("Play").Click += (s, e) => Send("toggle");
                    Find<Button>("Next").Click += (s, e) => Send("next");
                    Find<Button>("Previous").Click += (s, e) => Send("previous");
                    Find<Button>("MouseNav").Click += (s, e) => Jump("MouseHeading");
                    Find<Button>("KeysNav").Click += (s, e) => Jump("KeysHeading");
                    Find<Button>("Overview").Click += (s, e) => Find<ScrollViewer>("Scroller").ScrollToTop();
                    Find<Button>("PhoneNav").Click += (s, e) => Jump("ConnectionCard");
                    Find<Button>("CopyLink").Click += (s, e) => { try { Clipboard.SetText(server.Links()[0] + "#" + server.PairCode); Status("配对链接已复制"); } catch (Exception ex) { Status(ex.Message); } };
                    Find<Button>("OpenPhone").Click += (s, e) => { try { Process.Start("http://127.0.0.1:" + server.Port + "/#" + server.PairCode); } catch (Exception ex) { Status(ex.Message); } };
                    Find<Button>("RefreshLinks").Click += (s, e) => RefreshLinks();
                    Find<Button>("AllowPhone").Click += (s, e) =>
                    {
                        try
                        {
                            var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Allow-Phone.ps1") + "\" -Port " + server.Port) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                            Status("请完成 Windows 管理员确认");
                            ThreadPool.QueueUserWorkItem(_ => { using (process) { process.WaitForExit(); UpdateStatus(process.ExitCode == 0 ? "已允许同一子网的手机连接" : "网络规则未设置成功，请检查管理员权限"); } });
                        }
                        catch (Exception ex) { Status("未更改网络规则：" + ex.Message); }
                    };
                    mouse = new MouseHook(() => Send("next")); ApplyMouse();
                    tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "随手控 1.0", Visible = true };
                    tray.DoubleClick += (s, e) => ShowMain();
                    var menu = new Forms.ContextMenuStrip(); menu.Items.Add("打开随手控", null, (s, e) => ShowMain()); menu.Items.Add("退出", null, (s, e) => { exiting = true; window.Close(); }); tray.ContextMenuStrip = menu;
                    window.Closing += (s, e) => { CancelRecord(); if (!exiting) { e.Cancel = true; window.Hide(); tray.ShowBalloonTip(1800, "随手控仍在运行", "右击托盘图标可退出。", Forms.ToolTipIcon.Info); } };
                    window.Closed += (s, e) => Cleanup(); window.Deactivated += (s, e) => CancelRecord();
                    window.SizeChanged += (s, e) => Resize(); window.Loaded += (s, e) => { Resize(); Select(selected, false); RefreshLinks(); if (warning != null) Status(warning); };
                    window.SourceInitialized += (s, e) => { try { int enabled = 1; DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref enabled, 4); } catch { } };
                    timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, e) => { try { Find<TextBlock>("Song").Text = Music.Running() ? Music.Title() : "请打开网易云音乐"; } catch { } }; timer.Start();
                    server.Start(); app.Run(window);
                }
                catch (Exception ex) { Cleanup(); MessageBox.Show(ex.Message, "随手控启动失败"); }
            }
        }
        static void Cleanup() { if (timer != null) timer.Stop(); if (recorder != null) recorder.Dispose(); if (mouse != null) mouse.Dispose(); if (server != null) server.Dispose(); if (tray != null) tray.Dispose(); }
        static void ShowMain() { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
        static void Jump(string name) { var body = Find<StackPanel>("Body"); double y = Find<FrameworkElement>(name).TransformToAncestor(body).Transform(new Point(0, 0)).Y; Find<ScrollViewer>("Scroller").ScrollToVerticalOffset(y + body.Margin.Top); }
        static void RefreshLinks() { Find<TextBox>("Links").Text = String.Join(Environment.NewLine, server.Links()); Find<TextBlock>("PairCode").Text = "配对码  " + server.PairCode; }
        static void Resize() { bool compact = window.ActualWidth < 760; Find<ColumnDefinition>("SidebarWidth").Width = new GridLength(compact ? 0 : 175); Find<StackPanel>("Body").Margin = new Thickness(compact ? 22 : 30, 27, compact ? 22 : 30, 27); }
        static void Select(int index, bool save)
        {
            if (save) { try { var data = Preferences.Snapshot(); data.Side = index; Preferences.Save(data); } catch (Exception ex) { Status(ex.Message); return; } }
            selected = index; ApplyMouse(); double width = Math.Max(0, Find<Grid>("Segments").ActualWidth / 3); Find<Border>("Selection").Width = width;
            Find<TranslateTransform>("SelectionOffset").BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(width * index, TimeSpan.FromMilliseconds(save && SystemParameters.ClientAreaAnimation ? 280 : 0)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            var hint = Find<TextBlock>("SideHint"); hint.Text = index == 2 ? "已禁用：两个侧键均不触发本工具的切歌操作。" : "已选择" + (index == 0 ? "上侧键" : "下侧键") + "，按下切换下一首。";
            if (save) { hint.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(250))); Status("侧键设置已保存"); }
        }
        static void ApplyMouse() { if (mouse != null) { mouse.Button = selected == 0 ? 2 : 1; mouse.Enabled = selected != 2 && recording == null; } }
        static void Send(string action) { ThreadPool.QueueUserWorkItem(_ => { try { Music.Control(action); UpdateStatus("已发送网易云快捷键"); } catch (Exception ex) { UpdateStatus(ex.Message); } }); }
        static void UpdateStatus(string value) { if (window != null && !window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke((Action)(() => Status(value))); }
        static void Status(string value) { Find<TextBlock>("Status").Text = value; }
        static void RefreshKeys() { foreach (var action in Preferences.Actions) buttons[action].Content = Preferences.Get(action).Label; }
        static void Record(string action)
        {
            CancelRecord(); recording = action; ApplyMouse(); buttons[action].Content = "请按组合键…"; buttons[action].BorderBrush = new SolidColorBrush(Color.FromRgb(168, 207, 188));
            try { recorder = new KeyboardRecorder((key, modifiers) => window.Dispatcher.BeginInvoke((Action)(() => Capture(key, modifiers)))); Status("按组合键录入 · Esc 取消"); }
            catch (Exception ex) { CancelRecord(); Status(ex.Message); }
        }
        static void CancelRecord() { if (recorder != null) { recorder.Dispose(); recorder = null; } if (recording != null) { buttons[recording].ClearValue(Control.BorderBrushProperty); recording = null; RefreshKeys(); } ApplyMouse(); }
        static void Capture(int key, int modifiers)
        {
            if (recording == null) return;
            if (key == 27) { CancelRecord(); Status("已取消录入"); return; }
            try { var data = Preferences.Snapshot(); data.Keys[recording] = new Shortcut { Key = key, Modifiers = modifiers }; Preferences.Save(data); string label = data.Keys[recording].Label; CancelRecord(); Status("已保存：" + label); }
            catch (Exception ex) { Status(ex.Message); }
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }
    // Only installed while the user explicitly records a shortcut. No keystrokes are logged.
    sealed class KeyboardRecorder : IDisposable
    {
        delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        HookProc callback; IntPtr handle; Action<int, int> captured;
        public KeyboardRecorder(Action<int, int> handler)
        {
            captured = handler; callback = Hook; handle = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        IntPtr Hook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int key = Marshal.ReadInt32(data); int msg = message.ToInt32();
                bool modifier = key == 16 || key == 17 || key == 18 || (key >= 160 && key <= 165) || key == 91 || key == 92;
                if (!modifier)
                {
                    if (msg == 0x100 || msg == 0x104)
                    {
                        int mods = (Held(17) ? 1 : 0) | (Held(18) ? 2 : 0) | (Held(16) ? 4 : 0);
                        if (Held(91) || Held(92)) mods = 0;
                        captured(key, mods);
                    }
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(handle, code, message, data);
        }
        static bool Held(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        public void Dispose() { if (handle != IntPtr.Zero) { UnhookWindowsHookEx(handle); handle = IntPtr.Zero; } }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr msg, IntPtr data);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    }
}

