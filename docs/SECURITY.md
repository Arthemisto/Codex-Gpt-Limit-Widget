# Executable trust and account storage

Before starting Codex, the widget uses Windows Authenticode verification (`WinVerifyTrust`) and requires the verified signer to be **OpenAI OpCo, LLC**. Signature integrity, Windows trust policy and certificate revocation checks must succeed. Verification runs off the UI thread and requests no certificate dialogs.

The selected EXE stays open with read-only sharing during verification and startup, preventing ordinary writes/deletion of that file in that interval. This reduces substitution races; it is not a sandbox or a guarantee against an attacker already controlling the Windows account, its trust store or the machine.

Discovery prefers versioned desktop installation folders, then the legacy installation root, then absolute PATH entries. Directory modification time is a discovery ordering heuristic, not proof of a version number or publisher identity. **Every selected candidate is verified regardless of its location.** An invalid candidate causes a controlled error; there is no silent fallback to an unverified binary.

Unsigned CLI builds and binaries from other publishers are not supported. Repair/update the official Codex desktop installation if trust verification fails. A publisher-name change requires a reviewed application update. Windows may need network access for certificate-chain/revocation validation; an unavailable or untrusted chain is not silently accepted.

The widget delegates OAuth and credential persistence to Codex: `keyring` for remembered widget sign-in, `ephemeral` otherwise, in a separate widget home. It does not downgrade to plaintext storage. Existing Codex login uses Codex's own configuration. Real remembered login and disconnect should also be checked manually after upgrades.

The widget installer itself is currently unsigned. Checking the helper's signature does not sign the widget or authenticate a downloaded installer. Release SHA-256 hashes are supplied for integrity comparisons.

The Windows Job Object closes assigned helpers when the widget exits. A short interval between process creation and job assignment remains; atomic process containment is not claimed.

Implementation reference: [Microsoft WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust).
