using System.Text.Json.Serialization;

namespace AvnChecker.Desktop.Models;

public sealed class AppConfig
{
    [JsonPropertyName("supabase")]
    public SupabaseSettings Supabase { get; set; } = new();

    [JsonPropertyName("logging")]
    public LoggingSettings Logging { get; set; } = new();

    [JsonPropertyName("interface_language")]
    public string InterfaceLanguage { get; set; } = "ru";

    [JsonPropertyName("custom_client_paths")]
    public List<string> CustomClientPaths { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = [];

    [JsonPropertyName("mods_risk")]
    public ModRiskSettings ModsRisk { get; set; } = ModRiskSettings.CreateDefault();

    [JsonPropertyName("mods_scan")]
    public ModsScanSettings ModsScan { get; set; } = ModsScanSettings.CreateDefault();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Supabase = new SupabaseSettings
            {
                Url = "https://vravdhytgjmvjoksjuyc.supabase.co",
                ApiKey = "sb_publishable_MG2uCAw3bHOA8CXH77s8gw_WxMDislN",
                RpcTimeoutSeconds = 15
            },
            Logging = new LoggingSettings
            {
                Mode = "verbose"
            },
            InterfaceLanguage = "ru",
            ModsRisk = ModRiskSettings.CreateDefault(),
            ModsScan = ModsScanSettings.CreateDefault(),
            Tools =
            [
                new ToolDefinition
                {
                    Name = "Everything",
                    Description = "Быстрый поиск файлов",
                    OfficialDownloadUrl = "https://www.voidtools.com/Everything-1.5.0.1404a.x64.zip"
                },
                new ToolDefinition
                {
                    Name = "Shellbag Analyzer",
                    Description = "Анализ ShellBag артефактов",
                    OfficialDownloadUrl = "https://privazer.com/ru/shellbag_analyzer_cleaner.exe"
                },
                new ToolDefinition
                {
                    Name = "RegScanner",
                    Description = "Сканер реестра Windows",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/regscanner-x64.zip"
                },
                new ToolDefinition
                {
                    Name = "RecentFilesView",
                    Description = "Просмотр недавно открытых файлов",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/recentfilesview.zip"
                },
                new ToolDefinition
                {
                    Name = "BrowserDownloadsView",
                    Description = "История загрузок браузеров",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/browserdownloadsview-x64.zip"
                },
                new ToolDefinition
                {
                    Name = "UsbDriveLog",
                    Description = "Журнал подключений USB-накопителей",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/usbdrivelog.zip"
                },
                new ToolDefinition
                {
                    Name = "LastActivityView",
                    Description = "Последняя активность системы",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/lastactivityview.zip"
                },
                new ToolDefinition
                {
                    Name = "ExecutedProgramsList",
                    Description = "Список ранее запускавшихся программ",
                    OfficialDownloadUrl = "https://www.nirsoft.net/utils/executedprogramslist.zip"
                },
                new ToolDefinition
                {
                    Name = "System Informer",
                    Description = "Продвинутый монитор процессов",
                    OfficialDownloadUrl = "https://netix.dl.sourceforge.net/project/systeminformer/systeminformer-3.2.25011-release-setup.exe?viasf=1"
                },
                new ToolDefinition
                {
                    Name = "Journal Trace",
                    Description = "Трассировка NTFS Journal",
                    OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/899349770/ab6a048b-1554-4866-86ee-9c5cee36bc24?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A24%3A37Z&rscd=attachment%3B+filename%3DJournalTrace.exe&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A23%3A40Z&ske=2026-02-13T21%3A24%3A37Z&sks=b&skv=2018-11-09&sig=ER%2FJ7czQaWAulZUQCXEpG7uiL%2FAt%2F8CP0vqTAsru7rw%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDczNiwibmJmIjoxNzcxMDE0NDM2LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.tAXuaNNQIBL9Wooyqbi7FpykFWbVPiY7W88vqw__-N0&response-content-disposition=attachment%3B%20filename%3DJournalTrace.exe&response-content-type=application%2Foctet-stream"
                },
                new ToolDefinition
                {
                    Name = "SimpleUnlocker",
                    Description = "Разблокировка занятых файлов",
                    OfficialDownloadUrl = "https://simpleunlocker.ds1nc.ru/release/simpleunlocker_release.zip"
                },
                new ToolDefinition
                {
                    Name = "BamParser",
                    Description = "Парсер BAM-активности",
                    OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/890129826/43223953-f0eb-4544-9ba2-bd0a4181d38b?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A21%3A24Z&rscd=attachment%3B+filename%3DBAMParser.exe&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A20%3A44Z&ske=2026-02-13T21%3A21%3A24Z&sks=b&skv=2018-11-09&sig=ZBZMmaQMjktRg78vdooYElOeqC9oeFaKLAMhcC8Muzg%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDgxMSwibmJmIjoxNzcxMDE0NTExLCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.pD6v_t91El_eSWss8vBDpTeKGbcCpt1qJy_ifrmEFkM&response-content-disposition=attachment%3B%20filename%3DBAMParser.exe&response-content-type=application%2Foctet-stream"
                },
                new ToolDefinition
                {
                    Name = "Registry Explorer",
                    Description = "Обозреватель реестра",
                    OfficialDownloadUrl = "https://download.ericzimmermanstools.com/net9/RegistryExplorer.zip"
                },
                new ToolDefinition
                {
                    Name = "JumpList Explorer",
                    Description = "Анализатор JumpList",
                    OfficialDownloadUrl = "https://download.ericzimmermanstools.com/net9/JumpListExplorer.zip"
                },
                new ToolDefinition
                {
                    Name = "Recaf",
                    Description = "Редактор Java-байткода",
                    OfficialDownloadUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/696482446/03ad570e-b04d-4a8e-8273-778ee819a564?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-02-13T21%3A28%3A49Z&rscd=attachment%3B+filename%3Drecaf-cli-0.8.8.jar&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-02-13T20%3A28%3A29Z&ske=2026-02-13T21%3A28%3A49Z&sks=b&skv=2018-11-09&sig=7KiRyXvgmYgelpJyv%2BICjjcM1rD9K0KLg9tj%2BrzrzBM%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3MTAxNDk0MiwibmJmIjoxNzcxMDE0NjQyLCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.9szOh_QancGySQIWNA-VsqVwlfmY6uzRbmBy5hYykX8&response-content-disposition=attachment%3B%20filename%3Drecaf-cli-0.8.8.jar&response-content-type=application%2Foctet-stream"
                },
                new ToolDefinition
                {
                    Name = "MinecraftVersionChecker",
                    Description = "Проверка версии Minecraft",
                    OfficialDownloadUrl = "https://github.com/HolyWorldWEB/VersionChecker/releases/download/v1.0.0/MinecraftVersionChecker.exe"
                }
            ]
        };
    }
}

public sealed class ModsScanSettings
{
    [JsonPropertyName("include_downloads")]
    public bool IncludeDownloads { get; set; } = true;

    [JsonPropertyName("include_desktop")]
    public bool IncludeDesktop { get; set; } = true;

    [JsonPropertyName("include_documents")]
    public bool IncludeDocuments { get; set; } = true;

    [JsonPropertyName("include_onedrive")]
    public bool IncludeOneDrive { get; set; } = true;

    public static ModsScanSettings CreateDefault()
    {
        return new ModsScanSettings
        {
            IncludeDownloads = true,
            IncludeDesktop = true,
            IncludeDocuments = true,
            IncludeOneDrive = true
        };
    }
}

public sealed class ModRiskSettings
{
    [JsonPropertyName("cheat_tokens")]
    public List<string> CheatTokens { get; set; } = [];

    [JsonPropertyName("safe_tokens")]
    public List<string> SafeTokens { get; set; } = [];

    public static ModRiskSettings CreateDefault()
    {
        return new ModRiskSettings
        {
            CheatTokens =
            [
                "akrien",
                "aimassist",
                "aimbot",
                "airjump",
                "antikb",
                "arbuz",
                "ares",
                "aristois",
                "armor hotswap",
                "atomic",
                "autoattack",
                "autoclick",
                "autoclicky",
                "autototem",
                "bariton",
                "baritone",
                "bedrock breaker mode",
                "betterhitreg",
                "bleachhack",
                "bhop",
                "calestial",
                "camerautils",
                "celestial",
                "celka",
                "chest",
                "chunk copy",
                "clean cut",
                "clickcrystals",
                "clicker",
                "clientcommands",
                "criticals",
                "crystal optimizer",
                "cutthrough",
                "dauntiblyat",
                "deadcode",
                "delta",
                "diamond sim",
                "doomsday",
                "double hotbar",
                "dreampool",
                "eclipse",
                "elytra hack",
                "elytra swap",
                "elytrafly",
                "entity outliner",
                "entity xray",
                "esp",
                "expensive",
                "exire",
                "extazyy",
                "fastplace",
                "feather client",
                "forge hax",
                "freecam",
                "future",
                "fuzeclient",
                "hach",
                "hack",
                "hakari",
                "hitbox",
                "hush",
                "huzuni",
                "impact",
                "inertia",
                "infinity",
                "inventory profiles next",
                "inventory walk",
                "inventorysorter",
                "invmove",
                "invtweaks",
                "jex",
                "killaura",
                "konas",
                "librarian trade finder",
                "liquidbounce",
                "lowdurabilityswitcher",
                "meteor",
                "minced",
                "mobhitbox",
                "moonhack",
                "nemo's inventory",
                "neverhook",
                "nightware",
                "nodus",
                "nofall",
                "nova",
                "nurik",
                "nursultan",
                "reach",
                "ricardo",
                "richclient",
                "rogalik",
                "rusherhack",
                "sacurachorusfind",
                "save searcher",
                "scaffold",
                "seed cracker",
                "showinginvisibleplayers",
                "sigma",
                "skill client",
                "smart moving",
                "speedhack",
                "squad",
                "step up",
                "swingthroughgrass",
                "takker",
                "thunderhack",
                "tool swap",
                "topkaautobuy",
                "topkaautodrop",
                "triggerbot",
                "troxill",
                "tweakeroo",
                "vape",
                "vec.dll",
                "velocity",
                "venus",
                "viabackwards",
                "viaforge",
                "viaproxy",
                "wallhack",
                "wexside",
                "wildclient",
                "winner",
                "worlddownloader",
                "wurst",
                "xray",
                "zamorozka",
                "zenithclient",
                "zeusclient"
            ],
            SafeTokens =
            [
                "appleskin",
                "architectury",
                "cloth",
                "controlling",
                "entityculling",
                "fabric",
                "ferritecore",
                "forge",
                "indium",
                "iris",
                "jei",
                "journeymap",
                "lithium",
                "malilib",
                "memoryleakfix",
                "modmenu",
                "mouse",
                "neoforge",
                "nofog",
                "notenoughanimations",
                "optifine",
                "phosphor",
                "rei",
                "sodium",
                "starlight",
                "xaeros"
            ]
        };
    }
}

public sealed class SupabaseSettings
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("rpc_timeout_seconds")]
    public int RpcTimeoutSeconds { get; set; } = 15;
}

public sealed class LoggingSettings
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "verbose";
}

