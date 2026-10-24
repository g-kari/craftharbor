# 0.3.1-preview

Keyboard accessibility patch for the 0.3 preview. Server-list and publishing dialogs now close with Escape. Import dialogs retain their existing cancel button behavior, including cancellation while a download is in progress. Read-only tables use Tab to move to the next control while arrow keys remain available for row selection. Styling preserves explicit accessible names on data grids instead of replacing them with a generic table name.

The 0.3 features and operating constraints below remain unchanged. Updating from the legacy manager to the persistent host still requires one save-and-stop; no running server is migrated automatically.

# 0.3.0-preview

General-purpose, multi-server preview of CraftHarbor.

This version adds independent persistent server/tunnel processes with UI reconnection, HTTPS server-pack ZIP import with progress and cancellation, a pinned Create: Astral preset, selectable playit/ngrok/direct/external-tool publishing, voice UDP detection and configuration, and per-user setup/uninstall programs. Input fields, choices, checkboxes and number controls share the application's design; operational pages use a larger editing area.
The console can show all records, Minecraft only, public connection logs only, or manager messages only. Its source choice is remembered per workspace, and switching sources preserves recent records in a bounded buffer. Saving logs exports the currently displayed view.

Body text, inputs, buttons and chart labels use a 12pt baseline (approximately 16px at 96dpi). Input heights and action widths follow text size, and narrow console toolbars wrap. Publishing settings show only the fields for the selected provider and keep address-query feedback in the dialog. Dialogs inherit the application's large-text and high-contrast choices.

Per-server performance settings now choose conservative view/simulation distances from logical CPU share, safe heap budget, MOD/plugin presence and estimated concurrent players. Automatic Java options inspect the selected 64-bit runtime, use compatible G1 settings on HotSpot, and account for CPU sharing across registered servers. Manual modes remain available. These are starting policies rather than benchmark-proven optimal settings. Changes apply on the next launch, with properties backed up and active-port writes rejected. See [performance behavior](PERFORMANCE.md).

The CPU/RAM graph updates its rolling time bounds and RAM scale after samples are added or removed, shows a marker for the first sample, and explains missing/stopped data. Short initial histories use a shorter axis before expanding to five minutes.

Read-only playit agent credentials cannot create public tunnels. The application retrieves existing addresses and opens the official setup page for creation. ngrok TCP does not carry UDP voice traffic. External port tools are launched for the operator to configure; successful routing is not assumed.

Renamed from Astral Server Manager. The application title, executable, product metadata, repository and release assets now use CraftHarbor. The new block-and-water icon represents independent server worlds managed in one harbor. Existing workspace data is retained; close the old manager before upgrading.

This download contains the manager, persistent host, icon, public preset metadata and documentation. Minecraft, Java, MOD packs, playit, user worlds and credentials are not included.

Features include local server controls, a metrics dashboard, console/player management, backup and restore, dedicated-PC memory budgeting, accessible navigation, participant instructions and optional Discord webhook notifications.

This preview adds per-workspace server profiles, configurable Minecraft/pack metadata and participant instructions, JAR/argument-file launch modes, Java selection, port settings, optional spark/FTB capabilities, and registered server switching or concurrent windows. Heap budgets protect other managed servers' reserved growth headroom.

## Verification

The GitHub Actions build runs offline checks for memory policy, webhook URL validation, mention suppression, DPAPI encryption and archive path traversal rejection.
Additional checks cover distance and Java policies, installed Java accepting GC/CPU flags in a small isolated version probe, 600-sample graph rollover, gaps and first samples, pending/manual distance settings, active-port protection, backup preservation, and the console at 1024×720 in normal and enlarged/high-contrast modes.
It also tests a fake child process surviving disposal/recreation of the UI, command/log/metric transport through the host, secure ZIP extraction, and voice configuration detection/backup/idempotence. Synthetic UI captures cover Vanilla, modpacks and argument-file launch profiles, including import and publishing dialogs.
These checks are not an end-to-end test of Minecraft, public tunneling, accessibility tools or real Discord delivery.

On 2026-10-25, the operator manually installed 0.3.1-preview on the development PC; the installed manager and host matched the CI artifacts. After an approved maintenance window and a verified full backup, the existing Create: Astral 2.1.5b / Minecraft 1.18.2 server started under Java 17 through the persistent host. Automatic settings selected a 4 GiB initial heap, a 38 GiB maximum heap on the final restart, G1 flags, view distance 10 and simulation distance 6. These values depend on this PC and are recalculated on launch. With zero players online, the installed dashboard showed TPS 20.0 and median MSPT 0.5 ms, plus live CPU/RAM; the graph rendered samples from the real JVM. Closing the manager and reopening its installed Start-menu entry preserved the same game and tunnel process identities and host session, while Minecraft status remained reachable through the public playit address. A subsequent save-and-stop saved all dimensions and exited Java with code 0; the manager then restarted the server successfully and public status passed again. The server was left running. Previous gameplay had been confirmed by the operator, but player login after this migration was not retested. Real voice UDP and new ngrok routing remain unverified. UI fixtures keep their registry inside synthetic workspaces, away from the operator's list. Native screen-reader behavior and Windows display scaling above 100% remain unverified.

## Signing status

**Unsigned preview.** Smart App Control may block this application. Code signing has not been approved or provided by SignPath.
See the [Code signing policy](../CODE_SIGNING_POLICY.md). No security-policy changes or protection bypass are part of this application.
