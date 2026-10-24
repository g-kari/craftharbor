# Privacy policy

CraftHarbor does not include analytics, tracking, advertising, or an automatic update checker.
It does not transmit information unless requested by the user or the person operating it.

## Local data

The selected workspace holds server files, logs, settings, world backups, and optional CSV exports.
The selected folder path is remembered under `%LOCALAPPDATA%/CraftHarbor/workspace.txt`.
The same local directory holds `servers.json` (registered workspace paths) and `memory-reservations.json` (workspace, process ID, process start time and heap budget for coordination). These files are not uploaded or included in releases.
On first use after the rename, existing `workspace.txt` and `servers.json` files from the former `%LOCALAPPDATA%/AstralServerManager` directory are copied locally if their CraftHarbor counterparts do not exist. Legacy files are preserved.
The application reads process CPU and working-set memory, system physical/available memory and disk free space.

Discord webhook URLs are encrypted with Windows DPAPI for the current user and stored in `discord-webhook.dpapi`.
Other Discord options are stored in `discord-settings.json` without the URL. No webhook is preconfigured.
playit maintains its own credential file in the user-controlled workspace. Neither file belongs in source control or support attachments.
Connection choices, selected tool paths and public voice addresses are stored in `connection-settings.json` in the workspace. Import receipts retain only the download host, archive SHA-256 and timestamp; they omit signed URL queries. A bounded ring of runtime logs stays in the local host process for reconnecting the manager.
The console's selected source filter is stored as a number in `manager-log-filter.txt`. Filtering changes the displayed/exported view; the manager retains a bounded local in-memory buffer so sources can be switched without losing recent messages.

## Optional network actions

When enabled and configured by the operator, Discord notifications send the server name and selected lifecycle/backup results to that operator's webhook channel.
These notifications do not include player names, full logs, private addresses, or credentials. Discord mentions are disabled.
The manual test button sends a test notification only after the operator confirms it.

The application can launch the separately installed playit agent. That agent connects to playit's service and forwards game traffic as configured by the operator.
When playit is selected and an existing credential is present, the manager queries `https://api.playit.gg/v1/agents/rundata` approximately every 30 seconds to find this server's public TCP and voice UDP addresses. The credential is sent only to that fixed HTTPS API with redirects disabled. Creating tunnels with a read-only agent credential is not supported; the manager opens the official setup page.
The operator can instead launch a separately installed ngrok agent, use direct port forwarding, or open a chosen external port tool. These programs have their own terms and network behavior. The manager does not change Windows protection settings or router settings automatically.
Requested server ZIP downloads follow HTTPS redirects, use a temporary workspace and validate a supplied or preset SHA-256. The Create: Astral preset also downloads its pinned missing MOD from ForgeCDN and verifies its SHA-256. No downloaded startup script is automatically executed.
The application also starts the separately installed Minecraft/modpack server. That server and its mods have their own network behavior, authentication, and privacy terms.
Links opened from the application use the operating system's default browser.

External service policies:

- [Discord privacy](https://discord.com/privacy)
- [playit privacy](https://playit.gg/privacy-policy/)
- [ngrok privacy](https://ngrok.com/privacy)
- [Microsoft privacy](https://privacy.microsoft.com/privacystatement)
- [Create: Astral source and documentation](https://github.com/Laskyyy/Create-Astral)

Signing approval, if granted in the future, will involve submitting application build artifacts to the signing provider. User workspaces and credentials are excluded from release packages.

## Installation and removal

The per-user installer adds the application, Windows installed-apps entry and Start menu shortcut. A desktop shortcut is optional. Removing the app deletes its installed files and shortcuts; operator workspaces, worlds, backups and private settings are retained. Stop servers and managed connections before uninstalling or updating the host program.
