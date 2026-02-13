using System.Collections.ObjectModel;
using System.Windows;

namespace HolyChecker.ViewModels;

public sealed class EverythingQueryItem : BaseViewModel
{
    private string _name = string.Empty;
    private string _query = string.Empty;
    private string _copyStatus = string.Empty;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Query { get => _query; set => SetProperty(ref _query, value); }
    public string CopyStatus { get => _copyStatus; set => SetProperty(ref _copyStatus, value); }
}

public sealed class EverythingQueriesViewModel : BaseViewModel
{
    public ObservableCollection<EverythingQueryItem> Queries { get; } = new();
    public AsyncRelayCommand CopyCommand { get; }

    public EverythingQueriesViewModel()
    {
        CopyCommand = new AsyncRelayCommand(CopyToClipboardAsync);

        Queries.Add(new EverythingQueryItem
        {
            Name = "Cheat Clients (exe/jar/zip/rar)",
            Query = @"ext:.exe;.jar;.zip;.rar regex:(impact|wurst|bleach[-_]?hack|aristois|huzuni|skill[-_]?client|nodus|inertia|ares|sigma|meteor|BetterHitReg|atomic|zamorozka|liquid[-_]?bounce|nurik|nursultan|celestial|calestial|celka|expensive|neverhook|excellent|wexside|wild|minced|deadcode|akrien|future|dreampool|vape|infinity|squad|no[-_]?rules|konas|zeus[-_]?client|rich[-_]?client|ghost[-_]?client|rusher[-_]?hack|thunder[-_]?hack|moon[-_]?hack|winner|nova|exire|doomsday|nightware|ricardo|extazyy|troxill|arbuz|dauntiblyat|rename[-_]?me[-_]?please|edit[-_]?me|takker|faker|xameleon|fuze[-_]?client|wise[-_]?folder|net[-_]?limiter|feather|delta|eclipse|venus|jex|hakari|hush|hach|rogalik|catlavan|haruka|wissend|fluger|sperma|vortex|newcode|astra|britva|bariton|bot|player|freecam|bedrock|hotbar|swap|chest|gumball|tweak|entity|crystal|lowdurabilityswitcher|optimizer|viabackwards|viaforge|viaproxy|hitbox|elytra|through|mob|auto|place|health|inventory|x[-_]?ray|clean[-_]?cut|smart[-_]?moving|save[-_]?searcher|world[-_]?downloader|trade[-_]?finder|chorus[-_]?find|inv[-_]?move|chunk[-_]?copy|seed[-_]?cracker|diamond[-_]?sim|forge[-_]?hax|step[-_]?up|client[-_]?commands|camera[-_]?utils|cheat[-_]?utils|universal[-_]?mod|swing[-_]?through[-_]?grass)"
        });

        Queries.Add(new EverythingQueryItem
        {
            Name = "Config Files (recent 14 days)",
            Query = @"ext:.txt;.json;.toml;.yml;.cfg;.properties;.ini | folder: dm:last14days regex:(shellbag|bariton|bot|player|freecam|bedrock|hotbar|swap|chest|gumball|tweak|entity|crystal|optimizer|viabackwards|viaforge|viaproxy|hitbox|elytra|through|mob|auto|place|health|inventory|x[-_]?ray|clean[-_]?cut|smart[-_]?moving|save[-_]?searcher|world[-_]?downloader|trade[-_]?finder|chorus[-_]?find|inv[-_]?move|chunk[-_]?copy|seed[-_]?cracker|diamond[-_]?sim|forge[-_]?hax|step[-_]?up|client[-_]?commands|camera[-_]?utils|cheat[-_]?utils|universal[-_]?mod|swing[-_]?through[-_]?grass)"
        });

        Queries.Add(new EverythingQueryItem
        {
            Name = "AxisAlignedBB / Specific Size",
            Query = @"size:30720 utf8content:net/minecraft/util/math/axisalignedbb | size:9400174"
        });

        Queries.Add(new EverythingQueryItem
        {
            Name = "Cheat Sizes / Patterns",
            Query = @"*.exe size:1566208 | size:22285824 | size:1010176 | size:22433280 | size:348672 | size:352256 | size:782848 | size:6887424 | size:763392 | size:6111 | size:743424 | size:1767424 | size:823808 | size:18126848 | <size:700kb..5mb utf8content:net/minecraftforge/fml/loading/FMLLoader | glowEsp> | <size:14mb..17mb utf8content:D3D11CreateDeviceAndSwapChain|LoadLibraryA>"
        });

        Queries.Add(new EverythingQueryItem
        {
            Name = "Obfuscated Jar Classes",
            Query = @"*.jar size:21kb-10mb utf8content:net/java/s.class utf8content:net/java/f.class"
        });
    }

    private async Task CopyToClipboardAsync(object? parameter)
    {
        if (parameter is not EverythingQueryItem item) return;
        try
        {
            Clipboard.SetText(item.Query);
            item.CopyStatus = "Copied!";
            await Task.Delay(1500);
            item.CopyStatus = string.Empty;
        }
        catch
        {
            item.CopyStatus = "Error";
        }
    }
}