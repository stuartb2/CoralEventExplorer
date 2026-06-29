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
| Tree navigation | Expand via +/− glyph | Single click on a node expands it; the +/− glyph still toggles normally (so you can collapse); `previsesystems/events` opens automatically after connecting |
| Loading messages | Messages button → receive dialog → peeks from the **front** (oldest) of the entity | Double-click a subscription to load its **newest** 50 messages instantly; Shift+double-click does the same for its **dead-letter queue**; **◀ Older 50** buttons page backwards in chronological order |
| Message body | Shown as-is | A **Coral Payload** tab (defaulting to half the view width) decodes the CloudEvents `data_base64` field and shows it as formatted, **foldable** JSON, kept in sync with the selected message; an embedded-JSON string field (by default `customdata`) is inlined as a nested node rather than an escaped blob |
| Searching messages | SQL filter expression over properties; date filter | Additionally: a free-text search box that filters the visible messages to those whose body contains the text, with an optional **Search payload** checkbox that extends the search to the decoded `data_base64` payload (incremental, case-insensitive, composes with the existing filters) |
| Searching the payload | n/a | Find box on the Coral Payload tab highlights every match and Enter jumps to the next |
| Saved connections | Flat alphabetical list | **File → Saved Connections** lists pinned favourites first (gold dot), then recently used (grey dot), then the rest alphabetically; right-click an entry to pin/unpin it |
| Message inspectors | "Select a BrokeredMessage inspector..." by default | `ZipBrokeredMessageInspector` pre-selected in every send/receive inspector dropdown, so gzip-compressed Coral messages decode transparently |
| Raw message bytes | Body shown only as decoded text / JSON / XML | **Right-click a message → View Raw (Hex)…** opens an offset / hex / ASCII dump, defaulting to the **on-the-wire (compressed)** bytes, with a checkbox to switch to the decompressed body |
| Update checks | Notifies about new **upstream** Service Bus Explorer releases | Checks **this fork's** GitHub releases (`coral-v*` tags) and links to them, so the prompt matches what you actually run |
| About box | Standard SBE about | Adds the fork name, author and description, with a clickable link to the Coral repository |
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

If the decoded payload contains a field holding an *embedded* JSON document as a string
(by default one named `customdata`), that string is parsed and shown as a nested node — the
field becomes a parent with the embedded values as foldable sub-fields — instead of an
escaped one-line blob. This applies wherever the field appears (including nested), and is
left untouched if its value is not valid JSON. The field name is configurable via
`inlineEmbeddedJsonField`; blank disables the behaviour.

#### Body search
Every message list (messages and dead-letter views of queues and subscriptions) has a
search strip above the grid. Type to filter — after a short pause the visible messages are
narrowed to those whose body text contains the search text. A **Search payload** checkbox
(off by default) extends each search to the message's decompressed `data_base64` payload;
because that means decoding every body, it is opt-in, and payloads are decoded only when
the box is ticked. The payload is searched in the same normalized form the Coral Payload
tab shows — pretty-printed with embedded-JSON fields (e.g. `customdata`) inlined — so a
match lines up with what you see rather than the raw escaped string. Matching runs on a background thread and each message's searchable text
is cached, so refining a search is instant. Searches are interruptable — typing again,
pressing Enter, or toggling the checkbox cancels any search still in flight — so a long
decode never blocks the next search. Clearing the box restores the full list. The search
stacks with the stock SQL-filter and date-range filters.

#### Pinned & recent connections
The **File → Saved Connections** menu — the usual way to open an existing connection —
is ordered so the connections you care about are at the top: pinned favourites first
(marked with a gold dot), then the connections you used most recently (grey dot), then the
rest alphabetically. Left-click connects as before; **right-click** any entry to pin or
unpin it. The most-recently-used connection is remembered automatically each time you
connect. Pinned and recent lists are persisted in the configuration file
(`coralPinnedConnections` / `coralRecentConnections`), so the order survives restarts. The
first five entries keep the Ctrl+1…5 shortcuts, so your favourites get them.

#### Raw message view (hex)
Right-clicking a message in any message or dead-letter list adds a **View Raw (Hex)…**
entry that opens an offset / hex / ASCII dump of the body. Because Coral messages arrive
gzip-compressed, the view **defaults to the bytes as they came off the wire** (before the
ZIP inspector decompressed them) so you can see exactly what was transmitted; a **Show
decompressed body** checkbox switches to the uncompressed body when the two differ. The
dump reads from a *clone* of the message, so viewing it never consumes the message or
interferes with resubmitting it. The window title shows which view you are looking at and
the byte count.

#### Update notifications
Stock SBE checks the upstream project's GitHub releases, which would always look "out of
date" against this independently-versioned fork. Coral Event Explorer instead checks
**this fork's** releases (`coral-v*` tags) and links to them, so the "new version
available" prompt only appears for a genuinely newer Coral build. See
[Relationship to upstream](#relationship-to-upstream) for how fork versions are managed.

## Configuration

These settings live in the `appSettings` section of `CoralEventExplorer.exe.config`:

| Key | Default | Purpose |
|---|---|---|
| `topicsOnlyTree` | `true` | Set `false` to restore the full stock entity tree |
| `defaultExpandTopicPath` | `previsesystems/events` | Topic path (or name prefix) opened automatically after the tree loads; empty disables |
| `doubleClickPeekMessageCount` | `50` | Page size for the double-click peek and the Older button |
| `inlineEmbeddedJsonField` | `customdata` | Payload field whose embedded-JSON string value is inlined as a nested node on the Coral Payload tab; blank disables |

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
the `src/ServiceBusExplorer/UIHelpers/Coral*.cs` helper files — `CoralHelper`,
`CoralTheme`, `CoralHexView`, `CoralConnectionList` — with only minimal edits to shared
forms) so upstream updates merge cleanly. Licensed under the same terms as upstream — see [LICENSE.txt](LICENSE.txt).
