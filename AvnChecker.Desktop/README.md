# AvnChecker Desktop (1.0.0)

WPF desktop client for AvnChecker based on the provided specification.

## Implemented

- one-time code screen with Supabase RPC `validate_code_for_checker`
- main sections: Information, Applications, System, Twinks, Mods, Settings
- system collection: OS, uptime, CPU/GPU, motherboard, VM flag, HWID, EventLog IDs 104/3079
- twink scan from Minecraft-related JSON files (`tlauncher_profiles.json`, `usercache.json`, `accounts.json`, `ias.json`)
- mods scan in `.minecraft`, TLauncher game dir, active client roots, custom roots
- process memory signature scan for inject/cheat markers + DPS tags
- suspicious process scan for `extra.processes`
- tool downloader to `./Tools/{ToolName}`
- report submit to Supabase RPC `register_check`
- local logs and exported report JSON files in `./Logs`

## Configuration

Edit `appsettings.json`:

- `supabase.url`
- `supabase.api_key`
- `supabase.rpc_timeout_seconds`

Default values are placeholders and must be replaced.

## Build and run

```powershell
dotnet build
dotnet run
```

Project target: `net8.0-windows`.
