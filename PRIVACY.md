# Privacy policy

Astral Server Manager does not include analytics, tracking, advertising, or an automatic update checker.
It does not transmit information unless requested by the user or the person operating it.

## Local data

The selected workspace holds server files, logs, settings, world backups, and optional CSV exports.
The selected folder path is remembered under `%LOCALAPPDATA%/AstralServerManager/workspace.txt`.
The application reads process CPU and working-set memory, system physical/available memory and disk free space.

Discord webhook URLs are encrypted with Windows DPAPI for the current user and stored in `discord-webhook.dpapi`.
Other Discord options are stored in `discord-settings.json` without the URL. No webhook is preconfigured.
playit maintains its own credential file in the user-controlled workspace. Neither file belongs in source control or support attachments.

## Optional network actions

When enabled and configured by the operator, Discord notifications send the server name and selected lifecycle/backup results to that operator's webhook channel.
These notifications do not include player names, full logs, private addresses, or credentials. Discord mentions are disabled.
The manual test button sends a test notification only after the operator confirms it.

The application can launch the separately installed playit agent. That agent connects to playit's service and forwards game traffic as configured by the operator.
The application also starts the separately installed Minecraft/modpack server. That server and its mods have their own network behavior, authentication, and privacy terms.
Links opened from the application use the operating system's default browser.

External service policies:

- [Discord privacy](https://discord.com/privacy)
- [playit privacy](https://playit.gg/privacy-policy/)
- [Microsoft privacy](https://privacy.microsoft.com/privacystatement)
- [Create: Astral source and documentation](https://github.com/Laskyyy/Create-Astral)

Signing approval, if granted in the future, will involve submitting application build artifacts to the signing provider. User workspaces and credentials are excluded from release packages.
