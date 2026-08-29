# CI build setup

The workflow in `.github/workflows/ci.yml` runs the unit tests and builds signed,
store-ready packages for Android (`.aab` for Google Play + side-loadable `.apk`) and iOS
(`.ipa` for App Store Connect / TestFlight) on every push.

Android Release builds use full managed trimming and R8 Java shrinking/optimization.
The packages contain only the `arm64-v8a` physical-device ABI; 32-bit ARM and emulator
`x86`/`x86_64` binaries are excluded.

This document lists the one-time setup you must do, and how to keep the pinned tooling
in sync. Nothing sensitive is ever checked into the repository: signing key material and
passwords live in GitHub Actions **secrets**, which are encrypted and masked in logs.
Non-sensitive signing configuration lives in GitHub Actions **variables**. Remember that
in a public repository the build *logs* are public, so anything that should not be
world-readable must be a secret, never hardcoded in the workflow or repository.

## 1. One-time GitHub configuration

All of this is under **Settings → Secrets and variables → Actions** in the
`xecrets/xecrets-mobile` repository.

### 1.1 Repository variables

Create these repository **variables** (not secrets):

| Variable | Content                                                                             |
|---|-------------------------------------------------------------------------------------|
| `BUILD_NUMBER_OFFSET` | Offset added to GitHub's workflow run number to form the store build number         |
| `ANDROID_KEY_ALIAS` | Alias of the signing key inside the upload keystore (e.g. `upload`)                 |
| `APPLE_CODESIGN_KEY` | Exact signing identity name, e.g. `Apple Distribution: Firstname Lastname (TEAMID)` |
| `APPLE_CODESIGN_PROFILE` | Name of the installed provisioning profile, e.g. `Xecrets Ez App Store`             |

`BUILD_NUMBER_OFFSET` is used as follows:

- The workflow calculates the store build number as `BUILD_NUMBER_OFFSET +
  GITHUB_RUN_NUMBER`. GitHub assigns `GITHUB_RUN_NUMBER` automatically and increments
  it for each new run of this workflow; no token or API call is needed.
- The result is used as the Android `versionCode`, the iOS `CFBundleVersion`, and the
  third component of the displayed version `2.4.[build-number]`.
- Both stores require this number to be strictly increasing. Choose the offset so that
  **the offset plus the next workflow run number is higher than every versionCode /
  CFBundleVersion ever uploaded** to Google Play or App Store Connect for this app. If
  there are no previous store builds, an offset such as `100` leaves ample room.
- Never decrease the offset, and do not reset it when bumping the version to 2.5 — the
  stores compare build numbers across versions. A rerun of an existing workflow run
  retains the same `GITHUB_RUN_NUMBER`; a newly dispatched run gets a new one.
- If this workflow is ever replaced in a way that resets its GitHub run-number sequence,
  raise the offset before the next store build so the calculated number remains above
  every previously uploaded build.
- The workflow deliberately fails if the variable does not exist or is empty, rather
  than silently using an unsafe value.

### 1.2 Secrets

Create these repository **secrets** (sections 2 and 3 explain how to produce the values):

| Secret                              | Content                                                                                    |
|-------------------------------------|--------------------------------------------------------------------------------------------|
| `ANDROID_KEYSTORE_BASE64`           | The upload keystore file, base64-encoded                                                   |
| `ANDROID_KEYSTORE_PASSWORD`         | Password of the keystore                                                                   |
| `ANDROID_KEY_PASSWORD`              | Password of the key (same as the keystore password for PKCS12 keystores)                   |
| `APPLE_DIST_CERT_P12_BASE64`        | Apple Distribution certificate + private key (`.p12`), base64-encoded                      |
| `APPLE_DIST_CERT_PASSWORD`          | Password chosen when exporting the `.p12`                                                  |
| `APPLE_PROVISIONING_PROFILE_BASE64` | App Store provisioning profile (`.mobileprovision`), base64-encoded                        |

The provisioning profile does not contain the private signing key, but it is kept with
the restricted signing material as a secret. The key alias, signing identity, and profile
name are non-sensitive configuration and are intentionally repository variables.

`GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (the Google Play publishing credential, section 6.2)
is not a repository secret but an **environment secret** on the `publish-google-play`
environment, so only the publish job can read it.

### 1.3 Repository settings hardening

One-time settings on the repository itself (Settings in the GitHub web UI, or the
`gh` commands shown). They matter especially once the repository is public.

1. **Default workflow permissions: read-only.** Settings → Actions → General →
   Workflow permissions → *Read repository contents and packages permissions*.
   `ci.yml` declares the permissions it needs per job; this makes read-only the
   default for any future workflow that forgets to.

   ```pwsh
   gh api -X PUT repos/xecrets/xecrets-mobile/actions/permissions/workflow `
       -f default_workflow_permissions=read -F can_approve_pull_request_reviews=false
   ```

2. **Secret scanning and push protection.** Settings → Advanced Security →
   enable *Secret scanning* and *Push protection*. Free for public repositories
   (on private ones it requires a paid Advanced Security plan — enable it when
   the repository goes public). Push protection blocks pushes that contain
   recognizable credentials before they ever reach the repository.

   ```pwsh
   gh api -X PATCH repos/xecrets/xecrets-mobile `
       -f 'security_and_analysis[secret_scanning][status]=enabled' `
       -f 'security_and_analysis[secret_scanning_push_protection][status]=enabled'
   ```

3. **Branch protection for `main` and `develop`** (when collaboration or the
   public flip approaches): Settings → Branches → add a ruleset requiring the
   CI check to pass before merging and blocking force pushes.

The `gh` commands require the GitHub CLI (`brew install gh`, then `gh auth login`)
with admin access to the repository.

## 2. Android signing material

Google Play uses *Play App Signing*: Google holds the actual app signing key, and you
sign uploads with an **upload key** that you create yourself. The same upload-key
signature also serves for the side-loadable `.apk`.

1. Create the upload keystore (anywhere with a JDK; `keytool` ships with it):

   ```sh
   keytool -genkeypair -v -keystore xecrets-ez-upload.p12 -alias xecrets-ez-upload -keyalg RSA -keysize 4096 -validity 18262 -dname "CN=Axantum Software AB, O=Axantum Software AB, C=SE"
   ```

   You will be prompted for the keystore password. Modern keystores are PKCS12, where
   the key password is the same as the keystore password. You may be prompted to convert it to PKCS12. If so, do it.

2. Store the values in GitHub:

   ```pwsh
   [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path '.\xecrets-ez-upload.p12').Path)) | Set-Clipboard
   # → paste as ANDROID_KEYSTORE_BASE64
   ```

   Then `ANDROID_KEYSTORE_PASSWORD` = the password, `ANDROID_KEY_PASSWORD` = the same
   password, and the `ANDROID_KEY_ALIAS` repository variable = `xecrets-ez-upload`.

3. Keep `xecrets-ez-upload.p12` and its password backed up privately (password manager /
   offline). Never commit it. If it is ever lost or leaked, Play App Signing lets you
   register a new upload key — a nuisance, not a catastrophe.

4. In the Google Play Console, when enrolling the app, choose Play App Signing and
   register this keystore's certificate as the upload key (the Console guides you;
   `keytool -export -rfc -keystore xecrets-ez-upload.p12 -alias xecrets-ez-upload` produces the
   certificate if asked for it).

## 3. Apple signing material

Prerequisite: an Apple Developer Program membership, with the App ID
`com.axantum.xecrets-ez.app` registered at <https://developer.apple.com>.

### 3.1 Apple Distribution certificate (`.p12`)

1. On your Mac: **Keychain Access → Certificates tab → Certificate Assistant → Request a Certificate From
   a Certificate Authority…**, enter your email, and a Common Name for your own use and select *Saved to disk*, save the
   `.certSigningRequest` file.
2. At developer.apple.com → **Certificates → +** → *Apple Distribution* → upload the
   CSR → download `distribution.cer` → double-click it to install it into your keychain.
3. In Keychain Access (My Certificates), expand the new "Apple Distribution: …"
   certificate so both certificate and private key are selected, right-click →
   **Export 2 items…** → save as `Certificates.p12` with a strong password.
4. Store in GitHub:

   ```pwsh
   [Convert]::ToBase64String([IO.File]::ReadAllBytes('Certificates.p12')) | Set-Clipboard
   # → paste as APPLE_DIST_CERT_P12_BASE64
   ```

   and the export password as `APPLE_DIST_CERT_PASSWORD`.
5. Find the exact identity name for the `APPLE_CODESIGN_KEY` repository variable:

   ```sh
   security find-identity -v -p codesigning
   ```

   Use the full string, e.g. `Apple Distribution: Firstname Lastname (ABCDE12345)`.

Distribution certificates expire after about a year; when that happens, repeat this
section and update the two secrets. Keep the `.p12` backed up privately — the private
key only exists in your keychain and in the exported file.

### 3.2 App Store provisioning profile

1. At developer.apple.com → **Profiles → +** → *App Store Connect* (under
   Distribution) → select the App ID `com.axantum.xecrets-ez.app` → select the
   distribution certificate from 3.1 → name it, e.g. `Xecrets Ez App Store` →
   download the `.mobileprovision` file.
2. Store in GitHub:

   ```pwsh
   [Convert]::ToBase64String([IO.File]::ReadAllBytes('Xecrets_Ez_App_Store.mobileprovision')) | Set-Clipboard
   # → APPLE_PROVISIONING_PROFILE_BASE64
   ```

   and the profile *name* (`Xecrets Ez App Store`) as the `APPLE_CODESIGN_PROFILE`
   repository variable. Supplying the name makes profile selection explicit rather than
   relying on automatic selection when only one matching profile is installed.

The profile expires with the certificate. Regenerate and update the secret when
renewing the certificate.

## 4. Pinned tooling, and keeping it in sync

Nothing floats on "latest"; each pin has one authoritative location:

| Tool                                       | Pinned in                                           | Check locally with                        |
|--------------------------------------------|-----------------------------------------------------|-------------------------------------------|
| .NET SDK                                   | `global.json` → `sdk.version`                       | `dotnet --version` (in the repo root)     |
| .NET workload set (Android/iOS SDKs, MAUI) | `global.json` → `sdk.workloadVersion`               | `dotnet workload --version`               |
| MAUI package version                       | `Xecrets.Mobile.csproj` (`Microsoft.Maui.Controls`) | —                                         |
| Xcode                                      | `.xcode-version`                                    | `xcodebuild -version`                     |
| Runner images                              | `ci.yml` (`ubuntu-24.04`, `macos-26`)               | —                                         |
| Actions                                    | `ci.yml`, full commit SHAs                          | Dependabot PRs (`.github/dependabot.yml`) |
| JDK                                        | `ci.yml` (`setup-java`, Temurin 21)                 | `java -version`                           |

When you update the .NET SDK or workloads locally, update `global.json` in the same
commit (note the sibling repos `xecrets-net`, `xecrets-common` and
`xecrets-localization` pin the same SDK version in their own `global.json` files and
should be moved together). When you update Xcode, update `.xcode-version` — but first
verify the version is installed on the `macos-26` image, listed at
<https://github.com/actions/runner-images>.

The sibling repositories follow the xecrets-mobile branch being built: each sibling is
checked out at the branch with the same name as the mobile branch (so `main` builds
against `main`, `develop` against `develop`, `feature/newstuff` against
`feature/newstuff`), and falls back to `develop` when the sibling has no branch with
that name. To pin a sibling to an exact branch, tag or commit SHA instead, set the
corresponding `XECRETS_*_REF` variable in the `env:` block at the top of `ci.yml` —
a non-empty value is used verbatim and disables the automatic resolution.

R8 is supplied by the pinned .NET Android workload and runs on the configured JDK.
The `InstallAndroidDependencies` target installs the required Android SDK and build
tools, so R8 does not require a separate CI installation step.

### Nightly rebuild on sibling changes

Pushes to the sibling repositories cannot trigger this workflow (GitHub Actions
triggers are repository-local), so a nightly schedule (03:17 UTC) runs the `nightly`
gate job instead. For each of `main` and `develop` it compares the current tip commits
of xecrets-mobile and the resolved siblings against the commits recorded by the
branch's last successful build, and dispatches a real build only when something
changed — an unchanged night costs one ~30-second Ubuntu job and no build number.

The comparison state lives in a small `build-state` artifact that every successful
build uploads; nothing ever writes to repository variables or any other mutable
configuration. A failed build records no state, so the next night automatically tries
again, and an expired artifact (90-day retention) merely causes one redundant build.

## 5. Versioning and branch semantics

- The repository declares `2.4.0` / build `1` in `Xecrets.Mobile.csproj`, so any manual
  build outside CI shows `2.4.0`. CI overrides this to `2.4.[build-number]`.
- To move to 2.5, change only `ApplicationDisplayVersion` to `2.5.0` in the csproj —
  the workflow derives the `2.5` prefix from it. Leave `BUILD_NUMBER_OFFSET` alone.
- The app metadata declares **Xecrets Ez Beta** by default (`IsBeta=true` in the
  csproj), so local builds and CI builds from any branch other than `main` are visibly
  Beta. CI builds from `main` pass `-p:IsBeta=false` and are release builds named
  **Xecrets Ez**. Non-`main` artifacts carry a `-beta` suffix in their names.
- Every push build (any branch) is signed and uses the workflow run number in its build
  number. Runs that do not produce a signed store build can leave harmless gaps in the
  store build-number sequence.

## 6. Using the build output

Artifacts appear on each workflow run page (Actions → the run → Artifacts):

- `XecretsEz-2.4.N[-beta]-aab` — contains `XecretsEz-2.4.N[-beta].aab` to upload to
  the Google Play Console.
- `XecretsEz-2.4.N[-beta]-apk` — contains `XecretsEz-2.4.N[-beta].apk`, which is
  side-loadable on any Android device that allows installing from unknown sources.
- `XecretsEz-2.4.N[-beta]-ios` — contains `XecretsEz-2.4.N[-beta].ipa` and its
  `XecretsEz-2.4.N[-beta].app.dSYM`. Upload the IPA to App Store Connect with the
  **Transporter** app (Mac App Store) or `xcrun altool`/App Store Connect API, and
  retain the matching dSYM for crash symbolication. There is no practical iOS
  side-loading; use **TestFlight** to distribute Beta builds to devices.

Android publishing to Google Play is automated (section 6.2). A sensible future
enhancement is automatic upload to TestFlight too, using an App Store Connect API key;
for now iOS submission stays a deliberate manual step.

### 6.1 iOS testing via TestFlight (App Store Connect)

The `.ipa` is signed for App Store distribution and cannot be installed on a device
directly; TestFlight is the distribution channel for testing.

One-time setup:

1. At <https://appstoreconnect.apple.com> → **My Apps → + → New App**: platform iOS,
   the app name, primary language, the bundle ID registered at developer.apple.com
   (matching `ApplicationId` in the csproj), and an SKU (any internal identifier,
   e.g. `xecrets-ez`).
2. Install **Transporter** from the Mac App Store and sign in with the same
   Apple ID.

Per build:

1. Download the `-ios` artifact from the workflow run, unzip, and drop the `.ipa`
   into Transporter → **Deliver**.
2. The build appears under the app's **TestFlight** tab after a few minutes of
   processing. The first time (and after changing the answer), complete the export
   compliance questionnaire — the app uses standard encryption (AES), so answer the
   encryption questions accordingly; consider the annual US self-classification
   report requirement and, if distributing in France, the French declaration.

Testers:

- **Internal testing** (up to 100 testers who are members of your App Store Connect
  team): TestFlight → Internal Testing → create a group, add testers, enable
  automatic distribution. Builds are available to the group minutes after upload,
  with no review.
- **External testing** (up to 10,000 testers, no team membership needed): create an
  external group, invite by email or share the public link. The group's first build
  (and periodically thereafter) passes a lightweight Beta App Review, typically
  within a day.
- Testers install the **TestFlight** app on their device, accept the invitation,
  and install/update builds from there. Beta builds expire after 90 days.

### 6.2 Android testing via Google Play internal testing

The `.apk` in the artifact side-loads directly for quick checks. Every push build on
`main`, `develop`, and `feature/*` is published automatically by the
`publish-google-play` job (`.github/scripts/Publish-GooglePlay.ps1`) to the Play
Console's internal testing track — no manual upload needed, no review, available
within minutes.

One-time setup:

1. In the Play Console (<https://play.google.com/console>) → **Create app**: name,
   default language, app (not game), free/paid.
2. On the first release upload, enroll in **Play App Signing** (the default):
   Google generates and holds the app signing key, and the key that signed your
   first uploaded bundle — the CI upload keystore — is registered as the upload key
   automatically.
3. **Testing → Internal testing**, add testers (email list or Google Group).
4. **Setup → API access**: create/link a Google Cloud service account, grant it
   at least **Release manager** access to the app, and download its JSON key.
   Base64 is not needed — store the raw JSON as the `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`
   secret on the `publish-google-play` GitHub environment (section 1.2).

Testers:

- Add testers by email (or a Google Group) on the internal track's testers tab, then
  share the **opt-in link** shown there. Each tester opens the link, accepts, and
  installs the app from the Play Store like any other app; updates arrive through
  Play automatically after each CI publish.
- For external testers, promote a specific internal release to the closed testing
  (alpha) track from the Play Console: **Testing → Internal testing → the release →
  Promote release → Closed testing → Alpha**. This reuses the already-uploaded build
  (no re-upload) and is a deliberate manual step, kept separate from CI because Play
  reviews closed testing releases before they go live. From there, promoting further
  to **Open testing** or **Production** is likewise manual.

## 7. Notes

- Build numbers may have gaps (a failed build still consumed its number). That is
  harmless; the stores only require "greater than last upload".
- GitHub assigns a distinct `GITHUB_RUN_NUMBER` to each new workflow run, so concurrent
  runs cannot calculate the same build number. Rerunning the same workflow run reuses
  its build number.
- While the repository is private, macOS runner minutes are billed at a 10× multiplier
  against the included quota. Once the repository is public, standard GitHub-hosted
  runners are free.
- Rapid pushes to the same branch cancel the superseded in-flight build (the
  `concurrency` block in `ci.yml`); builds on `main` always run to completion. A
  cancelled run still consumed its run number, which is harmless.
- Every non-schedule run also lints the workflow itself with `actionlint` (the
  `lint` job); a lint failure fails the run but does not block the platform builds,
  which run in parallel with it.
- The `test` job runs the unit tests on Ubuntu via
  `.github/scripts/Invoke-Tests.ps1`, which runs every `*.Test.csproj` listed in
  `src/Xecrets.Mobile.slnx` — currently the shared-library tests in `xecrets-net` and
  `xecrets-common`, plus the mobile-model tests. The job checks out the sibling
  repositories those tests reference before running them. Like `lint`, it runs in
  parallel with the platform builds: a failing test fails the run without cancelling
  them, so one run reports everything that is wrong at once. `build-state` requires it,
  so a run with failing tests is never recorded as built and the nightly gate will try
  the branch again.
- Contributors run the same script locally before submitting; see
  [CONTRIBUTING.md](../CONTRIBUTING.md#running-the-tests).
