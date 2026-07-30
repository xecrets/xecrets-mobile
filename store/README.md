# Store assets

Artwork and other material for the app store listings. Nothing here is an asset of the
app: none of it is referenced by `Xecrets.Mobile.csproj`, compiled, or bundled into a
build. It exists only to be uploaded to a store console, by hand or by a future release
workflow.

App assets that *are* built and bundled live under
[`src/Xecrets.Mobile/Resources`](../src/Xecrets.Mobile/Resources).

| Folder | Store |
| --- | --- |
| [`play-store`](play-store) | Google Play Console listing for the Android app |
| [`app-store`](app-store) | App Store Connect listing for the iOS and Mac Catalyst apps |
| [`tools`](tools) | Scripts that generate assets in the folders above |

Each store folder has its own README with that store's requirements and the current
state of its assets.

## Provenance

Prefer generating store artwork from the app's own sources over hand-editing a copy, so
that a change to the app icon or the UI cannot silently leave a store listing showing
something the app no longer looks like. Where an asset is generated, the store README
names the script and the sources it derives from.
