# Coral Event Explorer

**Fork author:** [Stuart Bishop](https://github.com/stuartb2)

Coral Event Explorer is a fork of [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer)
by Paolo Salvatori, tailored for working with **Coral** Service Bus namespaces and their
CloudEvents-style messages.

**[CORAL.md](CORAL.md) describes everything that differs from the standard Service Bus Explorer**,
including:

- Topics-only entity tree with the Coral events path auto-expanded
- Double-click a subscription to load its newest messages (Shift+double-click for its dead-letter queue), with chronological paging back
- A Coral Payload tab decoding the CloudEvents `data_base64` field as foldable, formatted JSON
- Free-text search across message bodies, with an optional checkbox to also search decoded payloads
- ZIP message inspector pre-selected everywhere, Coral branding, side-by-side install

## Installation

Download from the [releases page](https://github.com/stuartb2/CoralEventExplorer/releases):
either the per-user installer (`CoralEventExplorerSetup-x.y.z.exe`, no admin rights needed)
or the portable zip. Requires .NET Framework 4.7.2.

## Building

Open `src\ServiceBusExplorer.sln`, or run
`dotnet build src\ServiceBusExplorer\ServiceBusExplorer.csproj -c Release`.
The installer is built from `installer\CoralEventExplorer.iss` with
[Inno Setup](https://jrsoftware.org/isinfo.php).

## Upstream

All credit for the foundation goes to the original
[Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) by
Paolo Salvatori and its [contributors](https://github.com/paolosalvatori/ServiceBusExplorer/graphs/contributors) —
if you need the full feature set (queues, relays, Event Hubs, Notification Hubs), use the
original. The `main` branch of this repository mirrors upstream; the Coral changes live on
the `coral-event-explorer` branch. Same license as upstream: see [LICENSE.txt](LICENSE.txt).
