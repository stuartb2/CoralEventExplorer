# Publishing a GitHub release

GitHub is the **distribution** target for Coral Event Explorer (the installer and
portable zip are attached to GitHub releases). It is not the primary source-control
system, so everything GitHub-specific is kept here, separate from the general build.

Releases are tagged `coral-vX.Y.Z` and versioned independently of upstream Service Bus
Explorer. The in-app "new version available" check reads this repo's latest release, so
publishing a release is what notifies existing users.

## Option A — one-click build and publish (recommended)

The `github-release` workflow builds the app, compiles the installer and publishes the
release entirely on GitHub:

1. Go to the [Actions tab](https://github.com/stuartb2/CoralEventExplorer/actions).
2. Select **GitHub - build and publish release**.
3. Click **Run workflow**, enter the version (e.g. `1.2.0`) and run it.

It produces `coral-v<version>` with `CoralEventExplorerSetup-<version>.exe` and
`CoralEventExplorer.zip` attached. No local build required.

> Commit any version bumps to the source first if you also want the in-repo
> csproj/installer defaults to match; the workflow overrides the version at build time
> regardless.

## Option B — local build and publish

Used by the `/github-coral-release <version>` command:

1. Bump `<Version>`, `<AssemblyVersion>`, `<FileVersion>` in
   `src/ServiceBusExplorer/ServiceBusExplorer.csproj` and `MyAppVersion` in
   `installer/CoralEventExplorer.iss`.
2. Build Release and confirm `CoralEventExplorer.exe`'s file version matches.
3. Compile `installer/CoralEventExplorer.iss` with Inno Setup (`ISCC.exe`).
4. Zip the Release output (excluding `*.pdb`/`*.xml`).
5. `gh release create coral-v<version> --target coral-event-explorer <installer> <zip>`.

## Repository workflows

- `build-test` — CI build and unit tests.
- `upstream-watch` — opens an issue when upstream releases something newer than
  `upstream-version.txt`.
- `github-release` — the dispatch workflow above.

The original upstream automation (WinGet submission, publish, tag/PR handlers) has been
removed from this branch; do not reintroduce it on upstream merges.
