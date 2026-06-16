# Coral Event Explorer

**Fork author:** [Stuart Bishop](https://github.com/stuartb2)

Coral Event Explorer is a fork of [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer)
by Paolo Salvatori, tailored for working with **Coral** Service Bus namespaces and their
CloudEvents-style messages. All upstream functionality is still present — this fork changes
defaults, adds Coral-specific tooling, and trims the UI to the parts Coral users actually use.

It installs and runs **side by side** with a stock Service Bus Explorer: the executable,
configuration file and product identity are all distinct (`CoralEventExplorer.exe`).
Saved connections are shared with stock SBE via the common user settings file
(`%APPDATA%\Service Bus Explorer\UserSettings.config`).

## How it differs from standard Service Bus Explorer

| Area | Standard SBE | Coral Event Explorer |
|---|---|---|
| Entity tree | Namespace root with Queues, Topics, Event Hubs, Notification Hubs and Relays | Topics only, with the Topics list as the tree root |
| Tree navigation | Expand via +/− glyph | Single click expands; `previsesystems/events` opens automatically after connecting |
| Loading messages | Messages button → receive dialog → peeks from the **front** (oldest) of the entity | Double-click a subscription to load its **newest** 50 messages instantly; Shift+double-click does the same for its **dead-letter queue**; **◀ Older 50** buttons page backwards in chronological order |
| Message body | Shown as-is | A **Coral Payload** tab (defaulting to half the view width) decodes the CloudEvents `data_base64` field and shows it as formatted, **foldable** JSON, kept in sync with the selected message |
| Searching messages | SQL filter expression over properties; date filter | Additionally: a free-text search box that filters the visible messages to those whose body **or decoded payload** contains the text (incremental, case-insensitive, composes with the existing filters) |
| Searching the payload | n/a | Find box on the Coral Payload tab highlights every match and Enter jumps to the next |
| Saved connections | Flat alphabetical list | **File → Saved Connections** lists pinned favourites first (gold dot), then recently used (grey dot), then the rest alphabetically; right-click an entry to pin/unpin it |
| Message inspectors | "Select a BrokeredMessage inspector..." by default | `ZipBrokeredMessageInspector` pre-selected in every send/receive inspector dropdown, so gzip-compressed Coral messages decode transparently |
| Look and feel | Pale blue Windows theme, Azure-blue logo | Coral web app palette (deep teal chrome, lime/amber accents) applied at runtime; yellow logo; teal tree icons, yellow toolbar icons |
| Application identity | `ServiceBusExplorer.exe`, "Service Bus Explorer" | `CoralEventExplorer.exe`, "Coral Event Explorer" |

### Feature details

#### Topics-only tree
Coral users work almost exclusively with topics, so the tree shows the Topics list as its
root with the topics directly beneath it. Queues, Event Hubs, Notification Hubs and Relays
are not loaded at all (which also makes connecting faster). Right-clicking the root offers
the usual Topics actions (create, refresh, export). Event Hubs namespaces keep the stock
tree, since topics do not apply there.

#### Newest-first message loading with paging
Service Bus can only peek forwards, so the fork locates a subscription's last sequence
number with a handful of one-message probes (exponential search plus binary search — fast
even on very deep backlogs), then peeks the page that ends there. Double-clicking a
subscription node shows the newest page in chronological order (oldest at the top);
Shift+double-clicking does the same for the subscription's dead-letter queue. Each press
of **◀ Older 50** (available above both the messages and dead-letter lists) loads the page
immediately preceding the oldest message shown and prepends it, so the grid grows upwards
while staying in order. Partitioned topics fall back to the standard front peek, because
their sequence numbers are per-partition.

#### Coral Payload tab
The right-hand side of the message views — defaulting to half the view's width — is a tab
control: **Coral Payload** (default) and **Properties** (the stock system/custom property
grids). The payload tab base64-decodes the top-level `data_base64` field of the message
body and pretty-prints it as JSON with syntax highlighting; objects and arrays are
**foldable** from the +/- gutter so uninteresting sections can be collapsed. It offers its
own Copy Body button and a find-with-highlight box. Rendering is debounced and runs off
the UI thread, so scrolling quickly through the message list stays fluid. Messages without
a `data_base64` field simply show an empty payload panel.

#### Body search
Every message list (messages and dead-letter views of queues and subscriptions) has a
search strip above the grid. Type to filter — after a short pause the visible messages are
narrowed to those whose body text *or decoded payload* contains the search text. Matching
runs on a background thread and each message's searchable text is cached, so refining a
search is instant. Clearing the box restores the full list. The search stacks with the
stock SQL-filter and date-range filters.

#### Pinned & recent connections
The **File → Saved Connections** menu — the usual way to open an existing connection —
is ordered so the connections you care about are at the top: pinned favourites first
(marked with a gold dot), then the connections you used most recently (grey dot), then the
rest alphabetically. Left-click connects as before; **right-click** any entry to pin or
unpin it. The most-recently-used connection is remembered automatically each time you
connect. Pinned and recent lists are persisted in the configuration file
(`coralPinnedConnections` / `coralRecentConnections`), so the order survives restarts. The
first five entries keep the Ctrl+1…5 shortcuts, so your favourites get them.

## Configuration

These settings live in the `appSettings` section of `CoralEventExplorer.exe.config`:

| Key | Default | Purpose |
|---|---|---|
| `topicsOnlyTree` | `true` | Set `false` to restore the full stock entity tree |
| `defaultExpandTopicPath` | `previsesystems/events` | Topic path (or name prefix) opened automatically after the tree loads; empty disables |
| `doubleClickPeekMessageCount` | `50` | Page size for the double-click peek and the Older button |

Tip: in *Options → config file*, choose the **user config** so connections you save from
the UI persist in `%APPDATA%\Service Bus Explorer\UserSettings.config` and survive
upgrades that replace the application folder.

## Installation

Grab either asset from the [releases page](https://github.com/stuartb2/CoralEventExplorer/releases):

- `CoralEventExplorerSetup-x.y.z.exe` — per-user installer (no admin rights), Start-menu
  entry and optional desktop shortcut, uninstall via Settings → Apps.
- `CoralEventExplorer.zip` — portable; unblock, extract anywhere, run
  `CoralEventExplorer.exe`.

Requires .NET Framework 4.7.2 (preinstalled on current Windows 10/11).

## Building

Open `src\ServiceBusExplorer.sln` (or `dotnet build src\ServiceBusExplorer\ServiceBusExplorer.csproj -c Release`).
To produce the installer, compile `installer\CoralEventExplorer.iss` with
[Inno Setup](https://jrsoftware.org/isinfo.php) after a Release build.

## Relationship to upstream

The `main` branch mirrors upstream Service Bus Explorer; all Coral changes live on the
`coral-event-explorer` branch, deliberately kept small and additive (most features live in
`src/ServiceBusExplorer/UIHelpers/CoralHelper.cs` and `CoralTheme.cs`) so upstream updates
merge cleanly. Licensed under the same terms as upstream — see [LICENSE.txt](LICENSE.txt).
