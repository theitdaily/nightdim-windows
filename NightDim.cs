using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NightDim
{
    internal static class Native
    {
        internal const int WS_EX_TRANSPARENT = 0x20;
        internal const int WS_EX_TOOLWINDOW = 0x80;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WM_HOTKEY = 0x0312;
        internal const uint MOD_ALT = 1;
        internal const uint MOD_CONTROL = 2;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr awareness);
        [DllImport("user32.dll", EntryPoint = "SetProcessDPIAware")]
        internal static extern bool SetProcessDPIAware();
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);
        [DllImport("gdi32.dll")]
        internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        internal static extern bool GetDeviceGammaRamp(IntPtr dc, [Out] ushort[] ramp);
        [DllImport("gdi32.dll")]
        internal static extern bool SetDeviceGammaRamp(IntPtr dc, [In] ushort[] ramp);
    }

    // Click-through window that covers desktop applications, including the tray and
    // the settings window. A hardware cursor is NOT covered by layered windows.
    internal sealed class DimOverlay : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return p;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        internal DimOverlay(Rectangle bounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.01;
        }
    }

    internal sealed class Settings
    {
        internal int Amount = 35;
        internal bool Enabled = true;
        internal bool UseSchedule = true;
        internal int PauseStart = 8 * 60;
        internal int PauseEnd = 20 * 60;
        internal bool TryDimCursor = true;
        internal string Language = UiText.SystemLanguage();

        internal Settings Copy()
        {
            return (Settings)MemberwiseClone();
        }

        internal bool SameAs(Settings other)
        {
            return Amount == other.Amount && Enabled == other.Enabled &&
                UseSchedule == other.UseSchedule && PauseStart == other.PauseStart &&
                PauseEnd == other.PauseEnd && TryDimCursor == other.TryDimCursor;
        }

        internal bool IsPaused(DateTime now)
        {
            if (!UseSchedule) return false;
            int minute = now.Hour * 60 + now.Minute;
            if (PauseStart == PauseEnd) return true; // Whole-day pause.
            if (PauseStart < PauseEnd) return minute >= PauseStart && minute < PauseEnd;
            return minute >= PauseStart || minute < PauseEnd; // Crosses midnight.
        }

        internal static Settings Read(string path)
        {
            Settings value = new Settings();
            try
            {
                if (!File.Exists(path)) return value;
                string source = File.ReadAllText(path).Trim();
                int legacy;
                if (int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out legacy))
                {
                    value.Amount = Limit(legacy, 0, 95);
                    return value;
                }
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string data = line.Substring(eq + 1).Trim();
                    int number;
                    if (key == "Amount" && int.TryParse(data, out number)) value.Amount = Limit(number, 0, 95);
                    else if (key == "Enabled") value.Enabled = data == "1";
                    else if (key == "UseSchedule") value.UseSchedule = data == "1";
                    else if (key == "PauseStart" && int.TryParse(data, out number)) value.PauseStart = Limit(number, 0, 1439);
                    else if (key == "PauseEnd" && int.TryParse(data, out number)) value.PauseEnd = Limit(number, 0, 1439);
                    else if (key == "TryDimCursor") value.TryDimCursor = data == "1";
                    else if (key == "Language" && UiText.IsSupported(data)) value.Language = data;
                }
            }
            catch { /* Keep safe defaults if settings were corrupted. */ }
            return value;
        }

        internal void Write(string path)
        {
            // Only confirmed settings are saved. An unconfirmed change is never
            // made permanent, even if the process gets killed during the preview.
            string contents = "Amount=" + Amount.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "Enabled=" + (Enabled ? "1" : "0") + "\r\n" +
                "UseSchedule=" + (UseSchedule ? "1" : "0") + "\r\n" +
                "PauseStart=" + PauseStart.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "PauseEnd=" + PauseEnd.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "TryDimCursor=" + (TryDimCursor ? "1" : "0") + "\r\n" +
                "Language=" + Language + "\r\n";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, contents);
                if (File.Exists(path))
                {
                    try { File.Replace(temp, path, null); }
                    catch
                    {
                        File.Copy(temp, path, true);
                        File.Delete(temp);
                    }
                }
                else File.Move(temp, path);
            }
            catch { /* Failure to save must not break the emergency stop. */ }
        }

        private static int Limit(int n, int low, int high) { return Math.Max(low, Math.Min(high, n)); }
    }

    // Windows gamma ramps are shared system state and have severe driver/HDR
    // limitations. Save the previous ramp and always attempt to restore it.
    internal sealed class GammaControl : IDisposable
    {
        private sealed class Display
        {
            internal IntPtr Dc;
            internal ushort[] Original;
            internal ushort[] Applied;
            internal bool Modified;
        }

        private readonly List<Display> displays = new List<Display>();

        internal GammaControl(Screen[] screens)
        {
            foreach (Screen screen in screens)
            {
                Display display = new Display();
                try
                {
                    display.Dc = Native.CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                    if (display.Dc != IntPtr.Zero)
                    {
                        ushort[] original = new ushort[3 * 256];
                        if (Native.GetDeviceGammaRamp(display.Dc, original)) display.Original = original;
                    }
                }
                catch { }
                displays.Add(display);
            }
        }

        // Returns the gamma factor assumed on each screen, or 1.0 on failure.
        // Drivers may silently reject gamma values despite returning success.
        internal double[] Apply(int amount, bool tryCursor)
        {
            double[] factors = new double[displays.Count];
            for (int i = 0; i < factors.Length; i++) factors[i] = 1.0;
            bool shouldDim = tryCursor && amount > 0;
            double factor = Math.Max(0.60, 1.0 - amount / 100.0);
            for (int i = 0; i < displays.Count; i++)
            {
                Display display = displays[i];
                if (display.Original == null || display.Dc == IntPtr.Zero) continue;
                if (!shouldDim)
                {
                    if (display.Modified) RestoreOne(display);
                    continue;
                }
                ushort[] proposed = new ushort[3 * 256];
                for (int j = 0; j < proposed.Length; j++)
                    proposed[j] = (ushort)Math.Max(0, Math.Min(65535,
                        Math.Round(display.Original[j] * factor)));
                try
                {
                    if (Native.SetDeviceGammaRamp(display.Dc, proposed))
                    {
                        display.Modified = true;
                        // Some drivers report success without applying the ramp.
                        ushort[] check = new ushort[3 * 256];
                        if (Native.GetDeviceGammaRamp(display.Dc, check) &&
                            IsSimilarRamp(proposed, check))
                        {
                            display.Applied = proposed;
                            factors[i] = factor;
                        }
                        else RestoreOne(display);
                    }
                }
                catch { /* Fall back to overlay-only dimming. */ }
                if (factors[i] == 1.0 && display.Modified) RestoreOne(display);
            }
            return factors;
        }

        private static bool IsSimilarRamp(ushort[] a, ushort[] b)
        {
            // Allow small quantization/driver rounding differences.
            int[] samples = { 64, 128, 192, 255, 64 + 256, 128 + 256, 255 + 512 };
            foreach (int i in samples)
                if (Math.Abs((int)a[i] - (int)b[i]) > 512) return false;
            return true;
        }

        private static void RestoreOne(Display display)
        {
            try
            {
                ushort[] current = new ushort[3 * 256];
                // Do not overwrite a gamma profile replaced by Windows or an
                // independent calibration tool after our previous change.
                if (display.Applied == null ||
                    !Native.GetDeviceGammaRamp(display.Dc, current) ||
                    IsSimilarRamp(display.Applied, current))
                    Native.SetDeviceGammaRamp(display.Dc, display.Original);
            }
            catch { }
            display.Applied = null;
            display.Modified = false;
        }

        internal void Restore()
        {
            foreach (Display display in displays)
                if (display.Modified && display.Original != null && display.Dc != IntPtr.Zero)
                    RestoreOne(display);
        }

        public void Dispose()
        {
            Restore();
            foreach (Display display in displays)
            {
                if (display.Dc != IntPtr.Zero) Native.DeleteDC(display.Dc);
            }
            displays.Clear();
        }
    }

    // Stays bright and above all overlays so a too-dark preview is reversible.
    internal sealed class ConfirmWindow : Form
    {
        internal event EventHandler Accepted;
        internal event EventHandler Rejected;
        private readonly Label heading;
        private readonly Label countdownLabel;
        private readonly Button accept;
        private readonly Button reject;
        private string language;
        private int remaining = 30;
        private bool dismissing;

        internal ConfirmWindow(string language)
        {
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            ClientSize = new Size(440, 209);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(245, 247, 251);
            ForeColor = Color.FromArgb(22, 28, 43);

            heading = new Label();
            heading.Location = new Point(19, 17);
            heading.Size = new Size(402, 35);
            heading.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            Controls.Add(heading);

            countdownLabel = new Label();
            countdownLabel.Location = new Point(20, 65);
            countdownLabel.Size = new Size(402, 58);
            Controls.Add(countdownLabel);

            accept = new Button();
            accept.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            accept.Location = new Point(20, 141);
            accept.Size = new Size(188, 43);
            accept.BackColor = Color.FromArgb(216, 239, 218);
            accept.Click += delegate { if (Accepted != null) Accepted(this, EventArgs.Empty); };
            Controls.Add(accept);

            reject = new Button();
            reject.Location = new Point(223, 141);
            reject.Size = new Size(193, 43);
            reject.Click += delegate { if (Rejected != null) Rejected(this, EventArgs.Empty); };
            Controls.Add(reject);
            AcceptButton = accept;
            CancelButton = reject;
            ApplyLanguage(language);
        }

        internal void ApplyLanguage(string code)
        {
            language = code;
            Text = UiText.Get(code, "ConfirmTitle");
            heading.Text = UiText.Get(code, "ConfirmHeading");
            accept.Text = UiText.Get(code, "Accept");
            reject.Text = UiText.Get(code, "Revert");
            UpdateRemaining(remaining);
        }

        internal void UpdateRemaining(int seconds)
        {
            remaining = seconds;
            countdownLabel.Text = string.Format(CultureInfo.InvariantCulture,
                UiText.Get(language, "ConfirmCountdown"), seconds);
        }

        internal void Dismiss()
        {
            dismissing = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!dismissing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (Rejected != null) Rejected(this, EventArgs.Empty);
                return;
            }
            base.OnFormClosing(e);
        }
    }

    internal sealed class ControlWindow : Form
    {
        private const int TimeoutSeconds = 30;
        private const int ConfirmDelayMilliseconds = 650;
        private const int HotkeyDarker = 101;
        private const int HotkeyLighter = 102;
        private const int HotkeyEmergency = 103;
        private readonly string settingsFile;
        private readonly EventWaitHandle stopSignal;
        private readonly System.Windows.Forms.Timer clock;
        private readonly NotifyIcon tray;
        private readonly TrackBar slider;
        private readonly Label amountLabel;
        private readonly Label taglineLabel;
        private readonly Label rangeLabel;
        private readonly Label languageLabel;
        private readonly Label fromLabel;
        private readonly Label toLabel;
        private readonly ComboBox languagePicker;
        private readonly Label scheduleLabel;
        private readonly Label statusLabel;
        private readonly Button toggleButton;
        private readonly Button hideButton;
        private readonly Button exitButton;
        private readonly CheckBox scheduleCheck;
        private readonly CheckBox cursorCheck;
        private readonly DateTimePicker pauseFrom;
        private readonly DateTimePicker pauseTo;
        private readonly ToolStripMenuItem trayToggle;
        private readonly ToolStripMenuItem trayOpen;
        private readonly ToolStripMenuItem trayDarker;
        private readonly ToolStripMenuItem trayLighter;
        private readonly ToolStripMenuItem trayEmergency;
        private readonly ToolStripMenuItem trayExit;
        private readonly Stopwatch previewClock = new Stopwatch();
        private readonly Stopwatch popupDelay = new Stopwatch();
        private readonly bool[] registeredHotkeys = new bool[3];
        private Settings confirmed;
        private Settings draft;
        private bool updatingControls;
        private bool draggingSlider;
        private bool exiting;
        private bool firstShown;
        private bool emergencyOff;
        private readonly bool firstLaunch;
        private ConfirmWindow confirmation;
        private DimOverlay[] overlays = new DimOverlay[0];
        private GammaControl gamma;
        private Icon appIcon;
        private int lastEffective = -1;
        private bool lastTryCursor;

        internal ControlWindow(EventWaitHandle stopSignal)
        {
            this.stopSignal = stopSignal;
            settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NightDim", "settings.txt");
            firstLaunch = !File.Exists(settingsFile);
            confirmed = Settings.Read(settingsFile);
            draft = confirmed.Copy();
            // First run is a safety preview: no dimming setting exists yet.
            // If the user does not confirm, revert to 0% automatically.
            if (firstLaunch) confirmed.Amount = 0;

            Text = "NightDim";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(510, 472);
            MinimumSize = new Size(526, 511);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(25, 27, 35);
            ForeColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
            // Not TopMost! The overlay must cover our own window and the tray.

            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (appIcon != null) Icon = appIcon;

            AddLabel("NightDim", 22, 13, 205, 40, 20F, FontStyle.Bold, Color.White);
            languageLabel = AddLabel("", 228, 21, 81, 27, 9F,
                FontStyle.Regular, Color.FromArgb(172, 181, 200));
            languagePicker = new ComboBox();
            languagePicker.DropDownStyle = ComboBoxStyle.DropDownList;
            languagePicker.Location = new Point(310, 19);
            languagePicker.Size = new Size(179, 28);
            languagePicker.Items.AddRange(new object[] { "English", "Русский", "Српски (ћирилица)" });
            languagePicker.SelectedIndex = UiText.Index(draft.Language);
            languagePicker.SelectedIndexChanged += delegate
            {
                if (!updatingControls) ChangeLanguage(UiText.FromIndex(languagePicker.SelectedIndex));
            };
            Controls.Add(languagePicker);
            taglineLabel = AddLabel("", 24, 56, 460, 25, 9F,
                FontStyle.Regular, Color.FromArgb(172, 181, 200));

            amountLabel = AddLabel("", 385, 89, 102, 46, 20F, FontStyle.Bold,
                Color.FromArgb(147, 197, 253));
            amountLabel.TextAlign = ContentAlignment.MiddleRight;
            slider = new TrackBar();
            slider.Minimum = 0;
            slider.Maximum = 95;
            slider.TickFrequency = 10;
            slider.SmallChange = 1;
            slider.LargeChange = 5;
            slider.Value = draft.Amount;
            slider.Location = new Point(17, 91);
            slider.Size = new Size(364, 53);
            slider.BackColor = BackColor;
            slider.ValueChanged += delegate { if (!updatingControls) UpdateDraft(delegate(Settings s) { s.Amount = slider.Value; s.Enabled = true; }); };
            slider.MouseDown += delegate { draggingSlider = true; DismissConfirmation(); };
            slider.MouseUp += delegate { EndSliderDrag(); };
            slider.MouseCaptureChanged += delegate { if (draggingSlider && !slider.Capture) EndSliderDrag(); };
            slider.KeyUp += delegate { if (previewClock.IsRunning) popupDelay.Restart(); };
            Controls.Add(slider);
            rangeLabel = AddLabel("", 23, 149, 463, 24, 8.5F,
                FontStyle.Regular, Color.FromArgb(168, 173, 188));

            scheduleCheck = new CheckBox();
            scheduleCheck.Text = "";
            scheduleCheck.Checked = draft.UseSchedule;
            scheduleCheck.Location = new Point(23, 188);
            scheduleCheck.Size = new Size(454, 26);
            scheduleCheck.CheckedChanged += delegate { if (!updatingControls)
                UpdateDraft(delegate(Settings s) { s.UseSchedule = scheduleCheck.Checked; }); };
            Controls.Add(scheduleCheck);

            fromLabel = AddLabel("", 25, 228, 49, 27, 10F, FontStyle.Regular, Color.White);
            pauseFrom = MakeTimePicker(78, 221, draft.PauseStart);
            toLabel = AddLabel("", 221, 228, 35, 27, 10F, FontStyle.Regular, Color.White);
            pauseTo = MakeTimePicker(262, 221, draft.PauseEnd);
            pauseFrom.ValueChanged += delegate { if (!updatingControls)
                UpdateDraft(delegate(Settings s) { s.PauseStart = ClockMinute(pauseFrom.Value); }); };
            pauseTo.ValueChanged += delegate { if (!updatingControls)
                UpdateDraft(delegate(Settings s) { s.PauseEnd = ClockMinute(pauseTo.Value); }); };

            scheduleLabel = AddLabel("", 23, 268, 460, 37, 9F,
                FontStyle.Regular, Color.FromArgb(176, 193, 214));

            cursorCheck = new CheckBox();
            cursorCheck.Text = "";
            cursorCheck.Checked = draft.TryDimCursor;
            cursorCheck.Location = new Point(23, 311);
            cursorCheck.Size = new Size(466, 27);
            cursorCheck.CheckedChanged += delegate { if (!updatingControls)
                UpdateDraft(delegate(Settings s) { s.TryDimCursor = cursorCheck.Checked; }); };
            Controls.Add(cursorCheck);

            toggleButton = MakeButton("", 22, 362, 142, 40);
            toggleButton.Click += delegate { UpdateDraft(delegate(Settings s) { s.Enabled = !s.Enabled; }); };
            hideButton = MakeButton("", 174, 362, 171, 40);
            hideButton.Click += delegate { Hide(); };
            exitButton = MakeButton("", 355, 362, 133, 40);
            exitButton.Click += delegate { Quit(); };

            statusLabel = AddLabel("", 23, 415, 463, 51, 8.5F,
                FontStyle.Regular, Color.FromArgb(168, 176, 190));


            ContextMenuStrip menu = new ContextMenuStrip();
            trayOpen = new ToolStripMenuItem("", null, delegate { ShowSettings(); });
            trayDarker = new ToolStripMenuItem("", null, delegate { ChangeAmount(5); });
            trayLighter = new ToolStripMenuItem("", null, delegate { ChangeAmount(-5); });
            trayToggle = new ToolStripMenuItem("", null,
                delegate { UpdateDraft(delegate(Settings s) { s.Enabled = !s.Enabled; }); });
            trayEmergency = new ToolStripMenuItem("", null, delegate { EmergencyReset(); });
            trayExit = new ToolStripMenuItem("", null, delegate { Quit(); });
            menu.Items.Add(trayOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(trayDarker);
            menu.Items.Add(trayLighter);
            menu.Items.Add(trayToggle);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(trayEmergency);
            menu.Items.Add(trayExit);
            tray = new NotifyIcon();
            tray.Icon = appIcon ?? SystemIcons.Application;
            tray.Text = "NightDim";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseDoubleClick += delegate(object o, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowSettings();
            };

            clock = new System.Windows.Forms.Timer();
            clock.Interval = 250;
            clock.Tick += OnClockTick;
            clock.Start();
            SyncControls();
            ApplyLanguage();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        private Label AddLabel(string text, int x, int y, int width, int height,
            float fontSize, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.Font = new Font("Segoe UI", fontSize, style);
            label.ForeColor = color;
            Controls.Add(label);
            return label;
        }

        private DateTimePicker MakeTimePicker(int x, int y, int minute)
        {
            DateTimePicker picker = new DateTimePicker();
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.Location = new Point(x, y);
            picker.Size = new Size(124, 31);
            DateTime today = DateTime.Today;
            picker.Value = today.AddMinutes(minute);
            Controls.Add(picker);
            return picker;
        }

        private Button MakeButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(96, 110, 134);
            button.BackColor = Color.FromArgb(46, 50, 64);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            Controls.Add(button);
            return button;
        }

        private static int ClockMinute(DateTime time) { return time.Hour * 60 + time.Minute; }
        private static string PrettyTime(int minutes)
        {
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (firstShown) return;
            firstShown = true;
            RecreateDisplays();
            registeredHotkeys[0] = Native.RegisterHotKey(Handle, HotkeyDarker,
                Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.Down);
            registeredHotkeys[1] = Native.RegisterHotKey(Handle, HotkeyLighter,
                Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.Up);
            registeredHotkeys[2] = Native.RegisterHotKey(Handle, HotkeyEmergency,
                Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.F12);
            ApplyLanguage();
            if (firstLaunch) UpdateDraft(delegate(Settings s) { /* Confirm initial 35%. */ });
        }

        private void RecreateDisplays()
        {
            if (gamma != null) { gamma.Dispose(); gamma = null; }
            foreach (DimOverlay overlay in overlays)
            {
                try { overlay.Close(); overlay.Dispose(); } catch { }
            }
            Screen[] screens = Screen.AllScreens;
            overlays = new DimOverlay[screens.Length];
            for (int i = 0; i < screens.Length; i++) overlays[i] = new DimOverlay(screens[i].Bounds);
            gamma = new GammaControl(screens);
            lastEffective = -1;
            ApplyDim();
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            if (exiting || IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(new MethodInvoker(RecreateDisplays));
                else RecreateDisplays();
            }
            catch { }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (exiting || IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(delegate { HandlePowerMode(e.Mode); }));
                }
                else HandlePowerMode(e.Mode);
            }
            catch { }
        }

        private void HandlePowerMode(PowerModes mode)
        {
            if (mode == PowerModes.Suspend)
            {
                if (gamma != null) gamma.Restore();
                lastEffective = -1;
            }
            else if (mode == PowerModes.Resume) RecreateDisplays();
        }

        private void UpdateDraft(Action<Settings> change)
        {
            if (exiting) return;
            change(draft);
            emergencyOff = false;
            SyncControls();
            ApplyDim();
            if (draft.SameAs(confirmed))
            {
                StopConfirmation();
                return;
            }
            // Preview immediately but do not steal focus while the user is
            // dragging. Restart the 30-second safety window on each change.
            previewClock.Restart();
            popupDelay.Restart();
            DismissConfirmation();
        }

        private void EndSliderDrag()
        {
            if (!draggingSlider) return;
            draggingSlider = false;
            if (previewClock.IsRunning) popupDelay.Restart();
        }

        private void ChangeLanguage(string code)
        {
            if (!UiText.IsSupported(code) || confirmed.Language == code) return;
            // Language is safe to save immediately. Only confirmed dim settings
            // are persisted, even when a dimming preview is in progress.
            confirmed.Language = code;
            draft.Language = code;
            confirmed.Write(settingsFile);
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            string lang = draft.Language;
            Text = UiText.Get(lang, "SettingsTitle");
            languageLabel.Text = UiText.Get(lang, "Language");
            taglineLabel.Text = UiText.Get(lang, "Tagline");
            rangeLabel.Text = UiText.Get(lang, "Range");
            scheduleCheck.Text = UiText.Get(lang, "ScheduleCheck");
            fromLabel.Text = UiText.Get(lang, "From");
            toLabel.Text = UiText.Get(lang, "To");
            cursorCheck.Text = UiText.Get(lang, "CursorCheck");
            hideButton.Text = UiText.Get(lang, "Hide");
            exitButton.Text = UiText.Get(lang, "Exit");
            statusLabel.Text = UiText.Get(lang, firstShown && !registeredHotkeys[2] ? "HotkeyWarning" : "Hint");
            trayOpen.Text = UiText.Get(lang, "TrayOpen");
            trayDarker.Text = UiText.Get(lang, "Darker");
            trayLighter.Text = UiText.Get(lang, "Lighter");
            trayEmergency.Text = UiText.Get(lang, "Emergency");
            trayExit.Text = UiText.Get(lang, "Exit");
            if (confirmation != null && !confirmation.IsDisposed) confirmation.ApplyLanguage(lang);
            UpdateLabels(lastEffective < 0 ? 0 : lastEffective);
        }

        private void ShowConfirmation()
        {
            if (confirmation != null || !previewClock.IsRunning) return;
            confirmation = new ConfirmWindow(draft.Language);
            confirmation.Accepted += delegate { AcceptChanges(); };
            confirmation.Rejected += delegate { RejectChanges(); };
            confirmation.UpdateRemaining((int)Math.Ceiling(TimeoutSeconds - previewClock.Elapsed.TotalSeconds));
            confirmation.Show();
            confirmation.BringToFront();
            confirmation.Activate();
        }

        private void DismissConfirmation()
        {
            if (confirmation == null) return;
            ConfirmWindow old = confirmation;
            confirmation = null;
            old.Dismiss();
            old.Dispose();
        }

        private void SyncControls()
        {
            updatingControls = true;
            try
            {
                slider.Value = draft.Amount;
                scheduleCheck.Checked = draft.UseSchedule;
                cursorCheck.Checked = draft.TryDimCursor;
                pauseFrom.Value = DateTime.Today.AddMinutes(draft.PauseStart);
                pauseTo.Value = DateTime.Today.AddMinutes(draft.PauseEnd);
                pauseFrom.Enabled = pauseTo.Enabled = draft.UseSchedule;
            }
            finally { updatingControls = false; }
        }

        private void ChangeAmount(int delta)
        {
            int target = Math.Max(0, Math.Min(95, draft.Amount + delta));
            if (target == draft.Amount && draft.Enabled) return;
            UpdateDraft(delegate(Settings s) { s.Amount = target; s.Enabled = true; });
        }

        private void AcceptChanges()
        {
            if (!previewClock.IsRunning) return;
            if (previewClock.Elapsed >= TimeSpan.FromSeconds(TimeoutSeconds))
            {
                RejectChanges();
                return;
            }
            confirmed = draft.Copy();
            confirmed.Write(settingsFile);
            StopConfirmation();
            ApplyDim();
        }

        private void RejectChanges()
        {
            StopConfirmation();
            draft = confirmed.Copy();
            SyncControls();
            ApplyDim();
        }

        private void StopConfirmation()
        {
            previewClock.Reset();
            popupDelay.Reset();
            draggingSlider = false;
            DismissConfirmation();
        }

        private void EmergencyReset()
        {
            StopConfirmation();
            draft = confirmed.Copy();
            emergencyOff = true; // Session-only. No automatic reactivation.
            SyncControls();
            ApplyDim();
            ShowSettings();
        }

        private void ApplyDim()
        {
            bool paused = draft.IsPaused(DateTime.Now);
            int effective = draft.Enabled && !paused && !emergencyOff ? draft.Amount : 0;
            bool tryCursor = draft.TryDimCursor;
            // Reapply gamma only when needed, not on each timer tick.
            if (gamma != null && (effective != lastEffective || tryCursor != lastTryCursor))
            {
                double[] factors = gamma.Apply(effective, tryCursor);
                for (int i = 0; i < overlays.Length; i++)
                {
                    DimOverlay overlay = overlays[i];
                    double desired = 1.0 - effective / 100.0;
                    double overlayOpacity = 1.0 - desired / factors[i];
                    overlayOpacity = Math.Max(0.0, Math.Min(0.95, overlayOpacity));
                    if (overlayOpacity < 0.005) overlay.Hide();
                    else
                    {
                        overlay.Opacity = overlayOpacity;
                        if (!overlay.Visible) overlay.Show();
                    }
                }
                lastEffective = effective;
                lastTryCursor = tryCursor;
                if (confirmation != null && !confirmation.IsDisposed) confirmation.BringToFront();
            }
            UpdateLabels(effective);
        }

        private void UpdateLabels(int effective)
        {
            string lang = draft.Language;
            if (amountLabel != null) amountLabel.Text = draft.Amount.ToString() + "%";
            if (toggleButton != null) toggleButton.Text = UiText.Get(lang, draft.Enabled ? "Off" : "On");
            if (trayToggle != null) trayToggle.Text = UiText.Get(lang, draft.Enabled ? "Off" : "On");
            if (scheduleLabel != null)
            {
                if (!draft.UseSchedule) scheduleLabel.Text = UiText.Get(lang, "ScheduleDisabled");
                else scheduleLabel.Text = UiText.Get(lang, "Pause") + PrettyTime(draft.PauseStart) + "–" +
                    PrettyTime(draft.PauseEnd) + UiText.Get(lang, draft.IsPaused(DateTime.Now) ? "PausedNow" : "ActiveNow") +
                    (draft.PauseStart == draft.PauseEnd ? UiText.Get(lang, "PauseAllDay") : "");
            }
            if (tray != null) tray.Text = "NightDim — " +
                (emergencyOff ? UiText.Get(lang, "EmergencyOff") :
                (effective == 0 ? UiText.Get(lang, "NoDimming") : effective + "%"));
        }

        private void OnClockTick(object sender, EventArgs e)
        {
            if (exiting) return;
            if (stopSignal.WaitOne(0)) { Quit(); return; }
            if (previewClock.IsRunning)
            {
                double seconds = TimeoutSeconds - previewClock.Elapsed.TotalSeconds;
                if (seconds <= 0) { RejectChanges(); return; }
                if (confirmation == null && !draggingSlider && popupDelay.IsRunning &&
                    popupDelay.ElapsedMilliseconds >= ConfirmDelayMilliseconds)
                    ShowConfirmation();
                if (confirmation != null) confirmation.UpdateRemaining((int)Math.Ceiling(seconds));
            }
            // Time schedule changes take effect automatically, including when
            // the application is hidden in the system tray.
            if (lastEffective != (draft.Enabled && !draft.IsPaused(DateTime.Now) && !emergencyOff ? draft.Amount : 0))
                ApplyDim();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && slider != null)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyDarker) ChangeAmount(5);
                else if (id == HotkeyLighter) ChangeAmount(-5);
                else if (id == HotkeyEmergency) EmergencyReset();
            }
            base.WndProc(ref m);
        }

        private void ShowSettings()
        {
            if (IsDisposed) return;
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            if (confirmation != null) { confirmation.BringToFront(); confirmation.Activate(); }
        }

        private void Quit()
        {
            exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clock.Stop();
            StopConfirmation();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            if (gamma != null) { gamma.Dispose(); gamma = null; }
            foreach (DimOverlay overlay in overlays)
            {
                try { overlay.Close(); overlay.Dispose(); } catch { }
            }
            for (int i = 0; i < registeredHotkeys.Length; i++)
                if (registeredHotkeys[i]) Native.UnregisterHotKey(Handle, 101 + i);
            tray.Visible = false;
            tray.Dispose();
            clock.Dispose();
            if (appIcon != null) appIcon.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal static class Program
    {
        private const string MutexName = "Local\\NightDim_4fa715de-f89d-4c3c-a079-1fbef6317cf4";
        private const string ExitEventName = "Local\\NightDim_Stop_4fa715de-f89d-4c3c-a079-1fbef6317cf4";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--stop", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (EventWaitHandle signal = EventWaitHandle.OpenExisting(ExitEventName)) signal.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { /* Not running. */ }
                return;
            }

            bool first;
            using (Mutex mutex = new Mutex(true, MutexName, out first))
            {
                if (!first)
                {
                    string language = Settings.Read(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "NightDim", "settings.txt")).Language;
                    MessageBox.Show(UiText.Get(language, "AlreadyRunning"),
                        "NightDim", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (EventWaitHandle stopSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName))
                {
                    try
                    {
                        try
                        {
                            if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware();
                        }
                        catch (EntryPointNotFoundException)
                        {
                            try { Native.SetProcessDPIAware(); } catch { }
                        }
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new ControlWindow(stopSignal));
                    }
                    catch (Exception ex)
                    {
                        string language = Settings.Read(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "NightDim", "settings.txt")).Language;
                        MessageBox.Show(UiText.Get(language, "Error") + ex.Message,
                            "NightDim", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
