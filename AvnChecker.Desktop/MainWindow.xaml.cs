
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvnChecker.Desktop.Models;
using AvnChecker.Desktop.Services;
using Microsoft.Win32;

namespace AvnChecker.Desktop;

public partial class MainWindow : Window
{
    private readonly string _baseDirectory = AppContext.BaseDirectory;
    private readonly ConfigService _configService;
    private readonly LoggerService _logger;
    private readonly SupabaseService _supabaseService;
    private readonly ToolDownloadService _toolDownloadService;
    private readonly MinecraftPathService _minecraftPathService;
    private readonly SystemInfoService _systemInfoService;
    private readonly TwinkScannerService _twinkScannerService;
    private readonly ModsScannerService _modsScannerService;
    private readonly InjectScannerService _injectScannerService;
    private readonly ProcessScannerService _processScannerService;
    private readonly CheckerSession _session = new();
    private readonly Dictionary<string, Grid> _sections;

    private AppConfig _config;
    private SystemReport? _latestSystem;

    public ObservableCollection<ToolDownloadItem> ToolItems { get; } = [];
    public ObservableCollection<TwinkEntry> Twinks { get; } = [];
    public ObservableCollection<ModEntry> Mods { get; } = [];
    public ObservableCollection<string> CustomPaths { get; } = [];

    public MainWindow()
    {
        _configService = new ConfigService(_baseDirectory);
        _config = _configService.Load();
        _logger = new LoggerService(_baseDirectory, _config.Logging.Mode);

        var rpcHttp = new HttpClient();
        var downloadHttp = new HttpClient();
        _supabaseService = new SupabaseService(rpcHttp, _config.Supabase);
        _toolDownloadService = new ToolDownloadService(downloadHttp, _baseDirectory);
        _minecraftPathService = new MinecraftPathService();
        _systemInfoService = new SystemInfoService(_logger);
        _twinkScannerService = new TwinkScannerService(_logger);
        _modsScannerService = new ModsScannerService(_logger);
        _injectScannerService = new InjectScannerService(_logger);
        _processScannerService = new ProcessScannerService();

        InitializeComponent();
        DataContext = this;

        _sections = new Dictionary<string, Grid>
        {
            ["Information"] = InfoSection,
            ["Applications"] = AppsSection,
            ["System"] = SystemSection,
            ["Twinks"] = TwinksSection,
            ["Mods"] = ModsSection,
            ["Settings"] = SettingsSection
        };

        LoadConfigToUi();
        SetActiveSection("Information");
        CheckStatusComboBox.SelectedIndex = 0;
        MainShell.Visibility = Visibility.Hidden;
        CodeOverlayPanel.Visibility = Visibility.Visible;

        Loaded += async (_, _) =>
        {
            await RefreshSystemAsync();
            await RescanTwinksAsync();
        };
    }

    private void LoadConfigToUi()
    {
        ToolItems.Clear();
        foreach (var tool in _config.Tools)
        {
            ToolItems.Add(new ToolDownloadItem(tool));
        }

        CustomPaths.Clear();
        foreach (var path in _config.CustomClientPaths)
        {
            CustomPaths.Add(path);
        }

        LanguageComboBox.SelectedIndex = _config.InterfaceLanguage.Equals("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        LoggingModeComboBox.SelectedIndex = _config.Logging.Mode.Equals("minimal", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private void SaveUiToConfig()
    {
        _config.InterfaceLanguage = GetComboTag(LanguageComboBox, "ru");
        _config.Logging.Mode = GetComboTag(LoggingModeComboBox, "verbose");
        _logger.SetMode(_config.Logging.Mode);
        _config.CustomClientPaths = CustomPaths.ToList();
        _configService.Save(_config);
    }

    private static string GetComboTag(System.Windows.Controls.ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }

        return fallback;
    }

    private void SetActiveSection(string section)
    {
        foreach (var pair in _sections)
        {
            pair.Value.Visibility = pair.Key == section ? Visibility.Visible : Visibility.Collapsed;
        }

        MainTitleText.Text = section switch
        {
            "Information" => "Информация",
            "Applications" => "Приложения",
            "System" => "О системе",
            "Twinks" => "Твинки",
            "Mods" => "Mods",
            "Settings" => "Настройки",
            _ => "AvnChecker"
        };

        ResetNavHighlight();
        switch (section)
        {
            case "Information":
                InfoNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
            case "Applications":
                AppsNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
            case "System":
                SystemNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
            case "Twinks":
                TwinksNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
            case "Mods":
                ModsNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
            case "Settings":
                SettingsNavButton.Background = System.Windows.Media.Brushes.Orange;
                break;
        }
    }

    private void ResetNavHighlight()
    {
        InfoNavButton.ClearValue(Button.BackgroundProperty);
        AppsNavButton.ClearValue(Button.BackgroundProperty);
        SystemNavButton.ClearValue(Button.BackgroundProperty);
        TwinksNavButton.ClearValue(Button.BackgroundProperty);
        ModsNavButton.ClearValue(Button.BackgroundProperty);
        SettingsNavButton.ClearValue(Button.BackgroundProperty);
    }
    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string section)
        {
            SetActiveSection(section);
        }
    }

    private async void OnValidateCodeClick(object sender, RoutedEventArgs e)
    {
        var code = new string(AccessCodeTextBox.Text.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (code.Length != 6)
        {
            CodeValidationText.Text = "Код должен содержать 6 символов A-Z0-9.";
            return;
        }

        try
        {
            ValidateCodeButton.IsEnabled = false;
            CodeValidationText.Text = "Проверяем код...";

            var response = await _supabaseService.ValidateCodeAsync(code);
            if (response.Ok && string.Equals(response.ErrorCode, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _session.IsAuthorized = true;
                _session.AccessCode = code;
                _session.CheckerId = response.CheckerId ?? string.Empty;
                _session.CheckerName = response.CheckerName ?? "checker";
                _session.ExpiresAt = response.ExpiresAt;

                NavMenuPanel.IsEnabled = true;
                MainShell.Visibility = Visibility.Visible;
                CodeOverlayPanel.Visibility = Visibility.Collapsed;
                CodeValidationText.Text = "Код подтвержден";
                PlayerNickTextBox.Text = _session.CheckerName;
                _logger.Info($"Access granted for {_session.CheckerName}");
                return;
            }

            CodeValidationText.Text = response.ErrorCode switch
            {
                "expired" => "Код истек.",
                "used" => "Код уже использован.",
                _ => "Код не найден."
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Code validation failed", ex);
            CodeValidationText.Text = ex.Message;
        }
        finally
        {
            ValidateCodeButton.IsEnabled = true;
        }
    }

    private static void OpenExternalLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignored
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // ignored
        }
    }

    private void OnMinimizeWindowClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeWindowClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnOpenGithubClick(object sender, RoutedEventArgs e)
    {
        OpenExternalLink("https://github.com");
    }

    private void OnOpenWebsiteClick(object sender, RoutedEventArgs e)
    {
        OpenExternalLink("https://t.me/avnmanager");
    }

    private void OnOpenVkClick(object sender, RoutedEventArgs e)
    {
        OpenExternalLink("https://vk.com/id808000786");
    }

    private async Task RefreshSystemAsync()
    {
        try
        {
            var system = await _systemInfoService.CollectAsync();
            _latestSystem = system;

            var user = string.IsNullOrWhiteSpace(system.WindowsUser) ? "?" : system.WindowsUser;
            AvatarInitialText.Text = user[..1].ToUpperInvariant();
            UserNameText.Text = user;
            MachineNameText.Text = $"ПК: {system.ComputerName}";
            WindowsUserText.Text = $"Пользователь: {system.WindowsUser}";
            ScreensCountText.Text = system.ScreensCount.ToString();
            UptimeText.Text = system.Uptime;
            OsNameText.Text = system.OsName;
            InstallDateText.Text = system.OsInstallDate;
            WindowsVersionText.Text = system.WindowsVersion;
            WindowsBuildText.Text = system.WindowsBuild;
            CpuText.Text = system.Cpu;
            GpuText.Text = string.Join(Environment.NewLine, system.Gpu);
            MotherboardText.Text = system.Motherboard;
            VmText.Text = system.IsVm ? "ДА" : "НЕТ";
            HwidText.Text = system.Hwid;
            EventLogsText.Text = $"System 104: {system.EventLogs.System104.Status} ({system.EventLogs.System104.LastEventTime}); " +
                                 $"Application 3079: {system.EventLogs.Application3079.Status} ({system.EventLogs.Application3079.LastEventTime})";
        }
        catch (Exception ex)
        {
            _logger.Error("System refresh failed", ex);
            MessageBox.Show($"Ошибка сбора системы: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRefreshSystemClick(object sender, RoutedEventArgs e)
    {
        await RefreshSystemAsync();
    }

    private IReadOnlyList<string> GetClientRoots()
    {
        return _minecraftPathService.GetClientRoots(CustomPaths);
    }
    private async Task RescanTwinksAsync()
    {
        try
        {
            TwinksStatusText.Text = "Сканирование...";
            var roots = GetClientRoots();
            var items = await _twinkScannerService.ScanAsync(roots);

            Twinks.Clear();
            foreach (var item in items)
            {
                Twinks.Add(item);
            }

            TwinksStatusText.Text = $"Найдено {Twinks.Count} записей";
        }
        catch (Exception ex)
        {
            _logger.Error("Twinks scan failed", ex);
            TwinksStatusText.Text = "Ошибка";
            MessageBox.Show($"Ошибка сканирования твинков: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRescanTwinksClick(object sender, RoutedEventArgs e)
    {
        await RescanTwinksAsync();
    }

    private void OnCopyTwinksClick(object sender, RoutedEventArgs e)
    {
        if (Twinks.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (var twink in Twinks)
        {
            sb.AppendLine($"{twink.Nick}\t{twink.Source}\t{twink.Path}");
        }

        Clipboard.SetText(sb.ToString());
        TwinksStatusText.Text = "Скопировано в буфер обмена";
    }

    private void OnExportTwinksClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = $"AvnChecker_Twinks_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var lines = Twinks.Select(x => $"{x.Nick} | {x.Source} | {x.Path}");
        File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
        TwinksStatusText.Text = "Файл сохранен";
    }

    private async Task ScanModsAsync()
    {
        try
        {
            var progress = new Progress<int>(count =>
            {
                ModsStatusText.Text = $"Сканирование... (найдено {count} модов)";
            });

            var roots = GetClientRoots();
            var items = await _modsScannerService.ScanAsync(roots, progress);

            Mods.Clear();
            foreach (var item in items)
            {
                Mods.Add(item);
            }

            ModsStatusText.Text = $"Готово: {Mods.Count} модов";
        }
        catch (Exception ex)
        {
            _logger.Error("Mods scan failed", ex);
            ModsStatusText.Text = "Ошибка";
            MessageBox.Show($"Ошибка сканирования модов: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnScanModsClick(object sender, RoutedEventArgs e)
    {
        await ScanModsAsync();
    }

    private void OnModsRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModEntry selected)
        {
            return;
        }

        if (File.Exists(selected.Path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.Path}\"") { UseShellExecute = true });
        }
    }

    private async void OnDownloadToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToolDownloadItem item })
        {
            return;
        }

        try
        {
            var progress = new Progress<string>(status => item.Status = status);
            var path = await _toolDownloadService.DownloadAsync(item.Definition, progress);
            item.Status = $"Скачано: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _logger.Error($"Tool download failed: {item.Definition.Name}", ex);
            item.Status = "Ошибка";
            MessageBox.Show($"Ошибка скачивания: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAddCustomPathClick(object sender, RoutedEventArgs e)
    {
        var path = CustomPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show("Укажите существующую директорию.", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!CustomPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            CustomPaths.Add(path);
            SaveUiToConfig();
        }

        CustomPathTextBox.Clear();
    }

    private void OnRemoveCustomPathClick(object sender, RoutedEventArgs e)
    {
        if (CustomPathsListBox.SelectedItem is string path)
        {
            CustomPaths.Remove(path);
            SaveUiToConfig();
        }
    }

    private void OnExportConfigClick(object sender, RoutedEventArgs e)
    {
        SaveUiToConfig();

        var dialog = new SaveFileDialog
        {
            Filter = "Json files (*.json)|*.json",
            FileName = $"AvnChecker_config_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            _configService.Export(_config, dialog.FileName);
        }
    }

    private void OnResetConfigClick(object sender, RoutedEventArgs e)
    {
        _config = _configService.ResetToDefault();
        LoadConfigToUi();
        SaveUiToConfig();
    }
    private async void OnSubmitCheckClick(object sender, RoutedEventArgs e)
    {
        if (!_session.IsAuthorized)
        {
            MessageBox.Show("Сначала подтвердите одноразовый код.", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var playerNick = PlayerNickTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(playerNick))
        {
            MessageBox.Show("Укажите ник игрока.", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SaveUiToConfig();
            SubmitStatusText.Text = "Сбор данных...";

            _latestSystem ??= await _systemInfoService.CollectAsync();

            if (Twinks.Count == 0)
            {
                var twinks = await _twinkScannerService.ScanAsync(GetClientRoots());
                Twinks.Clear();
                foreach (var item in twinks)
                {
                    Twinks.Add(item);
                }
            }

            if (Mods.Count == 0)
            {
                var mods = await _modsScannerService.ScanAsync(GetClientRoots());
                Mods.Clear();
                foreach (var item in mods)
                {
                    Mods.Add(item);
                }
            }

            SubmitStatusText.Text = "Анализ инжектов...";
            var injects = await _injectScannerService.AnalyzeAsync();
            var processes = _processScannerService.ScanSuspiciousProcesses();

            var status = GetComboTag(CheckStatusComboBox, "clean");
            if (injects.Found && status == "clean")
            {
                status = "cheat";
                CheckStatusComboBox.SelectedIndex = 2;
            }

            var sourceReport = new CheckSourceReport
            {
                System = _latestSystem,
                Twinks = Twinks.ToList(),
                Mods = Mods.ToList(),
                Injects = injects,
                Extra = new ExtraReport
                {
                    Processes = processes,
                    DpsTags = injects.DpsTags,
                    FileChanges = [],
                    DllScan = []
                },
                Notes = NotesTextBox.Text.Trim()
            };

            var sourceCodeJson = JsonSerializer.Serialize(sourceReport, new JsonSerializerOptions { WriteIndented = true });
            var reportPath = Path.Combine(_baseDirectory, "Logs", $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, sourceCodeJson, Encoding.UTF8);

            SubmitStatusText.Text = "Отправка в Supabase...";
            var checkId = await _supabaseService.RegisterCheckAsync(
                _session.AccessCode,
                playerNick,
                _latestSystem.Hwid,
                status,
                DateTimeOffset.Now,
                sourceCodeJson
            );

            SubmitStatusText.Text = checkId > 0 ? $"Успешно. ID проверки: {checkId}" : "Отчёт отправлен";
        }
        catch (Exception ex)
        {
            _logger.Error("Submit check failed", ex);
            SubmitStatusText.Text = "Ошибка";
            MessageBox.Show($"Ошибка отправки отчета: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}



