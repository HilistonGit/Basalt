# Release maintenance

1. Increment `VERSION` by one patch step. Update the assembly versions in both `AssemblyInfo.cs` files and the visible Basalt/User-Agent version strings. The updater accepts exactly `x.y.z` release tags.
2. Run `scripts/fetch-framework-references.ps1`, `scripts/build.ps1`, then `scripts/fetch-official-cores.ps1`.
3. Run `scripts/test-integration.ps1` with the x64 and x86 core paths. `scripts/test-tun.ps1 -Run` is an explicit elevated network test; disconnect any other VPN first. `scripts/test-split-tun.ps1 -Run` additionally reads the saved Happ test profile and makes external requests; it is not part of CI.
4. Prepare source assets with `scripts/prepare-release-sources.ps1 -Go <go executable>`. Keep corresponding source alongside every distributed binary. It vendors sing-box dependencies and retains upstream build instructions/workflows. The current core pins are Xray 26.10.21 and sing-box 1.14.4.
5. Update `docs/RELEASE-NOTES.md`, commit and push the reviewed source to `main`.
6. Authenticate with standard `gh auth login`; run `scripts/publish-release.ps1 -BuildDirectory build -Gh gh`.

The publication script checks the built version, packages universal/x86 archives, generates `SHA256SUMS.txt` and uploads both packages and source archives. Do not edit a published asset in place; publish a new patch version. Never commit `build`, `dist`, settings, subscription responses, private URLs, keys, diagnostic runtime configs or credentials.

## Updater contract

- Repository: `HilistonGit/Basalt`; latest stable GitHub Release API.
- Tag: `vX.Y.Z`; packages: `Basalt-X.Y.Z-test.zip` and `Basalt-X.Y.Z-test-x86.zip`.
- Required checksum asset: `SHA256SUMS.txt` (`hex-sha256  filename`).
- HTTPS only; redirects restricted to GitHub release asset hosts. Package size is bounded and verified.
- Partial files have random `.partial` names and are removed after failure. Download completion does not install or execute the archive.
- Current/no-release returns no update. API/network/rate-limit errors are shown to the user.

## Startup verification

Model tests exercise persisted profiles, unavailable/partial subscriptions, empty profiles populated on refresh and cancellation. UI tests cover the scheduled task XML and live language switching. They do not modify the developer's Windows startup configuration. Manually verify sign-in, UAC and startup task registration/removal on supported target systems before claiming OS certification.
