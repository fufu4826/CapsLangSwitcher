using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CapsLangSwitcher {
    internal static class Program {
        [STAThread]
        private static void Main() {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\CapsLangSwitcher", out createdNew)) {
                if (!createdNew) {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
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
        private const uint KLF_ACTIVATE = 0x00000001;
        private const uint KLF_SUBSTITUTE_OK = 0x00000002;
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CapsLangSwitcher";

        private static LowLevelKeyboardProc hookProc;
        private static IntPtr hookId = IntPtr.Zero;
        private static bool enabled = true;
        private static bool capsDown = false;

        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem enabledItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly string exePath;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public TrayApp() {
            exePath = Application.ExecutablePath;
            Application.ApplicationExit += OnApplicationExit;

            hookProc = HookCallback;
            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, IntPtr.Zero, 0);

            enabledItem = new ToolStripMenuItem("Enabled: Caps Lock changes language");
            enabledItem.Checked = enabled;
            enabledItem.CheckOnClick = true;
            enabledItem.Click += (s, e) => {
                enabled = enabledItem.Checked;
                UpdateText();
            };

            startupItem = new ToolStripMenuItem("Start with Windows");
            startupItem.Checked = IsStartupEnabled();
            startupItem.CheckOnClick = true;
            startupItem.Click += (s, e) => {
                SetStartup(startupItem.Checked);
                startupItem.Checked = IsStartupEnabled();
            };

            var settingsItem = new ToolStripMenuItem("Open language settings");
            settingsItem.Click += (s, e) => Process.Start(new ProcessStartInfo("ms-settings:typing") { UseShellExecute = true });

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitThread();

            var menu = new ContextMenuStrip();
            menu.Items.Add(enabledItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += (s, e) => {
                enabled = !enabled;
                enabledItem.Checked = enabled;
                UpdateText();
            };
            UpdateText();
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                int msg = wParam.ToInt32();
                KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if (data.vkCode == VK_CAPITAL) {
                    if (enabled && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)) {
                        if (!capsDown) {
                            capsDown = true;
                            ThreadPool.QueueUserWorkItem(_ => {
                                Thread.Sleep(120);
                                SwitchToNextKeyboardLayout();
                            });
                        }
                    } else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) {
                        capsDown = false;
                    }
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private static void SwitchToNextKeyboardLayout() {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) {
                return;
            }

            string[] klids = GetPreloadedKeyboardLayouts();
            if (klids.Length < 2) {
                return;
            }

            uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
            IntPtr currentLayout = GetKeyboardLayout(threadId);
            long currentLanguageId = currentLayout.ToInt64() & 0xffff;
            IntPtr[] layouts = new IntPtr[klids.Length];
            int currentIndex = -1;

            for (int i = 0; i < klids.Length; i++) {
                layouts[i] = LoadKeyboardLayout(klids[i], KLF_ACTIVATE | KLF_SUBSTITUTE_OK);
                if ((layouts[i].ToInt64() & 0xffff) == currentLanguageId) {
                    currentIndex = i;
                }
            }

            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % layouts.Length;
            PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[nextIndex]);
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
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false)) {
                string value = key == null ? null : key.GetValue(RunValueName) as string;
                return String.Equals(value, Quote(exePath), StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SetStartup(bool start) {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath)) {
                if (start) {
                    key.SetValue(RunValueName, Quote(exePath), RegistryValueKind.String);
                } else {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static string Quote(string value) {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void UpdateText() {
            notifyIcon.Text = enabled ? "Caps Lock changes language" : "Caps Lock language switcher disabled";
            enabledItem.Text = enabled ? "Enabled: Caps Lock changes language" : "Disabled";
        }

        private void OnApplicationExit(object sender, EventArgs e) {
            if (hookId != IntPtr.Zero) {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
    }
}
