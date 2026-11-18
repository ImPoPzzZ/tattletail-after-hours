# Αυτόματα updates (3.9): πώς δουλεύουν και πώς βγάζεις release

## Γιατί ξεχωριστό δημόσιο repo και branch `public`

Το κύριο repo είναι **private**: οι παίκτες δεν μπορούν να διαβάσουν τίποτα από εκεί. Γι' αυτό υπάρχει ένα **δημόσιο repo μόνο για releases** (`ImPoPzzZ/after-hours-releases`) με branch **`public`**: το mod διαβάζει πάντα `https://raw.githubusercontent.com/ImPoPzzZ/after-hours-releases/public/latest.txt`. Αυτό είναι το "ξεχωριστό branch για δημόσια updates": βάζεις εκεί μόνο δημόσιες εκδόσεις και αυτές ακριβώς ελέγχονται σε κάθε εκκίνηση.

## Τι κάνει

1. Στην εκκίνηση του παιχνιδιού (**αμέσως, στο title screen, κάθε φορά που πατάς Play**), το `AutoUpdate` (`src/AutoUpdate.cs`) κατεβάζει το `latest.txt` και το `latest.txt.sig` από το δημόσιο repo releases. Το title screen **δεν προχωράει στην Arcade μέχρι να τελειώσει** ο έλεγχος/η λήψη (γράφει "Checking for updates..." και δείχνει μπάρα προόδου). Offline ή αποτυχία: προχωράει κανονικά (χρονικά όρια 15 s για τον έλεγχο, 180 s ανά αρχείο). Μετά από επιτυχή λήψη, σε Windows το παιχνίδι **ξανανοίγει μόνο του** (Steam: μέσω `steam://rungameid/568090`, αλλιώς το exe) ώστε ο updater να εγκαταστήσει πριν ξαναφανεί το title· σε Wine/Proton δείχνει "Restart the game". Config: `Updates/AutoRestart`.
2. Ελέγχει την **υπογραφή** (RSA, SHA-256) με το δημόσιο κλειδί που είναι μέσα στο mod (`src/UpdateKey.cs`). Αν δεν ταιριάζει, **τίποτα δεν γίνεται**.
3. Αν η έκδοση είναι νεότερη, κατεβάζει **μόνο τα αρχεία που άλλαξαν** (σύγκριση SHA-256 με τα εγκατεστημένα) στο `BepInEx\AfterHoursUpdate\<έκδοση>\`, ελέγχει hash και μέγεθος, και γράφει `manifest.txt`, `manifest.sig`, `ready.txt`.
4. Δείχνει μήνυμα στο Arcade ("Update X is downloaded. Restart the game to install it.").
5. Στην επόμενη εκκίνηση, ο **updater** (`BepInEx\patchers\AfterHoursUpdater.dll`, πηγή στο `tools/updater`) τρέχει **πριν φορτώσει οποιοδήποτε plugin** (δεν υπάρχει κλείδωμα στο DLL), ξανα-ελέγχει υπογραφή και hashes, και αντιγράφει τα αρχεία **μόνο μέσα στο** `BepInEx\plugins\KaleidoscopeEndless\`. Γράφει `BepInEx\afterhours_update.log`.
6. Το mod, μόλις ξεκινήσει, δείχνει "After Hours was updated to X".

Δεν τρέχει: με `Updates/AutoUpdate = false`, με `DevTools = true` (dev build: για να μη γράφει πάνω από το δικό σου build), ή αν λείπει ο updater.

## Ασφάλεια

- Το **ιδιωτικό κλειδί** είναι στο `%USERPROFILE%\.after_hours_keys\update_private.xml`, **έξω από το repo**. Κράτα backup. Αν χαθεί, κάθε παίκτης χρειάζεται τον installer ξανά (το δημόσιο κλειδί είναι μέσα στο mod).
- Αν κάποιος πάρει τον λογαριασμό GitHub δεν μπορεί να στείλει κώδικα: δεν έχει το ιδιωτικό κλειδί.
- Ένα αρχείο του manifest δεν μπορεί να γραφτεί έξω από τον φάκελο του mod (`UpdateCore.IsSafePath`).

## Πώς βγάζεις release (ό,τι κάνεις για κάθε νέα έκδοση)

1. Αλλαγή έκδοσης: `VersionTag.VERSION`, `[BepInPlugin]`, installer (`VersionLabel`, `<Version>`).
2. Build **χωρίς να μπει στο παιχνίδι σου**: `dotnet build KaleidoscopeEndless.csproj -c Release -p:SkipDeploy=true`, μετά `tools/ilscan`.
3. `dotnet build tools/updater/AfterHoursUpdater.csproj -c Release -p:SkipDeploy=true` (μόνο αν άλλαξε ο updater).
4. `powershell -ExecutionPolicy Bypass -File tools/release/make_release.ps1 -Version 3.9.0 -Notes "..." -Dll bin\Release\net46\KaleidoscopeEndless.dll -Game "D:\SteamLibrary\steamapps\common\Tattletail" -Out release_out`
   Φτιάχνει `release_out\assets\*`, `latest.txt`, `latest.txt.sig`.
5. Έλεγχος: `dotnet run --project tools/update_test -c Release -- release_out` και `... -- --apply release_out tools\updater\bin\Release\net46\AfterHoursUpdater.dll`.
6. **Δημοσίευση:** `powershell -ExecutionPolicy Bypass -File tools/release/publish_release.ps1 -Version 3.9.0 -Dir release_out`. Κάνει δύο πράγματα με αυτή τη σειρά: (α) ανεβάζει τα αρχεία ως GitHub release `v3.9.0` στο δημόσιο repo `ImPoPzzZ/after-hours-releases` (οι παίκτες δεν το βλέπουν ακόμα), (β) **μετά** βάζει `latest.txt` + `latest.txt.sig` στο **branch `public`**. Από εκείνη τη στιγμή κάθε παιχνίδι που ξεκινά (κάθε φορά που πατάνε Play στο Steam) το βρίσκει. Ό,τι δεν έχει σπρωχτεί στο `public` δεν φαίνεται ποτέ στους παίκτες (ούτε dev builds, ούτε early access).
7. Νέος installer: `tools/installer/make_payload.ps1` και `dotnet build tools/installer/AfterHoursInstaller.csproj -c Release` (ο installer είναι ο μόνος τρόπος για πρώτη εγκατάσταση και για τον updater).

## Όρια

- **Το update κατεβαίνει όταν ξεκινά το παιχνίδι και εγκαθίσταται στο επόμενο Play.** Δεν γίνεται να εγκατασταθεί πριν ξεκινήσει το ίδιο το παιχνίδι: το DLL κλειδώνεται όταν τρέχει, και στο στάδιο που τρέχει ο updater (πριν τα plugins) ο παλιός Mono δεν υποστηρίζει TLS 1.2, άρα δεν μπορεί να κατεβάσει από το GitHub. Γι' αυτό υπάρχει το βήμα "Restart the game".
- Παίκτες σε έκδοση πριν το 3.9 δεν έχουν updater: χρειάζονται τον installer του 3.9 μία φορά.
- Ο ίδιος ο updater (`AfterHoursUpdater.dll`) δεν ενημερώνεται αυτόματα (μόνο τα αρχεία του φακέλου του mod): αν αλλάξει, χρειάζεται installer.
- Το HTTPS γίνεται με `UnityWebRequest` (το `WebClient` του παλιού Mono δεν υποστηρίζει TLS 1.2). Δεν έχει δοκιμαστεί σε πραγματικό παιχνίδι ακόμα.
