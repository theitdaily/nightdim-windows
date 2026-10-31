using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Windows.Forms;

namespace NightDim
{
    // Offline installer: a single download installs both NightDim.exe and a
    // local uninstaller registered in Windows Installed apps.
    // A familiar, self-contained Windows setup wizard; no extra setup tools required.
    internal sealed class SetupWindow : Form
    {
        private readonly Panel header;
        private readonly Panel content;
        private readonly Panel footer;
        private readonly Label pageTitle;
        private readonly Label pageSubtitle;
        private readonly ComboBox language;
        private readonly Button backButton;
        private readonly Button nextButton;
        private readonly Button cancelButton;
        private string lang;
        private int page;
        private bool accepted;
        private bool launchAtLogin;
        private bool createShortcut = true;
        private bool launchAfterInstall = true;
        private readonly string previousLocation;
        private readonly Version previousVersion;
        private readonly Version setupVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private bool busy;
        private string destination;
        private string warnings = "";
        private string installedExe;

        internal SetupWindow()
        {
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang != "ru" && lang != "sr") lang = "en";
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (string.IsNullOrEmpty(programFiles))
                programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            destination = Path.Combine(programFiles, "TheITDaily", "NightDim");
            previousLocation = WindowsIntegration.GetInstalledLocation();
            if (!string.IsNullOrEmpty(previousLocation) && Directory.Exists(previousLocation))
            {
                destination = previousLocation;
                string oldExe = Path.Combine(previousLocation, "NightDim.exe");
                Version found;
                if (File.Exists(oldExe) && Version.TryParse(
                    FileVersionInfo.GetVersionInfo(oldExe).FileVersion, out found))
                    previousVersion = found;
                launchAtLogin = WindowsIntegration.IsStartupEnabled(oldExe);
                createShortcut = WindowsIntegration.IsStartMenuEnabled(oldExe);
            }

            Text = "NightDim Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 455);
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            header = new Panel { Location = new Point(0, 0), Size = new Size(620, 91), BackColor = Color.White };
            pageTitle = new Label { Location = new Point(23, 14), Size = new Size(430, 32),
                Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            pageSubtitle = new Label { Location = new Point(25, 50), Size = new Size(570, 35) };
            header.Controls.Add(pageTitle);
            header.Controls.Add(pageSubtitle);
            language = new ComboBox { Location = new Point(459, 19), Width = 144,
                DropDownStyle = ComboBoxStyle.DropDownList };
            language.Items.AddRange(new object[] { "English", "Русский", "Српски (ћирилица)" });
            language.SelectedIndex = lang == "ru" ? 1 : lang == "sr" ? 2 : 0;
            language.SelectedIndexChanged += delegate
            {
                lang = language.SelectedIndex == 1 ? "ru" : language.SelectedIndex == 2 ? "sr" : "en";
                ShowPage();
            };
            header.Controls.Add(language);
            Controls.Add(header);

            content = new Panel { Location = new Point(0, 92), Size = new Size(620, 292),
                BackColor = SystemColors.Control };
            Controls.Add(content);

            footer = new Panel { Location = new Point(0, 387), Size = new Size(620, 68),
                BackColor = Color.FromArgb(245, 245, 245) };
            footer.Controls.Add(new Label { Location = new Point(0, 0), Size = new Size(620, 1),
                BackColor = Color.FromArgb(214, 214, 214) });
            backButton = new Button { Location = new Point(285, 21), Size = new Size(99, 29) };
            nextButton = new Button { Location = new Point(393, 21), Size = new Size(100, 29) };
            cancelButton = new Button { Location = new Point(502, 21), Size = new Size(99, 29) };
            backButton.Click += delegate { if (page > 0 && !busy) { page--; ShowPage(); } };
            nextButton.Click += Advance;
            cancelButton.Click += delegate { if (!busy) Close(); };
            footer.Controls.Add(backButton);
            footer.Controls.Add(nextButton);
            footer.Controls.Add(cancelButton);
            Controls.Add(footer);
            AcceptButton = nextButton;
            CancelButton = cancelButton;
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
            ShowPage();
        }

        private string Tr(string en, string ru, string sr)
        {
            return lang == "ru" ? ru : lang == "sr" ? sr : en;
        }

        private Label AddLabel(string text, int x, int y, int width, int height)
        {
            Label label = new Label { Text = text, Location = new Point(x, y),
                Size = new Size(width, height) };
            content.Controls.Add(label);
            return label;
        }

        private void ShowPage()
        {
            while (content.Controls.Count > 0) content.Controls[0].Dispose();
            Text = Tr("NightDim Setup", "Установка NightDim", "Инсталација NightDim-а");
            backButton.Text = Tr("< Back", "< Назад", "< Назад");
            cancelButton.Text = Tr("Cancel", "Отмена", "Откажи");
            backButton.Enabled = page > 0 && page < 5 && !busy;
            nextButton.Enabled = !busy && (page != 1 || accepted);
            nextButton.Text = page == 4 ?
                previousVersion == null ? Tr("Install", "Установить", "Инсталирај") :
                setupVersion.CompareTo(previousVersion) > 0 ? Tr("Update", "Обновить", "Ажурирај") :
                setupVersion.CompareTo(previousVersion) == 0 ?
                    Tr("Reinstall", "Переустановить", "Поново инсталирај") :
                    Tr("Older version", "Старая версия", "Старија верзија") :
                page == 6 ? Tr("Finish", "Готово", "Заврши") :
                Tr("Next >", "Далее >", "Даље >");
            cancelButton.Enabled = !busy && page != 6;
            language.Enabled = !busy;

            if (page == 0)
            {
                pageTitle.Text = Tr("Welcome to NightDim Setup", "Добро пожаловать", "Добро дошли");
                pageSubtitle.Text = Tr("This wizard will install NightDim on your PC.",
                    "Мастер поможет установить NightDim на компьютер.",
                    "Овај чаробњак ће инсталирати NightDim на рачунар.");
                if (previousVersion != null)
                    AddLabel(Tr("Existing installation found: ", "Найдена установленная версия: ",
                        "Пронађена је инсталирана верзија: ") + previousVersion.ToString(3) +
                        "  →  " + setupVersion.ToString(3), 32, 8, 545, 26);
                AddLabel(Tr("NightDim dims your display below the usual Windows minimum.\n\n" +
                    "You will be able to choose the install folder and Windows shortcuts.\n\n" +
                    "Click Next to continue.",
                    "NightDim затемняет экран сильнее, чем позволяют настройки Windows.\n\n" +
                    "Вы сможете выбрать папку установки и настройки ярлыков.\n\n" +
                    "Нажмите «Далее», чтобы продолжить.",
                    "NightDim затамњује екран испод Windows минималне осветљености.\n\n" +
                    "Можете изабрати фасциклу и пречице.\n\n" +
                    "Кликните на „Даље” за наставак."), 32, 42, 544, 186);
            }
            else if (page == 1)
            {
                pageTitle.Text = Tr("License agreement", "Лицензионное соглашение", "Уговор о лиценци");
                pageSubtitle.Text = Tr("Read and accept the terms before installing.",
                    "Прочитайте и примите условия перед установкой.",
                    "Прочитајте и прихватите услове пре инсталације.");
                RichTextBox terms = new RichTextBox { Location = new Point(24, 17), Size = new Size(572, 202),
                    ReadOnly = true, DetectUrls = false, BackColor = Color.White, TabStop = true,
                    Text = Tr("License and liability information (official English text below).\n\n",
                        "Соглашение: программа предоставляется «как есть», без гарантий. " +
                        "Ответственность ограничивается в пределах, допускаемых законом. " +
                        "Лицензия MIT разрешает свободное использование и изменение. " +
                        "Полный текст соглашения на английском языке ниже.\n\n",
                        "Услови: програм се испоручује „какав јесте”, без гаранција. " +
                        "Одговорност је ограничена у мери дозвољеној законом. " +
                        "MIT лиценца дозвољава слободно коришћење и измене. " +
                        "Пун текст уговора на енглеском је испод.\n\n") +
                        SetupProgram.LoadResource("NightDim.EULA.txt") };
                content.Controls.Add(terms);
                CheckBox checkbox = new CheckBox { Location = new Point(25, 234), Size = new Size(568, 28),
                    Checked = accepted,
                    Text = Tr("I have read and accept the license agreement",
                        "Я прочитал(а) и принимаю условия соглашения",
                        "Прочитао/ла сам и прихватам услове лиценце") };
                checkbox.CheckedChanged += delegate
                {
                    accepted = checkbox.Checked;
                    nextButton.Enabled = accepted;
                };
                content.Controls.Add(checkbox);
            }
            else if (page == 2)
            {
                pageTitle.Text = Tr("Select destination", "Выберите папку установки", "Изаберите фасциклу");
                pageSubtitle.Text = Tr("Choose where NightDim will be installed.",
                    "Укажите, куда установить программу.", "Изаберите где ће NightDim бити инсталиран.");
                AddLabel(Tr("Installation folder:", "Папка установки:", "Инсталациона фасцикла:"),
                    25, 39, 560, 25);
                TextBox path = new TextBox { Location = new Point(25, 74), Width = 453, Text = destination };
                path.TextChanged += delegate { destination = path.Text; };
                content.Controls.Add(path);
                Button browse = new Button { Location = new Point(488, 72), Size = new Size(107, 28),
                    Text = Tr("Browse…", "Обзор…", "Изабери…") };
                browse.Click += delegate
                {
                    using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                    {
                        dlg.Description = Text;
                        dlg.SelectedPath = Directory.Exists(path.Text) ? path.Text :
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        if (dlg.ShowDialog(this) == DialogResult.OK) path.Text = dlg.SelectedPath;
                    }
                };
                content.Controls.Add(browse);
                AddLabel(Tr("Windows may request administrator permission for protected folders.\n" +
                    "The shortcut and startup settings apply to your Windows account.",
                    "Windows может запросить права администратора для защищённых папок.\n" +
                    "Автозапуск и ярлык относятся к вашей учётной записи Windows.",
                    "Windows може тражити администраторску дозволу за заштићене фасцикле.\n" +
                    "Пречица и аутоматско покретање важе за ваш Windows налог."),
                    25, 128, 565, 70);
            }
            else if (page == 3)
            {
                pageTitle.Text = Tr("Additional tasks", "Дополнительные задачи", "Додатне опције");
                pageSubtitle.Text = Tr("Choose which shortcuts to create.",
                    "Выберите дополнительные действия.", "Изаберите додатне опције.");
                CheckBox menu = new CheckBox { Location = new Point(30, 40), Size = new Size(554, 31),
                    Checked = createShortcut,
                    Text = Tr("Create a Start menu shortcut", "Создать ярлык в меню «Пуск»",
                        "Направи пречицу у Старт менију") };
                menu.CheckedChanged += delegate { createShortcut = menu.Checked; };
                content.Controls.Add(menu);
                CheckBox auto = new CheckBox { Location = new Point(30, 83), Size = new Size(554, 31),
                    Checked = launchAtLogin,
                    Text = Tr("Start NightDim when I sign in to Windows",
                        "Запускать NightDim при входе в Windows",
                        "Покрени NightDim приликом пријаве на Windows") };
                auto.CheckedChanged += delegate { launchAtLogin = auto.Checked; };
                content.Controls.Add(auto);
                AddLabel(Tr("You can change both options later in NightDim settings.",
                    "Обе настройки можно изменить позднее в NightDim.",
                    "Обе опције можете касније променити у подешавањима NightDim-а."),
                    30, 145, 550, 50);
            }
            else if (page == 4)
            {
                pageTitle.Text = previousVersion == null ?
                    Tr("Ready to install", "Всё готово к установке", "Спремно за инсталацију") :
                    setupVersion.CompareTo(previousVersion) > 0 ?
                    Tr("Ready to update", "Всё готово к обновлению", "Спремно за ажурирање") :
                    setupVersion.CompareTo(previousVersion) == 0 ?
                    Tr("Ready to reinstall", "Всё готово к переустановке", "Спремно за поновну инсталацију") :
                    Tr("Newer version is installed", "Установлена более новая версия", "Инсталирана је новија верзија");
                pageSubtitle.Text = Tr("Review your choices and continue.",
                    "Проверьте параметры и продолжите.",
                    "Проверите избор и наставите.");
                AddLabel(Tr("Destination:", "Папка:", "Фасцикла:") + "\n" + destination + "\n\n" +
                    Tr("Start menu shortcut: ", "Ярлык в меню «Пуск»: ", "Пречица у Старт менију: ") +
                    (createShortcut ? Tr("Yes", "Да", "Да") : Tr("No", "Нет", "Не")) + "\n" +
                    Tr("Launch with Windows: ", "Автозапуск с Windows: ", "Покретање уз Windows: ") +
                    (launchAtLogin ? Tr("Yes", "Да", "Да") : Tr("No", "Нет", "Не")),
                    26, 29, 560, 208);
            }
            else if (page == 5)
            {
                pageTitle.Text = Tr("Installing NightDim", "Установка NightDim", "Инсталација NightDim-а");
                pageSubtitle.Text = Tr("Please do not close the installer.",
                    "Не закрывайте установщик.", "Не затварајте инсталатер.");
                AddLabel(Tr("Copying the application and configuring Windows integration…",
                    "Копирование программы и настройка интеграции с Windows…",
                    "Копирање програма и подешавање Windows интеграције…"),
                    28, 64, 560, 30);
                ProgressBar progress = new ProgressBar { Location = new Point(28, 113),
                    Size = new Size(560, 24), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
                content.Controls.Add(progress);
            }
            else if (page == 6)
            {
                pageTitle.Text = Tr("Installation complete", "Установка завершена", "Инсталација је завршена");
                pageSubtitle.Text = Tr("NightDim is ready to use.", "NightDim готов к работе.",
                    "NightDim је спреман за коришћење.");
                AddLabel(Tr("NightDim is ready in:\n",
                    "NightDim готов к работе в папке:\n", "NightDim је спреман у фасцикли:\n") +
                    Path.GetDirectoryName(installedExe) +
                    (warnings.Length == 0 ? "" : "\n\n" + Tr("Some optional tasks could not be completed:\n",
                        "Не удалось выполнить некоторые дополнительные действия:\n",
                        "Неке додатне опције нису примењене:\n") + warnings),
                    27, 22, 560, 188);
                CheckBox launch = new CheckBox { Location = new Point(27, 222), Size = new Size(563, 32),
                    Checked = launchAfterInstall,
                    Text = Tr("Launch NightDim now", "Запустить NightDim сейчас",
                        "Покрени NightDim сада") };
                launch.CheckedChanged += delegate { launchAfterInstall = launch.Checked; };
                content.Controls.Add(launch);
            }
        }

        private void Advance(object sender, EventArgs e)
        {
            if (busy) return;
            if (page == 1 && !accepted) return;
            if (page == 2)
            {
                try { destination = ValidateDestination(destination); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Tr("Choose a valid installation folder.",
                        "Выберите корректную папку установки.",
                        "Изаберите исправну инсталациону фасциклу.") + "\n" + ex.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            if (page == 4)
            {
                if (previousVersion != null && previousVersion.CompareTo(setupVersion) > 0)
                {
                    MessageBox.Show(this, Tr("A newer version is already installed. Download the latest installer.",
                        "Уже установлена более новая версия. Скачайте актуальный установщик.",
                        "Већ је инсталирана новија верзија. Преузмите најновији инсталатер."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                BeginInstallation(); return;
            }
            if (page == 6)
            {
                string executable = installedExe;
                bool launch = launchAfterInstall;
                Close();
                if (launch)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(executable)
                        {
                            WorkingDirectory = Path.GetDirectoryName(executable),
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Tr("NightDim was installed but could not be started:\n",
                            "NightDim установлен, но не удалось запустить программу:\n",
                            "NightDim је инсталиран, али покретање није успело:\n") + ex.Message,
                            "NightDim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                return;
            }
            if (page < 4) { page++; ShowPage(); }
        }

        private static string ValidateDestination(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !Path.IsPathRooted(input.Trim()) || input.IndexOf('"') >= 0)
                throw new ArgumentException("Choose a valid absolute installation folder.");
            string full = Path.GetFullPath(input.Trim()).TrimEnd('\\');
            if (Path.GetPathRoot(full).TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a folder, not the drive root.");
            return full;
        }

        private void BeginInstallation()
        {
            busy = true;
            page = 5;
            ShowPage();
            // The worker must be STA for the COM-based Windows shortcut helper.
            System.Threading.Thread worker = new System.Threading.Thread(delegate()
            {
                Exception failure = null;
                string messages = "";
                try { InstallCore(out messages); }
                catch (Exception ex) { failure = ex; }
                try { BeginInvoke(new Action(delegate { EndInstallation(failure, messages); })); }
                catch (InvalidOperationException) { }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(System.Threading.ApartmentState.STA);
            worker.Start();
        }

        private void EndInstallation(Exception error, string messages)
        {
            busy = false;
            if (error != null)
            {
                page = 4;
                ShowPage();
                if (!(error is Win32Exception && ((Win32Exception)error).NativeErrorCode == 1223))
                    MessageBox.Show(this, Tr("Installation failed. Close NightDim if it is running.\n",
                        "Не удалось установить NightDim. Закройте работающую программу.\n",
                        "Инсталација није успела. Затворите NightDim ако је покренут.\n") +
                        error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            warnings = messages;
            page = 6;
            ShowPage();
        }

        private void InstallCore(out string messages)
        {
            string previous = WindowsIntegration.GetInstalledLocation();
            if (!string.IsNullOrEmpty(previous) &&
                !string.Equals(Path.GetFullPath(previous).TrimEnd('\\'), destination.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase) && Directory.Exists(previous))
                throw new IOException("Please uninstall the previous NightDim installation first: " + previous);
            // Reinstall and upgrade in-place. Never downgrade an installed build.
            if (!string.IsNullOrEmpty(previous) && Directory.Exists(previous))
            {
                string oldExe = Path.Combine(previous, "NightDim.exe");
                if (File.Exists(oldExe))
                {
                    Version old;
                    if (Version.TryParse(FileVersionInfo.GetVersionInfo(oldExe).FileVersion, out old) &&
                        old.CompareTo(setupVersion) > 0)
                        throw new IOException("A newer NightDim version is already installed (" +
                            old.ToString(3) + "). Download a newer installer.");
                    StopRunningNightDim(oldExe);
                }
            }
            InstallPayload(destination);
            installedExe = Path.Combine(destination, "NightDim.exe");
            WindowsIntegration.RegisterUninstall(installedExe, Path.Combine(destination, "uninstall.exe"));
            // These entries belong to the original Windows user, not the elevated copy helper.
            messages = "";
            try { WindowsIntegration.SetStartup(installedExe, launchAtLogin); }
            catch (Exception ex) { messages += ex.Message + Environment.NewLine; }
            try { WindowsIntegration.SetStartMenu(installedExe, createShortcut); }
            catch (Exception ex) { messages += ex.Message + Environment.NewLine; }
        }

        private static void StopRunningNightDim(string installedExe)
        {
            // Signal the old application to close normally so it can restore the
            // gamma ramp and release its EXE before an in-place replacement.
            bool running = false;
            foreach (Process process in Process.GetProcessesByName("NightDim"))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule.FileName, installedExe,
                            StringComparison.OrdinalIgnoreCase)) running = true;
                    }
                    catch (Win32Exception) { /* A process in another session. */ }
                    catch (InvalidOperationException) { /* Already exited. */ }
                }
            }
            if (!running) return;
            using (Process stop = Process.Start(new ProcessStartInfo(installedExe, "--stop")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }))
                if (stop != null) stop.WaitForExit(5000);

            // Fail rather than force-killing NightDim or overwriting a running
            // EXE. The app's graceful exit restores the display calibration.
            foreach (Process process in Process.GetProcessesByName("NightDim"))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule.FileName, installedExe,
                            StringComparison.OrdinalIgnoreCase) && !process.WaitForExit(15000))
                            throw new IOException("Close NightDim before updating it.");
                    }
                    catch (Win32Exception) { /* File copy will report access denial. */ }
                    catch (InvalidOperationException) { /* Already exited. */ }
                }
            }
        }

        private void InstallPayload(string target)
        {
            if (CanWrite(target)) { SetupProgram.CopyPayload(target); return; }
            string log = Path.Combine(Path.GetTempPath(), "NightDimSetup-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(Application.ExecutablePath,
                        "--copy " + Quote(target) + " " + Quote(log));
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new IOException(File.Exists(log) ? File.ReadAllText(log) :
                            "Elevated installation failed (exit code " + process.ExitCode + ").");
                }
            }
            finally { try { File.Delete(log); } catch { } }
        }

        private static string Quote(string path) { return "\"" + path + "\""; }

        private static bool CanWrite(string target)
        {
            string check = Path.Combine(target, ".nightdim-write-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(target);
                using (File.Create(check)) { }
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (SecurityException) { return false; }
            catch (IOException) { return false; }
            finally { try { if (File.Exists(check)) File.Delete(check); } catch { } }
        }
    }

    // The registered uninstaller is a copy of NightDimSetup.exe. It runs from
    // the installed directory, but performs removal from a temporary copy so
    // Windows can delete the installed uninstaller itself.
    internal static class UninstallProgram
    {
        private static string Tr(string en, string ru, string sr)
        {
            string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return language == "ru" ? ru : language == "sr" ? sr : en;
        }

        internal static int Begin()
        {
            string folder = Path.GetDirectoryName(Application.ExecutablePath);
            if (!WindowsIntegration.IsRegisteredLocation(folder))
            {
                Error(Tr("This copy of NightDim is not registered as an installed app.",
                    "Эта копия NightDim не зарегистрирована как установленная программа.",
                    "Ова копија NightDim-а није регистрована као инсталиран програм."));
                return 1;
            }

            if (MessageBox.Show(Tr("Uninstall NightDim? Your saved preferences will be kept.",
                "Удалить NightDim? Сохранённые настройки останутся на компьютере.",
                "Деинсталирати NightDim? Сачувана подешавања ће остати на рачунару."),
                "NightDim", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 0;

            string tempDir = Path.Combine(Path.GetTempPath(), "NightDim-Uninstall-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string tempExe = Path.Combine(tempDir, "uninstall.exe");
                File.Copy(Application.ExecutablePath, tempExe);
                Process.Start(new ProcessStartInfo(tempExe, "--remove " + Quote(folder) + " " + Quote(tempDir))
                {
                    UseShellExecute = false,
                    WorkingDirectory = tempDir
                });
                return 0;
            }
            catch (Exception ex)
            {
                Error(ex.Message);
                try { Directory.Delete(tempDir, true); } catch { }
                return 1;
            }
        }

        internal static int Remove(string folder, string tempDir)
        {
            try
            {
                if (!WindowsIntegration.IsRegisteredLocation(folder))
                    throw new IOException("NightDim installation location does not match Windows registration.");
                if (!Directory.Exists(folder))
                    throw new DirectoryNotFoundException("NightDim installation folder was not found.");

                string exe = Path.Combine(folder, "NightDim.exe");
                try
                {
                    if (File.Exists(exe))
                    {
                        Process.Start(new ProcessStartInfo(exe, "--stop")
                        {
                            UseShellExecute = false, CreateNoWindow = true
                        });
                        foreach (Process running in Process.GetProcessesByName("NightDim"))
                        {
                            using (running)
                            {
                                string runningPath;
                                try { runningPath = running.MainModule.FileName; }
                                catch { continue; /* An unrelated process or a protected session. */ }
                                if (string.Equals(runningPath, exe, StringComparison.OrdinalIgnoreCase) &&
                                    !running.WaitForExit(5000))
                                    throw new IOException("Close NightDim before uninstalling it.");
                            }
                        }
                    }
                }
                catch (Win32Exception) { /* File deletion will report any remaining lock. */ }

                if (CanDelete(folder)) DeleteApplicationFiles(folder);
                else ElevatedDelete(folder);

                // Do not remove a shortcut or a startup entry belonging to a
                // different NightDim installation or a user-customized target.
                if (WindowsIntegration.IsStartupEnabled(exe)) WindowsIntegration.SetStartup(exe, false);
                if (WindowsIntegration.IsStartMenuEnabled(exe)) WindowsIntegration.SetStartMenu(exe, false);
                WindowsIntegration.UnregisterUninstall();

                MessageBox.Show(Tr("NightDim has been uninstalled. Saved preferences were kept.",
                    "NightDim удалён. Сохранённые настройки оставлены.",
                    "NightDim је деинсталиран. Сачувана подешавања су задржана."),
                    "NightDim", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223) Error(ex.Message);
                return 1;
            }
            catch (Exception ex) { Error(ex.Message); return 1; }
            finally { CleanupTemporaryUninstaller(tempDir); }
        }

        // This operation also runs in the elevated helper. It only deletes
        // NightDim-owned filenames, never arbitrary files in the chosen folder.
        internal static void DeleteApplicationFiles(string folder)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            foreach (string name in new[] { "NightDim.exe", "uninstall.exe", "NightDimUninstall.exe", "LICENSE.txt", "EULA.txt" })
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path)) File.Delete(path);
            }
            try { Directory.Delete(folder, false); }
            catch (IOException) { /* Leave a nonempty folder with the user's files. */ }
        }

        private static bool CanDelete(string folder)
        {
            string check = Path.Combine(folder, ".nightdim-delete-" + Guid.NewGuid().ToString("N"));
            try { using (File.Create(check)) { } return true; }
            catch (UnauthorizedAccessException) { return false; }
            catch (SecurityException) { return false; }
            catch (IOException) { return false; }
            finally { try { File.Delete(check); } catch { } }
        }

        private static void ElevatedDelete(string folder)
        {
            string log = Path.Combine(Path.GetTempPath(), "NightDimRemove-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(Application.ExecutablePath,
                        "--delete " + Quote(folder) + " " + Quote(log));
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new IOException(File.Exists(log) ? File.ReadAllText(log) :
                            "The elevated uninstall failed (exit code " + process.ExitCode + ").");
                }
            }
            finally { try { File.Delete(log); } catch { } }
        }

        private static void CleanupTemporaryUninstaller(string tempDir)
        {
            // Allow this helper process to exit, then delete its temporary copy.
            // Only clean up its own GUID-named temporary folder.
            if (!string.Equals(Path.GetDirectoryName(Application.ExecutablePath), tempDir,
                StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                string exe = Application.ExecutablePath;
                string command = "ping -n 4 127.0.0.1 >nul & del /f /q " + Quote(exe) +
                    " & rd " + Quote(tempDir);
                Process.Start(new ProcessStartInfo("cmd.exe", "/d /c \"" + command + "\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(), WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { /* Failed cleanup only leaves a temporary copy. */ }
        }

        private static string Quote(string text) { return "\"" + text + "\""; }

        private static void Error(string message)
        {
            MessageBox.Show(Tr("Uninstallation failed:\n", "Ошибка удаления:\n",
                "Деинсталација није успела:\n") + message,
                "NightDim", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0 && string.Equals(Path.GetFileName(Application.ExecutablePath),
                "uninstall.exe", StringComparison.OrdinalIgnoreCase))
                return UninstallProgram.Begin();
            if (args.Length > 0 && args[0] == "--uninstall")
            {
                if (args.Length != 1) return 2;
                return UninstallProgram.Begin();
            }
            if (args.Length > 0 && args[0] == "--remove")
            {
                if (args.Length != 3) return 2;
                return UninstallProgram.Remove(args[1], args[2]);
            }
            if (args.Length > 0 && args[0] == "--delete")
            {
                if (args.Length != 3) return 2;
                try { UninstallProgram.DeleteApplicationFiles(args[1]); return 0; }
                catch (Exception ex)
                {
                    try { File.WriteAllText(args[2], ex.Message); } catch { }
                    return 1;
                }
            }
            if (args.Length > 0 && args[0] == "--copy")
            {
                if (args.Length != 3) return 2;
                try { CopyPayload(args[1]); return 0; }
                catch (Exception ex)
                {
                    try { File.WriteAllText(args[2], ex.Message); } catch { }
                    return 1;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (SetupWindow window = new SetupWindow()) Application.Run(window);
            return 0;
        }

        internal static void CopyPayload(string destination)
        {
            Directory.CreateDirectory(destination);
            string output = Path.Combine(destination, "NightDim.exe");
            string staging = Path.Combine(destination, "NightDim-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("NightDim.Payload.exe"))
                {
                    if (source == null) throw new InvalidOperationException("The embedded NightDim.exe was not found.");
                    using (FileStream target = File.Create(staging)) source.CopyTo(target);
                }
                if (File.Exists(output)) File.Replace(staging, output, null);
                else File.Move(staging, output);
            }
            finally { try { File.Delete(staging); } catch { } }

            // A local copy of this setup binary also serves as Uninstall.exe.
            // No extra compiler tool or payload is required.
            string uninstall = Path.Combine(destination, "uninstall.exe");
            string setupStaging = Path.Combine(destination,
                "NightDim-Uninstall-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(Application.ExecutablePath, setupStaging);
                if (File.Exists(uninstall)) File.Replace(setupStaging, uninstall, null);
                else File.Move(setupStaging, uninstall);
            }
            finally { try { File.Delete(setupStaging); } catch { } }

            // Keep the MIT notice and the setup terms available after installation.
            WriteEmbeddedText("NightDim.LICENSE.txt", Path.Combine(destination, "LICENSE.txt"));
            WriteEmbeddedText("NightDim.EULA.txt", Path.Combine(destination, "EULA.txt"));
            // Previous versions used the longer filename; avoid leaving a duplicate.
            string legacy = Path.Combine(destination, "NightDimUninstall.exe");
            try { if (File.Exists(legacy)) File.Delete(legacy); }
            catch (IOException) { /* An older uninstaller may still be running. */ }
        }

        internal static string LoadResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing embedded installer document: " + name);
                using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }

        private static void WriteEmbeddedText(string name, string path)
        {
            File.WriteAllText(path, LoadResource(name), new System.Text.UTF8Encoding(false));
        }
    }
}
