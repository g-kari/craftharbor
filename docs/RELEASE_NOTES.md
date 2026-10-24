# 0.3.0-preview

General-purpose, multi-server preview of CraftHarbor.

This version adds independent persistent server/tunnel processes with UI reconnection, HTTPS server-pack ZIP import with progress and cancellation, a pinned Create: Astral preset, selectable playit/ngrok/direct/external-tool publishing, voice UDP detection and configuration, and per-user setup/uninstall programs. Input fields, choices, checkboxes and number controls share the application's design; operational pages use a larger editing area.

Read-only playit agent credentials cannot create public tunnels. The application retrieves existing addresses and opens the official setup page for creation. ngrok TCP does not carry UDP voice traffic. External port tools are launched for the operator to configure; successful routing is not assumed.

Renamed from Astral Server Manager. The application title, executable, product metadata, repository and release assets now use CraftHarbor. The new block-and-water icon represents independent server worlds managed in one harbor. Existing workspace data is retained; close the old manager before upgrading.

This download contains the manager and its icon only. Minecraft, Java, MOD packs, playit, user worlds and credentials are not included.

Features include local server controls, a metrics dashboard, console/player management, backup and restore, dedicated-PC memory budgeting, accessible navigation, participant instructions and optional Discord webhook notifications.

This preview adds per-workspace server profiles, configurable Minecraft/pack metadata and participant instructions, JAR/argument-file launch modes, Java selection, port settings, optional spark/FTB capabilities, and registered server switching or concurrent windows. Heap budgets protect other managed servers' reserved growth headroom.

## Verification

The GitHub Actions build runs offline checks for memory policy, webhook URL validation, mention suppression, DPAPI encryption and archive path traversal rejection.
It also tests a fake child process surviving disposal/recreation of the UI, command/log/metric transport through the host, secure ZIP extraction, and voice configuration detection/backup/idempotence. Synthetic UI captures cover Vanilla, modpacks and argument-file launch profiles, including import and publishing dialogs.
These checks are not an end-to-end test of Minecraft, public tunneling, accessibility tools or real Discord delivery.

On the development PC, the renamed manager started an existing Create: Astral server using Java 17, observed the ready state and live TPS/MSPT/process memory, and stopped the server gracefully. Real Discord delivery and an external player's connection have not been tested. UI fixture checks keep their server registry inside the synthetic workspace so they do not register fake servers in the operator's list.

## Signing status

**Unsigned preview.** Smart App Control may block this application. Code signing has not been approved or provided by SignPath.
See the [Code signing policy](../CODE_SIGNING_POLICY.md). No security-policy changes or protection bypass are part of this application.
