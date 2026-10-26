## Basalt 0.6.7

- English/Russian UI with system-language default and live switching.
- GitHub Release checks, architecture-aware downloads and SHA-256 verification.
- Optional Windows startup and auto-connect: load saved profiles, refresh subscriptions, then connect; failed updates preserve cached servers.
- Retry backoff for automatic connection failures; manual disconnect cancels pending retries.
- GPL-3.0 licensing, bilingual README and an AI-anonymized demo screenshot.

Download the universal portable ZIP, or the x86-only ZIP for 32-bit Windows. Extract the entire package. Exit the previous client from its tray menu before switching versions. If moving the portable folder, re-enable Windows startup to update its registered path.

Windows 8/8.1 are the primary compatibility target; dedicated VM certification remains pending. XP is not supported by the bundled TUN/core stack. See README for supported routing formats and current limits.

`SHA256SUMS.txt` covers both portable packages and corresponding core source archives. SHA-256 provides integrity checking, not a separate code signature.
