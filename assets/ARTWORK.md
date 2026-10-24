# CraftHarbor artwork

The repository's MIT license applies to the original CraftHarbor artwork. Copyright (c) 2026 CraftHarbor contributors.

- `lighthouse-ui.png`: original AI-generated promotional-site lighthouse, from [g-kari/craftharbor-site](https://github.com/g-kari/craftharbor-site), revision `e587c600ec17971682b1a0569aefc178c0a68d98`, source `public/assets/lighthouse-128.png`.
- `harbor-ui.png`: retained 480-pixel-wide promotional-site harbor asset; no longer used in the app's sidebar.
- `harbor-tall.png`: newly generated transparent lighthouse, cabin, crane and island illustration for the taller sidebar, 2026-10-25. Generated independently; not a screenshot or UI background.
- `harbor-icons.png`: newly generated transparent 24-icon atlas, 2026-10-25. The native renderer selects alpha-trimmed source rectangles, preserving the original atlas file. Includes server, public connection, dashboard, console, people, settings, backup, clock, mods, guide, community, CPU, RAM, TPS, MSPT, disk, copy, save, link, folder, play, stop, refresh and lighthouse. The community glyph is an original game/chat symbol, not the official Discord trademark.
- The wordmark, wood grain, beveled frames and state markers are native vector/pixel drawing. No additional font or icon package is required.

The lighthouse, tall harbor and atlas are embedded into both binaries. No controls, text, credentials, game textures or user worlds are baked into artwork. Controls remain native and operable. Decoration is excluded from keyboard navigation. High-contrast mode hides harbor decoration and uses monochrome operation symbols.

`tools/prepare-art.ps1` only reproduces the two original promotional assets. It does not replace the new atlas or tall harbor.
