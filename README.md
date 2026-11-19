# Fishao Bot

> [!WARNING]
> **Jen pro privátní server.** Tento bot je určený pouze pro soukromý server hry FISHAO – Reborn.
> **Používáš ho na vlastní riziko** – za používání botů můžeš dostat **ban** (zablokování účtu).
> Autor nenese žádnou odpovědnost za ztrátu účtu, postupu ani čehokoli jiného.
>
> **For the private server only.** This bot is meant only for the private server of FISHAO – Reborn.
> **Use it at your own risk** – using bots can get your account **banned**.
> The author takes no responsibility for a lost account, progress or anything else.

Automatický rybářský bot pro FISHAO – Reborn. Běží na pozadí: čte obraz z okna hry a posílá kliknutí přímo do něj, takže myš a klávesnice zůstávají volné.

Auto-fishing bot for FISHAO – Reborn. Runs in the background: it reads the game window and sends clicks straight to it, so your mouse and keyboard stay free.

## Stažení / Download
Stáhni `FishaoBot.exe` z nejnovějšího [vydání](../../releases/latest). Je to jeden soubor a sám se aktualizuje, když vyjde nová verze.

Grab `FishaoBot.exe` from the latest [release](../../releases/latest). It is a single file; it updates itself when a new release comes out.

## Sestavení / Build
`src\build.cmd` – uses the C# compiler that ships with Windows (.NET Framework 4), nothing to install. Output: `FishaoBot.exe` next to `src`.

## Nová verze / New release
1. Raise `Updater.Version` in `src\Updater.cs` and build.
2. GitHub → Releases → new release, tag `v<version>`, attach `FishaoBot.exe`, publish.
