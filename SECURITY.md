# Security Policy

Xecrets Ez for Mobile is an encryption application. We take reports of security
vulnerabilities seriously and appreciate the effort of anyone who investigates and
reports them responsibly.

## Supported Versions

The apps are currently in closed beta. Only the latest published build (per platform)
is supported with security fixes. There are no older major versions to maintain yet.

## No Telemetry, No Servers

As described in the [README](README.md#no-internet-no-servers), the apps never connect
to the Internet on their own and have no backend of their own. The relevant attack
surface is therefore local to the device: the app itself, the files it processes, and
the platform APIs it calls — not a network service.

The apps do allow the device operating system to include their data in the platform's
own cloud backup, so that a reinstallation or a move to a new device restores the profile
without a manual export. The sensitive material in that backup — the profile key pair —
is encrypted with the profile password, which is never stored alongside it and never
leaves the device. The app itself still makes no network connections of its own; the
backup is performed by the operating system, under the user's platform settings.

## Reporting a Vulnerability

Please do **not** report security vulnerabilities through public GitHub issues, discussions,
or pull requests.

Instead, report them through one of these private channels:

- GitHub's [private vulnerability reporting](https://github.com/xecrets/xecrets-mobile/security/advisories/new)
  for this repository (preferred, if enabled), or
- [Axantum support](https://www.axantum.com/support), noting that the report concerns a
  security vulnerability.

Please include as much of the following as you can:

- A description of the vulnerability and its potential impact.
- Steps to reproduce, including affected platform(s) (Android/iOS), app version, and OS version.
- Any proof-of-concept code, crafted file, or screen recording that demonstrates the issue.

We will acknowledge your report, investigate, and keep you informed as we work on a fix.
Please give us a reasonable amount of time to address the issue before any public
disclosure, and avoid accessing or modifying other users' data while investigating.

## AI Use

AI may be used to help investigate or draft a report, but you are responsible for what you
submit. Verify the vulnerability yourself first: confirm it is real, reproduce it against the
actual app, and make sure the report reflects genuine behavior rather than a plausible-sounding
guess.

Reports that are obviously AI-generated without evident human verification — including
hallucinated vulnerabilities, invented reproduction steps that don't actually reproduce, or
findings against code that doesn't exist in this project — will be rejected immediately without
further consideration.

## Scope

This policy covers the mobile apps in this repository. Vulnerabilities in the shared
encryption library ([xecrets-net](https://github.com/axantum/xecrets-net)) or the format
compatibility with AxCrypt 2.x are in scope too — report them the same way and we will
route them appropriately.
