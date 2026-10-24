# CraftHarbor UI — lighthouse and harbor

The approved October 2026 app design takes precedence over the former navy/mint design. C#, Windows Forms and .NET Framework 4.8 remain the implementation.

## Shared visual system

`HarborTheme` owns the palette: cream header #F7EED8, navy workspace #14212A, panels #1E303A, green primary/selected controls #345F2C, wood trim #A67C4A, body text #F2F0E6. Muted text is #BBCBD0; borders #506976; focus #F2D28A. The primary green is a surface, not small text on navy. Selection also has an inset border and light vertical marker. Keyboard focus has a dotted outline.

`ActionButton`, `SurfacePanel`, `SurfaceTable`, `HarborFrame`, `InputFrame`, `HarborChoice`, `HarborCheckBox` and `HarborNumber` and `HarborTimeField` provide shared treatment across pages and dialogs. Frames are thin and corners modestly pixel-shaped. Pixel artwork is reserved for the logo and decoration; no rendered mockup is used as a UI background.

Yu Gothic UI is used for Japanese text and normal readable fonts for metrics, input and charts. Body text, buttons, input and chart labels are at least 12pt, with a 1.16× large-text setting. The native Windows title bar, resize borders and normal window controls remain intact; the cream masthead is part of the client area.

## Navigation and hierarchy

The sidebar retains the current server, server list, nine page destinations, large text and high contrast. Its navigation scrolls when needed. The harbor illustration is omitted when height is insufficient and in high contrast.

The dashboard has the server identity and main actions, independent game/public-connection controls, shareable address, four primary metrics, three secondary metrics, CPU/RAM history and recent logs, then metric actions. The chart and recent logs are side by side at wide widths and stacked at narrower widths. The dashboard scrolls as a whole. Forms and table pages retain their working area and wrap action rows instead of forcing identical card layouts.

## Data and states

All identity, version, provider, endpoint and metric values come from existing workspace/runtime data. Public provider names are never fixed to playit. Server and tunnel running states are independent; a stopped server with a running tunnel enables server start and tunnel stop.

Recent logs use the same bounded `logRecords` collection as the console and show its last ten records across all sources, independent of the console filter. An empty view explains that no logs have arrived. No separate collector or fake runtime data is added.

The existing metric cadence, staleness rule, 300-point/five-minute chart window and CSV source are unchanged. Stopped/unavailable metric values remain —. Retained graph points explicitly say they were collected before stopping. Startup, saving/stopping, missing addresses, unavailable integrations and failures keep their descriptive text.

## Accessibility and verification

Alt+1–9, Ctrl+L, F1, native Tab navigation and dialog Escape behavior remain. Focus and selected state do not rely on color alone. High contrast removes decorative art, uses white/black controls, and updates tables, chart text and distinct graph series. Font scaling also applies to the chart.

Cloud Windows CI uses the repository build/core/integration/UI checks. Synthetic captures cover nine pages, normal/narrow/large/high-contrast layouts, publishing methods and dialogs, plus independent status combinations and recent-log behavior. Size/font scaling simulations are distinct from physical high-DPI display tests; native multi-monitor transitions and screen-reader output must not be claimed from synthetic captures. See [UI_VERIFICATION.md](UI_VERIFICATION.md) for observed results, native screenshots and remaining limitations. Layout work is coalesced after native resize messages settle so narrow header actions cannot cover profile metadata.
