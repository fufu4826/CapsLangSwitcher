using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("CapsLangSwitcher")]
[assembly: AssemblyDescription("Use Caps Lock to switch Windows keyboard languages")]
[assembly: AssemblyCompany("CapsLangSwitcher")]
[assembly: AssemblyProduct("CapsLangSwitcher")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace CapsLangSwitcher {
    internal enum OsdPosition {
        Default,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    internal static class Program {
        internal static Mutex instanceMutex;

        [STAThread]
        private static void Main(string[] args) {
            if (!TrayApp.IsElevated()) {
                if (TrayApp.TryRestartElevated(null)) {
                    return;
                }
            }

            bool retryMutex = false;
            for (int i = 0; i < args.Length; i++) {
                if (String.Equals(args[i], "--elevated", StringComparison.OrdinalIgnoreCase)) {
                    retryMutex = true;
                    break;
                }
            }

            if (!TryTakeMutex(retryMutex)) {
                return;
            }

            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            } finally {
                ReleaseMutex();
            }
        }

        internal static void ReleaseMutex() {
            if (instanceMutex != null) {
                instanceMutex.Dispose();
                instanceMutex = null;
            }
        }

        private static bool TryTakeMutex(bool retry) {
            int attempts = retry ? 25 : 1;
            for (int i = 0; i < attempts; i++) {
                try {
                    bool createdNew;
                    Mutex mutex = new Mutex(true, @"Global\CapsLangSwitcher", out createdNew);
                    if (createdNew) {
                        instanceMutex = mutex;
                        return true;
                    }
                    mutex.Dispose();
                } catch (UnauthorizedAccessException) {
                    return false;
                }
                Thread.Sleep(80);
            }
            return false;
        }
    }

    public sealed class TrayApp : ApplicationContext {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private const int VK_CAPITAL = 0x14;
        private const uint KLF_SUBSTITUTE_OK = 0x00000002;
        private const uint LLKHF_INJECTED = 0x10;
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CapsLangSwitcher";
        private const string TaskName = "CapsLangSwitcher";
        private const string SettingsKeyPath = @"Software\CapsLangSwitcher";

        private static LowLevelKeyboardProc hookProc;
        private static IntPtr hookId = IntPtr.Zero;
        private static TrayApp instance;
        private static volatile bool enabled = true;
        private static volatile bool capsDown = false;

        private readonly UiPump ui;
        private readonly OsdForm osd;
        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem enabledItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly ToolStripMenuItem osdVisibleItem;
        private readonly ToolStripMenuItem positionItem;
        private readonly ToolStripMenuItem menuLanguageItem;
        private readonly ToolStripMenuItem settingsItem;
        private readonly ToolStripMenuItem restartItem;
        private readonly ToolStripMenuItem exitItem;
        private readonly System.Windows.Forms.Timer enforceTimer;
        private readonly System.Windows.Forms.Timer hookWatchdogTimer;
        private readonly Dictionary<string, Icon> iconCache = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IntPtr, WindowLayoutState> windowStates = new Dictionary<IntPtr, WindowLayoutState>();
        private readonly string exePath;
        private readonly bool elevated;

        private bool showOsd = true;
        private bool thaiUi = true;
        private OsdPosition osdPosition = OsdPosition.Default;
        private string lastLangCode = "";
        private IntPtr pendingForegroundHwnd = IntPtr.Zero;
        private IntPtr pendingFocusHwnd = IntPtr.Zero;
        private IntPtr pendingLayout = IntPtr.Zero;
        private uint pendingThreadId;
        private uint pendingProcessId;
        private int enforceAttempts;
        private IntPtr[] cachedLayouts;
        private IntPtr lastSwitchHwnd = IntPtr.Zero;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        private struct WindowLayoutState {
            public uint ProcessId;
            public IntPtr RequestedLayout;
            public bool Unreliable;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public TrayApp() {
            instance = this;
            exePath = Application.ExecutablePath;
            elevated = IsElevated();
            LoadSettings();
            ui = new UiPump();
            osd = new OsdForm();

            enforceTimer = new System.Windows.Forms.Timer();
            enforceTimer.Interval = 45;
            enforceTimer.Tick += (s, e) => VerifyAndEnforceLayout();

            // Windows can silently remove a low-level hook after sleep, unlock,
            // or a hook timeout. A very low-frequency renewal keeps the tray app
            // from looking alive while Caps Lock has reverted to normal.
            hookWatchdogTimer = new System.Windows.Forms.Timer();
            hookWatchdogTimer.Interval = 30000;
            hookWatchdogTimer.Tick += (s, e) => RenewKeyboardHook(false);

            Application.ApplicationExit += OnApplicationExit;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            hookProc = HookCallback;

            enabledItem = new ToolStripMenuItem("เปิดใช้: Caps Lock สลับภาษา");
            enabledItem.Checked = enabled;
            enabledItem.CheckOnClick = true;
            enabledItem.Click += (s, e) => {
                enabled = enabledItem.Checked;
                if (enabled) {
                    RenewKeyboardHook(true);
                }
                UpdateText();
            };

            startupItem = new ToolStripMenuItem("เปิดพร้อม Windows");
            startupItem.Checked = IsStartupEnabled();
            startupItem.CheckOnClick = true;
            startupItem.Click += (s, e) => {
                SetStartup(startupItem.Checked);
                startupItem.Checked = IsStartupEnabled();
            };

            osdVisibleItem = new ToolStripMenuItem();
            osdVisibleItem.Checked = showOsd;
            osdVisibleItem.CheckOnClick = true;
            osdVisibleItem.Click += (s, e) => {
                showOsd = osdVisibleItem.Checked;
                if (!showOsd) {
                    osd.HideNow();
                }
                SaveSettings();
            };

            positionItem = new ToolStripMenuItem();
            foreach (OsdPosition position in Enum.GetValues(typeof(OsdPosition))) {
                var item = new ToolStripMenuItem();
                item.Tag = position;
                item.Click += (s, e) => {
                    osdPosition = (OsdPosition)((ToolStripMenuItem)s).Tag;
                    UpdatePositionMenu();
                    SaveSettings();
                };
                positionItem.DropDownItems.Add(item);
            }

            menuLanguageItem = new ToolStripMenuItem();
            menuLanguageItem.Click += (s, e) => {
                thaiUi = !thaiUi;
                ApplyMenuLanguage();
                SaveSettings();
            };

            settingsItem = new ToolStripMenuItem();
            settingsItem.Click += (s, e) => Process.Start(new ProcessStartInfo("ms-settings:typing") { UseShellExecute = true });

            exitItem = new ToolStripMenuItem();
            exitItem.Click += (s, e) => ExitThread();

            var menu = new ContextMenuStrip();
            menu.Opening += (s, e) => RefreshLanguageIcon(true);
            menu.Items.Add(enabledItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(osdVisibleItem);
            menu.Items.Add(positionItem);
            menu.Items.Add(menuLanguageItem);
            restartItem = elevated ? null : new ToolStripMenuItem();
            if (!elevated) {
                restartItem.Click += (s, e) => RestartAsAdministrator();
                menu.Items.Add(restartItem);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;

            // Do not toggle Enabled on double-click. Accidental double-clicks on
            // the tray icon used to leave the app running while restoring normal
            // Caps Lock, which looked exactly like a broken keyboard hook.

            ApplyMenuLanguage();
            UpdatePositionMenu();
            RenewKeyboardHook(true);
            hookWatchdogTimer.Start();

            RefreshLanguageIcon(true);

            if (elevated && IsRunKeyEnabled()) {
                SetStartup(true);
                startupItem.Checked = IsStartupEnabled();
            }

            if (!elevated) {
                notifyIcon.BalloonTipTitle = "CapsLangSwitcher";
                notifyIcon.BalloonTipText = "ไม่ได้รันแบบ Administrator การสลับภาษาจะไม่ทำงานในเกมหรือโปรแกรมแอดมิน";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                notifyIcon.ShowBalloonTip(5000);
            }
        }

        public static bool IsElevated() {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool TryRestartElevated(string extraArgs) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                if (!String.IsNullOrEmpty(extraArgs)) {
                    psi.Arguments = extraArgs;
                }
                Process.Start(psi);
                return true;
            } catch (Win32Exception) {
                return false;
            }
        }

        private void RestartAsAdministrator() {
            if (TryRestartElevated("--elevated")) {
                Program.ReleaseMutex();
                ExitThread();
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) {
            if (e.Mode == PowerModes.Resume) {
                RenewKeyboardHook(false);
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) {
            if (e.Reason == SessionSwitchReason.SessionUnlock ||
                e.Reason == SessionSwitchReason.SessionLogon ||
                e.Reason == SessionSwitchReason.ConsoleConnect ||
                e.Reason == SessionSwitchReason.RemoteConnect) {
                RenewKeyboardHook(false);
            }
        }

        private void RenewKeyboardHook(bool notifyOnFailure) {
            IntPtr oldHook = hookId;
            hookId = IntPtr.Zero;
            if (oldHook != IntPtr.Zero) {
                UnhookWindowsHookEx(oldHook);
            }

            capsDown = false;
            IntPtr module = GetModuleHandle(null);
            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, module, 0);
            if (hookId == IntPtr.Zero) {
                // Some Windows/.NET combinations accept a null module handle
                // for WH_KEYBOARD_LL even when GetModuleHandle was rejected.
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, IntPtr.Zero, 0);
            }

            if (hookId == IntPtr.Zero && notifyOnFailure && notifyIcon != null) {
                notifyIcon.BalloonTipTitle = "CapsLangSwitcher";
                notifyIcon.BalloonTipText = thaiUi
                    ? "ไม่สามารถติดตั้งตัวดัก Caps Lock ได้ โปรแกรมจะลองใหม่อัตโนมัติ"
                    : "Caps Lock hook failed. The app will retry automatically.";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                notifyIcon.ShowBalloonTip(5000);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                uint vkCode = unchecked((uint)Marshal.ReadInt32(lParam));
                uint flags = unchecked((uint)Marshal.ReadInt32(lParam, 8));
                if ((flags & LLKHF_INJECTED) != 0) {
                    return CallNextHookEx(hookId, nCode, wParam, lParam);
                }

                if (vkCode == VK_CAPITAL) {
                    if (!enabled) {
                        return CallNextHookEx(hookId, nCode, wParam, lParam);
                    }

                    int msg = wParam.ToInt32();
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) {
                        if (!capsDown) {
                            capsDown = true;
                            PostUi(SwitchLanguageImmediately);
                        }
                    } else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) {
                        capsDown = false;
                    }
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private static void PostUi(Action action) {
            TrayApp app = instance;
            if (app == null || app.ui == null || !app.ui.IsHandleCreated) {
                return;
            }
            try {
                app.ui.BeginInvoke(action);
            } catch (ObjectDisposedException) {
            } catch (InvalidOperationException) {
            }
        }

        private static void SwitchLanguageImmediately() {
            TrayApp app = instance;
            if (app != null && enabled) {
                app.SwitchLanguageFromTap();
            }
        }

        private void SwitchLanguageFromTap() {
            IntPtr hwnd;
            IntPtr nextLayout;
            if (!TrySwitchToNextKeyboardLayout(out hwnd, out nextLayout)) {
                return;
            }
            Announce(hwnd, nextLayout);
        }

        private bool TrySwitchToNextKeyboardLayout(out IntPtr hwnd, out IntPtr nextLayout) {
            hwnd = GetForegroundWindow();
            nextLayout = IntPtr.Zero;
            if (hwnd == IntPtr.Zero) {
                return false;
            }

            uint processId;
            uint threadId = GetWindowThreadProcessId(hwnd, out processId);
            IntPtr focusHwnd = GetFocusWindow(threadId);

            IntPtr[] layouts = GetLayoutsForSwitch();
            if (layouts.Length < 2) {
                return false;
            }

            IntPtr currentLayout = GetKeyboardLayout(threadId);
            IntPtr requestedLayout;
            bool unreliable;
            if (TryGetWindowLayout(hwnd, out requestedLayout, out unreliable) && unreliable) {
                // Windows Terminal and elevated console hosts can accept a
                // language request without updating GetKeyboardLayout(). Use
                // the last layout requested for this window as the source for
                // the next toggle so EN <-> TH continues to alternate.
                currentLayout = requestedLayout;
            }
            int currentIndex = IndexOfLayout(layouts, currentLayout);
            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % layouts.Length;
            nextLayout = layouts[nextIndex];

            RequestLayoutChange(hwnd, focusHwnd, nextLayout);
            SetWindowState(hwnd, processId, nextLayout, unreliable);
            lastSwitchHwnd = hwnd;
            BeginLayoutEnforcement(hwnd, focusHwnd, threadId, processId, nextLayout);

            return true;
        }

        private void BeginLayoutEnforcement(IntPtr foregroundHwnd, IntPtr focusHwnd, uint threadId, uint processId, IntPtr targetLayout) {
            enforceTimer.Stop();
            pendingForegroundHwnd = foregroundHwnd;
            pendingFocusHwnd = focusHwnd;
            pendingThreadId = threadId;
            pendingProcessId = processId;
            pendingLayout = targetLayout;
            enforceAttempts = 0;
            enforceTimer.Start();
        }

        private void VerifyAndEnforceLayout() {
            if (pendingForegroundHwnd == IntPtr.Zero || pendingLayout == IntPtr.Zero) {
                enforceTimer.Stop();
                return;
            }

            WindowLayoutState state;
            if (!windowStates.TryGetValue(pendingForegroundHwnd, out state) || state.ProcessId != pendingProcessId) {
                // The window closed and its HWND got reused; the pending
                // request no longer refers to the same window.
                StopLayoutEnforcement();
                return;
            }

            IntPtr observedLayout = GetKeyboardLayout(pendingThreadId);
            if (SameLanguage(observedLayout, pendingLayout)) {
                SetWindowState(pendingForegroundHwnd, pendingProcessId, observedLayout, false);
                StopLayoutEnforcement();
                return;
            }

            // A newly focused app may discard the first request while its input
            // context is still activating. Re-send the exact same target layout;
            // this is not a second toggle and does not synthesize key presses.
            RequestLayoutChange(pendingForegroundHwnd, pendingFocusHwnd, pendingLayout);
            enforceAttempts++;
            if (enforceAttempts >= 3) {
                // Some terminal hosts change language successfully but keep
                // reporting their old HKL. Remember that behavior per window.
                SetWindowState(pendingForegroundHwnd, pendingProcessId, pendingLayout, true);
                StopLayoutEnforcement();
            }
        }

        private void StopLayoutEnforcement() {
            enforceTimer.Stop();
            pendingForegroundHwnd = IntPtr.Zero;
            pendingFocusHwnd = IntPtr.Zero;
            pendingLayout = IntPtr.Zero;
            pendingThreadId = 0;
            pendingProcessId = 0;
            enforceAttempts = 0;
        }

        private void Announce(IntPtr hwnd, IntPtr layout) {
            string code = GetLangCode(layout);
            SetTrayIcon(code);
            if (showOsd) {
                Screen screen = hwnd == IntPtr.Zero ? Screen.PrimaryScreen : Screen.FromHandle(hwnd);
                osd.ShowLang(GetOsdText(layout), screen, osdPosition);
            }
        }

        private void RefreshLanguageIcon(bool force) {
            // Opening the tray menu makes the taskbar the foreground window,
            // so prefer the window where Caps Lock last switched languages.
            IntPtr hwnd = lastSwitchHwnd;
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) {
                hwnd = GetForegroundWindow();
            }
            uint threadId = hwnd == IntPtr.Zero ? GetCurrentThreadId() : GetWindowThreadProcessId(hwnd, IntPtr.Zero);
            IntPtr layout = GetKeyboardLayout(threadId);
            IntPtr requestedLayout;
            bool unreliable;
            if (hwnd != IntPtr.Zero && TryGetWindowLayout(hwnd, out requestedLayout, out unreliable) && unreliable) {
                layout = requestedLayout;
            }
            string code = GetLangCode(layout);
            if (!force && String.Equals(code, lastLangCode, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            SetTrayIcon(code);
        }

        private void SetTrayIcon(string code) {
            lastLangCode = code;
            notifyIcon.Icon = GetOrCreateIcon(code);
            UpdateText();
        }

        private Icon GetOrCreateIcon(string code) {
            Icon icon;
            if (iconCache.TryGetValue(code, out icon)) {
                return icon;
            }
            icon = CreateLangIcon(code);
            iconCache[code] = icon;
            return icon;
        }

        private static Icon CreateLangIcon(string code) {
            string text = code.Length > 2 ? code.Substring(0, 2) : code;
            Color bg = BackgroundFor(code);
            int size = 32;
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush bgBrush = new SolidBrush(bg))
            using (Font font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat()) {
                g.Clear(bg);
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.FillRectangle(bgBrush, 0, 0, size, size);
                g.DrawString(text, font, Brushes.White, new RectangleF(-1, 1, size + 2, size), sf);
                IntPtr handle = bmp.GetHicon();
                try {
                    using (Icon tmp = Icon.FromHandle(handle)) {
                        return (Icon)tmp.Clone();
                    }
                } finally {
                    DestroyIcon(handle);
                }
            }
        }

        private static Color BackgroundFor(string code) {
            if (String.Equals(code, "TH", StringComparison.OrdinalIgnoreCase)) {
                return Color.FromArgb(184, 28, 48);
            }
            if (String.Equals(code, "EN", StringComparison.OrdinalIgnoreCase)) {
                return Color.FromArgb(28, 86, 168);
            }
            return Color.FromArgb(48, 48, 52);
        }

        private static string GetLangCode(IntPtr hkl) {
            int langId = (int)(hkl.ToInt64() & 0xffff);
            if (langId == 0) {
                return "??";
            }
            try {
                return new CultureInfo(langId).TwoLetterISOLanguageName.ToUpperInvariant();
            } catch (ArgumentException) {
                return langId.ToString("X4");
            }
        }

        private static string GetOsdText(IntPtr hkl) {
            string code = GetLangCode(hkl);
            if (code == "TH") {
                return "ไทย";
            }
            return code;
        }

        private static IntPtr GetFocusWindow(uint threadId) {
            GUITHREADINFO info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != IntPtr.Zero) {
                return info.hwndFocus;
            }
            return IntPtr.Zero;
        }

        private static void RequestLayoutChange(IntPtr foregroundHwnd, IntPtr focusHwnd, IntPtr nextLayout) {
            // Console hosts and Windows Terminal do not always handle the request
            // on the same HWND. Sending the same target layout to both is
            // idempotent and does not synthesize any keyboard input.
            PostMessage(foregroundHwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, nextLayout);
            if (focusHwnd != IntPtr.Zero && focusHwnd != foregroundHwnd) {
                PostMessage(focusHwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, nextLayout);
            }
        }

        private bool TryGetWindowLayout(IntPtr hwnd, out IntPtr requestedLayout, out bool unreliable) {
            requestedLayout = IntPtr.Zero;
            unreliable = false;
            WindowLayoutState state;
            if (!windowStates.TryGetValue(hwnd, out state)) {
                return false;
            }
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            if (processId == 0 || processId != state.ProcessId) {
                // HWND values get reused for new windows; drop stale state.
                windowStates.Remove(hwnd);
                return false;
            }
            requestedLayout = state.RequestedLayout;
            unreliable = state.Unreliable;
            return true;
        }

        private void SetWindowState(IntPtr hwnd, uint processId, IntPtr requestedLayout, bool unreliable) {
            if (windowStates.Count > 128) {
                windowStates.Clear();
            }
            WindowLayoutState state;
            state.ProcessId = processId;
            state.RequestedLayout = requestedLayout;
            state.Unreliable = unreliable;
            windowStates[hwnd] = state;
        }

        private static int IndexOfLayout(IntPtr[] layouts, IntPtr currentLayout) {
            for (int i = 0; i < layouts.Length; i++) {
                if (layouts[i] == currentLayout) {
                    return i;
                }
            }

            long currentLanguageId = currentLayout.ToInt64() & 0xffff;
            for (int i = 0; i < layouts.Length; i++) {
                if ((layouts[i].ToInt64() & 0xffff) == currentLanguageId) {
                    return i;
                }
            }
            return -1;
        }

        private static bool SameLanguage(IntPtr first, IntPtr second) {
            return (first.ToInt64() & 0xffff) == (second.ToInt64() & 0xffff);
        }

        private IntPtr[] GetLayoutsForSwitch() {
            // Query the live list every time: the user can add or remove
            // keyboard languages while the app is running.
            int count = GetKeyboardLayoutList(0, null);
            if (count >= 2) {
                IntPtr[] loaded = new IntPtr[count];
                if (GetKeyboardLayoutList(count, loaded) > 0) {
                    cachedLayouts = null;
                    return loaded;
                }
            }

            // Rare: fewer than two layouts are loaded right now. Loading the
            // registry preload is comparatively expensive, so cache it.
            if (cachedLayouts == null || cachedLayouts.Length < 2) {
                string[] klids = GetPreloadedKeyboardLayouts();
                IntPtr[] layouts = new IntPtr[klids.Length];
                for (int i = 0; i < klids.Length; i++) {
                    layouts[i] = LoadKeyboardLayout(klids[i], KLF_SUBSTITUTE_OK);
                }
                cachedLayouts = layouts;
            }
            return cachedLayouts;
        }

        private static string[] GetPreloadedKeyboardLayouts() {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload", false)) {
                if (key == null) {
                    return new string[0];
                }

                string[] names = key.GetValueNames();
                Array.Sort(names, (a, b) => {
                    int ai;
                    int bi;
                    bool ap = Int32.TryParse(a, out ai);
                    bool bp = Int32.TryParse(b, out bi);
                    if (ap && bp) {
                        return ai.CompareTo(bi);
                    }
                    return String.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });

                var layouts = new List<string>();
                foreach (string name in names) {
                    string klid = key.GetValue(name) as string;
                    if (!String.IsNullOrWhiteSpace(klid)) {
                        layouts.Add(klid);
                    }
                }
                return layouts.ToArray();
            }
        }

        private bool IsStartupEnabled() {
            return IsLogonTaskEnabled() || IsRunKeyEnabled();
        }

        private bool IsRunKeyEnabled() {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false)) {
                string value = key == null ? null : key.GetValue(RunValueName) as string;
                return String.Equals(value, Quote(exePath), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsLogonTaskEnabled() {
            return RunHidden("schtasks.exe", "/Query /TN \"" + TaskName + "\"") == 0;
        }

        private void SetStartup(bool start) {
            if (start) {
                if (elevated && TryCreateLogonTask()) {
                    DeleteRunKey();
                } else {
                    SetRunKey(true);
                }
            } else {
                DeleteLogonTask();
                DeleteRunKey();
            }
        }

        private bool TryCreateLogonTask() {
            string args = "/Create /F /SC ONLOGON /RL HIGHEST /IT /DELAY 0000:05 /TN \"" +
                TaskName + "\" /TR \"" + exePath + "\"";
            if (RunHidden("schtasks.exe", args) == 0) {
                return true;
            }
            args = "/Create /F /SC ONLOGON /RL HIGHEST /IT /TN \"" + TaskName + "\" /TR \"" + exePath + "\"";
            return RunHidden("schtasks.exe", args) == 0;
        }

        private static void DeleteLogonTask() {
            RunHidden("schtasks.exe", "/Delete /F /TN \"" + TaskName + "\"");
        }

        private void SetRunKey(bool start) {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath)) {
                if (start) {
                    key.SetValue(RunValueName, Quote(exePath), RegistryValueKind.String);
                } else {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private void DeleteRunKey() {
            SetRunKey(false);
        }

        private static int RunHidden(string fileName, string arguments) {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = fileName;
            psi.Arguments = arguments;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process process = Process.Start(psi)) {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static string Quote(string value) {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void LoadSettings() {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false)) {
                if (key == null) {
                    return;
                }
                showOsd = Convert.ToInt32(key.GetValue("ShowOsd", 1), CultureInfo.InvariantCulture) != 0;
                thaiUi = !String.Equals(Convert.ToString(key.GetValue("MenuLanguage", "th"), CultureInfo.InvariantCulture), "en", StringComparison.OrdinalIgnoreCase);
                OsdPosition savedPosition;
                if (Enum.TryParse(Convert.ToString(key.GetValue("OsdPosition", "Default"), CultureInfo.InvariantCulture), true, out savedPosition)) {
                    osdPosition = savedPosition;
                }
            }
        }

        private void SaveSettings() {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath)) {
                key.SetValue("ShowOsd", showOsd ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("MenuLanguage", thaiUi ? "th" : "en", RegistryValueKind.String);
                key.SetValue("OsdPosition", osdPosition.ToString(), RegistryValueKind.String);
            }
        }

        private void ApplyMenuLanguage() {
            startupItem.Text = thaiUi ? "เปิดพร้อม Windows" : "Start with Windows";
            osdVisibleItem.Text = thaiUi ? "แสดงป้ายภาษา EN/TH" : "Show EN/TH indicator";
            positionItem.Text = thaiUi ? "ตำแหน่งป้ายภาษา" : "Indicator position";
            menuLanguageItem.Text = thaiUi ? "Menu language: ไทย" : "ภาษาเมนู: English";
            settingsItem.Text = thaiUi ? "เปิดการตั้งค่าภาษา" : "Open language settings";
            exitItem.Text = thaiUi ? "ออก" : "Exit";
            if (restartItem != null) {
                restartItem.Text = thaiUi ? "เปิดใหม่แบบ Administrator" : "Restart as Administrator";
            }

            string[] thaiNames = { "ค่าเริ่มต้น", "มุมซ้ายบน", "มุมขวาบน", "มุมซ้ายล่าง", "กึ่งกลางล่าง", "มุมขวาล่าง" };
            string[] englishNames = { "Default", "Top left", "Top right", "Bottom left", "Bottom center", "Bottom right" };
            for (int i = 0; i < positionItem.DropDownItems.Count; i++) {
                positionItem.DropDownItems[i].Text = thaiUi ? thaiNames[i] : englishNames[i];
            }
            UpdateText();
        }

        private void UpdatePositionMenu() {
            foreach (ToolStripItem rawItem in positionItem.DropDownItems) {
                ToolStripMenuItem item = rawItem as ToolStripMenuItem;
                if (item != null) {
                    item.Checked = (OsdPosition)item.Tag == osdPosition;
                }
            }
        }

        private void UpdateText() {
            string suffix = elevated ? " (Admin)" : "";
            string prefix = lastLangCode.Length > 0 ? lastLangCode + " · " : "";
            notifyIcon.Text = prefix + (enabled ? (thaiUi ? "Caps Lock สลับภาษา" : "Caps Lock switches language") : (thaiUi ? "ปิดอยู่" : "Disabled")) + suffix;
            enabledItem.Text = enabled
                ? (thaiUi ? "เปิดใช้: Caps Lock สลับภาษา" : "Enabled: Caps Lock switches language")
                : (thaiUi ? "ปิดอยู่: Caps Lock ทำงานปกติ" : "Disabled: normal Caps Lock");
        }

        private void OnApplicationExit(object sender, EventArgs e) {
            instance = null;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            if (hookId != IntPtr.Zero) {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
            enforceTimer.Stop();
            hookWatchdogTimer.Stop();
            enforceTimer.Dispose();
            hookWatchdogTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Icon = null;
            notifyIcon.Dispose();
            foreach (Icon icon in iconCache.Values) {
                icon.Dispose();
            }
            iconCache.Clear();
            windowStates.Clear();
            cachedLayouts = null;
            osd.Dispose();
            ui.Dispose();
        }
    }

    internal sealed class UiPump : Control {
        public UiPump() {
            CreateHandle();
        }
    }

    internal sealed class OsdForm : Form {
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private string label = "";
        private readonly System.Windows.Forms.Timer hideTimer;
        private readonly Font labelFont;
        private readonly StringFormat labelFormat;

        public OsdForm() {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(220, 96);
            BackColor = Color.FromArgb(22, 22, 26);
            DoubleBuffered = true;
            Opacity = 0.92;
            labelFont = new Font("Segoe UI", 28f, FontStyle.Bold, GraphicsUnit.Point);
            labelFormat = new StringFormat();
            labelFormat.Alignment = StringAlignment.Center;
            labelFormat.LineAlignment = StringAlignment.Center;
            hideTimer = new System.Windows.Forms.Timer();
            hideTimer.Interval = 500;
            hideTimer.Tick += (s, e) => {
                hideTimer.Stop();
                ShowWindow(Handle, SW_HIDE);
            };
        }

        public void ShowLang(string text, Screen screen, OsdPosition position) {
            label = text ?? "";
            if (screen == null) {
                screen = Screen.PrimaryScreen;
            }
            Rectangle wa = screen.WorkingArea;
            const int margin = 32;
            int centerX = wa.Left + (wa.Width - Width) / 2;
            int left = wa.Left + margin;
            int right = wa.Right - Width - margin;
            int top = wa.Top + margin;
            int bottom = wa.Bottom - Height - margin;
            switch (position) {
                case OsdPosition.TopLeft:
                    Location = new Point(left, top);
                    break;
                case OsdPosition.TopRight:
                    Location = new Point(right, top);
                    break;
                case OsdPosition.BottomLeft:
                    Location = new Point(left, bottom);
                    break;
                case OsdPosition.BottomCenter:
                    Location = new Point(centerX, bottom);
                    break;
                case OsdPosition.BottomRight:
                    Location = new Point(right, bottom);
                    break;
                default:
                    Location = new Point(centerX, wa.Top + wa.Height / 5);
                    break;
            }
            hideTimer.Stop();
            hideTimer.Start();
            Invalidate();
            IntPtr handle = Handle;
            SetWindowPos(handle, HWND_TOPMOST, Left, Top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            ShowWindow(handle, SW_SHOWNOACTIVATE);
            Update();
        }

        public void HideNow() {
            hideTimer.Stop();
            if (IsHandleCreated) {
                ShowWindow(Handle, SW_HIDE);
            }
        }

        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            if (IsHandleCreated) {
                ApplyRoundedRegion();
            }
        }

        private void ApplyRoundedRegion() {
            int r = 20;
            Rectangle rc = ClientRectangle;
            if (rc.Width <= 0 || rc.Height <= 0) {
                return;
            }
            using (GraphicsPath path = new GraphicsPath()) {
                path.AddArc(rc.X, rc.Y, r, r, 180, 90);
                path.AddArc(rc.Right - r - 1, rc.Y, r, r, 270, 90);
                path.AddArc(rc.Right - r - 1, rc.Bottom - r - 1, r, r, 0, 90);
                path.AddArc(rc.X, rc.Bottom - r - 1, r, r, 90, 90);
                path.CloseFigure();
                Region old = Region;
                Region = new Region(path);
                if (old != null) {
                    old.Dispose();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.DrawString(label, labelFont, Brushes.White, ClientRectangle, labelFormat);
        }

        protected override bool ShowWithoutActivation {
            get { return true; }
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;
                cp.ExStyle |= 0x00000080;
                cp.ExStyle |= 0x00000008;
                cp.ExStyle |= 0x00000020;
                return cp;
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                hideTimer.Stop();
                hideTimer.Dispose();
                labelFont.Dispose();
                labelFormat.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
