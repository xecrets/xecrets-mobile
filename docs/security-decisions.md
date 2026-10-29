# Security Design Decisions

This is a record of intentional product choices and accepted risks.

## Unbounded Incoming Reads

**Decision: accepted risk; no read limit or authentication gate is required.**

A hostile document provider, another local app, or other input conditions can cause
Xecrets Ez to read an unbounded amount of data, including before profile authentication.
This can exhaust storage or stall an import. We accept this narrowly local denial of
service risk. The identified behavior does not expose secrets or bypass decryption
authentication.

## Plaintext Lifetime After Failed Preview Decryption

**Decision: accepted behavior; no separate failure cleanup is required.**

Successful preview decryption already leaves plaintext in the app's private temporary
directory until the existing cleanup runs. We accept that lifetime for failed preview
decryption as well. A failed preview does not need immediate cleanup solely because
decryption failed. The app's private storage boundary and existing cleanup policy
remain the intended protection.

## Automatic Diagnostic Clipboard Copying

**Decision: accepted behavior; preserve automatic copying of full diagnostic reports.**

When the crash-report page is displayed, the app automatically copies the full report
to the clipboard. We accept that diagnostic reports can contain filenames, paths,
document URIs, and other technical details. Convenient reporting is important, and
there is no requirement to add a separate copy action or redact the report for this
finding.

## Session Locking

**Decision: the app will not implement its own automatic session locking.**

Xecrets Ez will not add an app-specific inactivity, backgrounding, or device-lock
timeout. The existing profile sign-in and explicit sign-out behavior remain, together
with the operating system's device locking and access controls.

## Screen Capture

**Decision: the app will not restrict screen capture.**

There is no requirement to add screenshot, recording, casting, or task-preview
restrictions such as Android's `FLAG_SECURE`. Much of the sensitive decrypted content
is displayed by external applications, whose windows Xecrets Ez does not control.
We accept screen capture of the app's own content as well.
