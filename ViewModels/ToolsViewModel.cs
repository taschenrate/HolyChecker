using System.Collections.ObjectModel;
using System.IO;
using HolyChecker.Models;
using HolyChecker.Services;

namespace HolyChecker.ViewModels;

public sealed class ToolsViewModel : BaseViewModel
{
    private readonly IExternalToolService _toolService;
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<ExternalTool> Tools { get; } = new();
    public AsyncRelayCommand LaunchCommand { get; }

    public ToolsViewModel()
    {
        _toolService = new ExternalToolService(new DownloadService());
        LaunchCommand = new AsyncRelayCommand(LaunchToolAsync);

        var toolsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HolyChecker", "Tools");

        Tools.Add(new ExternalTool
        {
            Name = "Everything",
            Description = "Сверхбыстрый поиск файлов",
            OfficialDownloadUrl = "https://www.voidtools.com/Everything-1.5.0.1404a.x64.zip",
            FileName = "Everything-1.5.0.1404a.x64.zip",
            LocalPath = Path.Combine(toolsFolder, "Everything-1.5.0.1404a.x64.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "Shellbag Analyzer",
            Description = "Криминалистический анализ Windows Shellbag",
            OfficialDownloadUrl = "https://privazer.com/ru/shellbag_analyzer_cleaner.exe",
            FileName = "shellbag_analyzer_cleaner.exe",
            LocalPath = Path.Combine(toolsFolder, "shellbag_analyzer_cleaner.exe")
        });

        Tools.Add(new ExternalTool
        {
            Name = "RegScanner",
            Description = "Сканер реестра Windows",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/regscanner-x64.zip",
            FileName = "regscanner-x64.zip",
            LocalPath = Path.Combine(toolsFolder, "regscanner-x64.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "RecentFilesView",
            Description = "Просмотр недавно открытых файлов",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/recentfilesview.zip",
            FileName = "recentfilesview.zip",
            LocalPath = Path.Combine(toolsFolder, "recentfilesview.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "BrowserDownloadsView",
            Description = "Просмотр истории загрузок браузера",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/browserdownloadsview-x64.zip",
            FileName = "browserdownloadsview-x64.zip",
            LocalPath = Path.Combine(toolsFolder, "browserdownloadsview-x64.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "UsbDriveLog",
            Description = "Журнал подключений USB-накопителей",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/usbdrivelog.zip",
            FileName = "usbdrivelog.zip",
            LocalPath = Path.Combine(toolsFolder, "usbdrivelog.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "LastActivityView",
            Description = "Просмотр последней активности системы",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/lastactivityview.zip",
            FileName = "lastactivityview.zip",
            LocalPath = Path.Combine(toolsFolder, "lastactivityview.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "ExecutedProgramsList",
            Description = "Список ранее запущенных программ",
            OfficialDownloadUrl = "https://www.nirsoft.net/utils/executedprogramslist-x64.zip",
            FileName = "executedprogramslist-x64.zip",
            LocalPath = Path.Combine(toolsFolder, "executedprogramslist-x64.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "System Informer",
            Description = "Продвинутый системный монитор (форк Process Hacker)",
            OfficialDownloadUrl = "https://netix.dl.sourceforge.net/project/systeminformer/systeminformer-3.2.25011-release-setup.exe?viasf=1",
            FileName = "systeminformer-3.2.25011-release-setup.exe",
            LocalPath = Path.Combine(toolsFolder, "systeminformer-3.2.25011-release-setup.exe")
        });

        Tools.Add(new ExternalTool
        {
            Name = "Journal Trace",
            Description = "Утилита трассировки журнала NTFS",
            OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/899349770/ab6a048b-1554-4866-86ee-9c5cee36bc24?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A24%3A37Z&rscd=attachment%3B+filename%3DJournalTrace.exe&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A23%3A40Z&ske=2026-02-13T21%3A24%3A37Z&sks=b&skv=2018-11-09&sig=ER%2FJ7czQaWAulZUQCXEpG7uiL%2FAt%2F8CP0vqTAsru7rw%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDczNiwibmJmIjoxNzcxMDE0NDM2LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.tAXuaNNQIBL9Wooyqbi7FpykFWbVPiY7W88vqw__-N0&response-content-disposition=attachment%3B%20filename%3DJournalTrace.exe&response-content-type=application%2Foctet-stream",
            FileName = "JournalTrace.exe",
            LocalPath = Path.Combine(toolsFolder, "JournalTrace.exe")
        });

        Tools.Add(new ExternalTool
        {
            Name = "SimpleUnlocker",
            Description = "Утилита разблокировки файлов",
            OfficialDownloadUrl = "https://simpleunlocker.ds1nc.ru/release/simpleunlocker_release.zip",
            FileName = "simpleunlocker_release.zip",
            LocalPath = Path.Combine(toolsFolder, "simpleunlocker_release.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "BamParser",
            Description = "Парсер фоновой активности (BAM)",
            OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/890129826/43223953-f0eb-4544-9ba2-bd0a4181d38b?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A21%3A24Z&rscd=attachment%3B+filename%3DBAMParser.exe&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A20%3A44Z&ske=2026-02-13T21%3A21%3A24Z&sks=b&skv=2018-11-09&sig=ZBZMmaQMjktRg78vdooYElOeqC9oeFaKLAMhcC8Muzg%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDgxMSwibmJmIjoxNzcxMDE0NTExLCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.pD6v_t91El_eSWss8vBDpTeKGbcCpt1qJy_ifrmEFkM&response-content-disposition=attachment%3B%20filename%3DBAMParser.exe&response-content-type=application%2Foctet-stream",
            FileName = "BamParser.exe",
            LocalPath = Path.Combine(toolsFolder, "BamParser.exe")
        });

        Tools.Add(new ExternalTool
        {
            Name = "Registry Explorer",
            Description = "Продвинутый обозреватель реестра",
            OfficialDownloadUrl = "https://download.ericzimmermanstools.com/net9/RegistryExplorer.zip",
            FileName = "RegistryExplorer.zip",
            LocalPath = Path.Combine(toolsFolder, "RegistryExplorer.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "JumpList Explorer",
            Description = "Криминалистический анализ JumpList Windows",
            OfficialDownloadUrl = "https://download.ericzimmermanstools.com/net9/JumpListExplorer.zip",
            FileName = "JumpListExplorer.zip",
            LocalPath = Path.Combine(toolsFolder, "JumpListExplorer.zip")
        });

        Tools.Add(new ExternalTool
        {
            Name = "Recaf",
            Description = "Редактор Java-байткода",
            OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/696482446/03ad570e-b04d-4a8e-8273-778ee819a564?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A28%3A49Z&rscd=attachment%3B+filename%3Drecaf-cli-0.8.8.jar&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A28%3A29Z&ske=2026-02-13T21%3A28%3A49Z&sks=b&skv=2018-11-09&sig=7KiRyXvgmYgelpJyv%2BICjjcM1rD9K0KLg9tj%2BrzrzBM%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDk0MiwibmJmIjoxNzcxMDE0NjQyLCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.9szOh_QancGySQIWNA-VsqVwlfmY6uzRbmBy5hYykX8&response-content-disposition=attachment%3B%20filename%3Drecaf-cli-0.8.8.jar&response-content-type=application%2Foctet-stream",
            FileName = "recaf.jar",
            LocalPath = Path.Combine(toolsFolder, "recaf.jar")
        });

        Tools.Add(new ExternalTool
        {
            Name = "MinecraftVersionChecker",
            Description = "Проверка версии Minecraft",
            OfficialDownloadUrl = "https://github.com/HolyWorldWEB/VersionChecker/releases/download/v1.0.0/MinecraftVersionChecker.exe",
            FileName = "MinecraftVersionChecker.exe",
            LocalPath = Path.Combine(toolsFolder, "MinecraftVersionChecker.exe")
        });
    }

    private void CheckExistingFiles()
    {
        foreach (var tool in Tools)
        {
            tool.IsInstalled = !string.IsNullOrEmpty(tool.LocalPath) && File.Exists(tool.LocalPath);
            tool.Status = tool.IsInstalled ? "Готово" : "Не скачано";
        }
    }

    private async Task LaunchToolAsync(object? parameter)
    {
        if (parameter is not ExternalTool tool) return;
        await _toolService.LaunchAsync(tool, _cts.Token);
    }
}