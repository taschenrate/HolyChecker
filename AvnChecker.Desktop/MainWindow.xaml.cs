
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AvnChecker.Desktop.Models;
using AvnChecker.Desktop.Services;
using Microsoft.Win32;

namespace AvnChecker.Desktop;

public partial class MainWindow : Window
{
    private static readonly Regex AccessCodeRegex = new("^[0-9A-F]{8}$", RegexOptions.Compiled);

    private readonly string _baseDirectory = AppContext.BaseDirectory;
    private readonly ConfigService _configService;
    private readonly LoggerService _logger;
    private readonly SupabaseService _supabaseService;
    private readonly ToolDownloadService _toolDownloadService;
    private readonly MinecraftPathService _minecraftPathService;
    private readonly SystemInfoService _systemInfoService;
    private readonly TwinkScannerService _twinkScannerService;
    private ModsScannerService _modsScannerService;
    private readonly InjectScannerService _injectScannerService;
    private readonly ProcessScannerService _processScannerService;
    private readonly CheckerSession _session = new();
    private readonly Dictionary<string, Grid> _sections;

    private AppConfig _config;
    private SystemReport? _latestSystem;
    private bool _isNormalizingAccessCode;
    private bool _isSubmitInProgress;
    private bool _autoSubmitTriggered;
    private bool _isFinishCheckInProgress;

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
        _modsScannerService = new ModsScannerService(_logger, _config.ModsRisk, _config.ModsScan);
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
        OverlayWindowButtons.Visibility = Visibility.Visible;

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
            var item = new ToolDownloadItem(tool);
            var existingPath = _toolDownloadService.TryGetExistingDownloadPath(tool);
            if (!string.IsNullOrWhiteSpace(existingPath))
            {
                item.DownloadedPath = existingPath;
                item.Status = $"Скачано: {Path.GetFileName(existingPath)}";
            }

            ToolItems.Add(item);
        }

        CustomPaths.Clear();
        foreach (var path in _config.CustomClientPaths)
        {
            CustomPaths.Add(path);
        }

        LanguageComboBox.SelectedIndex = _config.InterfaceLanguage.Equals("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        LoggingModeComboBox.SelectedIndex = _config.Logging.Mode.Equals("minimal", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        IncludeDownloadsCheckBox.IsChecked = _config.ModsScan.IncludeDownloads;
        IncludeDesktopCheckBox.IsChecked = _config.ModsScan.IncludeDesktop;
        IncludeDocumentsCheckBox.IsChecked = _config.ModsScan.IncludeDocuments;
        IncludeOneDriveCheckBox.IsChecked = _config.ModsScan.IncludeOneDrive;
    }

    private void SaveUiToConfig()
    {
        _config.InterfaceLanguage = GetComboTag(LanguageComboBox, "ru");
        _config.Logging.Mode = GetComboTag(LoggingModeComboBox, "verbose");
        _config.ModsScan.IncludeDownloads = IncludeDownloadsCheckBox.IsChecked == true;
        _config.ModsScan.IncludeDesktop = IncludeDesktopCheckBox.IsChecked == true;
        _config.ModsScan.IncludeDocuments = IncludeDocumentsCheckBox.IsChecked == true;
        _config.ModsScan.IncludeOneDrive = IncludeOneDriveCheckBox.IsChecked == true;
        _logger.SetMode(_config.Logging.Mode);
        _config.CustomClientPaths = CustomPaths.ToList();
        _configService.Save(_config);
        _modsScannerService = new ModsScannerService(_logger, _config.ModsRisk, _config.ModsScan);
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
        var code = NormalizeAccessCode(AccessCodeTextBox.Text);
        AccessCodeTextBox.Text = code;
        AccessCodeTextBox.CaretIndex = code.Length;

        if (!AccessCodeRegex.IsMatch(code))
        {
            CodeValidationText.Text = "Код должен быть из 8 символов 0-9 и A-F. Пример: 82EE8F1F.";
            return;
        }

        try
        {
            ValidateCodeButton.IsEnabled = false;
            CodeValidationText.Text = "Проверяем код...";

            var response = await _supabaseService.ValidateCodeAsync(code);
            _logger.Info($"Code validation response: ok={response.Ok}, error_code={response.ErrorCode}, checker={response.CheckerName ?? "-"}");
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
                OverlayWindowButtons.Visibility = Visibility.Collapsed;
                CodeValidationText.Text = "Код подтвержден";
                PlayerNickTextBox.Text = _session.CheckerName;
                _logger.Info($"Access granted for {_session.CheckerName}");
                SubmitStatusText.Text = "Запущен автоматический сбор и отправка...";
                StartAutomaticSubmit();
                return;
            }

            CodeValidationText.Text = response.ErrorCode switch
            {
                "expired" => "Код истек.",
                "used" => "Код уже использован.",
                "not_found" => "Код не найден.",
                "rpc_empty" => "Код недоступен для проверки на сервере.",
                _ => $"Ошибка RPC: {response.ErrorCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Code validation failed", ex);
            CodeValidationText.Text = GetCodeValidationErrorText(ex);
        }
        finally
        {
            ValidateCodeButton.IsEnabled = true;
        }
    }

    private static string GetCodeValidationErrorText(Exception ex)
    {
        if (ex is InvalidOperationException &&
            ex.Message.Contains("Supabase не настроен", StringComparison.OrdinalIgnoreCase))
        {
            return "Supabase не настроен в appsettings.json.";
        }

        if (ex is InvalidOperationException invalidOperationException)
        {
            if (invalidOperationException.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) &&
                invalidOperationException.Message.Contains("column", StringComparison.OrdinalIgnoreCase))
            {
                return "Ошибка структуры БД: в RPC запрошена несуществующая колонка.";
            }

            var postgrestMessage = TryExtractPostgrestMessage(invalidOperationException.Message);
            if (!string.IsNullOrWhiteSpace(postgrestMessage))
            {
                return $"Ошибка Supabase: {postgrestMessage}";
            }
        }

        if (ex is TaskCanceledException)
        {
            return "Таймаут запроса к серверу. Повторите попытку.";
        }

        return "Ошибка проверки кода. Смотрите логи.";
    }

    private static string? TryExtractPostgrestMessage(string rawMessage)
    {
        var jsonStart = rawMessage.IndexOf('{');
        if (jsonStart < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawMessage[jsonStart..]);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string NormalizeAccessCode(string raw)
    {
        return new string(raw
            .ToUpperInvariant()
            .Where(static c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
            .Take(8)
            .ToArray());
    }

    private void OnAccessCodeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isNormalizingAccessCode || sender is not TextBox textBox)
        {
            return;
        }

        var normalized = NormalizeAccessCode(textBox.Text);
        if (string.Equals(textBox.Text, normalized, StringComparison.Ordinal))
        {
            return;
        }

        var caretIndex = textBox.CaretIndex;
        _isNormalizingAccessCode = true;
        textBox.Text = normalized;
        textBox.CaretIndex = Math.Min(normalized.Length, caretIndex);
        _isNormalizingAccessCode = false;
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

            var safeCount = Mods.Count(m => string.Equals(m.RiskLevel, "safe", StringComparison.OrdinalIgnoreCase));
            var cheatCount = Mods.Count(m => string.Equals(m.RiskLevel, "cheat", StringComparison.OrdinalIgnoreCase));
            var unknownCount = Mods.Count - safeCount - cheatCount;
            ModsStatusText.Text = $"Готово: {Mods.Count} | safe: {safeCount} | unknown: {unknownCount} | cheat: {cheatCount}";
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

        await DownloadToolItemAsync(item);
    }

    private async void OnDownloadAllToolsClick(object sender, RoutedEventArgs e)
    {
        var itemsToDownload = ToolItems
            .Where(item => !item.IsBusy)
            .ToList();

        if (itemsToDownload.Count == 0)
        {
            return;
        }

        var tasks = itemsToDownload.Select(DownloadToolItemAsync).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task DownloadToolItemAsync(ToolDownloadItem item)
    {
        if (item.IsBusy)
        {
            return;
        }

        try
        {
            item.IsBusy = true;
            var progress = new Progress<string>(status => item.Status = status);
            var path = await _toolDownloadService.DownloadAsync(item.Definition, progress);
            item.DownloadedPath = path;
            item.Status = $"Скачано: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _logger.Error($"Tool download failed: {item.Definition.Name}", ex);
            item.Status = $"Ошибка: {GetToolDownloadErrorText(ex)}";
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private void OnOpenToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToolDownloadItem item } ||
            string.IsNullOrWhiteSpace(item.DownloadedPath) ||
            !File.Exists(item.DownloadedPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(item.DownloadedPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Tool open failed: {item.Definition.Name}", ex);
            MessageBox.Show($"Ошибка открытия: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string GetToolDownloadErrorText(Exception ex)
    {
        if (ex is InvalidOperationException invalidOperationException &&
            invalidOperationException.Message.Contains("jwt:expired", StringComparison.OrdinalIgnoreCase))
        {
            return "Ссылка истекла (jwt expired). Обновите URL утилиты в appsettings.json.";
        }

        if (ex is HttpRequestException httpRequestException && httpRequestException.StatusCode.HasValue)
        {
            return $"HTTP {(int)httpRequestException.StatusCode.Value}";
        }

        if (ex is TaskCanceledException)
        {
            return "Таймаут загрузки.";
        }

        return ex.Message;
    }

    private void OnFinishCheckClick(object sender, RoutedEventArgs e)
    {
        if (_isFinishCheckInProgress)
        {
            return;
        }

        FinishCheckOverlay.Visibility = Visibility.Visible;
    }

    private void OnCancelFinishCheckClick(object sender, RoutedEventArgs e)
    {
        if (_isFinishCheckInProgress)
        {
            return;
        }

        FinishCheckOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnConfirmFinishCheckClick(object sender, RoutedEventArgs e)
    {
        if (_isFinishCheckInProgress)
        {
            return;
        }

        _isFinishCheckInProgress = true;
        try
        {
            ScheduleSelfCleanup();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _isFinishCheckInProgress = false;
            _logger.Error("Failed to schedule self-cleanup", ex);
            MessageBox.Show($"Не удалось завершить проверку: {ex.Message}", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
            FinishCheckOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void ScheduleSelfCleanup()
    {
        var currentProcess = Process.GetCurrentProcess();
        var processId = currentProcess.Id;
        var targetDirectory = Path.GetFullPath(_baseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var toolsDirectory = Path.Combine(targetDirectory, "Tools");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"avnchecker_cleanup_{Guid.NewGuid():N}.cmd");

        static string EscapeCmdValue(string value)
        {
            return value.Replace("^", "^^").Replace("&", "^&").Replace("|", "^|").Replace("<", "^<").Replace(">", "^>").Replace("%", "%%");
        }

        var escapedTargetDirectory = EscapeCmdValue(targetDirectory);
        var escapedToolsDirectory = EscapeCmdValue(toolsDirectory);

        var script = $"""
                      @echo off
                      setlocal
                      set "PID={processId}"
                      set "TARGET_DIR={escapedTargetDirectory}"
                      set "TOOLS_DIR={escapedToolsDirectory}"
                      
                      for /L %%i in (1,1,40) do (
                          tasklist /FI "PID eq %PID%" | find " %PID% " >nul
                          if errorlevel 1 goto cleanup
                          timeout /t 1 /nobreak >nul
                      )
                      
                      :cleanup
                      if exist "%TOOLS_DIR%" rmdir /s /q "%TOOLS_DIR%" >nul 2>&1
                      if exist "%TARGET_DIR%" (
                          for /f "delims=" %%f in ('dir /b /a-d "%TARGET_DIR%" 2^>nul') do del /f /q "%TARGET_DIR%\%%f" >nul 2>&1
                          for /f "delims=" %%d in ('dir /b /ad "%TARGET_DIR%" 2^>nul') do rmdir /s /q "%TARGET_DIR%\%%d" >nul 2>&1
                          rmdir /s /q "%TARGET_DIR%" >nul 2>&1
                      )
                      
                      del /f /q "%~f0" >nul 2>&1
                      endlocal
                      """;

        File.WriteAllText(scriptPath, script, Encoding.ASCII);
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory
        });
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

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        var configPath = Path.Combine(_baseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{configPath}\"") { UseShellExecute = true });
        }
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        var logsPath = Path.Combine(_baseDirectory, "Logs");
        Directory.CreateDirectory(logsPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logsPath}\"") { UseShellExecute = true });
    }

    private void OnApplySettingsClick(object sender, RoutedEventArgs e)
    {
        SaveUiToConfig();
        SubmitStatusText.Text = "Настройки сохранены.";
        SubmitStatusText.Foreground = (Brush)FindResource("TextMuted");
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
        if (_isSubmitInProgress)
        {
            return;
        }

        if (!_session.IsAuthorized)
        {
            MessageBox.Show("Сначала подтвердите одноразовый код.", "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var playerNick = ResolvePlayerNick();

        _isSubmitInProgress = true;
        SubmitProgressPanel.Visibility = Visibility.Visible;
        SubmitResultPanel.Visibility = Visibility.Collapsed;
        SubmitStatusText.Foreground = (Brush)FindResource("Accent");
        SetSubmitProgress(0, "Подготовка к сбору...");

        try
        {
            SaveUiToConfig();
            SubmitStatusText.Text = "Сбор данных...";
            SetSubmitProgress(12, "Сбор системной информации...");

            _latestSystem ??= await _systemInfoService.CollectAsync();
            SetSubmitProgress(20, "Проверка HWID по черному списку...");
            var blacklistResult = await _supabaseService.CheckHwidBlacklistAsync(_latestSystem.Hwid);
            if (blacklistResult.IsBlacklisted)
            {
                _logger.Info($"HWID blacklist match: {_latestSystem.Hwid}; source={blacklistResult.Source ?? "-"}; reason={blacklistResult.Reason ?? "-"}");
            }
            else if (string.Equals(blacklistResult.Source, "blacklist_check_unavailable", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"HWID blacklist check unavailable: hwid={_latestSystem.Hwid}; reason={blacklistResult.Reason ?? "-"}");
            }

            SetSubmitProgress(28, "Проверка твинков...");

            if (Twinks.Count == 0)
            {
                var twinks = await _twinkScannerService.ScanAsync(GetClientRoots());
                Twinks.Clear();
                foreach (var item in twinks)
                {
                    Twinks.Add(item);
                }
            }

            SetSubmitProgress(45, "Проверка mods...");
            if (Mods.Count == 0)
            {
                var mods = await _modsScannerService.ScanAsync(GetClientRoots());
                Mods.Clear();
                foreach (var item in mods)
                {
                    Mods.Add(item);
                }
            }

            SubmitStatusText.Text = "Дамп процесса игры и анализ...";
            SetSubmitProgress(66, "Дамп/анализ процесса игры...");
            var injects = await _injectScannerService.AnalyzeAsync();
            var processes = _processScannerService.ScanSuspiciousProcesses();

            var status = DetermineStatusFromSignals(injects, blacklistResult.IsBlacklisted);
            SetStatusInUi(status);

            var autoComment = BuildAutoComment(injects, processes, blacklistResult, _latestSystem.Hwid);
            NotesTextBox.Text = autoComment;
            PlayerNickTextBox.Text = playerNick;

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
                Notes = autoComment
            };

            var sourceReportJson = JsonSerializer.Serialize(sourceReport, new JsonSerializerOptions { WriteIndented = true });
            var reportPath = Path.Combine(_baseDirectory, "Logs", $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, sourceReportJson, Encoding.UTF8);

            SubmitStatusText.Text = "Авто-отправка отчета в Supabase...";
            SetSubmitProgress(80, "Отправка на сервер: 80%");
            using var uploadProgressCts = new CancellationTokenSource();
            var uploadProgressTask = AnimateUploadProgressAsync(uploadProgressCts.Token);
            long checkId;
            try
            {
                checkId = await _supabaseService.RegisterCheckAsync(
                    _session.AccessCode,
                    playerNick,
                    _latestSystem.Hwid,
                    status,
                    DateTimeOffset.Now,
                    sourceReportJson
                );
            }
            finally
            {
                uploadProgressCts.Cancel();
                try
                {
                    await uploadProgressTask;
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }

            var successText = checkId > 0
                ? $"Отчёт отправлен. ID проверки: {checkId}"
                : "Отчёт отправлен.";
            SubmitStatusText.Text = $"✓ {successText}";
            SubmitStatusText.Foreground = new SolidColorBrush(Color.FromRgb(164, 238, 140));
            SetSubmitProgress(100, "Отправка на сервер: 100%");
            await HideSubmitProgressPanelAsync();
            ShowSubmitResult(
                isSuccess: true,
                title: "Отправка завершена",
                details: successText);
        }
        catch (Exception ex)
        {
            _logger.Error("Submit check failed", ex);
            SubmitStatusText.Text = "✕ Ошибка отправки отчёта.";
            SubmitStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 130, 130));
            SetSubmitProgress(0, "Ошибка отправки");
            await HideSubmitProgressPanelAsync(delayMs: 260);
            ShowSubmitResult(
                isSuccess: false,
                title: "Отправка не выполнена",
                details: "Проверьте логи и ответ RPC.");
            MessageBox.Show(GetSubmitErrorText(ex), "AvnChecker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isSubmitInProgress = false;
        }
    }

    private string ResolvePlayerNick()
    {
        var nickFromProcess = _minecraftPathService.GetActivePlayerNick();
        if (!string.IsNullOrWhiteSpace(nickFromProcess))
        {
            return nickFromProcess;
        }

        if (Twinks.Count > 0)
        {
            return Twinks[0].Nick;
        }

        if (!string.IsNullOrWhiteSpace(_latestSystem?.WindowsUser))
        {
            return _latestSystem.WindowsUser;
        }

        return "unknown_player";
    }

    private static string DetermineStatusFromSignals(InjectReport injects, bool isBlacklisted)
    {
        if (isBlacklisted || injects.Found)
        {
            return "cheat";
        }

        return injects.Decision is "scan_failed" or "process_not_found"
            ? "probably_cheat"
            : "clean";
    }

    private void SetStatusInUi(string status)
    {
        CheckStatusComboBox.SelectedIndex = status switch
        {
            "clean" => 0,
            "probably_cheat" => 1,
            _ => 2
        };
    }

    private static string BuildAutoComment(
        InjectReport injects,
        IReadOnlyCollection<ProcessInfoEntry> processes,
        HwidBlacklistCheckResult blacklistResult,
        string hwid)
    {
        var reasons = new List<string>();

        if (blacklistResult.IsBlacklisted)
        {
            var reasonText = string.IsNullOrWhiteSpace(blacklistResult.Reason)
                ? "причина не указана"
                : blacklistResult.Reason.Trim();
            reasons.Add($"HWID в черном списке: {hwid}. Причина: {reasonText}");
        }
        else if (string.Equals(blacklistResult.Source, "blacklist_check_unavailable", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Проверка ЧС недоступна (RLS/RPC).");
        }

        if (!string.IsNullOrWhiteSpace(injects.Comment))
        {
            reasons.Add(injects.Comment.Trim());
        }

        if (injects.Matches.Count > 0)
        {
            reasons.Add($"Сигнатуры: {string.Join(", ", injects.Matches.Take(8))}");
        }

        if (processes.Count > 0)
        {
            var processList = string.Join(", ", processes.Take(6).Select(p => $"{p.Name}({p.Reason})"));
            reasons.Add($"Подозрительные процессы: {processList}");
        }

        return reasons.Count == 0
            ? "Автоанализ завершен: подозрительные сигнатуры не обнаружены."
            : string.Join(" | ", reasons);
    }

    private void SetSubmitProgress(int percent, string label)
    {
        var value = Math.Clamp(percent, 0, 100);
        SubmitProgressPanel.Visibility = Visibility.Visible;
        SubmitProgressBar.Value = value;
        SubmitProgressLabel.Text = label;
        SubmitProgressPercent.Text = $"{value}%";
    }

    private async Task AnimateUploadProgressAsync(CancellationToken cancellationToken)
    {
        var value = (int)SubmitProgressBar.Value;
        while (value < 99)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(120, cancellationToken);
            value = Math.Min(99, value + 1);
            SetSubmitProgress(value, $"Отправка на сервер: {value}%");
        }
    }

    private async Task HideSubmitProgressPanelAsync(int delayMs = 900)
    {
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        SubmitProgressPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowSubmitResult(bool isSuccess, string title, string details)
    {
        SubmitResultTitle.Text = title;
        SubmitResultText.Text = details;
        SubmitResultIcon.Text = isSuccess ? "✓" : "!";

        if (isSuccess)
        {
            SubmitResultPanel.Background = new SolidColorBrush(Color.FromRgb(19, 35, 21));
            SubmitResultPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 90, 43));
            SubmitResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(228, 250, 217));
            SubmitResultText.Foreground = new SolidColorBrush(Color.FromRgb(166, 180, 158));
            SubmitResultIcon.Foreground = new SolidColorBrush(Color.FromRgb(184, 244, 170));
        }
        else
        {
            SubmitResultPanel.Background = new SolidColorBrush(Color.FromRgb(40, 20, 20));
            SubmitResultPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 50, 50));
            SubmitResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 210));
            SubmitResultText.Foreground = new SolidColorBrush(Color.FromRgb(226, 166, 166));
            SubmitResultIcon.Foreground = new SolidColorBrush(Color.FromRgb(255, 170, 170));
        }

        SubmitResultPanel.Visibility = Visibility.Visible;
    }

    private static string GetSubmitErrorText(Exception ex)
    {
        if (ex is InvalidOperationException invalidOperationException)
        {
            if (invalidOperationException.Message.Contains("checks_source_code_fkey", StringComparison.OrdinalIgnoreCase))
            {
                return "Ошибка схемы БД: поле checks.source_code настроено как внешний ключ. RPC register_check нужно адаптировать под вашу структуру.";
            }

            if (invalidOperationException.Message.Contains("check_status", StringComparison.OrdinalIgnoreCase))
            {
                return "Ошибка RPC register_check: тип статуса в БД не приведен к enum check_status. Нужно исправить SQL-функцию на сервере.";
            }

            var jsonStart = invalidOperationException.Message.IndexOf('{');
            if (jsonStart >= 0)
            {
                try
                {
                    using var document = JsonDocument.Parse(invalidOperationException.Message[jsonStart..]);
                    if (document.RootElement.TryGetProperty("message", out var message))
                    {
                        return $"Ошибка отправки отчета: {message.GetString()}";
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        return $"Ошибка отправки отчета: {ex.Message}";
    }

    private void StartAutomaticSubmit()
    {
        if (_autoSubmitTriggered || _isSubmitInProgress)
        {
            return;
        }

        _autoSubmitTriggered = true;
        _ = Dispatcher.InvokeAsync(() => OnSubmitCheckClick(this, new RoutedEventArgs()));
    }
}



