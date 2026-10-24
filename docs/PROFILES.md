# Server profiles

CraftHarbor is a general-purpose Minecraft **Java Edition** manager. Bedrock and arbitrary game servers are outside the current scope.

Each workspace has its own `server/`, backups, playit credentials and optional `manager-profile.json`. Use **サーバー一覧** to register existing workspaces. **この画面で切替** requires the current server to be stopped. **別ウィンドウで開く** keeps the current server and opens another manager window, allowing concurrent servers.
Duplicate manager windows for one canonical workspace are blocked. Use distinct ports and distinct playit tunnels for concurrent servers.

The registry is stored locally under `%LOCALAPPDATA%/CraftHarbor/servers.json`. Removing a registration does not delete a server or world.
Auto-memory uses the registered server count as a sharing plan and protects live managed processes' reserved heap headroom. Register the servers you plan to run before starting them. A running JVM's maximum cannot be reduced dynamically; restart existing servers to rebalance after adding a profile.

## Launch types

| Type | Examples | Profile configuration |
| --- | --- | --- |
| Executable JAR | Vanilla, Paper, Purpur, Fabric launcher JAR, compatible server packs | `LaunchMode: jar`, `JarFile: path-relative-to-server.jar` |
| Java argument file | Installed Forge / NeoForge using `win_args.txt` | `LaunchMode: argfile`, `ArgumentsFile: libraries/.../win_args.txt` |

The Java installation must match the server's requirements. Set an absolute path to `java.exe` in the profile UI; leaving it blank uses `java.exe` from PATH. The manager does not install a loader, download mods or accept a license on the user's behalf.

The manager adds its initial/max heap settings and `nogui`. Argument files supplied by the server may contain additional Java options. Check them for conflicting memory settings.
Startup paths must stay inside the workspace's `server` folder. Shell/batch commands are not accepted as startup targets.

If no profile exists, the application defaults to a generic Vanilla client and `server.jar`. If exactly one non-installer JAR is present, it uses that filename. It does not infer a MOD pack or Minecraft version.

## Participant instructions

`ClientMode` is `vanilla`, `modpack`, or `custom`. The server name, Minecraft version, pack name/version, optional installation URL and public address are reflected in the generated Discord instructions.

## Optional metrics and extensions

- Process CPU, working-set RAM, uptime, player list and disk space are common features.
- TPS/MSPT require the spark server command. `SparkMode: auto` detects a spark JAR in `mods/` or `plugins/`; use `enabled` for a server with built-in spark, or `disabled` to avoid polling it.
- FTB Backups settings are enabled only if `config/ftbbackups2.json` is present. The manager's stopped-server full backup/restore is independent of that mod.
- A Vanilla server can have no `mods/` or `plugins/` folder.
- The listen port comes from `server.properties` and can be changed in the stopped-server profile settings. Update the playit tunnel to match it.

## Examples

Copy an example to `manager-profile.json` in a workspace, then edit it for the actual server:

- [Vanilla](examples/vanilla.json)
- [Java argument file](examples/java-argfile.json)
- [Create: Astral](examples/create-astral.json), an example configuration rather than the application's default.

The examples do not include any runtime, server or mod binaries.
