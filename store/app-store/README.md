# App Store listing assets

Uploaded in App Store Connect under the app's version page.

## App icon

Nothing to keep here. Apple takes the 1024x1024 marketing icon from the uploaded build
rather than from a separate upload, and `MauiIcon` already generates it: the resizetizer
writes `appiconItunesArtwork.png` at 1024x1024 into the `appicon.appiconset` asset
catalog for both the iOS and Mac Catalyst targets, and `actool` compiles it into the app
bundle. It is square, opaque and unrounded, which is what Apple requires, so the store
icon for Apple needs no work beyond keeping the app icon SVGs current.

That is the difference from [`../play-store`](../play-store), where the console wants a
512x512 upload the build never produces.

## Remaining listing assets

Not yet produced. Requirements as of this writing, see the [App Store Connect
specifications][spec] for the authoritative list, which changes with each device
generation:

| Asset | Requirement | Folder |
| --- | --- | --- |
| iPhone screenshots | Up to 10 per display size, at the exact pixel sizes Apple lists | `screenshots/iphone` |
| iPad screenshots | Up to 10, required if the app supports iPad | `screenshots/ipad` |
| Mac screenshots | Up to 10, required for the Mac Catalyst listing | `screenshots/mac` |

[spec]: https://developer.apple.com/help/app-store-connect/reference/screenshot-specifications
