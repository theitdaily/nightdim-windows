using System;
using System.Collections.Generic;
using System.Globalization;

namespace NightDim
{
    // Interface strings live in one place; no external translation files are needed.
    internal static class UiText
    {
        // Order: English, Russian, Serbian (Cyrillic).
        private static readonly Dictionary<string, string[]> strings = new Dictionary<string, string[]>
        {
            { "SettingsTitle", new[] { "NightDim — settings", "NightDim — настройки", "NightDim — подешавања" } },
            { "Language", new[] { "Language", "Язык", "Језик" } },
            { "Tagline", new[] { "Dimming below Windows' minimum brightness", "Затемнение ниже минимума Windows", "Затамњивање испод минимума Windows-а" } },
            { "Range", new[] { "0% — no dimming                            95% — very dark", "0% — без затемнения                       95% — очень темно", "0% — без затамњивања                    95% — веома тамно" } },
            { "ScheduleCheck", new[] { "Pause dimming on a schedule", "Не затемнять по расписанию", "Не затамњуј по распореду" } },
            { "From", new[] { "From", "С", "Од" } },
            { "To", new[] { "to", "до", "до" } },
            { "CursorCheck", new[] { "Try to dim the cursor (experimental)", "Пробовать затемнять курсор (экспериментально)", "Покушај затамњивања курсора (експериментално)" } },
            { "Off", new[] { "Turn off", "Выключить", "Искључи" } },
            { "On", new[] { "Turn on", "Включить", "Укључи" } },
            { "Hide", new[] { "Hide to tray", "Скрыть в трей", "Сакриј у палету" } },
            { "Exit", new[] { "Exit", "Выход", "Излаз" } },
            { "Hint", new[] { "Ctrl+Alt+↓ / ↑ — darker / lighter    •    Ctrl+Alt+F12 — reset\nConfirm each change within 30 seconds.",
                "Ctrl+Alt+↓ / ↑ — темнее / светлее    •    Ctrl+Alt+F12 — сброс\nПодтвердите каждое изменение за 30 секунд.",
                "Ctrl+Alt+↓ / ↑ — тамније / светлије    •    Ctrl+Alt+F12 — ресет\nПотврдите сваку промену у року од 30 секунди." } },
            { "HotkeyWarning", new[] { "WARNING: Emergency hotkey is already in use!\nUse the tray menu or NightDim.exe --stop.",
                "ВНИМАНИЕ: аварийная клавиша занята другой программой!\nИспользуйте меню трея или NightDim.exe --stop.",
                "УПОЗОРЕЊЕ: пречица за хитно искључивање је заузета!\nКористите системску палету или NightDim.exe --stop." } },
            { "TrayOpen", new[] { "Open settings", "Открыть настройки", "Отвори подешавања" } },
            { "Darker", new[] { "Darker by 5%", "Темнее на 5%", "Затамни за 5%" } },
            { "Lighter", new[] { "Lighter by 5%", "Светлее на 5%", "Посветли за 5%" } },
            { "Emergency", new[] { "Emergency reset", "Аварийно убрать затемнение", "Хитно искључи затамњивање" } },
            { "ConfirmTitle", new[] { "NightDim — confirm change", "NightDim — подтвердите изменение", "NightDim — потврдите промену" } },
            { "ConfirmHeading", new[] { "Keep the new settings?", "Оставить новые настройки?", "Задржати нова подешавања?" } },
            { "Accept", new[] { "Keep changes", "Принять", "Прихвати" } },
            { "Revert", new[] { "Revert (Esc)", "Вернуть (Esc)", "Врати (Esc)" } },
            { "ConfirmCountdown", new[] { "Previous settings return in {0} seconds.\nCtrl + Alt + F12 — emergency reset.",
                "Через {0} сек. прежние настройки восстановятся.\nCtrl + Alt + F12 — аварийно выключить.",
                "Претходна подешавања се враћају за {0} сек.\nCtrl + Alt + F12 — хитно искључивање." } },
            { "ScheduleDisabled", new[] { "Schedule off. Dimming stays available all day.", "Расписание выключено. Затемнение работает постоянно.", "Распоред је искључен. Затамњивање је увек доступно." } },
            { "Pause", new[] { "Pause: ", "Пауза: ", "Пауза: " } },
            { "PausedNow", new[] { "  •  paused now", "  •  сейчас отключено", "  •  сада искључено" } },
            { "ActiveNow", new[] { "  •  active now", "  •  сейчас активно", "  •  сада активно" } },
            { "PauseAllDay", new[] { "\nEqual times = pause all day.", "\nОдинаковое время = пауза круглые сутки.", "\nИсто време = пауза током целог дана." } },
            { "EmergencyOff", new[] { "emergency off", "аварийно выключено", "хитно искључено" } },
            { "NoDimming", new[] { "not dimming", "не затемняет", "не затамњује" } },
            { "AlreadyRunning", new[] { "NightDim is already running. Check the system tray.",
                "NightDim уже запущен. Проверьте системный трей.",
                "NightDim је већ покренут. Проверите системску палету." } },
            { "Error", new[] { "NightDim error:\n", "Ошибка NightDim:\n", "Грешка у NightDim-у:\n" } }
        };

        internal static string SystemLanguage()
        {
            string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return code == "ru" ? "ru" : code == "sr" ? "sr" : "en";
        }

        internal static bool IsSupported(string code)
        {
            return code == "en" || code == "ru" || code == "sr";
        }

        internal static int Index(string code)
        {
            return code == "ru" ? 1 : code == "sr" ? 2 : 0;
        }

        internal static string FromIndex(int index)
        {
            return index == 1 ? "ru" : index == 2 ? "sr" : "en";
        }

        internal static string Get(string code, string key)
        {
            string[] translations;
            return strings.TryGetValue(key, out translations) ? translations[Index(code)] : key;
        }
    }
}
