# Xecrets Ez for Mobile

Password protect files with strong encryption on your phone or tablet.

Xecrets Ez is our easy-to-use file encryption app. This repository contains the Android and iOS
apps, bringing the same simple approach to file encryption to mobile devices. The apps are
entirely free and open source, with no premium features, subscriptions or feature restrictions.

Use Xecrets Ez to encrypt any type of file and to decrypt encrypted files when you need them.
Depending on the platform and file type, decrypted files can be viewed directly inside the app.
Text files can also be edited in the app and securely re-encrypted when you save them. In-process
editing is intentionally limited to text files.

Any type of file can also be opened in a compatible application installed on the phone or shared
using the capabilities provided by Android or iOS and the applications installed there.

## Development Status

The Xecrets Ez mobile apps are currently in development and available for closed beta testing. We
are looking for beta testers who want to help us test the apps on Android and iOS and provide
feedback before the public release.

If you are interested in participating in the beta program, please
[contact Axantum support](https://www.axantum.com/support).

## Support Development

The mobile apps are entirely free, but developing, testing and maintaining them still takes time
and costs money. We suggest the [Xecrets Ez desktop app](https://www.axantum.com/xecrets-ez) as a
useful companion on Linux, macOS and Windows.

Even if you mainly use the mobile apps, we certainly appreciate a
[subscription to the desktop app](https://www.axantum.com/pricing). It helps support the continued
development and maintenance of both the mobile and desktop Xecrets Ez applications.

## No Internet, No Servers

The mobile apps work entirely locally on your device. They never connect to the Internet: not
during normal operation, not for analytics and not for crash analysis. There are no servers to
depend on, no usage tracking and no crash reports sent anywhere. Simply never.

This is the same zero-knowledge approach as the
[Xecrets Ez desktop app](https://www.axantum.com/xecrets-ez), its big brother for Linux, macOS and
Windows. Your files and passwords stay under your control.

## The Xecrets Family

The mobile apps are part of the same family as the other Xecrets projects:

- [xecrets-net](https://github.com/axantum/xecrets-net) contains the portable open source .NET
  encryption library used directly by the mobile apps.
- [Xecrets Cli](https://github.com/xecrets/xecrets-cli) uses the same library to provide a free,
  open source command line tool for people, scripts and software.
- [Xecrets Ez](https://www.axantum.com/xecrets-ez) for the desktop has a proprietary graphical
  user interface, while all encryption and decryption is performed through the open source
  Xecrets Cli.

The shared encryption foundation keeps encrypted files compatible across the Xecrets applications
and with AxCrypt 2.x. Encrypt a file on one supported device and decrypt it on another, without
tying the file to a particular app, account or storage provider.

## Free and Open Source

The Xecrets Ez mobile apps are free software, licensed under the
[GNU GPL Version 3 or later](https://www.gnu.org/licenses/). The complete source code is available
in this repository. There is nothing to buy and no paid edition is required to unlock encryption,
decryption, viewing, text editing, opening or sharing.

Copyright and license notices for incorporated third-party material are listed in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## Supported Platforms

Android and iOS are the primary platforms. The shared .NET MAUI codebase also includes Mac
Catalyst and Windows targets for development and debugging.

## Privacy

Xecrets Ez does not collect or transmit personal data. See the
[Privacy Policy](PRIVACY.md) for details about local file processing, device backup, crash
reports and operating-system sharing features.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and how to propose changes.

## Contact

Contact us via our [support site](https://www.axantum.com/support) or through GitHub.
