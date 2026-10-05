# WinGet package (portable)

The app is published to the [Windows Package Manager](https://learn.microsoft.com/windows/package-manager/)
as a **portable** package — it installs the single-file `ZCinemaSound.App.exe`
(self-contained, so no .NET runtime is needed).

We deliberately do **not** package the Inno installer: it waits for Equalizer APO to be
installed, which would block WinGet's automated validation. WinGet itself never mentions
Equalizer APO; the app shows a first-run prompt, and the package **Description** notes the
requirement.

```
winget install Hydraxon91.ZCinemaSound
```

## One-time: first submission
`winget-releaser` (used by `.github/workflows/winget.yml`) can only *update* a package
that already exists in `microsoft/winget-pkgs`, so the first version is submitted manually:

1. `winget install Microsoft.WingetCreate`
2. Put the real hash in `Hydraxon91.ZCinemaSound.installer.yaml` (`InstallerSha256`):
   `Get-FileHash .\ZCinemaSound.App.exe -Algorithm SHA256`
3. Submit this folder (it holds the three manifests):
   ```
   wingetcreate submit .\winget
   ```
   (or copy them into `manifests/h/Hydraxon91/ZCinemaSound/<version>/` in your
   `winget-pkgs` fork and open a PR).

## Ongoing: automatic
Once the package exists:
- Add a **classic** PAT (scopes `public_repo` + `workflow`) as the `WINGET_TOKEN` secret.
- Fork `microsoft/winget-pkgs` under the repo owner.

`.github/workflows/winget.yml` then opens a PR to `microsoft/winget-pkgs` on every
published release, filtered to the portable `ZCinemaSound.App.exe` asset.
