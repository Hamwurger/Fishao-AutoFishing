# Fishao Bot

Auto-fishing bot for FISHAO – Reborn. Runs in the background: it reads the game window and sends clicks straight to it, so your mouse and keyboard stay free.

## Download
Grab `FishaoBot.exe` from the latest [release](../../releases/latest). It is a single file; it updates itself when a new release comes out.

## Build
`src\build.cmd` – uses the C# compiler that ships with Windows (.NET Framework 4), nothing to install. Output: `FishaoBot.exe` next to `src`.

## New release
1. Raise `Updater.Version` in `src\Updater.cs` and build.
2. GitHub → Releases → new release, tag `v<version>`, attach `FishaoBot.exe`, publish.
