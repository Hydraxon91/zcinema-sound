# Code signing

Release artifacts are **unsigned** by default, so Windows SmartScreen may show an
"unknown publisher" warning on first run. Signing is optional and already staged in
`.github/workflows/release.yml` — the signing steps are **skipped** unless you set the
SignPath variable (so nothing changes until you configure it).

## SignPath (free for open source)
1. Apply to the **SignPath Foundation** (free code signing for open-source projects):
   <https://signpath.org/foundation>. The repo is public and MIT-licensed.
2. In SignPath, create a **project**, a **signing policy** (e.g. `release-signing`),
   and an **artifact configuration** whose root element matches `*.exe`.
3. Add these to the GitHub repo (Settings → Secrets and variables → Actions):
   - Secret **`SIGNPATH_API_TOKEN`**
   - Variable **`SIGNPATH_ORG_ID`**
   - Variable **`SIGNPATH_PROJECT_SLUG`**
   - Variable **`SIGNPATH_SIGNING_POLICY_SLUG`**
   - Variable **`SIGNPATH_ARTIFACT_CONFIG_SLUG`**
4. Done — the next tagged release is signed in CI (the `Sign with SignPath` step runs
   because `SIGNPATH_ORG_ID` is now set), and the signed files are what get attached to
   the release.

## Alternatives
- **Azure Trusted Signing** (~$10/mo, API-based): replace the SignPath step with
  `azure/trusted-signing-action`.
- **OV/EV certificate on a hardware token**: sign locally, or through the CA's cloud
  signing, with
  `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 <file>`.
- **Self-signed certificate**: only removes the warning on machines that trust your
  cert; not useful for public distribution.

## Verify
```
signtool verify /pa /v ZCinemaSound-Setup.exe
```
