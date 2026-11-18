# 3.9 - The Auto-Update Update

**Made By: ImPoPzzZ & Sa1monfr**

From now on the mod keeps itself up to date. This update includes everything from 3.8.

---

## Automatic updates

- Every time you start the game, the title screen checks for a newer version first ("Checking for updates..."). If there is one it downloads only the files that changed, with a progress bar, and the game restarts itself to install it before you play. (On Linux / Steam Deck it asks you to restart the game yourself.) If you are offline, the game just goes on.
- While it works, you see "Checking for updates..." on the title screen with a progress bar, and the game waits there until it is done. You never have to run the installer again for an update.
- Every update is checked before it is used: it must be signed by us, and every file must match exactly what we published. A damaged or tampered download is thrown away and nothing changes.
- You can turn it off: in `BepInEx\config\com.kaleidoscope.endless.cfg` set `AutoUpdate = false` under `[Updates]`.
- The check is one small request to GitHub. Nothing about you is sent.
- **To get automatic updates you need to run this installer once.** After that, updates arrive by themselves.

## Linux and Steam Deck

- The installer now understands Proton and Wine. Run it through Wine and it finds your Steam library on the Linux disk (the usual Steam folder, Flatpak, Snap, the Steam Deck, SD cards and extra drives) and sets up Tattletail's Proton folder so the mod loads, with no launch option to type.
- If Tattletail has never been started on that machine, start it once from Steam first, then run the installer again.
- If the mod does not load, the installer shows the launch option to add in Steam: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
- Not tested on every Linux setup yet: tell us how it goes.

---

Everyone needs the same version to play online together (Versus, Air Hockey and Recalled). Automatic updates make that easy.

Download: https://impopzzz.itch.io/tattletail-after-hours
Run the installer and it updates in one click. Your progress is safe.
