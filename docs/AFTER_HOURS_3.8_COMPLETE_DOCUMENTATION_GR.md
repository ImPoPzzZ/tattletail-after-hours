# TATTLETAIL: AFTER HOURS 3.8
# ΠΛΗΡΗΣ ΤΕΚΜΗΡΙΩΣΗ: ΑΠΟ ΤΑ FEATURES ΜΕΧΡΙ ΤΟΝ ΚΩΔΙΚΑ

*Έκδοση: 3.8 (δημόσια). Τα ονόματα κλάσεων, αρχείων, μεθόδων και keys γράφονται όπως είναι στον κώδικα (αγγλικά). Η εξήγηση είναι στα ελληνικά.*

*Αυτό το έγγραφο καλύπτει ΟΛΟ το mod: κάθε παιχνίδι, κάθε σύστημα, τον κώδικα, τα εργαλεία, τον installer, και αναλυτικά τι άλλαξε στο 3.8. Για τους αριθμούς-πίνακες του Recalled και για βαθύτερη μηχανική κάθε παλιού συστήματος υπάρχουν και τα παλιότερα αγγλικά έγγραφα (παράγραφος 22). Όπου κάτι δεν έχει δοκιμαστεί μέσα στο παιχνίδι, το γράφω ρητά (παράγραφος 21).*

---

## ΠΕΡΙΕΧΟΜΕΝΑ

1. Τι είναι το mod (επισκόπηση)
2. Η τεχνολογία και οι κανόνες του παλιού Mono
3. Το repository, το build, το deploy, τα εργαλεία, τα tests
4. Πώς ξεκινά το mod και πώς φορτώνει κάθε mode (SceneMonitor)
5. Το Arcade (το hub)
6. Endless
7. Endless στο Waygetter Facility (νέο στο 3.8)
8. Daily objectives (νέο στο 3.8)
9. Οικονομία: τα shards (αλλαγή στο 3.8)
10. Versus
11. Papa's 10th Birthday
12. Summer Vacation
13. Pinball και Air Hockey
14. Recalled
15. Τα παιδιά, τα ρούχα, ο Creator, οι φωνές, τα χρώματα του Tattletail
16. Δίκτυο (Steam P2P)
17. Αποθηκεύσεις, config, χειρισμός
18. Τι άλλαξε στο 3.8 (αναλυτικά, με αρχεία)
19. Ο installer και η υποστήριξη Linux
20. Διαδικασία release και μόνιμοι κανόνες
21. Τι δεν έχει δοκιμαστεί, τι είναι μόνο για dev, τι μένει ανοιχτό
22. Παλιά έγγραφα και πού βρίσκεται τι
23. Παράρτημα Α: χάρτης όλων των αρχείων του `src/`
24. Παράρτημα Β: τα marks του Facility, οι patrol nodes της Mama
25. Παράρτημα Γ: όλα τα daily objectives (η λίστα)

---

## 1. Τι είναι το mod

Το **Tattletail: After Hours** (εσωτερικό όνομα **Kaleidoscope Endless**: το DLL, ο φάκελος plugin και το BepInEx GUID κράτησαν το παλιό όνομα επίτηδες) είναι ένα **δωρεάν fan mod** για το Tattletail (Waygetter Games, 2016). Φτιάχτηκε από τους **ImPoPzzZ και Sa1monfr**. Μετατρέπει το παιχνίδι σε ένα Arcade με πολλά παιχνίδια μέσα. Δεν είναι ανεξάρτητο παιχνίδι: χρειάζεται το βασικό Tattletail (Steam ή itch.io).

| Παιχνίδι | Μία γραμμή |
|---|---|
| **Endless** | Επιβίωσε από τη Mama μέσα από ατελείωτους στόχους. Τηλέφωνο που χτυπά για να ξεφύγεις, shards, traits, gadgets |
| **Endless στο Waygetter Facility** (3.8) | Το ίδιο Endless, αλλά στο εγκαταλελειμμένο εργοστάσιο: κάμερα με night vision, φώτα που σβήνουν όταν έρχεται η Mama |
| **Versus** | 1v1 online: ο ένας είναι το παιδί, ο άλλος η Mama (ή ο Papa) |
| **Recalled** | Third-person co-op RPG για έως 4 παίκτες, σε μια πόλη φτιαγμένη εξ ολοκλήρου με κώδικα |
| **Papa's 10th Birthday!** | Πάρτι αντί για κυνηγητό: τάισε τον Papa, κράτα το πάρτι ζωντανό |
| **Summer Vacation** | Το μπαρ με smoothies της Mama σε μια παραλία |
| **Tattletail Pinball** | Πραγματικό 3D φλίπερ, first person |
| **Tattletail Air Hockey** | Πραγματικό 3D air hockey (1v1 με AI ή φίλο) |
| **Daily objectives** (3.8) | Τρεις στόχοι την ημέρα στον τοίχο του Arcade, με ανταμοιβές σε shards |
| **Το Arcade** | Το hub με όλα τα παραπάνω ως μηχανές, 3 shops, gumball, ρούχα, καθρέφτης |

**Στοιχεία:**
- Έκδοση **3.8**, δημόσια (`VersionTag.EARLY_ACCESS = false`, πάνω δεξιά στο Arcade γράφει "PUBLIC VERSION 3.8").
- **54.961 γραμμές C#** σε **148 αρχεία** στο `src/`, συν Python εργαλεία, ένα WinForms installer και headless test projects.
- **Δωρεάν και πάντα θα είναι.** Τα DLC χαρακτήρες δεν κοστίζουν ποτέ πραγματικά λεφτά.
- Credits παντού: **"Made By: ImPoPzzZ & Sa1monfr"**. Discord: discord.gg/PjSQQrhsMx. Ko-fi: ko-fi.com/impopzzz.
- Σελίδες: itch.io `impopzzz.itch.io/tattletail-after-hours` (το mod), Steam app 568090 και itch.io `waygetter-games.itch.io/tattletail` (το βασικό παιχνίδι).
- Παραδοτέα: zip με BepInEx μέσα (Steam, 32-bit loader), zip για itch.io (64-bit loader), και ο installer `Install Tattletail After Hours.exe`.

---

## 2. Η τεχνολογία και οι κανόνες του παλιού Mono

### 2.1 Τι τρέχει

- **Παιχνίδι:** Tattletail, **Unity 5.5.3**, Mono runtime της εποχής 2.0/3.5. Windows. Αντίγραφα Steam (32-bit) και itch.io (64-bit).
- **Loader:** **BepInEx 5** (proxy `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `BepInEx/core`, `BepInEx/plugins`). Το 32-bit και το 64-bit παιχνίδι θέλουν διαφορετικό `winhttp.dll`: ο installer διαλέγει διαβάζοντας τον τύπο CPU από το exe του παιχνιδιού.
- **Patching:** **HarmonyX**. Τα prefix μπορούν να ακυρώσουν, τα postfix ακούνε.
- **Γλώσσα/target:** C# για **net46** απέναντι στα DLL του ίδιου του παιχνιδιού (`Assembly-CSharp.dll`, `UnityEngine.dll`, `Assembly-CSharp-firstpass.dll` που περιέχει και το Steamworks.NET).
- **Plugin id:** `com.kaleidoscope.endless`, όνομα "Tattletail: After Hours", έκδοση `3.8.0` (σε συμφωνία με το `VersionTag.VERSION`).
- Όλα βρίσκονται στο namespace `KaleidoscopeEndless`.

### 2.2 Παλιός Mono: τι σκοτώνει το plugin και πώς αποφεύγεται

Έγκυρη C# μπορεί να σκάσει σε αυτό το runtime. Κάθε στοιχείο προέρχεται από πραγματικό περιστατικό:

1. **Όχι `System.Func`, `System.Action`, LINQ.** Ζουν στο `System.Core` και μία αναφορά μπορεί να χαλάσει ολόκληρο τύπο (`TypeLoadException`, το log του BepInEx σταματά χωρίς εξήγηση). Το mod έχει δικά του delegates (`FloatGetter`, `TextGetter`, `StepHandler`, `ClickHandler` στο `OptionsMenu.cs`, και τοπικά `Done`, `Fn` όπου χρειάζεται).
2. **`IteratorStateMachineAttribute`**: ο compiler το βάζει σε κάθε coroutine. Το `CompatShims.cs` το ορίζει τοπικά. Επίσης: όχι coroutines μέσα σε κλάσεις `[HarmonyPatch]` (το `PatchAll` διαβάζει όλες τις μεθόδους).
3. **Οι reflection handles συγκρίνονται ως `(object)x != null`.** Το `FieldInfo != null` γίνεται `op_Inequality` (.NET 4) και ολόκληρη η μέθοδος σκάει με `MissingMethodException`.
4. **Μέθοδος `params` χωρίς επιπλέον ορίσματα** γίνεται `System.Array.Empty<T>()` (.NET 4.6) και σκοτώνει τον καλούντα. Δίνεται ρητός κενός πίνακας.
5. **Μόνο shaders που υπάρχουν στο build:** `Standard`, `Diffuse`, `Unlit/Color`, `Particles/Alpha Blended`, `Particles/Additive`, `Custom/FurShaderV1`, `UI/Default`, `Sprites/Default`. Το `Unlit/Texture` ΔΕΝ υπάρχει.
6. **Όχι `ValueTuple`, `Array.Empty`, default interface members.** Αν υπάρχει αμφιβολία: scan στο DLL.
7. **Το `Destroy` είναι αναβαλλόμενο** (τέλος του frame). Όπου ένα component πρέπει να λείπει αμέσως, `DestroyImmediate`. *(Έμαθε το 3.8: στη Mama του Facility, μια καθυστερημένη `Destroy` άφησε τη Mama να διαλέξει node που μετά εξαφανίστηκε.)*
8. **Όχι Canvas στο `Awake`.** Το UI χτίζεται στο `Start` ή στο πρώτο `Show()`.
9. **Το `harmony.PatchAll()` είναι σε try/catch.**
10. **Near clip plane στο 1 cm.** Το HUD του παιχνιδιού (και η κάρτα τίτλου) είναι σε plane 2 cm μπροστά στην κάμερα: μεγαλύτερο near clip το εξαφανίζει σιωπηλά.
11. **ΝΕΟ στο 3.8: όχι `Type == Type`.** Ο τελεστής `==` μεταξύ δύο `System.Type` δεν υπάρχει στον παλιό Mono (`MissingMethodException: System.Type.op_Equality`). Χρησιμοποίησε `.Equals(...)` ή `IsAssignableFrom`. Αυτό έσπασε την ενεργοποίηση της Mama στο Facility. Ο `tools/ilscan` τώρα το πιάνει.

### 2.3 Ο έλεγχος: `tools/ilscan`

Μια απλή αναζήτηση κειμένου για "Array.Empty" μέσα στο DLL δεν βρίσκει τίποτα, ακόμα κι όταν η κλήση υπάρχει (οι κλήσεις είναι metadata tokens). Το `tools/ilscan` (C#, `System.Reflection.Metadata`) διαβάζει το χτισμένο DLL και περπατά το IL κάθε μεθόδου για κλήσεις προς `Array::Empty`, `System.Linq.*`, `ValueTuple`, `Func`/`Action`, και (από το 3.8) `System.Type::op_Equality/op_Inequality`. Τρέχει μετά από ΚΑΘΕ build και πρέπει να τυπώνει `CLEAN`.

### 2.4 Χτίζουμε τα πάντα με κώδικα

Το mod **δεν στέλνει Unity scenes ή prefabs**. Όλα φτιάχνονται στο runtime:

- **Interactables**: ένα γυμνό `Interactable` component (`PartyGameRunner.MakeInteractable(go, actionText, holdTime)`), με το layer αντιγραμμένο από ένα επιβεβαιωμένα κλικαρόμενο instance, και ΠΑΝΤΑ `newStateOnInteract = Enabled` (αλλιώς το αντικείμενο κλείνει το collider του μετά την πρώτη χρήση).
- **Γεωμετρία**: primitives (`Mat.Box`, `CreatePrimitive`) με παραγόμενα υλικά (`Mat.Lit(color, gloss)` = Standard, `Mat.Unlit(color)` = `Unlit/Color`), ή merged meshes.
- **Κείμενο στον κόσμο**: μικροσκοπικά world-space UI canvases (`WorldText`), όχι `TextMesh` (φαίνεται μέσα από τοίχους).
- **Μοντέλα Tattletail και Mama**: clones των πραγματικών του level, με τα live scripts αφαιρεμένα.
- **Clone πραγματικά instances, ποτέ τροποποίηση στη θέση τους.** Βρίσκεις το πραγματικό prop μία φορά και φτιάχνεις φρέσκο `GameObject` για το διαδραστικό κομμάτι.
- **Θέσεις** από marks του developer, κολλημένα σε πίνακες.
- **UI**: `UnityEngine.UI.Text` με το δικό του font του παιχνιδιού. Το `SuperTextMesh` (ο text renderer του παιχνιδιού) δεν επιζεί του clone, δεν το χρησιμοποιεί τίποτα.

### 2.5 Τα Harmony patches (ανά σκοπό)

| Σκοπός | Patched μέλη | Αποτέλεσμα |
|---|---|---|
| Θάνατοι από τη Mama | `MamaManager.RequestJumpscareDeath`, `OnJumpscareFinished` (prefix) | Το Endless/Versus αποφασίζει αν μετράει το πιάσιμο |
| Η Mama ακούει | `MamaManager.OnNoise` (postfix ×2: Versus, Parrot grace· και από το 3.8 prefix για το Facility), `Mama.MoveToNode`, `Mama.IsFlashlightOnMama` | Μετρητής θορύβου, grace μετά από teleport, parrot |
| Πάγωμα του story | `QuestOrder.*` (prefix, return false) | Το vanilla story είναι ανενεργό μέσα στα modes |
| Αλληλεπιδράσεις | `Interactable.OnInteractComplete` (πολλά postfix), `OnInteract` (μπλοκ όταν ανοιχτό menu), `ConditionUpdate` | Μηχανές, objectives, care stations, πάρτι, daily reroll, camcorder μπαταρίες |
| HUD κείμενο | `HUD.SetNewQuest/ClearQuest/PlayTitle` | Overrides Arcade/πάρτι/τίτλων (στο Facility Endless επιτρέπεται το quest text) |
| Menus | `MainMenu.Start` κλπ | Κρύβουν τα κουμπιά, φορτώνουν το Arcade |
| Φωνές/ήχοι | `FirstPersonController.PlayJumpSound/PlayLandingSound` | Φωνές παιδιού |
| Φακός | `FlashlightController.LateUpdate` (Low Battery), και **από το 3.8** `GetLightPercentage` (postfix: στο Facility το night vision μετράει ως φως) | Modifier, φόβος του σκοταδιού |
| Save | `GD.SaveLevel`, `MainMenu.Start` (StorySave) | Κρατούν το πραγματικό save του story |
| Mama (3.8) | `MamaManager.IsAboveGround` (prefix: στο Facility μετρά από το δάπεδο του κτιρίου) | Υπόγειο = δεύτερο επίπεδο της Mama |

Ο ιδιωτικός state του παιχνιδιού διαβάζεται με cached reflection handles και `(object)` null checks. Κάποια πράγματα γίνονται polling αντί για patch (π.χ. ο μετρητής "φακός σβήστηκε" ρωτά `IsGlowing()` κάθε frame, γιατί το patch στο `KillLight()` χάλασε το fade coroutine του παιχνιδιού).

---

## 3. Το repository, το build, το deploy, τα εργαλεία, τα tests

### 3.1 Διάταξη

```
KaleidoscopeEndless/
  KaleidoscopeEndless.csproj    net46, αναφορές στα DLL του παιχνιδιού με path, embedded resources, post-build copy
  src/                          148 αρχεία C# (Παράρτημα Α)
  assets/                       kid_*.bin (7 χαρακτήρες), creator_kit.bin, pinball/*.jpg, airhockey/*.jpg,
                                factory/*.jpg|png, Stickers/*.png
  Sounds/                       τα WAV της μουσικής του Recalled (rc_*.wav) και ήχοι (δεν μπαίνουν στο git)
  tools/                        Python εργαλεία, installer, ilscan, recalled_test, trailer
  docs/                         τα έγγραφα, release_3.5 / 3.6 / 3.7 / 3.8, recalled_map/, facility_marks.txt
  DESIGN_DOCUMENT.md            το μακρύ δοκίμιο σχεδιασμού
  Install Tattletail After Hours.exe   το τελευταίο build του installer (gitignored)
```

Το `RecalledSecret.cs` (κλειδί υπογραφής του save του Recalled) **δεν είναι στο git** (`.gitignore`), υπάρχει μόνο το `RecalledSecret.cs.example`. Επίσης στο `.gitignore`: `/*.mp3`, `Sounds/rc_*.wav`.

### 3.2 Branches (σημαντικό)

- **`main`**: η παλιά γραμμή (3.7).
- **`recalled-music-only`**: ΕΔΩ χτίστηκε το 3.8 (μουσική, shards, Facility, daily, Linux installer). **Δεν έχει γίνει merge στο `main`.** Είναι pushed στο GitHub (private repo `ImPoPzzZ/KaleidoscopeEndless`).
- **`recalled-card-duels`**: τοπικά μόνο, ΔΕΝ είναι pushed. Περιέχει το πείραμα "Wizard101-style card duels" στο Recalled (μηχανή duel με 100 κάρτες, 7 σχολές, deck building, co-op, animated stage). Αποφασίστηκε να μην μπει: το Recalled έμεινε real-time. Ο κώδικας υπάρχει μόνο εκεί.

### 3.3 Build και deploy

`dotnet build KaleidoscopeEndless.csproj -c Release` κάνει compile και, μέσω του target `CopyToPlugins`, αντιγράφει το DLL (και τα `Sounds\*.wav`) στο `<Tattletail>\BepInEx\plugins\KaleidoscopeEndless\`. Στη μηχανή του developer το παιχνίδι είναι στο `D:\SteamLibrary\steamapps\common\Tattletail`. Ο κύκλος: αλλαγή κώδικα → build → `tools/ilscan` → δοκιμή στο πραγματικό παιχνίδι → ανάγνωση `BepInEx\LogOutput.log`.

Δεν υπάρχει unit test harness μέσα στο engine (σχεδόν όλα έχουν νόημα μόνο σε ζωντανή σκηνή). Αντ' αυτού:
- **Έλεγχος = δοκιμή στο παιχνίδι + συνήθεια logging.** Όλα τα logs έχουν πρόθεμα `KE:`, με tags (`[Recalled]`, `[Party]`, `[Daily]`, `[FacilityMama]`, `[Factory]`, `[MP]`...).
- **`tools/recalled_test`**: μεταγλωττίζει την καθαρή λογική του Recalled (δεδομένα, κανόνες, χαρακτήρα, save, ολόκληρο το plan της πόλης και την πλοήγηση) απέναντι σε Unity stubs σε σύγχρονο .NET. *(Μετά την αφαίρεση του Egypt, 5 έλεγχοι που αφορούσαν μόνο Egypt αποτυγχάνουν: είναι γνωστό.)*
- **PinballTable και AirHockeyTable** μεταγλωττίζονται με μικρά stubs και δοκιμάστηκαν offline με χιλιάδες προσομοιωμένες μπάλες/pucks.
- **`WineReg`** (installer, 3.8): 7 δοκιμές σε δείγματα `user.reg` (προσθήκη, δεύτερο τρέξιμο, υπάρχον section, λάθος τιμή, αφαίρεση, CRLF, αρχείο χωρίς section). Πέρασαν όλες.

### 3.4 Τα εργαλεία (`tools/`)

| Εργαλείο | Γλώσσα | Τι κάνει |
|---|---|---|
| `kid_model.py` | Python | Χτίζει τους 7 χαρακτήρες: SDF → marching cubes → σκελετός, βάρη, 30 καρέ animation → `assets/kid_*.bin` |
| `creator_kit.py` | Python | Χτίζει το `creator_kit.bin` (`KIT1`): σώματα, κεφάλια, μαλλιά, χαρακτηριστικά |
| `pinball_art.py`, `airhockey_art.py` | Python | Ζωγραφίζουν τα playfields και τα πλαϊνά |
| `factory_textures.py` | Python | Μετατρέπει τα CC0 ambientCG σε JPEG που ενσωματώνονται στο DLL |
| `factory_decals.py`, `factory_graffiti.py`, `factory_map.py` | Python | Decals φθοράς, graffiti, ο χάρτης "YOU ARE HERE" στο lobby του Facility |
| `gen_lines.py`, `gen_voice.py` | Python | Έγγραφα ιστορίας → `RecalledLines.cs`, `RecalledVoiceData.cs` |
| `convert_to_wav.ps1` | PowerShell | Μετατρέπει τα mp3 σε PCM WAV (το παιχνίδι δεν αποκωδικοποιεί mp3 στο runtime) |
| `trailer/` | Python + ffmpeg | Κόψιμο trailer, thumbnails |
| `installer/` | C# WinForms net48 + Python | Ο installer (παράγραφος 19) |
| `ilscan/` | C# | Ο σαρωτής παλιού Mono |
| `recalled_test/` | C# | Headless έλεγχοι Recalled |

Τα παραγόμενα binary assets χτίζονται **offline** και μπαίνουν στο git, ώστε ο παίκτης να μη χρειάζεται Python.

---

## 4. Πώς ξεκινά το mod και πώς φορτώνει κάθε mode

### 4.1 Boot (`KaleidoscopeEndlessPlugin.Awake()`)

1. Διαβάζει config (παράγραφος 17): `Developer/DevTools`, `Developer/OpenLastStop`, `Cosmetics/RecalledGraphics`.
2. `new Harmony(...).PatchAll()` σε try/catch.
3. `SceneMonitor.Init()` (πιάνει το `SceneManager.sceneLoaded` και το `OnSceneLoadComplete` του παιχνιδιού).
4. `SaveFiles.Init()` (μεταφέρει παλιά saves στο `Documents\Tattletail After Hours\`), `OptionsMenu.ApplySavedVolume()`, `Shop.Init()`.
5. Φτιάχνει το μόνιμο `KE_MpLobby` (`MpLobby`, `DontDestroyOnLoad`).
6. Γράφει στο log έναν έλεγχο τύπων ώστε ένα `TypeLoadException` να ονομάζει τον εαυτό του.

Το `Update()` τρέχει τα πάντα-ενεργά συστήματα σε ένα try/catch: `EndlessRunManager.Tick()`, `BlurFilter.Tick()`, `VersusNet.Tick()`, `KidVoice.Tick()`.

### 4.2 Τι σημαίνει "φόρτωση level"

ΟΛΑ τα modes φορτώνουν το **Night 6** του story και το ξαναχρησιμοποιούν. Ένα pending flag, που τίθεται λίγο πριν το `GameManager.PlayLevel(LevelEnum._6_TheParty)`, λέει σε ποιο mode αναφέρεται η φόρτωση. Ο `SceneMonitor` (στο `SceneBootstrap.cs`) το διαβάζει με σταθερή προτεραιότητα όταν τελειώσει η φόρτωση:

```
VersusMode.PendingMamaLoad   -> η πλευρά του κυνηγού στο Versus
HubMode.Pending              -> το Arcade
BeachMode.Pending            -> Summer Vacation
RecalledMode.Pending         -> Recalled
FactoryMode.Pending          -> το Waygetter Facility (και το Endless του Facility, FactoryMode.EndlessPreview)
(αλλιώς)                     -> SetupMonoBehaviours(): Endless / πάρτι / παιδί στο Versus
```

Το `EndlessRunManager.PendingEndlessRun` είναι η πύλη "το mod κατέχει αυτό το load": χωρίς αυτήν, παίκτης που ξαναπαίζει νόμιμα το Night 6 του story θα γινόταν Endless όταν πιάσει τον φακό. Τα loads του mod περνούν από `StorySave` ώστε το *Continue* να επιστρέφει πάντα στο πραγματικό level του story.

**Endless στο σπίτι:** στο level load δημιουργούνται όλοι οι μόνιμοι controllers και οπλίζεται το `WaitingForDoorTrigger`. Το run ξεκινά με την πρώτη πραγματική ενέργεια του παίκτη (postfix στο `InteractableActions.PlayActions` φιλτραρισμένο στο `FlashlightCarry`), γιατί ένα συγχρονισμένο ξεκίνημα θα έτρεχε πριν γεμίσουν τα `RM.fpsController`, `RM.tattletail` κλπ.

**Endless στο Facility (3.8):** η διαδρομή `FactoryMode.Pending` δεν περνά από το `SetupMonoBehaviours`. Αντί για την πρώτη πόρτα, το `FacilityRunStarter` περιμένει να υπάρχουν Tattletail, φακός, HUD και παίκτης (και 3 δευτερόλεπτα για να κοιτάξεις γύρω) και καλεί `SceneMonitor.StartFacilityRun()`: το ίδιο `SetupMonoBehaviours()` + `StartEndless()` με το σπίτι (παράγραφος 7).

Το **`LoadingCover`** κρύβει όλη τη διαδικασία: οθόνη με τίτλο, χρωματιστά blocks, tip, Discord και Ko-fi. Το Recalled περιμένει 4 frames πριν καλέσει `PlayLevel` ώστε να ζωγραφιστεί το cover πριν το παγωμένο load.

---

## 5. Το Arcade (το hub)

Από το 3.0 το παιχνίδι δεν δείχνει ποτέ το αρχικό του menu. Ένα postfix στο `MainMenu.Start` κρύβει τα κουμπιά, σηκώνει το `LoadingCover` και φορτώνει το Arcade. Αν αποτύχει, δείχνεται το πραγματικό menu ώστε ο παίκτης να μην κολλήσει.

### 5.1 Πώς υπάρχει το δωμάτιο

Το Arcade ΔΕΝ είναι scene. Το `HubMode.Launch` φορτώνει το Night 6 (ώστε να έρθουν δωρεάν ο πραγματικός παίκτης, κάμερα, χειρισμοί, HUD) και μετά ο `HubController` χτίζει ένα δωμάτιο **πολύ ψηλά πάνω από το σπίτι** (origin `(0, 400, 0)`· η παραλία είναι στο y 300, το Facility στο 200, το Recalled στο 600) και μετακινεί τον παίκτη μέσα. Το `HubLayout.cs` χτίζει το περιεχόμενο από πίνακες με marks του developer (συντεταγμένες δωματίου + yaw): μαύρους τοίχους, το μεγάλο διαχωριστικό, κρεμαστές πινακίδες πάνω από τις πόρτες, τις μηχανές, τους πίνακες. Η άφιξη μετά από ένα mode σε βάζει **εκεί που στεκόσουν**.

### 5.2 Οι μηχανές

Κάθε μηχανή είναι `Interactable` (κράτα πατημένο)· το `HubLayout.Register` την αντιστοιχεί σε `HubMachine` και το `HubLaunch.Use` κάνει dispatch.

| Μηχανή | Τι κάνει |
|---|---|
| NEW GAME / CONTINUE | Το story του βασικού παιχνιδιού |
| ENDLESS | `EndlessSetupMenu` (δυο στήλες από το 3.8), PLAY |
| PAPA'S 10TH BIRTHDAY! | `PapaSetupMenu`, μετά το πάρτι |
| VERSUS | `VersusMenu`: θέσεις Mama και Survivor στο Steam lobby σου |
| SUMMER VACATION | Η παραλία |
| ENDLESS SHOP / PAPA SHOP / VERSUS SHOP | `ShopPanel` (όλα με shards από το 3.8) |
| GUMBALLS | Ένα gadget για 150 shards |
| OUTFITS | Αγόρι ή κορίτσι, Creator, DLC χαρακτήρες |
| KALEIDOSCOPE | Το Memories DLC του παιχνιδιού (ξεκλειδώνει μετά το story) |
| LEVEL SELECT | Ξαναπαίξε οποιαδήποτε νύχτα του story |
| OPTIONS | Ρυθμίσεις, ένταση, 4 sliders εφέ (Blur, Bloom, Glow, Film) |
| QUIT | Κράτημα 1,5 s, μετά κλείνει |
| Η ΠΟΡΤΑ (πίσω από το σημείο άφιξης) | `RcDoorMenu`: βγες μόνος / κάλεσε φίλους / online (Recalled) |
| PINBALL, AIR HOCKEY | Παράγραφος 13 |
| ΚΑΘΡΕΦΤΗΣ (δίπλα στο OUTFITS) | `HubMirror` |
| STATS BOARD, HOW-TO-PLAY BOARD | Πίνακες ανάγνωσης |
| **DAILY PAPERS (3.8)** | 3 χαρτάκια δίπλα στον καθρέφτη + το χαρτί REROLL QUEST (παράγραφος 8) |

*Σημείωση: το Waygetter Facility ΔΕΝ είναι πια ξεχωριστή μηχανή ούτε F9. Παίζεται ως χάρτης του Endless.* (Το F9 του Arcade σε dev builds ανοίγει μόνο τον trailer director.)

### 5.3 OverlayPanel: οι οθόνες του Arcade

Οι οθόνες ΔΕΝ είναι clones των menus του παιχνιδιού. Το `OverlayPanel` (στο `OptionsMenu.cs`) είναι πάνελ πλήρους οθόνης, οδηγούμενο από ποντίκι, με δικό του hit-testing, ώστε να δουλεύει σε οποιοδήποτε level χωρίς `EventSystem`:
- `Show()` χτίζει το UI στο πρώτο άνοιγμα (ποτέ στο `Awake`), παγώνει τον παίκτη, ελευθερώνει τον κέρσορα.
- `OverlayPanel.BlocksWorld` είναι true όσο είναι ανοιχτό menu και 0,3 s μετά το κλείσιμο.
- Οι υποκλάσεις στήνουν γραμμές (sliders, steppers, toggles, buttons, κείμενο) και παίρνουν hooks `OnShown/OnHidden/RefreshAll`.
- Χτισμένα πάνω σε αυτό: `OptionsMenu`, `EndlessSetupMenu`, `PapaSetupMenu`, `LevelSelectMenu`, `ShopPanel`, `OutfitMenu`, `CreatorMenu`, `VersusMenu`, `GadgetSubmenu`, `ScrapbookSubmenu`, `GumballReveal`, και όλα τα menus του Recalled.
- `RpgPanel.cs` (3.6): προαιρετική RPG εμφάνιση (ξύλινο πλαίσιο, χρυσές άκρες, κουμπιά-πλακέτες). Μόνο το Recalled τη χρησιμοποιεί.

**Αλλαγή 3.8 στο `EndlessSetupMenu`:** η οθόνη ήταν στριμωγμένη (τέσσερα κουτιά πληροφορίας). Ξαναχτίστηκε σε δύο στήλες: αριστερά RUN (Difficulty, Gadget, Profile, Map) και LOOK (χρώμα, δέσμη φακού, φωνή, Mama)· δεξιά τα run modifiers και **ένα κουτί DETAILS** που εξηγεί ό,τι δείχνεις (modifier, gadget, profile, map).

### 5.4 Ο καθρέφτης (`HubMirror.cs`)

Πραγματικός επίπεδος καθρέφτης χωρίς custom shader. Επειδή είσαι first person και δεν έχεις σώμα, ο καθρέφτης χτίζει **το παιδί σου με τα ρούχα σου** σε layer 30 που το βλέπει μόνο η κάμερα του καθρέφτη. Δεύτερη κάμερα στο **είδωλο του ματιού** πίσω από τον τοίχο, με off-center frustum που ταιριάζει στο τζάμι:

```
d = dot(eye - center, n);  mirrorCam.position = eye - 2*d*n;  rotation = LookRotation(n, up)
near = d + 0.02;  k = near/d;  projection = OffCenter(...)
```

Αναστρέφεται αριστερά-δεξιά με texture tiling και δείχνεται ως emission σε μαύρο Standard υλικό. Ζωγραφίζεται μόνο όταν το τζάμι είναι στην οθόνη. Δείχνει και τον άλλο παίκτη (Versus lobby).

### 5.5 Το Awakening (`Awakening.cs`, 3.5, ιδέα του Sa1monfr)

Την **πρώτη φορά που φτάνει ποτέ ένας παίκτης**, ξυπνά ως **ξύλινο μανεκέν**: μαύρο, glitch, το σώμα σχηματίζεται από ξυλοκομμένα τσιπς, "...where am I?", "Find a mirror." Μένεις μανεκέν (και στον φίλο σου στο Versus) μέχρι να κοιτάξεις τον καθρέφτη και να διαλέξεις "Remember who you are", που ανοίγει το OUTFITS. Μία φορά ανά παίκτη (`PlayerPrefs` `KE_Awakened_v2`, `KE_Remembered_v2`). Το σώμα είναι το μοντέλο Boy με χέρια σηκωμένα στο `LateUpdate`, το ξύλο με copy του mesh χαρτογραφημένο από την ήρεμη στάση και procedural υφή.

### 5.6 Πίνακες και γωνία

`VersionTag` (κάτω δεξιά, μόνο στο Arcade): `PUBLIC VERSION 3.8`, με τα Discord και Ko-fi. `NET_VERSION = VERSION`: μόνο ίδιες εκδόσεις παίζουν online μεταξύ τους. Ο stats board διαβάζει το `EndlessStats`.

### 5.7 Το εργαλείο marks (`HubMarkTool.cs`, μόνο dev builds)

**E** ανοίγει πάνελ ονόματος· γράφεις όνομα, επιλέγεις Wall ή Prop, Enter. Wall: γραμμή στο δάπεδο. Prop: κουκκίδα που ακολουθεί. Οι θέσεις γράφονται στο log (`[HUB-MARK]`, `[BEACH-MARK]`, `[FACTORY-MARK]`). Το **3.8** πρόσθεσε στο Facility: **προεπιλογές ψηφίων** (στο κενό όνομα πάτα 1 έως 0 για Vase, Candle, Friend, Tape, WashingMachine, Speaker, FuseBox, Coin, Charger, Phone, με αυτόματη αρίθμηση), το **ύψος δαπέδου** στη γραμμή (0 ισόγειο, 4 πάνω όροφος, -4 υπόγειο), και αρχείο **`BepInEx\facility_marks.txt`**. Μόνο με `DevTools = true` τοποθετεί marks.

---

## 6. Endless

Το παιχνίδι από το οποίο ξεκίνησε το mod. Το Night 6 ("The Party") γίνεται ατέρμονο survival: οι στόχοι μπαίνουν σε τυχαία σειρά και η Mama σε κυνηγά.

### 6.1 Οι 8 στόχοι (και ο αποσυρμένος)

Κοινή μορφή: ένα singleton MonoBehaviour που φτιάχνεται στο level setup· το `StartObjective()` βάζει props σε **χειροποίητες θέσεις** με φρέσκα `Interactable`· η ολοκλήρωση καλεί πίσω τον manager. Πραγματικά vanilla components χρησιμοποιούνται μόνο ως πρότυπα clone.

| Στόχος | Αρχείο | Τι κάνεις | Σημειώσεις |
|---|---|---|---|
| Πλυντήριο | `WashingMachineObjective` | Βρες το και "Turn Off Washing Machine" (1,2 s) | Αόρατο marker στη θέση του πραγματικού· ο ήχος είναι το πραγματικό `dryer_clunk`, που πιάνεται μία φορά (το `Night2AudioScavenger` φορτώνει μια φορά το Night 2 από το menu) |
| Φακός | στον manager | Κράτα τον φακό ανοιχτό 30/60/90 s (διαιρεμένο με √difficulty) | `SetFlashlight(true)`/`MaxLight()`· τα kills ανιχνεύονται με polling `IsGlowing()` |
| 3 βάζα | `VaseObjective` | Σπάσε 3 από 6 θέσεις (0,6 s) | Clone του πραγματικού βάζου ×1,6, πράσινο· σπάσιμο = πραγματικό noise στη Mama |
| 3 κεριά | `CandleObjective` | Άναψε 3 από 6 | Το πραγματικό `Candle` component (`startOn = false`) |
| Ο φίλος του Tattletail | `TattletailFriendObjective` | "Say Hi" (1,2 s) σε 1 από 6 θέσεις | Clone του `PristineTattletail`, τυχαίο χρώμα από το pool |
| Κασέτα | `TapeObjective` | Γύρισέ την πίσω (κράτημα) σε 1 από 6 | Πραγματικό υλικό `vhs_tape` σε κουτί |
| Ηχείο | `SpeakerObjective` | Κλείσε το ηχείο (1 σταθερή θέση) | Παραγόμενος λευκός θόρυβος σε 3D πηγή |
| Ηλεκτρικός πίνακας | `FuseBoxObjective` | Κράτα 4,5 s στον τοίχο | Το κουτί (τρίγωνο προειδοποίησης, φως) είναι πάντα στον τοίχο· μόνο το trigger έρχεται και φεύγει |
| *Σίγαση θορύβου* (αποσυρμένος) | `NoiseObjective` | Μόνο πλυντήριο | Υπάρχει αλλά εκτός περιστροφής (διπλασίαζε το πλυντήριο) |

Οι στόχοι έρχονται από μια ανακατεμένη τσάντα (`NextObjectiveType`) με όλους τους τύπους μία φορά και χωρίς διαδοχική επανάληψη (και στα όρια της τσάντας).

### 6.2 `EndlessRunManager`: η μηχανή καταστάσεων

Ένα `DontDestroyOnLoad` singleton που χτυπά από το `Update()` του plugin. Σε κάθε στιγμή είναι ενεργό ένα πράγμα: στόχος, ξεκούραση ή τηλέφωνο.
- **Ξεκούραση** (`REST_DURATION = 30 s`): φώτα αναμμένα, η Mama φεύγει· το κείμενο μπαίνει **μία φορά** (το `SuperTextMesh` ξαναρχίζει την εμφάνιση σε κάθε ανάθεση).
- **Τηλέφωνο:** `LOOP_LENGTH = 3` (κάθε 3ος ολοκληρωμένος στόχος), `PHONE_RING_DURATION = 60 s`. Η απάντηση τελειώνει το run καθαρά (exfil) και πληρώνει **25%** των shards του run. Αν χτυπήσει και χαθεί, ο επόμενος στόχος ξεκινά αμέσως.
- **Θάνατος:** άγγιγμα ή οπτική επαφή με τη Mama, εκτός από το Totem of Undying. Σε Flashlight Survival, 3 φορές που ο φακός σβήνει από τη Mama τελειώνουν το run.
- **Δυσκολία:** Easy 0,7 / Normal 1,0 / Hard 1,4 / Impossible 2,0 (κλιμακώνει τη διάρκεια συναγερμού και την ταχύτητα περιπολίας της Mama).
- **7 modifiers** (όλα off): Low Battery Mode, No Rest for the Wicked, No More Mama! (μέγιστη επιθετικότητα), Batteries Not Included, Always Hungry, Brushing is Key, Power Ups.
- **Chaos Round** (15% κάθε στόχο): δύο στόχοι μαζί, διπλά shards. **Lucky Night** (10% στην εκκίνηση): διπλά shards. Τυχαία γεγονότα (`EndlessEvents`), π.χ. **Gift from Mama**.
- **Power Up coins** (`PowerUpObjective`): κέρμα σε χειροποίητη θέση (20% ανά στόχο, μόνο με το modifier Power Ups): Max Stats, Strong Battery, Totem of Undying.
- **Care stations:** Tattletail πεινά, λερώνεται, αδειάζει μπαταρία. Τα feed/groom/charge stations κρατιούνται ενεργά (patch στο `ConditionUpdate`).
- **Στατιστικά:** `EndlessStats` (`Stats.txt`): τελευταία/μεγαλύτερη επιβίωση, σύνολο στόχων, θάνατοι, exfils.
- **Ο στόχος-μυστικό "True Ending"** (`TrueEndingEasterEgg`): 7 βήματα· πύλη που εξαφανίζεται, πύλη kaleidoscope που τελειώνει το run.

### 6.3 Η Mama (`MamaController`)

Λεπτό περιτύλιγμα του πραγματικού `MamaManager` που παρακάμπτει το `MamaSpawner`: `InitializeNewMama()`, `ActivateMama()`, χειροκίνητη μουσική `mama_music`. Η Mama **μετακινείται με teleport σε patrol nodes** (`Mama.MoveToNode`). Grace 1,5 s μετά από teleport. Κάθε νέκρωση περνά από `RequestJumpscareDeath`. Ο ιδιαίτερος κανόνας: **το σπίτι σβήνει τα φώτα όταν είναι η Mama έξω** (`RM.lightsController.RequestLightsOut`).

### 6.4 Το Profile (3.7): 34 ψυχολογικά traits

Πριν το run διαλέγεις **δύο traits από 34** (γραμμή Profile στην οθόνη Setup, που ανοίγει την οθόνη PROFILE). Κάθε trait έχει όφελος και τίμημα· τα traits **δεν δίνουν ποτέ extra shards**. Οι δύο επιλογές σου δίνουν τίτλο ("The Methodical Survivor") και 6 ζευγάρια είναι ονομασμένα combos. Αρχεία: `EndlessTraits.cs` (τα 34, εξαιρέσεις, τίτλοι, combos, όλοι οι αριθμοί που ζητά το παιχνίδι), `EndlessTraitsRuntime.cs` (ταχύτητα, κλίμακα hold times, πάγωμα αναγκών, ακοές, φαντάσματα), `EndlessTraitsPatches.cs` (patches), `TraitPickerMenu.cs` (η οθόνη). Παράδειγμα: το κλειδί **X** προσπερνά έναν στόχο (Compulsive Checking). Λεπτομέρειες: `docs/ENDLESS_TRAITS_IDEAS.md`.

### 6.5 Gadgets και το gumball machine

Ένα επιπλέον εργαλείο ανά run, χωριστό από τα single-run Power Ups του shop. Από τη μηχανή GUMBALLS (**150 shards**), επιλέγεται στην Setup, χρησιμοποιείται με το **πλήκτρο 1**.

| Gadget | Αποτέλεσμα |
|---|---|
| Plunge-O-Launch | Στόχευσε το πάτωμα· ένα πιστόνι σε τραβά (εμβέλεια 14 m, cooldown 60 s) |
| Yo-Yo | Μικρότερη, ήσυχη εκδοχή (7 m, 2 χρήσεις) |
| Music Box | Νανούρισμα στέλνει τη Mama μακριά 20 s (μετά νυστάζεις 15 s) |
| Fortune Cookie | Ταΐζει τον Tattletail και διαβάζει τύχη (35% δώρο) |
| Flash Camera | Αν η Mama είναι κοντά, μπροστά, χωρίς τοίχο: την στέλνει μακριά 8 s |
| Spare Battery | Φορτίζει τον Tattletail τελείως |
| Toy Parrot (DLC) | Ένα παιχνίδι-παπαγάλος που κλαίει· η Mama πάει σε αυτό και σε αγνοεί (15 s) |

Οι ήχοι είναι συνθετικοί (`GadgetAudio`, 44,1 kHz).

---

## 7. Endless στο Waygetter Facility (νέο στο 3.8)

### 7.1 Τι είναι

Το **Waygetter Facility** (το εγκαταλελειμμένο εργοστάσιο Waygetter Electronics, 60 × 40 m, ισόγειο, πάνω όροφος, υπόγειο) υπήρχε ήδη ως κτίριο (`Factory*.cs`) από το 3.4, αλλά το story του (Night Shift) ακυρώθηκε στις 2026-09-28. Στο 3.8 έγινε **χάρτης του Endless**: στην οθόνη Setup η γραμμή **Map** (`The House` / `Waygetter Facility`) επιλέγει πού παίζεις. Είναι το **ίδιο ακριβώς Endless** (ο ίδιος `EndlessRunManager`, οι ίδιες κλάσεις στόχων, τα ίδια settings, shards, δυσκολία, modifiers, traits, gadgets, τηλέφωνο, ξεκούραση, Chaos Rounds), με τις παρακάτω διαφορές.

> **Κανόνας που έμαθε το έργο:** οι στόχοι του Facility ΔΕΝ γράφτηκαν από την αρχή. Πρώτα έγινε μια δική μου εκδοχή και ο owner την απέρριψε ("δες πώς γίνεται στο σπίτι, μην επινοείς"). Τώρα χρησιμοποιούνται οι πραγματικές κλάσεις του σπιτιού με αλλαγμένες θέσεις.

### 7.2 Πώς ξεκινά (ροή κώδικα)

1. `EndlessSetupMenu` → PLAY → αν `FacilityMap`: `HubLaunch.StartFacilityEndless()` → `EndlessSubmenu.LaunchEndless(true)` (ίδια settings με το σπίτι) → `FactoryMode.LaunchEndlessPreview()` (θέτει `Pending`, `EndlessPreview`, `PendingEndlessRun`, δεν πειράζει gadget/τίτλο).
2. Level load → `SceneBootstrap` κλάδος `FactoryMode.Pending` → `FactoryController.Create()`: χτίζει το κτίριο στο `Origin = (0, 200, 0)`, νύχτα, μετακινεί τον παίκτη μπροστά στο αυτοκίνητο.
3. `FactoryFreeRoam` (κλάδος `EndlessPreview`): ανοίγει το πορτμπαγκάζ, **φέρνει τον Tattletail στα χέρια σου** (`FactoryController.BringTattletail(false)`: οι ανάγκες του τρέχουν), βάζει την κάμερα (camcorder), δείχνει υπόδειξη, και προσθέτει το `FacilityRunStarter`.
4. `FacilityRunStarter`: μετά από 3 s και όταν υπάρχουν τα απαραίτητα, φτιάχνει τα care stations, το set dressing, τους patrol nodes της Mama, το βέλος, και καλεί `SceneMonitor.StartFacilityRun()` → `SetupMonoBehaviours()` + `StartEndless()` (χωρίς `ChargingStation.Setup`, `NoiseObjective`, `TrueEnding`).
5. Στο τέλος του run (`HubMode.ReturnToHub`), επιστρέφεις στο Arcade.

### 7.2.1 Διαφορές από το σπίτι (και πού στον κώδικα)

| Θέμα | Τι γίνεται | Πού |
|---|---|---|
| Θέσεις στόχων | Πίνακες `FacilityMarks` (Παράρτημα Β). Κάθε κλάση έχει `HouseXPositions` και μια ιδιότητα που επιστρέφει τα Facility όταν `FactoryMode.EndlessPreview` | `FacilityEndless.cs`, `VaseObjective`, `CandleObjective`, `TattletailFriendObjective`, `TapeObjective`, `PowerUpObjective` |
| Πλυντήριο / ηχείο / τηλέφωνο | Στέκονται στις θέσεις τους ΟΛΟ το run, ζωγραφισμένα με κώδικα (πλυντήρια ×3, ηχεία ×3, 1 τηλέφωνο, με solid collider)· ο στόχος ενεργοποιεί ήχο και hold σε ένα. (Τα αντίγραφα των πραγματικών μοντέλων δεν εμφανίζονταν στο Facility) | `FacilityMarks.BuildSetDressing()` |
| Ηλεκτρικός πίνακας | Στον δυτικό τοίχο (x ≈ 0,13), βλέπει ανατολικά | `FuseBoxObjective` (`FixedPosition`, `WallPoint`, `Facing` ως ιδιότητες) |
| Κασέτα | Το όνομα "tape" ταίριαζε με το διακοσμητικό **"Caution tape"** του Facility και το αντέγραφε. Στο Facility χρησιμοποιείται πάντα το fallback κουτί VHS με το πραγματικό υλικό `vhs_tape` | `TapeObjective.ResolveWhenReady` |
| Φακός | **Όχι φακός στην αρχή.** Ο στόχος "keep flashlight open" **αφαιρέθηκε** από το Facility (7 στόχοι) | `EndlessRunManager.NextObjectiveType` |
| HUD κείμενο | Το quest text του παιχνιδιού επιτρέπεται (ο στόχος χρειάζεται `RM.hud.SetNewQuest`) | `Patch_HUD_SetNewQuest_Hub` |
| Care stations | Δικά του, στα marks: **ψυγεία = Feed** (3 s), **βούρτσες/γραφείο = Groom** (3 s), **docks = Charge** (5 s). Ο ηχητικός πάγκος του σπιτιού δεν χτίζεται | `FactoryStations.cs` (`MarkedCareStations`, `MakeStationsInteractive`) |
| Σωματικό μέγεθος | **Μένεις παιδί** (δεν μεγαλώνεις σε ενήλικα, ώστε να φτάνεις τα αντικείμενα στο πάτωμα)· **+50% ταχύτητα** περπάτημα και τρέξιμο | `FactoryController` (`FasterHere`, `SPEED_MULT = 1.5`) |
| Φως | Ambient λίγο πιο φωτεινό από το κανονικό Facility (0,075) ώστε να φαίνονται τα props | `FactoryController.SetNight` |
| Room hints | Απενεργοποιημένα (ονομάζουν δωμάτια του σπιτιού) | `RoomHints.LookIn` |
| Chaos Round | Δεν διαλέγει τον αποσυρμένο "σίγαση θορύβου" | `EndlessRunManager` |

### 7.3 Η κάμερα (camcorder, `FactoryCamcorder*.cs`)

Κρατάς μια κάμερα. **Δεξί κλικ** την ανεβάζει στο μάτι, **αριστερό κλικ** (ανεβασμένη) αλλάζει **night vision**. Δεύτερη κάμερα στο μάτι με IR λάμπα που βλέπει μόνο αυτή· πράσινο και κοκκώδες. Ηχεί και αναβοσβήνει REC.
- **Μπαταρία:** αδειάζει ~5 λεπτά ανά πακέτο· <15% κάνει beep (θόρυβος). Έχεις 3 ανταλλακτικά. **Spare μπαταρίες** βρίσκονται στο κτίριο (11 θέσεις), τις παίρνεις κρατώντας πατημένο (`OnInteract` μέσω `Patch_Interactable_OnInteractComplete_CamBattery`), και **ξαναεμφανίζονται 1 λεπτό αργότερα** (`BATTERY_RESPAWN = 60`).
- **Θόρυβος (3.8):** το να ανάψεις το night vision κάνει **θόρυβο**, και μετά **κάθε 2 δευτερόλεπτα όσο είναι ανοιχτό** (`NIGHT_NOISE_EVERY = 2f`) μέσω του `FactoryController.MakeNoise` → `MamaManager.OnNoise`. Στο Endless Facility, όταν η Mama δεν είναι έξω, ένα prefix (`Patch_MamaManager_OnNoise_Facility`) δείχνει το **εικονίδιο θορύβου** του HUD (`RM.hud.OnNoise`) χωρίς να ακούσει η Mama.
- **Jam** (`Jam(seconds)`, για το μελλοντικό Versus): ο κυνηγός θα μπορεί να σκοτώνει την κάμερα.
- `NightLooking` / `NightOn`: ιδιότητες που διαβάζουν το Endless (arrow) και το patch φόβου.

### 7.4 Ο Tattletail φοβάται το σκοτάδι (το σωστό σύστημα)

Από το IL του `TattletailController`: ο φόβος (`ScaredUpdate`) κρίνεται από `RM.lightsController.AreLightsOn()` και `RM.flashlight.GetLightPercentage() < scaredThreshold`. Όταν είναι "σκοτεινά", ο Tattletail **πρώτα τρέμει** (`_currentShakeAmount`) και **μετά** το `screamingStartTimer` τον περνά σε ουρλιαχτό (screaming). Το 3.8 δεν μπλοκάρει το σκούξιμο. Αντί γι' αυτό, ένα postfix στο **`FlashlightController.GetLightPercentage`** (`Patch_FlashlightController_GetLightPercentage_NightVision`) επιστρέφει **1 όταν το night vision της κάμεράς σου είναι ανοιχτό** (`FactoryCamcorder.NightOn`). Αποτέλεσμα: με το night vision ανοιχτό ο Tattletail είναι ήρεμος, με κλειστό και σκοτάδι τρέμει και μετά ουρλιάζει, με τη δική του σειρά. Μόνο στο `EndlessPreview`.

### 7.5 Η Mama στο Facility

Η πραγματική Mama του παιχνιδιού (ο ίδιος `MamaManager`), πάνω σε **54 patrol nodes του Facility** (54 σημεία: σκοτεινές γωνίες και πόρτες σε όλο το κτίριο, πάνω στο δάπεδο: ισόγειο, πάνω όροφος, υπόγειο). Οι nodes και ο τρόπος εκκίνησης είναι ο κώδικας του παλιού Night Shift, που ξαναφέρθηκε από το git ιστορικό (`FactoryTapeProps.MamaPatrol`).

`FacilityMama` (`FacilityEndless.cs`):
- `Prepare()`: φτιάχνει ένα `GameObject` "Mama patrol nodes" με `PatrolNodeSet` και 54 παιδιά με `PatrolNode` (το `AddComponent<PatrolNode>()` δουλεύει), και `RegisterNodes()`.
- `Unfreeze(mm)`: το `freezeStateSetting = false`.
- `UseNodes(mm)`: **μετά** το `InitializeNewMama()` και **πριν** το `ActivateMama()`, θέτει το `currentPatrolNodeSet` στο σετ του Facility και ξανακαλεί `RegisterNodes()`. (Η σειρά έχει σημασία: το `InitializeNewMama` ενώνει το σετ του σπιτιού, και αν το αλλάξεις πριν, η Mama μαζεύει τα παιδιά του σετ του σπιτιού.)
- Το `MamaController.ActivateMamaForObjective` καλεί τα παραπάνω όταν `FactoryMode.EndlessPreview`.
- `Patch_MamaManager_IsAboveGround_Factory`: "πάνω από το έδαφος" μετρά από το δάπεδο του κτιρίου (y 200), ώστε το υπόγειο να είναι δεύτερο επίπεδο.
- `FacilityMamaWatch`: γράφει στο log "Mama at (x, y, z), N m from you" κάθε 4 s όσο είναι έξω.
- Safety net στο `Patch_MamaManager_OnNoise` (`GamePatches.cs`): το "αναγκαστικό alert όταν δεν έχει node" γίνεται το πολύ μία φορά ανά 8 s (πριν, κάθε noise το ξανάρχιζε και οι φωνές της επαναλαμβάνονταν 100 φορές).

> **Ιστορικό των λαθών (μην τα ξανακάνεις):** (1) αντίγραφα του node του σπιτιού στις θέσεις των marks και αλλαγή του `patrolNodes`: η Mama ξαναμάζευε τα nodes του σπιτιού γιατί το `RegisterNodes` διαβάζει τα **παιδιά του σετ**. (2) `Object.Destroy` των nodes του σπιτιού: αναβλήθηκε στο τέλος του frame, η Mama πρόλαβε και διάλεξε node που μετά εξαφανίστηκε, έμεινε χωρίς node, και κάθε θόρυβος την έβαζε σε alert. (3) σύγκριση `Type ==` που έσκασε στον παλιό Mono. Η τελική λύση είναι η παλιά δοκιμασμένη.

### 7.6 Όταν είναι έξω η Mama: τα φώτα σβήνουν

`FactoryController.SetHunt(bool)` (καλείται από το `MamaController` όταν ενεργοποιείται/απενεργοποιείται η Mama στο Endless Facility):
- Σβήνει **όλα τα lights του κτιρίου** (αυτά κάτω από τη ρίζα του Facility) **εκτός** από αυτά που ονομάζονται "Glow" (οι λάμψεις των μπαταριών και των stations)· ρίχνει το ambient στο 0,012.
- Στο τέλος του κυνηγιού επαναφέρει.
- Η φωτιά των κεριών, το φως του ηλεκτρικού πίνακα και η κάμερα παραμένουν (δεν ανήκουν στη ρίζα).

### 7.7 Λάμψεις (glows)

`FactoryStations.AddGlow`: ένα φωτεινό σφαιρίδιο με pulse (`GlowPulse`) πάνω από: **μπαταρίες (κίτρινο)**, **charge (πράσινο)**, **groom (ροζ)**, **feed (πορτοκαλί)**, ώστε ο παίκτης να ξέρει πού είναι. Ισχύει σε όλα τα modes του Facility.

### 7.8 Το βέλος προς τον κοντινότερο στόχο

`FacilityArrow.cs`: κίτρινο βέλος στο κάτω-μέσο της οθόνης με απόσταση ("14 m (upstairs)"). Μαζεύει τους ενεργούς στόχους από όλες τις κλάσεις (`RemainingPositions` για βάζα, κεριά, φίλο, κασέτα· `ActivePosition` για πλυντήριο, ηχείο, ηλεκτρικό, τηλέφωνο) και διαλέγει τον πιο κοντινό (κάθε όροφος μετράει ×2 στην απόσταση). Σε Chaos Round δείχνει τον κοντινότερο από τους δύο. Κρύβεται στις ξεκουράσεις.

### 7.9 Τι δεν έχει το Facility Endless (ακόμα)

Δεν έχει Versus (μελλοντικό: στο changelog γράφει "coming next"), δεν έχει True Ending, δεν έχει το charging station της ιστορίας. Το εργαλείο marks μένει dev-only.

---

## 8. Daily objectives (νέο στο 3.8)

### 8.1 Τι είναι

Τρεις στόχοι την ημέρα: **ένας εύκολος, ένας μεσαίος, ένας δύσκολος**, με ανταμοιβές **25 / 60 / 120 shards**. Αλλάζουν τα μεσάνυχτα (τοπική ώρα του παίκτη) και είναι οι ίδιοι όλη μέρα. Όταν ολοκληρωθεί ένας, **εξαφανίζεται** από τον τοίχο και πληρώνει αμέσως. Όταν ολοκληρωθούν και οι τρεις, ο τοίχος λέει "έλα αύριο".

### 8.2 Πού φαίνονται

- **Τρία μικρά χαρτάκια** καρφωμένα στον διαχωριστικό τοίχο του Arcade, **νότια του καθρέφτη** (όχι πίσω από τον παίκτη στην άφιξη). Πάνω από κάθε χαρτί, πάντα ορατό, τιέρ και ανταμοιβή ("EASY +25"). Στο χαρτί, ο στόχος και η πρόοδος ("Break 4 vases in Endless 2 / 4"). **Δεν αλληλεπιδράς με αυτά.**
- **Ένα τέταρτο χαρτί, REROLL QUEST**, το μόνο διαδραστικό (κράτημα 0,6 s).
- **Η λίστα πάνω δεξιά στην οθόνη** όσο είσαι στο Arcade (κρύβεται σε μενού, pinball, air hockey, Awakening). Ενημερώνεται κάθε δευτερόλεπτο.
- Αν ολοκληρώσεις έναν στόχο εκτός Arcade, το μήνυμα περιμένει όταν γυρίσεις (`Daily.TakePending()` στο `Hub.cs`).

### 8.3 Reroll

**Μία φορά ανά ημέρα.** Αλλάζει **όλους τους στόχους που δεν έχεις ολοκληρώσει** με άλλον του ίδιου τιέρ. **Ποτέ** δεν δίνει στόχο που η σημερινή σου πρόοδος ήδη ικανοποιεί (αλλιώς θα κάνεις reroll μέχρι να πάρεις δωρεάν ανταμοιβή). Μετά τη χρήση, το χαρτί εξαφανίζεται. Εξαφανίζεται και όταν έχουν ολοκληρωθεί και οι τρεις.

### 8.4 Κώδικας

| Αρχείο | Ρόλος |
|---|---|
| `DailyObjectives.cs` | Η κλάση `Daily`: το pool (`Def`: `Id`, `Tier`, `Text`, `Counter`, `Target`, `Best`), η επιλογή, η πρόοδος, ο έλεγχος ολοκλήρωσης, το reroll, το κείμενο λίστας |
| `DailyPapers.cs` | Τα χαρτάκια στον τοίχο, το χαρτί reroll, η λίστα στην οθόνη (`BuildHud`/`RefreshHud`), τα patches (`Patch_Interactable_OnInteractComplete_DailyReroll`) |
| Hooks σε άλλα αρχεία | Βλέπε 8.6 |

**Αποθήκευση:** στο `Shop.txt`, με κλειδιά που αρχίζουν με `Daily ` (το `Shop.Save` γράφει πλέον και αυτά): `Daily Date` (yyyymmdd), `Daily Done 0..2`, `Daily Pick 0..2` (δείκτης στο pool + 1), `Daily Reroll`, και ένα `Daily C <counter>` ανά μετρητή. Το `EnsureToday()` (σε κάθε πρόσβαση) καθαρίζει τα πάντα αν άλλαξε η ημερομηνία.

**Επιλογή:** `Picks` διαβάζει τα αποθηκευμένα· αν λείπουν, τα υπολογίζει με `System.Random(seed)` όπου seed = `ημερομηνία*31 + τιέρ*7 + 3` (ίδιο όλη μέρα, για όποιον ρωτήσει). Το `PickFor` προτιμά στόχους που δεν είναι ήδη ικανοποιημένοι.

**Μετρητές (`Daily.Add(counter, n)`, `Daily.Best(counter, value)`):** το `Add` προσθέτει στη μέρα· το `Best` κρατά τη μέγιστη τιμή (διάρκεια run, score). Μετά από κάθε αλλαγή το `Check()` ελέγχει ολοκληρώσεις: γράφει done, πληρώνει με `Shop.AddShards` μέσα σε `_paying` (ώστε η ανταμοιβή να μην μετράει στο "shards της ημέρας"), γράφει στο log και δείχνει μήνυμα (στο Arcade άμεσα, αλλιώς σε ουρά).

### 8.5 Το pool (21 στόχοι, 7 ανά τιέρ)

Δες το Παράρτημα Γ. Δεν υπάρχουν στόχοι Pinball, Air Hockey, Versus, Facility. Αφαιρέθηκαν αυτοί που εξαρτώνται από το πόσο έχεις προχωρήσει ή από μια ρύθμιση: "reach day N" στο Summer Vacation (αντικαταστάθηκε από "Play 1/3 day(s)"), κέρματα Power Up (απαιτούν modifier), jobs και bosses του Recalled (τελειώνουν), "Earn 300 shards" (έγινε 150).

### 8.6 Ποιος αναφέρει τι (οι hooks)

| Μετρητής | Πού αυξάνεται |
|---|---|
| `endless_obj` | `EndlessStats.RecordObjectiveCompleted` |
| `run_obj` (best), `hard_obj` | `EndlessRunManager.CompleteObjective` (το `hard_obj` όταν `_difficultyMult >= 1.39`) |
| `run_time` (best), `exfil` | `EndlessStats.RecordRunEnd` |
| `vases`, `candles` | `VaseObjective.BreakVaseAtSlot`, `CandleObjective.LightCandleAtSlot` |
| `coins` | `PowerUpObjective.OnCoinGrabbed` (δεν υπάρχει πια στόχος που το χρησιμοποιεί) |
| `served`, `beach_days` | `BeachShift` (πελάτης εξυπηρετήθηκε, μέρα τελείωσε στο `EndDay`) |
| `party_minutes` | `PartyGame` (κάθε πλήρες λεπτό) |
| `toys` | `RecalledEnemies` (κάθε νεκρό παιχνίδι), `jobs`/`boss` (έχουν hooks, χωρίς ενεργό στόχο) |
| `spent` | `Shop.TryBuy` και `Shop.SpendShards` (gumball, ρούχα, loot box) |
| `shards_today` | `Shop.AddShards` (όχι οι daily ανταμοιβές) |
| `pinball_games`, `pinball_score`, `hockey_wins`, `shutout` | Έχουν hooks αλλά **κανένας ενεργός στόχος** |

---

## 9. Οικονομία: τα shards (αλλαγή στο 3.8)

**Πριν (3.7):** shards (Endless), Party Tokens (πάρτι), Versus Tokens (Versus), Cash (Summer Vacation).
**Τώρα (3.8):** **όλα πληρώνουν shards** και **κάθε shop δέχεται shards**.

- `Shop.CurrencyKey(item)` επιστρέφει πάντα `SHARDS_KEY`· `CurrencyName` πάντα "shards".
- `Shop.AddPartyTokens` και `AddVersusTokens` έμειναν ως ονόματα αλλά καλούν `AddShards`.
- **Μετατροπή:** το `Shop.MergeTokensIntoShards()` τρέχει στη φόρτωση και μετατρέπει όποιο υπόλοιπο Party/Versus Tokens είχε ο παίκτης σε shards (μία φορά).
- **Πληρωμές:**

| Πηγή | Shards |
|---|---|
| Endless | 10 ανά στόχο + 10 ανά ενεργό modifier (όχι Power Ups), × δυσκολία, ×2 Chaos/Lucky, +25% στο exfil |
| Papa's party | **30** ανά πλήρες λεπτό (ήταν 100 tokens) |
| Versus | **150** για το κυνηγό ανά catch, **30** ανά στόχο του παιδιού (ήταν 1000/200 tokens) |
| Pinball | `5 + score/10000`, έως 100 ανά παιχνίδι |
| Air Hockey | 40 νίκη, 10 ήττα |
| Recalled | 20 ανά job, 100 ανά boss |
| Summer Vacation | μικρό μπόνους ανά μέρα (`10 + 5*(day-1)`) |
| Daily objectives | 25 / 60 / 120 |

- **Summer Vacation Cash** (tips και εβδομαδιαίος μισθός $100) **ΜΕΝΕΙ χωριστό νόμισμα** και αγοράζει μόνο τις αναβαθμίσεις της Mama (`BeachUpgrades`, κλειδιά `Beach ...` στο `Shop.txt`). Απόφαση του owner.
- **Τιμές shops:** δεν άλλαξαν. Gumball 150, outfits 100 ο καθένας (το πρώτο δωρεάν), loot box scrapbook 1000, χρώματα Tattletail 150 έως 1200, κ.λπ. (Πλήρης λίστα στο `docs/AFTER_HOURS_3.6_COMPLETE_REFERENCE.md`, παράγραφος 8.)
- **`Shop.txt`:** απλό αρχείο `Key = value`, ατομικές εγγραφές (`File.Replace` κρατά `Shop.txt.bak`). Το `Save()` γράφει τώρα ΚΑΙ τα κλειδιά `Daily `.
- Η εικόνα νομίσματος "token" (`Shards.GetTokenSprite`) δεν χρησιμοποιείται πια.

---

## 10. Versus

Online 1v1 πάνω στο Steam lobby: ο ένας είναι το **παιδί** (κανονικό Endless run με τους στόχους και το gadget του), ο άλλος ο **κυνηγός**, Mama ή Papa, ελεύθερος.

- **Θέσεις:** η μηχανή VERSUS δείχνει δύο θέσεις (Mama, Survivor) κοινές με τον φίλο στο lobby (`VersusLobby`). Ο **host κατέχει τις θέσεις**· ο guest ζητά (`VersusClaim`), ο host αποφασίζει και στέλνει το αποτέλεσμα (`VersusState`). Μια άδεια θέση προσφέρει INVITE PLAYER. Solo, μένει τοπικό και ένα bot (`VersusBots`) γεμίζει την άλλη πλευρά για δοκιμή. START (`VersusStart`) ξεκινά και τα δύο παιχνίδια.
- **Ο κυνηγός δεν περπατά.** Η κάμερα του παιχνιδιού μπαίνει πίσω από ένα σώμα σε ένα από τα πραγματικά patrol nodes της Mama, με night vision. Εργαλεία: **1 MAP** (top-down χάρτης, κλικ για teleport, cooldown 15 s, θόρυβοι του παιδιού ως κόκκινες κουκκίδες), **2 KILL LIGHT** (σβήνει τον φακό, 7 m, cooldown 20 s), **3 MAMA'S GAZE** (παγώνει το παιδί 2 s, cooldown 30 s) ή **PAPA'S ABSENCE** (αόρατος 30 s), **μετρητής kill** (κάθε θόρυβος του παιδιού τον γεμίζει, στο 4 μπορεί να πιάσει). Minimap πάντα.
- **Το πιάσιμο** παίζει τη δολοφονία σε first person (η κάμερα πλησιάζει, ουρλιάζει, 3 γρατζουνιές, το παιδί πέφτει πίσω).
- **Αυθεντία:** το παιχνίδι του παιδιού τρέχει το πραγματικό Endless· ο μετρητής ανήκει στον κυνηγό. `VersusNet` events: `Noise, MamaAway, MamaBack, FlashKill, Caught, Saved, RoundOver, ParrotLure, Gaze, Absence, Voice`.
- **Σώματα και skins** (`VersusHunters`): Mama (Classic, Kaleidoscope, Golden, Shadow, Crimson) και Papa (Classic, Golden, Midnight, Crimson, Rainbow) από το VERSUS SHOP, με shards.
- **3.8:** τα tokens έγιναν shards (150/30). Το Waygetter Facility θα γίνει δεύτερος χάρτης Versus στο μέλλον: **δεν είναι στο 3.8** (αναφέρεται "coming next"). Έχει ήδη προετοιμαστεί: το `FactoryCamcorder.Jam(seconds)` και το σχέδιο ενός hunter με 3 ορόφους· μια ημιτελής έκδοση του `VersusMama.cs` για το Facility αποθηκεύτηκε και δεν μπήκε.

---

## 11. Papa's 10th Birthday! (3.0)

Πάρτι αντί για κυνηγητό, στο ίδιο Night 6. Η Πείνα του Papa πέφτει από γεμάτη στο 0 σε **3:00**, η Ψυχαγωγία του πάρτι σε **2:30**. Το πάρτι τελειώνει όταν η Πείνα φτάσει 0· αν η Ψυχαγωγία φτάσει 0, η Mama κυνηγά μέχρι να ανακάμψει.
- **Τάισε τον Papa** (`PartyGame.cs`): κάθε φαγητό είναι **δύο υλικά σε οποιαδήποτε σειρά**: Popcorn (καλαμπόκι + βούτυρο), Cotton Candy, Ice Cream, Hot Dog, Nachos, Candy Apple, Waffle & Crepe, Pancakes. Κάθε φαγητό γεμίζει 30% την Πείνα. Hotbar με αριθμούς, κάρτα συνταγών στον τοίχο.
- **Υλικά** (`PartyWorld`): 18 σημεία, κάθε 30 s ένα τυχαίο υλικό αν το σημείο είναι άδειο. 4 μόνιμες μηχανές φαγητού.
- **Κράτα το πάρτι διασκεδαστικό:** 7 party Tattletails (clones με τυχαίο χρώμα) που φιλοξενούν 3 minigames (Face Paint, Rock-Paper-Scissors, Balloon)· νίκη = +20% Ψυχαγωγία· piñatas· **bouncy castle**.
- **Σκηνικό** (`BirthdayDecor`): η Mama με monocle και cylinder, τραπέζι, κεκάκια. Μουσική συνθετική (`BirthdayMusic`, `PartyTune`) και θετική (πλησιάζοντας το ηχείο δυναμώνει).
- **Shards:** 30 ανά πλήρες λεπτό (3.8). Το πάρτι έχει δικό του shop (PAPA SHOP) με χωριστά κλειδιά ιδιοκτησίας (`Party <key>`).

---

## 12. Summer Vacation (3.3)

Το μπαρ της Mama με smoothies σε ηλιόλουστη παραλία (στυλ Papa's Freezeria), μόνο-παίκτης. Χτίζεται όπως το Arcade στο y 300, με ουρανό, ήλιο, άμμο, αμμόλοφους με φοίνικες και θάλασσα (`Beach.cs`, `BeachLayout.cs`).
- **Η δουλειά** (`BeachShift.cs`, ~2600 γραμμές): Tattletails κατεβαίνουν από τους αμμόλοφους και στήνονται στην ουρά. Παραγγελία: μέγεθος ποτηριού, φρούτα, blend (σταμάτα στη σωστή υφή), toppings. 10 φρούτα, 10 toppings.
- **Μέρες:** κάθε μέρα ξεκλειδώνει φρούτο και topping· από τη μέρα 6 δύο toppings, από τη 8 τρία. **Πελάτες** με ονόματα και διαθέσεις (ευγενικοί, ανυπόμονοι, επιθετικοί, επικριτικοί). Κάθε μέρα προστίθεται ένας **τακτικός** (έως 5) που παραγγέλνει πάντα το ίδιο. Ο **Επιθεωρητής** έρχεται κάθε 3η μέρα (παράλογη παραγγελία, τριπλή πληρωμή).
- **Η Mama κρίνει τη μέρα:** ≥80% περήφανη και σε δίνει tip, ≥50% μένεις, <50% σε τρομάζει και ξαναπαίζεις τη μέρα. Πληρωμή: shards και **εβδομαδιαίος μισθός $100** κάθε 7η μέρα. **Τα tips αγοράζουν αναβαθμίσεις** (Tip Jar, Blender Alarm, Comfy Cushions, Free Peanuts, Turbo Blender...).
- **Εκπαίδευση** την πρώτη φορά, μουσική συνθετική (`BeachMusic`).
- **3.8:** ο μετρητής `served` (ανά πελάτη) και `beach_days` (ανά τέλος μέρας) τροφοδοτούν τα daily objectives.

---

## 13. Pinball και Air Hockey (3.4)

### 13.1 Tattletail Pinball

Πραγματικό φλίπερ στο hall, first person. Κράτα για να παίξεις· αριστερό κλικ (ή A) και δεξί κλικ (ή D) = flippers, **SPACE** κράτημα για το plunger, Q ή E για να φύγεις.
- **Η μηχανή του τραπεζιού είναι 2D μαθηματικά** (`PinballTable.cs`): επίπεδη φυσική σε μέτρα, **βήματα 1 ms** ώστε η γρήγορη μπάλα να μη περνά τοίχους: τοίχοι, slingshots, αστέρια, 3 pop bumpers, η πόρτα του Σπιτιού, η ράμπα, η λωρίδα του plunger, δύο flippers. Ακτίνα μπάλας 13,5 mm.
- **Κανόνες:** το **Σπίτι** τρώει την μπάλα για πολλούς πόντους ("It's hungry..."), 3 bumpers Tattletail που αναπηδούν, **ράμπα** με combo, 4 αστέρια (όταν ανάψουν όλα ανεβαίνει ο πολλαπλασιαστής), πολλαπλασιαστής που ανεβαίνει με τον χρόνο ζωής της μπάλας (έως ×8). 3 μπάλες ανά παιχνίδι· το high score αποθηκεύεται (`PlayerPrefs` `KE_PinballHigh`).
- **Δοκιμές offline:** το `PinballTable` τρέχει headless· χιλιάδες προσομοιωμένες μπάλες βρήκαν σφραγισμένες λωρίδες, παγίδες, διαρροές.
- **Shards (3.8):** `5 + score/10000`, έως 100 ανά παιχνίδι.

### 13.2 Tattletail Air Hockey

1v1, πρώτος στο 7. Κράτα σε ένα άκρο· η κάμερα πέφτει πίσω από το mallet σου και το ποντίκι (ή WASD) το κινεί. SPACE = ρεβάνς.
- **Φυσική** (`AirHockeyTable.cs`, 2D): το puck γλιστρά, οι άκρες είναι κυρτές, οι γωνίες κομμένες, οι τοίχοι λύνονται τελευταίοι.
- **Μόνος:** ένα AI Tattletail παίζει στο άλλο άκρο (φυλά, στοχεύει, χτυπά με ανακλάσεις, γίνεται λίγο καλύτερο όταν κερδίζεις). **Με φίλο** στο lobby: αντίθετα άκρα· ο host τρέχει το puck, ο guest στέλνει το mallet του (`PacketId.AirHockey = 9`).
- **Shards (3.8):** 40 νίκη, 10 ήττα.

*Δεν υπάρχουν πια daily στόχοι για Pinball και Air Hockey (αφαιρέθηκαν στο τέλος του 3.8).*

---

## 14. Recalled

Third-person **co-op RPG για έως 4 παίκτες** σε μια **πόλη φτιαγμένη εξ ολοκλήρου με κώδικα** (origin `(0, 600, 0)`).

### 14.1 Σχεδιασμός

- **Σκηνικό:** προάστιο τη νύχτα σε καραντίνα· οι ενήλικοι "συλλέχθηκαν" στη Γραμμή Recall· τα παιδιά που έμειναν ζουν σε ένα δεντρόσπιτο στο πάρκο (το μόνο ασφαλές μέρος). Είσαι ένα από αυτά.
- **Κύκλος:** πάρε jobs από τον πίνακα, πολέμα τα παιχνίδια (Tattletails σε χρωματικά επίπεδα και τα παιχνίδια γύρω τους) με όπλα που **φτιάχνεις από τα δικά τους μέρη**, ανέβαινε επίπεδα, άνοιξε την επόμενη περιοχή νικώντας το boss, δώσε τα jobs για εμπειρία και ανταμοιβές.
- **Πέντε περιοχές, πέντε bosses:** The Suburbs (Patches), The Mall (Display Model), The School (Hall Monitor), The Junkyard (Papa), The Waygetter Facility (Mama). Επίπεδα πύλης 1, 4, 7, 10, 13.
- **Επίπεδα 1 έως 15** (η πόλη δίνει XP μέχρι το 15). Κάθε επίπεδο +30 ζωή. **25 jobs** από τα παιδιά.
- **Μάχη σε πραγματικό χρόνο** (σημαντικό: το πείραμα card duels ΔΕΝ μπήκε): κάθε παιχνίδι **προετοιμάζει** πριν χτυπήσει (πορτοκαλί μπάρα πάνω από το κεφάλι), εσύ κάνεις **roll (Alt)**, **block (δεξί κλικ)**, ή **perfect block** (παράθυρο 0,3 s) για αντεπίθεση ×1,6.
- **Co-op:** έως 4· ο host (ιδιοκτήτης του lobby) τρέχει τα παιχνίδια· καθένας κρατά τον δικό του χαρακτήρα, λάφυρα και job· στην πόλη φίλου δουλεύετε όλοι στο **job του host**.
- **Save:** δικό του προστατευμένο αρχείο (`Recalled.sav` + `.bak` + `.best.sav`, υπογεγραμμένο με το `RecalledSecret`)· τίποτα κοινό με Endless/Versus.
- **Mounts** (3.6), **notices** (15 χαρτιά στις στοίβες σκραπ), **φωνές μόνο ως κείμενο** (333 γραμμές με ids και ομιλητές, χωρίς ηχογραφήσεις).

### 14.2 Κώδικας (πού είναι τι)

| Περιοχή | Αρχεία |
|---|---|
| Mode, session, static state | `Recalled.cs` (`RecalledMode`, `Rc`, `RecalledController`, `RcLamps`, `RcMeshSink`) |
| Κόσμοι | `RecalledWorlds.cs` (`RcWorlds`: **στο 3.8 το `Now` επιστρέφει πάντα TOWN**, το `EgyptOpen` πάντα false) |
| Plan πόλης, υλικά, πλοήγηση | `RecalledTown.cs` (`RcMat`, `RcSink`, `RcTown`, `RcNav`, `RcField`) |
| Διακόσμηση | `RecalledProps.cs` |
| Δεδομένα (αντικείμενα, συνταγές, εχθροί, jobs) | `RecalledData.cs` |
| Κανόνες (επίπεδα, κλιμάκωση, πύλες) | `RecalledRules.cs` |
| Ο χαρακτήρας σου | `RecalledChar.cs` |
| Το save | `RecalledSave.cs`, `RecalledSecret.cs` (όχι στο git) |
| Ο παίκτης | `RecalledPlayer.cs` (`RcPlayer`), `RecalledMount.cs`, `RecalledFx.cs` |
| Τα παιχνίδια | `RecalledEnemies.cs` (`RcEnemy`, `RcEnemies`, `RcLooks`) |
| Co-op | `RecalledNet.cs` (`RcPlay`, `RcLobby`, `RcPeer`, `RcNet`, `RcRemote`, `RcKidRig`) |
| UI | `RecalledUI.cs`, `RecalledNoticeUI.cs`, `RpgPanel.cs`, `RecalledTalk.cs` |
| Ιστορία και φωνές | `RecalledLines.cs`, `RecalledVoiceData.cs`, `RecalledVoice.cs`, `RecalledNotices.cs` |
| Οδηγός | `RecalledGuide.cs` |
| **Μουσική** | `RecalledMusic.cs` (`RcMusic`), **`RecalledTracks.cs` (`RcTracks`, νέο στο 3.8)** |
| Ατμόσφαιρα | `RecalledAtmos.cs` |

### 14.3 Τι άλλαξε στο Recalled στο 3.8

- **Ηχογραφημένη μουσική:** 7 κομμάτια (ένα background της πόλης, 5 combat, ένα background του Facility). Το παιχνίδι δεν αποκωδικοποιεί mp3 στο runtime, άρα μετατράπηκαν σε **PCM WAV 32 kHz mono** (`tools/convert_to_wav.ps1`) και φορτώνονται σε κομμάτια (`RcTracks.Load` coroutine, 16-bit PCM). Αρχεία `Sounds/rc_*.wav` δίπλα στο DLL (στο payload του installer, εκτός git). Στο `RecalledMusic`: `RecordedMusic`, `WantTrack`, `_bgFile/_cmbFile`, μόνο ένα background και ένα combat clip στη μνήμη τη φορά, fade μεταξύ τους· αν λείπει αρχείο, μένει η συνθετική μουσική.
- **Το Egypt αφαιρέθηκε:** αφαιρέθηκαν `RecalledEgypt.cs`, `RecalledEgyptLooks.cs`, `RecalledDataEgypt.cs`, `docs/RECALLED_EGYPT_LINES.md`· τα υπόλοιπα branches `RcWorlds.Egypt` στον κώδικα μένουν νεκρά (καθαρισμός που μένει). Ο κόσμος είναι πάντα η πόλη.
- **Shards:** 20 ανά job, 100 ανά boss. **Daily:** μετρητές `toys` (κάθε νεκρό παιχνίδι), `jobs`, `boss`.
- **Ιστορικό:** δοκιμάστηκε πλήρης αντικατάσταση της μάχης με Wizard101-style card duels (branch `recalled-card-duels`, τοπικό)· ο owner το αποφάσισε εκτός.

---

## 15. Τα παιδιά, τα ρούχα, ο Creator, οι φωνές, τα χρώματα

### 15.1 Μοντέλα παιδιών

Δεν υπήρχαν assets παιδιών στο παιχνίδι. Το παιδί στο Versus, το σώμα στον καθρέφτη, το παιδί στο OUTFITS, το παιδί στο Recalled και κάθε DLC χαρακτήρας **παράγονται offline από Python και ενσωματώνονται στο DLL** (`assets/kid_*.bin`: boy, girl, dez, kaz, rat, plague, fish· `creator_kit.bin`).
- **`tools/kid_model.py`:** κάθε μέρος σώματος είναι έλλειψη/κάψουλα ως **signed distance function**· τα μέρη ενώνονται με smooth-min· δειγματοληψία σε voxel grid· **marching cubes**. Κάθε κορυφή παίρνει υλικό και περιοχή (βάρη skinning ανά περιοχή). Animations (`idle`, `walk`, `run`, `scared`) δειγματοληπτούνται σε **30 καρέ** quaternions οστών. Έξοδος: δυαδικό `KID2`.
- **`ChildModel.cs`:** `ChildModel.Load` διαβάζει το `KID2`, `Build(name, gender, skin)` φτιάχνει οστά, `SkinnedMeshRenderer`, `KidAnimator`, `KidLook`. Το `KidAnimator` ανακατεύει idle → walk → run με την **μετρημένη ταχύτητα**, ένα scared pose, `Flinch` (πέφτει όταν τον πιάνει η Mama), τρέμουλο και pose αέρα. Το `KidLook` χρωματίζει τα υλικά (rainbow ρούχα κινούνται). `KidSkins`: ρούχα ανά χαρακτήρα, κλειδιά ιδιοκτησίας, `Code` (`φύλο*100 + ρούχο`, **99 = ΜΑΝΕΚΕΝ**).

### 15.2 Χαρακτήρες και ρούχα

Boy και Girl (δωρεάν, το πρώτο ρούχο δωρεάν· τα υπόλοιπα 100 shards), και DLC: **Dez** (του DrDezmen), **Kaz** (του Kazyzone), **Rat King**, **ImPoPzzZ** (γιατρός πανούκλας), **Sa1monfr** (δυνατός άντρας με κεφάλι ψαριού). Καθένας έχει 5 έως 8 ρούχα, με πάντα ένα Kaleidoscope (rainbow). Ο συνδυασμός σου στέλνεται ως ένας int (`Code`)· ένα custom παιδί στέλνει και το string `CharacterSpec`.

### 15.3 Ο Character Creator (`CharacterKit.cs`, `CreatorMenu.cs`)

Από το OUTFITS με "Kid: Custom" (EDIT CHARACTER). Tabbed `OverlayPanel` με ζωντανή προεπισκόπηση. Κάθε κομμάτι χτίστηκε χωριστά (`KIT1`): 6 σώματα (3 κατασκευές × παντελόνι/φούστα), 3 κεφάλια, 7 χτενίσματα, μάτια, φρύδια, μύτες, στόματα, γυαλιά, πανάδες, τσιρότα, κύκλοι ματιών. Επιλογές: Frame, Bottoms, Head, Hair (12 χρώματα), Skin (8), Eyes, Brows, Nose, Mouth, **8 sliders**, Expression (6), Glasses, Freckles, Band-aid, Eye circles, Accents, Voice (5 τύποι + pitch). **`CharacterSpec`: ένα παιδί σε 29 bytes** σε base64, μέσα σε κάθε pose packet. Κάθε χαρακτηριστικό τοποθετείται **πάνω στο πλέγμα προσώπου** και στρέφεται στην κάθετο της επιφάνειας ώστε οι sliders να γλιστρούν κατά μήκος του προσώπου.

### 15.4 Οι φωνές (`KidVoice.cs`)

Μικρός **συνθεσάιζερ formants**, χωρίς ηχογραφήσεις. Κάθε ήχος είναι λίστα keyframes `(time, pitch, loudness, voicing, breath, vowel)`. Rendering στα 22.050 Hz: sawtooth glottis (PolyBLEP), θόρυβος αναπνοής, **4 αλυσιδωτοί συντονιστές formants**, φίλτρο χειλιών. Βιβλιοθήκη: `Jump` ("hup!"), `Land`, `SeeMama` (14 αντιδράσεις), `Terrified`, `Caught`. 5 τύποι φωνής (Bright 300 Hz, Soft, Raspy, Squeaky, Deep). Η φωνή σου είναι **μόνο ήχος**: δεν τραβά ποτέ τη Mama.

### 15.5 Τα χρώματα του Tattletail και η γούνα

- **Gold και Diamond είναι custom** (το `RecolorManager` του παιχνιδιού αλλάζει μόνο έτοιμα Material assets: δεν υπάρχει χρυσό). Η γούνα είναι `Custom/FurShaderV1`· το mod αλλάζει **μόνο το `_FurColor`** (`FurShine`).
- `EarlyColor`: μόλις υπάρξουν Tattletail, recolor manager και φακός, εφαρμόζει το χρώμα σου ώστε να μην φανεί ποτέ ο μωβ Tattletail του story. `PristineTattletail`: καθαρό αντίγραφο για τους φίλους του Tattletail κλπ.
- **Φακοί:** Diamond Flashlight (single-run, 250), Golden Flashlight (passive, 800), **χρώματα δέσμης** (`FlashlightBeam`).

### 15.6 Προεπισκοπήσεις

`PreviewBaker` (στιγμιότυπα 320×320 PNG ανά χρώμα/δέσμη, ψημένα **στη μηχανή του παίκτη**, `ShopPreviews\color_gold_v4.png`)· `PreviewStage` (ζωντανή εκδοχή στα menus: layer 31, δικά του φώτα, δικό του camera).

---

## 16. Δίκτυο (Steam P2P)

Όλο το online είναι **Steam P2P** μέσω των Steamworks.NET τύπων που ήδη βρίσκονται στο `Assembly-CSharp-firstpass.dll`. Το mod **δεν καλεί ποτέ `SteamAPI.Init()`** (το κάνει το παιχνίδι και αποτυγχάνει στη δεύτερη κλήση). Υπάρχουν **δύο ξεχωριστά lobby συστήματα** (2 παικτών για Versus/Air Hockey, έως 4 για Recalled) και ένας παίκτης είναι μόνο σε ένα τη φορά.

- **`MpLobby`**: μόνιμο singleton, μόνο-φίλοι, 2 παίκτες, προσκλήσεις από το Steam overlay. **Έλεγχος έκδοσης:** κάθε μέλος δημοσιεύει το `NET_VERSION` ως member data (`ke_net`)· `PeerCompat` `Same`/`Different`. **Γι' αυτό όλοι χρειάζονται 3.8 για online.**
- **`MpPacket`**: ελάχιστος δυαδικός writer/reader (primitives σε σταθερή σειρά). **`MpNetworking`**: τυλίγει `SendP2PPacket/ReadP2PPacket`, πίνακας `PacketId → handler`, `SendReliable/SendUnreliable`.
- **IDs:** 0 Welcome, 1 LobbySettings (δυσκολία + modifiers, host → guest), 2 LevelTransition, 3 VersusState, 4 VersusClaim, 5 VersusStart, 6 VersusPose (~10/s, unreliable), 7 VersusEvent (reliable), 8 Marks, 9 AirHockey. Το Recalled έχει δικά του ops (στο `RecalledNet.cs`).
- **Host-authoritative ρυθμίσεις** (`MpLobbySettingsSync`): η δυσκολία και τα modifiers κλιμακώνουν τη μία κοινή Mama, άρα είναι μία τιμή: του host.
- **Poses και avatars:** `VersusNet` στέλνει θέση, yaw, παρουσία, κωδικό skin και το `CharacterSpec` ~10 φορές το δευτερόλεπτο. Το `VersusAvatars` ζωγραφίζει τον άλλο παίκτη στη θέση των **ποδιών** του (`WorldRays.FeetOf`).

---

## 17. Αποθηκεύσεις, config, χειρισμός

### 17.1 Config (`BepInEx\config\com.kaleidoscope.endless.cfg`, δεν στέλνεται ποτέ)

| Κλειδί | Προεπιλογή | Σημασία |
|---|---|---|
| `Developer/DevTools` | false | Εργαλείο marks (E) στο Arcade/Endless/Facility· trailer director (F9). **Δεν χρειάζεται πια για να παίξεις το Facility** |
| `Developer/OpenLastStop` | false | Παλιό, του Egypt (νεκρό) |
| `Cosmetics/RecalledGraphics` | true | Ουρανός, πυγολαμπίδες, color grade του Recalled |

### 17.2 Αρχεία (στο `Documents\Tattletail After Hours\`)

`Shop.txt` (νομίσματα, ιδιοκτησία, μέρα/ταμείο/αναβαθμίσεις παραλίας, **και από το 3.8 τα κλειδιά `Daily `**), `Stats.txt`, `Scrapbook.txt`, `Shards.txt` (παλιό, μεταφέρεται), `Recalled.sav` (+ `.bak`, `.best.sav`), `ShopPreviews\*.png`. Στο `PlayerPrefs`: `KE_Awakened_v2`, `KE_Remembered_v2`, `KE_BeachDay`, `KE_TRAIT_A/B`, `KE_PinballHigh`, `KE_VersusPlayPapa`, κλπ. Τα saves μένουν ασφαλή όταν ενημερώνεις.

### 17.3 Χειρισμός

WASD κίνηση, Shift τρέξιμο, Space άλμα, ποντίκι κάμερα, **κράτημα αριστερού κλικ** για αλληλεπίδραση, F φακός (όταν τον κρατάς), Esc menu, gadget στο **1**. Versus κυνηγός: **1** MAP, **2** Kill Light, **3** Gaze/Absence. Pinball: αριστερό κλικ/A, δεξί κλικ/D flippers, SPACE plunger. Air Hockey: ποντίκι/WASD. **Facility Endless: δεξί κλικ ανεβάζει την κάμερα, αριστερό κλικ (ανεβασμένη) εναλλάσσει night vision, R αλλάζει μπαταρίες, Backspace δεν χρησιμοποιείται πια.** Recalled: Alt roll, δεξί κλικ block, TAB σακίδιο, J τι να κάνω, M χάρτης, P φίλοι, V mount.

---

## 18. Τι άλλαξε στο 3.8 (αναλυτικά)

Η δημόσια έκδοση χτίστηκε στο branch `recalled-music-only`. Οι αλλαγές, ομαδοποιημένες:

### 18.1 Για τους παίκτες (αυτά είναι στο public changelog)

1. **Daily objectives:** χαρτάκια στον τοίχο + λίστα πάνω δεξιά, 3/μέρα, 25/60/120 shards, reroll 1/μέρα (παράγραφος 8).
2. **Ένα νόμισμα, shards:** τέλος τα Party/Versus Tokens, όλα πληρώνουν shards, μετατροπή υπολοίπων (παράγραφος 9).
3. **Οθόνη Setup του Endless:** νέα διάταξη σε δύο στήλες + ένα κουτί DETAILS.
4. **Recalled:** ηχογραφημένη μουσική· το ημιτελές δεύτερο κόσμο (Egypt) βγήκε από το build.
5. **Waygetter Facility στο Endless** (προστέθηκε στο public changelog στο τέλος, μετά το άνοιγμα για όλους): χάρτης με κάμερα night vision, Mama, φώτα που σβήνουν, φόβος του σκοταδιού, βέλος, glows (παράγραφος 7).
6. **Coming next:** το Facility στο Versus.

### 18.2 Εσωτερικά και εργαλεία

- **Camcorder μπαταρίες:** δεν μπορούσαν να παρθούν ποτέ (κανείς δεν δρομολογούσε το ολοκληρωμένο hold στην κάμερα): προστέθηκε το `Patch_Interactable_OnInteractComplete_CamBattery`, και respawn σε 1 λεπτό.
- **Κάμερα θόρυβος:** night vision → θόρυβος κάθε 2 s· το εικονίδιο θορύβου χωρίς Mama.
- **Marks:** προεπιλογές ψηφίων, ύψος, αρχείο `facility_marks.txt`.
- **`ilscan`:** πιάνει πια το `Type ==`.
- **Pinball, Air Hockey, Recalled, Beach, Party** πληρώνουν shards και τροφοδοτούν daily μετρητές.
- **Έκδοση:** `VersionTag.VERSION = "3.8"`, `[BepInPlugin]` 3.8.0, `EARLY_ACCESS = false`. Ο installer 3.8.
- **Αφαιρέθηκε:** το F9 free roam του Facility· τα Egypt αρχεία· οι στόχοι Pinball/Air Hockey από το daily pool.

### 18.3 Η αναλυτική ιστορία του Facility Endless (για να μην ξαναγίνουν τα λάθη)

Η υλοποίηση πέρασε από πολλά βήματα επειδή δεν είχε δοκιμαστεί στο παιχνίδι:
1. Πρώτη εκδοχή: δικός μου μηχανισμός στόχων (**απορρίφθηκε**: "έλεγξε πώς γίνεται στο σπίτι").
2. Αντικαταστάθηκε από την πραγματική `EndlessRunManager` με θέσεις `FacilityMarks`.
3. Κρασάρισμα στη φόρτωση: το **μακρύ δίγραμμο κείμενο στο quest channel του παιχνιδιού** (`HubMode.SetHudText`) αμέσως μετά την κάμερα. Λύση: δικό μας overlay (`ShowPreviewHint`).
4. Αόρατα πλυντήριο/ηχείο: αρχικά αποδόθηκε στο σκοτάδι, τελικά τα **αντίγραφα των πραγματικών μοντέλων δεν εμφανίζονταν**. Λύση: ζωγραφισμένα με κώδικα και **μόνιμα** στις θέσεις τους (όπως τα πραγματικά props του σπιτιού).
5. Αόρατη κασέτα: ταίριαζε το διακοσμητικό "Caution tape". Λύση: fallback κουτί VHS.
6. Mama: τρεις αποτυχίες (παράγραφος 7.5) μέχρι να επανέλθει ο παλιός δοκιμασμένος κώδικας από το ιστορικό git.

### 18.4 Αναλυτική λίστα commits (branch `recalled-music-only`, από το 3.7)

Η πλήρης λίστα δεν αντιγράφεται εδώ: τρέξε `git log f01cec2..HEAD` (το `f01cec2` είναι το commit του 3.7). Τα σημαντικά ορόσημα: επιστροφή σε real-time μάχη + μουσική (`d9cc988`), νομίσματα/menu/Facility map (`821176b`), έκδοση 3.8 (`13031e0`), Facility Endless αληθινό (`3d2c5ef`), care stations/glows/βέλος, Mama με τα 54 nodes (`64b95b5`), daily objectives (`1ee8a20` και επόμενα), public έκδοση (`2790b21`), Facility ανοιχτό σε όλους (`8247f78`), installer Linux (`b662f3f`).

---

## 19. Ο installer και η υποστήριξη Linux

### 19.1 Ο installer (`tools/installer/`, WinForms net48)

`Install Tattletail After Hours.exe` (68,6 MB, ενσωματώνει το `payload.zip`: 49 αρχεία, τα release αρχεία + `x64/` για το 64-bit loader).
- **`GameFinder`:** βρίσκει το Tattletail: όλες τις βιβλιοθήκες Steam (μητρώο + `libraryfolders.vdf`), τον φάκελο του itch app, και συνηθισμένα μέρη (Desktop, Downloads, `C:\Games`...). Το `Inspect` διαβάζει τον τύπο CPU του exe, αν είναι ήδη modded και αν είναι ίδια έκδοση.
- **`ModInstaller`:** αντιγράφει το payload (με το 64-bit loader αν χρειάζεται), `Uninstall` αφαιρεί `BepInEx`, `ShopPreviews` και τα αρχεία του loader (τα saves μένουν). Το `MatchesPayload` συγκρίνει SHA1 του DLL.
- **`InstallerForm`:** ζωγραφισμένη από κώδικα φόρμα (λογότυπο, sparkles, κουμπί PLAY NOW). **Χρειάζεται .NET Framework 4.8** (Windows 10/11 το έχουν).
- **Δημιουργία:** `make_installer.py` (ή η διαδικασία payload που χρησιμοποιήσαμε στο 3.8: ένα PowerShell script που φτιάχνει το `payload.zip` από το φάκελο του παιχνιδιού + το φρέσκο DLL + τον x64 loader· δεν μπαίνει config ή logs). Μετά `dotnet build tools/installer/AfterHoursInstaller.csproj`.

### 19.2 Linux (Proton / Wine / Steam Deck), νέο στο 3.8

**Πρόβλημα:** στο Linux το BepInEx δεν φορτώνεται αν το Wine δεν ξέρει ότι το `winhttp.dll` πρέπει να είναι "native". Πριν έπρεπε ο παίκτης να βάλει χειροκίνητα launch option.

**Λύση:** ο installer τρέχει μέσα στο Wine, βλέπει τον δίσκο του Linux ως `Z:`, και **γράφει μόνιμα** το DLL override στο prefix του παιχνιδιού:

```
[Software\\Wine\\AppDefaults\\tattletailWindows.exe\\DllOverrides]
"winhttp"="native,builtin"
```

Αυτό είναι ό,τι κάνει και το launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`, αλλά αποθηκεύεται και ισχύει μόνο για το exe του παιχνιδιού.

**Κώδικας:**
- `WineSupport.cs`: `IsWine` (ψάχνει το `wine_get_version` στο ntdll, ή τους φακέλους `Z:\home` και `Z:\usr`), `ToWin` (/home/x → Z:\home\x), `SteamLibraries()` (βρίσκει βιβλιοθήκες Steam σε: `~/.local/share/Steam`, `~/.steam/steam`, `~/.steam/root`, `~/.steam/debian-installation`, Flatpak `~/.var/app/com.valvesoftware.Steam/...`, Snap `~/snap/steam/common/...`, διαβάζει το `libraryfolders.vdf` για SD κάρτες/δεύτερους δίσκους, και ψάχνει `Z:\run\media`, `Z:\media`, `Z:\mnt` ένα-δύο επίπεδα κάτω· σαρώνει όλους τους χρήστες στο `Z:\home`, π.χ. `deck`), `PrefixOf(game)` (`<library>\steamapps\compatdata\568090\pfx` αν υπάρχει), `Enable(game)`, `Disable(game)`.
- `WineReg.cs`: καθαρό κείμενο, χωρίς κλήσεις Wine. `Enable` προσθέτει/διορθώνει το section χωρίς να αγγίζει τίποτα άλλο (κρατά τα τέλη γραμμών)· `Disable` αφαιρεί τη γραμμή. Φτιάχνει backup `user.reg.afterhours.bak` μία φορά.
- `Enable` γράφει και στο `user.reg` του prefix του Tattletail ΚΑΙ στο registry του prefix που τρέχει ο installer (για Lutris/Bottles/απλό Wine όπου το παιχνίδι τρέχει στο ίδιο prefix).
- `GameFinder.FindAll` προσθέτει τις βιβλιοθήκες Linux όταν `IsWine`. `InstallerForm` δείχνει ένα σημείωμα στην οθόνη "YOU'RE ALL SET!" και, αν χρειάζεται, το launch option ως εφεδρικό.
- Αν το prefix δεν υπάρχει (το παιχνίδι δεν έχει τρέξει ποτέ), λέει να ξεκινήσεις το παιχνίδι μία φορά από το Steam και να ξανατρέξεις τον installer.

**Όρια:** αν ο χρήστης σβήσει το prefix ή αλλάξει Proton και δημιουργηθεί νέο, πρέπει να ξανατρέξει τον installer. Μη-Steam αντίγραφα παίρνουν μόνο το κομμάτι του prefix του installer. **Δεν έχει δοκιμαστεί σε πραγματικό Linux.** Δεν ξέρουμε αν υπάρχουν άλλα προβλήματα του ίδιου του mod σε Linux (διαδρομές saves, Steam δίκτυο).

**"Παλιότερα συστήματα":** καλύφθηκαν παλιές διατάξεις Steam/Proton (η μορφή του `user.reg` δεν έχει αλλάξει). Ο installer απαιτεί ακόμα .NET 4.8: Windows 7/8 χωρίς αυτό χρειάζονται ξεχωριστή αλλαγή.

---

## 20. Διαδικασία release και μόνιμοι κανόνες

### 20.1 Συνταγή release

1. Όρισε `VersionTag.VERSION`, `[BepInPlugin]` έκδοση, `EARLY_ACCESS = false` (για δημόσιο).
2. `dotnet build -c Release`, τρέξε `tools/ilscan` (πρέπει `CLEAN`), τρέξε τα headless tests.
3. Φτιάξε το payload (PowerShell ή `make_installer.py`): μόνο τα release αρχεία, **όχι** `com.kaleidoscope.endless.cfg`, logs, saves. Έλεγξε ότι δεν υπάρχει `\` σε διαδρομές zip.
4. Χτίσε τον installer. Σάρωσέ τον με Defender (`MpCmdRun -Scan -ScanType 3`) πριν ανέβει (το 3.8 δεν έχει σαρωθεί ακόμα).
5. Έλεγχος: το DLL στο payload έχει ίδιο hash με το build· η γωνία γράφει PUBLIC· κανένα `Array.Empty`· τα `Debug*` flags off (`DebugForceFirstObjective = null`, `DebugDisableMama = false`, `DebugSpawnAllPowerUps = false`, `DebugSpawnAllTapes = false`...)· `DevTools` προεπιλογή false.
6. Γράψε τα κείμενα: changelog για παίκτες, σελίδα itch.io, Discord (κάτω από 2000 χαρακτήρες), post Reddit. Λιγότερα emojis (μόνιμος κανόνας).
7. Commit και push. **Tag `vX.Y` μόνο όταν το πει ο owner.** (Το 3.8 δεν έχει tag ούτε ανέβηκε από εμένα.)

### 20.2 Μόνιμοι κανόνες του owner

- **Διόρθωσε τους μηχανισμούς, μην τους αφαιρείς:** το resource management είναι κεντρικό.
- **Προτίμησε αυτόνομους μηχανισμούς:** μην εξαρτάσαι από vanilla interactable ή quest state σε αυτό το επαναχρησιμοποιημένο level.
- **Κράτα τους πραγματικούς μηχανισμούς του Tattletail:** τα νέα modes χτίζονται πάνω στους κανόνες Mama/θορύβου/φωτός. **Πριν χτίσεις στόχο, διάβασε πώς το κάνει το σπίτι. Μην επινοείς.**
- **Στείλε ολόκληρο το zip, όχι μόνο το DLL**, σε όποιον δοκιμάζει.
- **Κάθε commit γίνεται push** (από το 3.8, μόνιμη εντολή: "always push, if we dont like something we can always roll back"). Ποτέ force-push, ποτέ απευθείας στο `main`.
- **Ο owner διαβάζει κάθε γραμμή ιστορίας/φωνής πριν μπει.**
- **Δεν αναφέρεται το πώς φτιάχτηκε το mod σε credits/docs/commits για το κοινό** (ρητή επιθυμία του owner, 2026-11-18). Το repo είναι private.
- Οι changelogs είναι για παίκτες (απλή γλώσσα, μόνο ό,τι κυκλοφόρησε). Το loot box των 1000 shards είναι endgame επίτηδες. Τα DLC δεν κοστίζουν ποτέ πραγματικά λεφτά.
- **`PatchAll()` φυλαγμένο, όχι Func/Action/LINQ/`Type ==`, ilscan μετά από κάθε build.**
- **Ο owner δεν θέλει στιγμιότυπα της οθόνης του**· ηχογραφεί στο OBS ή στέλνει `LogOutput.log`.

---

## 21. Τι δεν έχει δοκιμαστεί, τι είναι μόνο για dev, τι μένει ανοιχτό

### 21.1 Δεν έχει δοκιμαστεί μέσα στο παιχνίδι (ειλικρινά)

- **Η Mama του Facility με τα 54 restored nodes** (η τελευταία εκδοχή). Το προηγούμενο build είχε δείξει στο log ότι η Mama κινούνταν σωστά στο Facility (θέσεις y≈200, κοντά στον παίκτη, χωρίς spam), αλλά η εκδοχή με τα 54 nodes δεν έχει φανεί ακόμα σε log. Αναμένεται: "54 patrol nodes built", "Mama walks 54 Facility nodes", θέσεις y στο 200, 204, 196.
- **Ο φόβος του σκοταδιού με το patch `GetLightPercentage`**, και ότι ο Tattletail τρέμει πριν ουρλιάξει.
- **Όλοι οι μετρητές του daily** σε πραγματικό run, και η θέση/εμφάνιση των χαρτιών στον τοίχο (η θέση διαλέχτηκε από τη διάταξη).
- **Τα απορρίμματα:** τα "ζωγραφισμένα" πλυντήρια/ηχεία/τηλέφωνο, το βέλος (η φορά περιστροφής), η ακριβής θέση του ηλεκτρικού πίνακα στον τοίχο, οι νέες θέσεις του Facility.
- **Ο installer στο Linux.** Μόνο το κομμάτι του `user.reg` δοκιμάστηκε (σε δείγματα).
- **Το exe του 3.8** δεν έχει σαρωθεί με Defender ούτε ανέβει/χρησιμοποιηθεί από εμένα.
- Από το 3.7 παραμένει "χτισμένο αλλά όχι δοκιμασμένο στο παιχνίδι": τα **traits (Profile)**.

### 21.2 Μόνο για dev

- `DevTools`: εργαλείο marks (E, Z) στο Arcade/Endless/Facility και ο trailer director (F9 στο Arcade).
- `TrailerDirector` (`Trailer.cs`, `TrailerRecalled.cs`): αυτοπαιζόμενο, ηχογραφήσιμο trailer.
- `KidPreviewTool`, `DebugLocationMarker`.
- Τα `Debug*` flags (όλα off).

### 21.3 Νεκρός κώδικας / καθαρισμός που μένει

- Περίπου **100 κλάδοι `RcWorlds.Egypt`** στα αρχεία του Recalled (`RecalledUI`, `Recalled`, `RecalledTown`, `RecalledNoticeUI`...). Δεν εκτελούνται ποτέ (ο κόσμος είναι πάντα η πόλη). Ο πίνακας ειδοποιήσεων του τρένου γράφει ακόμα "Egypt: UNDER DEVELOPMENT".
- **Τα έγγραφα** `AFTER_HOURS_3.6_COMPLETE_REFERENCE.md`, `RECALLED_*.md`, `STANDALONE_PLAN.md` περιγράφουν ακόμα το Egypt ως χτισμένο.
- **`tools/recalled_test`:** 5 έλεγχοι που αφορούν μόνο Egypt αποτυγχάνουν.
- Έχουν μείνει hooks daily για Pinball/Air Hockey/boss/jobs/coins χωρίς ενεργό στόχο.
- `FacilityMarks.MamaSpots/CareSpots` δεν χρησιμοποιούνται πια (η Mama χρησιμοποιεί τα 54 nodes).
- `PhoneObjective._facilityLook`: αχρησιμοποίητο πεδίο.
- Το branch `recalled-music-only` **δεν έχει γίνει merge στο `main`**.

### 21.4 Ανοιχτά σχέδια

- **Waygetter Facility στο Versus** (ο κυνηγός με 3 ορόφους, Jam camera, στόχοι του παιδιού στο κτίριο). Έχει σχεδιαστεί αλλά δεν έχει υλοποιηθεί.
- Mummytails / Dr. Tattlenstein (ιδέες στο shelf), άλλοι σταθμοί (Harbor, Lodge, Fairground, Hollow, the Depot).
- Daily που προσαρμόζονται στον παίκτη (π.χ. στόχοι pinball ανάλογοι του καλύτερου score).
- Daily για Versus (όταν υπάρχει φίλος στο lobby).
- Tag και ανέβασμα του 3.8 (ο owner αποφασίζει).

### 21.5 Γνωστοί αριθμοί που είναι εικασίες

Τα hold times 3 s για feed και groom στο Facility (το σπίτι έχει πραγματικά props με δικούς τους χρόνους)· οι στόχοι score του Pinball (αφαιρέθηκαν από το daily)· τα 54 nodes είναι ό,τι χτίστηκε για το Night Shift και μπορεί να θέλουν ρύθμιση.

---

## 22. Παλιά έγγραφα και πού βρίσκεται τι

| Έγγραφο | Τι περιέχει |
|---|---|
| `docs/AFTER_HOURS_3.6_COMPLETE_REFERENCE.md` | Η πλήρης αγγλική αναφορά του 3.6/3.7 (2300 γραμμές): κάθε αριθμός, πίνακες δεδομένων του Recalled (Παράρτημα D), χάρτης αρχείων, κατάλογος μοντέλων και ήχων. **Όπου μιλά για Egypt ή tokens είναι ξεπερασμένο.** |
| `DESIGN_DOCUMENT.md` | Το μακρύ δοκίμιο σχεδιασμού και ιστορίας (v2.6 και μετά) |
| `docs/RECALLED_COMPLETE_BOOK.md`, `RECALLED_DESIGN.md`, `RECALLED_LORE.md`, `RECALLED_MISSION_LORE.md`, `RECALLED_VOICE_LINES.md` | Το Recalled: σχεδιασμός, lore, jobs, φωνές |
| `docs/ENDLESS_TRAITS_IDEAS.md` | Τα 34 traits |
| `docs/STANDALONE_PLAN.md` | Το σχέδιο για ανεξάρτητο παιχνίδι (πάγωσε στις 2026-10-05) |
| `docs/release_3.5/3.6/3.7/3.8` | Κείμενα release (changelog, main page, discord) |
| `docs/facility_marks.txt` | Τα marks του owner για το Facility (48 γραμμές) |
| `docs/FACILITY_ENDLESS_MARKS.md` | Ο οδηγός marking του Facility |
| **Αυτό το έγγραφο** | Η ενιαία ελληνική τεκμηρίωση του 3.8 |

---

## 23. ΠΑΡΑΡΤΗΜΑ Α: ο χάρτης όλων των αρχείων του `src/`

*Παράγεται αυτόματα από τον τρέχοντα κώδικα: όνομα αρχείου, γραμμές, κλάσεις μέσα (οι πρώτες 5), και η πρώτη περιγραφή `/// <summary>` του αρχείου.*

| Αρχείο | Γραμμές | Κλάσεις | Περιγραφή |
|---|---|---|---|
| `AirHockey.cs` | 659 | AirHockeyGame |  TATTLETAIL AIR HOCKEY: a real 3D table in the Arcade, 1v1, first to 7. Walk up to an end and hold to play: your view drops behind your mallet and the mouse (or WASD) moves it. Alone, a Tattletail AI  |
| `AirHockeyTable.cs` | 170 | AirHockeyTable |  Tattletail Air Hockey's table as flat 2D physics: the puck gliding on air, two mallets, the rails (with cut corners so the puck never sits in one) and a goal slot in each end. Table space, meters: x  |
| `Awakening.cs` | 401 | Identity,ArcadeAwakening |  Who you are, in the Arcade (Sa1monfr's idea): your consciousness is trapped in there. The very first time a player arrives they wake up as a blank wooden mannequin (ArcadeAwakening), and stay one (in |
| `Beach.cs` | 503 | BeachMode,BeachController |  Summer Vacation (in progress): an empty daytime beach — sand, the sea up front (can't be entered), a real sky with the sun — built the same way as the Arcade: the Night 6 level loads (real player |
| `BeachLayout.cs` | 940 | BeachLayout,WatchPlayer,StandOnFloor |  Everything on the Summer Vacation beach, placed from the marks the user and their friend laid down with the E tool ([BEACH-MARK], beach-local meters: +z is the sea, 2026-09-26): the beach bar (walls, |
| `BeachMusic.cs` | 215 | BeachMusic |  Summer Vacation's music, synthesized like the party tune: a short steel-drum jingle when a day starts, a calypso loop while the bar is open, and a low drone while Mama judges you. Plays on its own 2D |
| `BeachShift.cs` | 2608 | BeachShift,MamaFloor,Flicker,Patch_Interactable_OnInteractComplete_Beach |  Summer Vacation's job, Papa's-Freezeria style: run Mama's smoothie bar. Task 1 is "Go to work" (step behind the counter) and the day starts. Party Tattletails walk down from the dunes, along the grav |
| `BeachUpgrades.cs` | 164 | BeachUpgrades,Upgrade,BeachUpgradeMenu |  Summer Vacation's bar: the day you're on, the cash in the register (tips) and Mama's upgrades, all kept in Shop.txt. Upgrades are bought at Mama's board between days. |
| `BirthdayDecor.cs` | 700 | FurRainbow,BirthdayDecor |  Papa's 10th Birthday! set dressing, at two hand-marked spots: mark 1 — a Mama (the real model, cloned): white fur, a monocle and a top hat, standing facing the table; mark 2 — a party table with  |
| `BirthdayMusic.cs` | 219 | BirthdayMusic,PartyTune |  Papa's 10th Birthday! music: an original, upbeat party loop composed and synthesized here in code (bass, arpeggiated chords, lead melody, drums), so there's no audio file to ship and nothing copyrigh |
| `BirthdaySubmenu.cs` | 131 | BirthdaySubmenu |  The menu for Papa's 10th Birthday!: Customization (its own picks), the Party Shop (Party Tokens), and the button that starts the mode. Same clone-real-widgets recipe as the other submenus. |
| `BirthdayTask.cs` | 41 | BirthdayTask,Patch_HUD_SetNewQuest_BirthdayTask,Patch_HUD_ClearQuest_BirthdayTask |  Papa's 10th Birthday! has one permanent task on the HUD. Anything that tries to write the task line (the vanilla story, an interaction, a hint reset) gets this text instead, and clearing it just puts |
| `BlurFilter.cs` | 147 | BlurFilter,Fx |  Strength sliders (Options) for the camera's post effects, each 0–100% where 100% is exactly the game's original look (defaults: Defaults below): Blur (depth of field), Bloom (halos around lights),  |
| `CandleObjective.cs` | 268 | CandleObjective |  "Light the candles" objective. Candle is a real, dedicated vanilla component (confirmed via decompile: SetCandle(bool) plays a flame ParticleSystem + restores emissive glow) — used here purely as a |
| `CharacterKit.cs` | 537 | CharacterSpec,CharacterKit,FaceGrid,KidFace |  Everything a custom kid is: body frame and bottom, head shape, hair and its color, skin tone, eyes and their color, brows, nose, mouth, slider tweaks, and the extras (expression, glasses, freckles, b |
| `ChargingStation.cs` | 209 | ChargingStation |  A real, hold-to-interact charging station, built entirely fresh in code and placed at the basement spot the level's own story already points to ('TattletailPlanningProp' position). The vanilla object |
| `ChildModel.cs` | 615 | KidGender,ChildModel,Clip,Data,KidSkins |  public enum KidGender { Boy = 0, Girl = 1, Dez = 2, Kaz = 3, Custom = 4, Rat = 5, Plague = 6, Fish = 7 } // Custom: built in the Character Creator The survivor as other players see them: a sculpted,  |
| `CompatShims.cs` | 15 | IteratorStateMachineAttribute |   |
| `CreatorMenu.cs` | 204 | CreatorMenu |  The Character Creator (from the OUTFITS screen, with "Kid: Custom"). Four tabs — BODY (frame, bottoms, skin tone, head shape), HAIR (style, color), FACE (eyes, eye color, brows, nose, mouth) and TW |
| `CustomizationSubmenu.cs` | 324 | CustomizationSubmenu |  Nested "submenu in the submenu" off EndlessSubmenu — per-player cosmetic picks, extracted out of EndlessSubmenu itself so that panel could shrink back down to a short list of category buttons (Cust |
| `DailyObjectives.cs` | 198 | Daily,Def |  Daily objectives: three goals on the Arcade's wall (one easy, one medium, one hard) that change every day (at local midnight; the same three all day). Each pays its own reward in shards when it is do |
| `DailyPapers.cs` | 189 | DailyPapers,Patch_Interactable_OnInteractComplete_DailyReroll |  The daily objectives as three small papers pinned to the divider wall, just south of the mirror (so you do not have them behind you when you arrive). Each has its tier and reward written above it, al |
| `DebugLocationMarker.cs` | 48 | DebugLocationMarker |  Temporary debug tool: press E anytime during a run to log the player's exact position (plus whatever's directly in front, if anything) — lets the user walk to wherever they actually want an object  |
| `DiamondFlashlight.cs` | 189 | DiamondFlashlight |  "Diamond Flashlight" — a single-run Shop power up now. Applied once at level start (SceneBootstrap.StartEndless()), not a per-frame patch. Duration: "60% more than the golden flashlight" — confir |
| `DiamondRecolor.cs` | 61 | DiamondRecolor |  Diamond tier, one step up from Gold — the same parts as GoldRecolor.cs (the fur's base coat + shell material and the ears; everything else left original), in pale icy blue with the same lit fur rec |
| `EndlessEvents.cs` | 87 | EndlessEvent,EndlessEvents |  Random events. Chaos Round has a 15% chance each time an objective starts; every 20 seconds of a run there's a separate 10% roll for one of the timed events (currently Gift from Mama). See EndlessRun |
| `EndlessHud.cs` | 146 | EndlessHud |  Small hand-built overlay canvas (same from-scratch-Canvas recipe the old StatsUI used, not a cloned vanilla panel — nothing here needs vanilla's busy background) for a top-right run timer, visible  |
| `EndlessRunManager.cs` | 1418 | ObjectiveType,EndlessRunManager |  public float CurrentMamaDifficulty { get { return MamaDifficultyMult(); } } private bool _objectiveActive; private float _objectiveTimer; private bool _inRestPhase; public bool InRestPhase { get { re |
| `EndlessStats.cs` | 103 | EndlessStats |  All-time stats shown in the new persistent Stats submenu (replaces the old auto-popup StatsUI death screen). Saved next to Shop.txt in the Documents save folder (see SaveFiles). Survival times are st |
| `EndlessTitle.cs` | 133 | EndlessTitle,Patch_HUD_PlayTitle_EndlessTitle |  The level title card shown when an Endless run starts. Each run start first rolls 10% for "Lucky Night" (every shard payout that run is doubled); a Lucky Night takes priority over everything else bel |
| `EndlessTraits.cs` | 604 | Trait,EndlessTraits,Combo |  3.7, The Profile Update: your character's psychological profile in Endless. You choose two traits on the setup screen (PROFILE). Each has a bonus and a price, so nothing is free; none of them pays sh |
| `EndlessTraitsPatches.cs` | 55 | Patch_MamaManager_OnNoise_Traits,Patch_FlashlightController_LateUpdate_Traits,Patch_Interactable_OnInteractComplete_TraitDecoy |  Noise you make: some of it never reaches Mama (Calm Under Pressure near her, Reality Avoidance), some of it counts as a big noise (Violent Tendencies, Aggressive Behaviour, Hypervigilant Behaviour, t |
| `EndlessTraitsRuntime.cs` | 621 | EndlessTraitsRuntime |  What the chosen traits make happen while a run is going (EndlessTraits has the numbers): your speed, how long holds take, Tattletail's needs freezing, and the things you see and hear (heartbeat, warn |
| `FacilityArrow.cs` | 124 | FacilityArrow |  Endless in the Facility: an arrow on the screen pointing at the nearest objective (in a Chaos Round, the nearest of the objectives that are on). It also says how far, and whether it is upstairs or do |
| `FacilityEndless.cs` | 308 | FSpot,FacilityMarks,FacilityRunStarter,FacilityMama,FacilityMamaWatch |  public struct FSpot { public float X, Z, H, Yaw; public FSpot(float x, float z, float h, float yaw) { X = x; Z = z; H = h; Yaw = yaw; } public Vector3 World { get { return FactoryController.Origin +  |
| `Factory.cs` | 602 | FactoryMode,FactoryController,FMat,FTex,FGeo |  The Waygetter Facility: the abandoned Waygetter Electronics factory, built like the beach: the Night 6 level loads (real player, camera, controls), then the factory is built far from the house and th |
| `FactoryCamcorder.cs` | 374 | FactoryCamcorder |  Found footage: you carry a camcorder in your right hand (through its strap, like a real handycam), always recording. No HUD: the proof is the blinking red REC light and the flip-out screen, which is  |
| `FactoryCamcorderBattery.cs` | 334 | FactoryCamcorder,Patch_Interactable_OnInteractComplete_CamBattery,Patch_MamaManager_OnNoise_Facility,Patch_FlashlightController_GetLightPercentage_NightVision,Patch_MamaManager_IsAboveGround_Factory |  The camcorder's battery. It drains (about five minutes a pack); under 15% it beeps every few seconds, and every beep is a noise. You carry spares (3 to start; more lie around the building). R swaps o |
| `FactoryDecay.cs` | 391 | FactoryLayout |  Decades of nobody, part two: decals (water stains, mold, cracks, soot, rust, cobwebs, footprints, leaves, torn posters, caution tape; tools/factory_decals.py) and clutter (trash, a squatter's camp, t |
| `FactoryFloors.cs` | 410 | FactoryLayout |  The rest of the Waygetter factory: the stairs, the upstairs offices and catwalk (floor y 4), the basement (floor y -4: generator, boiler room, storage, the reject pit, R&amp;D) and the testing lab's  |
| `FactoryLayout.cs` | 1322 | FactoryLayout |  The Waygetter factory's ground floor (step 1: the rooms Night 1 uses). Place-local meters: x runs west to east 0..60, z south to north 0..40, the floor is y 0. west block (x 0-14): lobby (z 0-20), ha |
| `FactoryStations.cs` | 234 | FactoryLayout,StationKind,Station,GlowPulse |  Care stations all over the factory, so Tattletail can be looked after wherever you are: TATTLE-TREATS boxes (feed), grooming kits (brush) and charging docks (charge). Each one calls Tattletail's own  |
| `FlashlightBeam.cs` | 58 | FlashlightBeam,BeamColor |  Shop cosmetic: flashlight beam colors. Same two levers DiamondFlashlight already proved out — the private currentColor field (drives the emissive glow parts every frame) plus spotlight.color (set o |
| `FurShine.cs` | 128 | FurShine |  Gold and Diamond Tattletail keep their REAL fur: the game's fur shader (Custom/FurShaderV1, drawn in layers by FurShaderControllerV1) stays, only its strand color (_FurColor) changes — exactly how  |
| `FuseBoxObjective.cs` | 360 | FuseBoxObjective |  "Restart the Fuse Box" objective — a single fixed, hand-picked wall spot, not a real named prop. The box itself (warning triangle, "FUSE BOX" plate, status light) is on the wall for every Endless r |
| `GadgetController.cs` | 718 | GadgetController |  Runs the gadget picked in the menu (Gadgets.RunGadget) during an Endless run: a one-slot bar in the style of Papa's 10th Birthday!'s hotbar, the 1 key to use it, and a visible 60s cooldown (the slot  |
| `GadgetSubmenu.cs` | 161 | GadgetSubmenu |  "Choose your gadget" — picked with the mouse from the Endless menu before a run. Hovering a gadget explains what it does; clicking equips it. Same clone-real-widgets recipe as the other submenus. |
| `Gadgets.cs` | 384 | GadgetType,Gadgets,GumballResult,GadgetAudio |  Endless-only gadgets: one is picked in the menu before a run (see GadgetSubmenu) and used in-run with the 1 key (see GadgetController). Separate from shop Power Ups — both work in the same run. |
| `GamePatches.cs` | 773 | Patch_MamaManager_RequestJumpscareDeath,Patch_MamaManager_OnJumpscareFinished,Patch_QuestOrder_ActivateNextQuest,Patch_QuestOrder_StartQuesting,Patch_QuestOrder_CompleteCurrentQuest |   |
| `GoldRecolor.cs` | 102 | GoldRecolor |  "Gold" isn't a real vanilla recolor — decompiling RecolorType (the game's own enum) confirmed it only has STORED_COLOR/YELLOW/TEAL/ ORIGINAL/EDUCATIONAL/BLACK/WHITE, and RecolorManager only ever sw |
| `GoldenFlashlight.cs` | 41 | GoldenFlashlight |  Shop passive: the game's own golden flashlight, Endless only. Rather than flipping the save's GD.hasSpecialFlashlight (which could leak into the story save), this applies exactly what the vanilla gol |
| `GumballReveal.cs` | 115 | GumballReveal |  The card that pops up after the Gumball Machine: a big gumball in the gadget's color, its name and what it does, with EQUIP / OK. |
| `Hub.cs` | 394 | HubMode,Patch_MainMenu_Start_GoToHub,Patch_HUD_SetNewQuest_Hub,HubController |  v3.0 arcade HUB (test build). Reached from a temporary "HUB (test)" main menu button: loads the same level as Endless (so the real player, camera and controls come for free), then builds the arcade r |
| `HubLayout.cs` | 777 | HubMachine,HubLayout,WorldText,Mat,HubLaunch |  Everything inside the arcade HUB, placed from the user's own marks (HubMarkTool, room-relative coordinates): black partition walls, the big full-height divider, hanging doorway signs, the arcade mach |
| `HubMarkTool.cs` | 513 | HubMarkTool |  Placement tool for laying out the arcade and the Summer Vacation beach (dev builds). Positions are logged relative to the place you're in ([HUB-MARK] or [BEACH-MARK]). (Originally hub-only: the place |
| `HubMirror.cs` | 170 | HubMirror |  A real mirror on the Arcade's wall (next to the OUTFITS machine): the room, the other player, and you — your own kid in your outfit, which only the mirror sees (you're first person). A second camer |
| `KaleidoscopeEndlessPlugin.cs` | 116 | KaleidoscopeEndlessPlugin |  The E-key location markers (arcade: HubMarkTool, Endless: DebugLocationMarker). Off unless turned on in BepInEx\config\com.kaleidoscope.endless.cfg — the release zip doesn't ship that file, so play |
| `KaleidoscopePortalScavenger.cs` | 139 | KaleidoscopePortalScavenger |  Two ways to get the real 'KaleidoscopePortal' prop (Transform + Animator, confirmed via a real scene load — see the removed [PORTAL-SCAVENGE] logs) instead of TrueEndingEasterEgg's placeholder ring |
| `KidVoice.cs` | 528 | KidVoice,Kind,Patch_FPS_PlayJumpSound_Voice,Patch_FPS_PlayLandingSound_Voice |  The kids' voices, synthesized (no recordings): a buzzing "throat" at the voice's pitch, shaped by three mouth resonances (formants) into vowels, plus breath noise. Each sound is a handful of keyframe |
| `LoadingCover.cs` | 148 | LoadingCover |  Full-screen "loading" cover shown while the arcade loads — hides the empty menu at startup and the level load on the way back to the hub. Shown by HubMode.Launch / the menu patch, hidden once the p |
| `MainMenuButton.cs` | 758 | Patch_MainMenu_SetOptionsActive,Patch_MainMenu_SetLevelSelectActive,Patch_MainMenu_SetWipeDataActive,Patch_MainMenu_SetMainMenuActive,MainMenuButtonInjector |  public static GameObject CloneButton(GameObject template, Transform parent, string label, UnityEngine.Events.UnityAction onClick) { if (template == null) return null; GameObject clone = Object.Instan |
| `MamaController.cs` | 391 | MamaController |  Thin wrapper around the game's MamaManager that adds difficulty scaling and hooks for our objective system. |
| `MamaTeleportGrace.cs` | 100 | MamaTeleportGrace,Patch_MamaManager_OnNoise_Parrot,Patch_MamaManager_OnNoise_Versus,Patch_Mama_MoveToNode_Grace,Patch_Mama_IsFlashlightOnMama_Grace |  When Mama can't touch the player (Endless only): - for GRACE_SECONDS after she teleports, and - the whole time a Toy Parrot is down: she ignores the player's noises too, and only goes after the parro |
| `ModelExport.cs` | 176 | ModelExport |  Dev builds only: writes a model in the scene out as a Wavefront .obj (+ .mtl and the textures as .png), for the team's 3D tools. Skinned meshes are baked in the pose they're standing in. |
| `MpLobby.cs` | 346 | MpLobby,PeerCompat |  Steam lobby lifecycle — friends-only, 2-player max, invite via the Steam overlay. Persistent DontDestroyOnLoad singleton created once from KaleidoscopeEndlessPlugin.Awake(), same shape as MainMenuB |
| `MpLobbySettingsSync.cs` | 29 | MpLobbySettingsSync |  Host-authoritative run-settings broadcast — difficulty and the 7 Run Modifiers toggles are shared/consistent for both players (they drive the one shared Mama's difficulty scaling), unlike Tattletai |
| `MpNetworking.cs` | 120 | MpNetworking,PacketId |  Thin wrapper around SteamNetworking's P2P packet API + a packet-ID dispatch table. Reliability is Steam's own (k_EP2PSendReliable), not hand-rolled — unlike the earlier UDP-only rewrite of this mod |
| `MpPacket.cs` | 79 | MpPacket |  Minimal binary packet reader/writer for Steam P2P messages — the common "write primitives in order, read them back in the same order" pattern, nothing proprietary. Write-mode instances start with t |
| `MpPacketHandlers.cs` | 39 | MpPacketHandlers |  Static handlers dispatched by MpNetworking.ListenData(), one per PacketId. Step 1 (lobby-only) scope: connectivity handshake, host's run-settings broadcast, and the "follow the host into the level" s |
| `MultiplayerSubmenu.cs` | 220 | MultiplayerSubmenu |  Nested "submenu in the submenu" off EndlessSubmenu — Steam-invite lobby: host a lobby, invite a friend via the Steam overlay, see connection status, start the run once a peer's connected. No manual |
| `Night2AudioScavenger.cs` | 134 | Night2AudioScavenger |  One-shot, main-menu-only diagnostic: additively loads Night 2's own level-specific scene purely to search it for the real washing- machine sound. Confirmed by direct player testimony to exist there ( |
| `NoiseObjective.cs` | 172 | NoiseObjective |  "Silence the noise" objective: something's making noise at the washing machine's real basement position — the only spot in this scene ever confirmed reliable (real name 'LevelSpecific_Washer', same |
| `OptionsMenu.cs` | 615 | OverlayPanel,OptionsMenu,EndlessSetupMenu,PapaSetupMenu,LevelSelectMenu |  A full-screen, mouse-driven settings panel that works inside any level (its own hit-testing, so no UI EventSystem is needed). While open the player is frozen and the cursor is free. Subclasses just l |
| `OutfitMenu.cs` | 174 | OutfitMenu |  The OUTFITS machine's screen: pick the boy or the girl (both free), or a DLC character from the "DLC CHARACTERS" dropdown (Dez), and an outfit (the first is free, the rest OUTFIT_PRICE shards each).  |
| `PartyCastle.cs` | 195 | PartyCastle |  The party's inflatable bouncy castle — built big, at one hand-marked spot. Just the structure for now (walkable floor, open front, four corner towers, a back wall, flags): the "carry a tattletail o |
| `PartyFoods.cs` | 735 | PartyFoods |  Papa's 10th Birthday! party foods and the ingredients they're made of, all built in code from simple shapes (matte, lit — like the other party props). Every model stands on y = 0 with its front tow |
| `PartyGame.cs` | 811 | PartyItems,PartyInventory,PartyGameRunner,Patch_Interactable_OnInteractComplete_Party,PartyIcons |  What can be crafted in Papa's 10th Birthday!. Every finished food is made from exactly two ingredients, in either order. Two items that aren't a recipe just fail and reset. |
| `PartyMachines.cs` | 152 | PartyMachineController |  The 4 permanent food machines in Papa's 10th Birthday!: hand-marked spots (Popcorn on the floor; Hot Dog, Nachos and Waffle & Crepe on a counter/ table). Each already visually has its "main" ingredie |
| `PartySoundEffects.cs` | 121 | PartySoundEffects |  Papa's 10th Birthday!'s short reaction sounds: a children's cheer on winning a tattletail minigame, a boo on losing one. Files live in Sounds/ next to the mod's DLL (children_yay.wav / children_boo.w |
| `PartyTattletailGames.cs` | 580 | PartyGameType,PartyTattletailController |  The 7 hand-marked party tattletails (each a random-colored clone from PartyTattletailModels), each hosting one of 3 minigames, randomly assigned: Face Paint (hold interact repeatedly to dab on 4 colo |
| `PartyTattletails.cs` | 130 | PartyTattletailModels |  Builds a decorative, inert clone of the real Tattletail model in a random color — every color the mod actually has: the vanilla Yellow/Teal/Black/ White, plus the mod's own Gold and Diamond, alongs |
| `PartyWorld.cs` | 372 | PartyMaterialSpawner,PartyLabelBillboard,PartyConfetti |  The world side of Papa's 10th Birthday!'s party materials: 18 hand-marked spots, each of which puts a random ingredient on the floor every 30 seconds — but only if it's empty (an ingredient nobody  |
| `PhoneObjective.cs` | 196 | PhoneObjective |  Spawns the walk-up-and-hold "answer the phone" interactable — exactly the same shape as NoiseObjective's washer marker: resolve a real reference position once (here, the real "Landline" SpookyPhone |
| `Pinball.cs` | 904 | PinballGame |  TATTLETAIL PINBALL: a real 3D pinball machine in the Arcade, played in first person. Walk up, hold to play: your view drops onto the playfield and the flippers are yours. The table's physics live in  |
| `PinballTable.cs` | 259 | PinballTable,Kind,Seg,Bumper,Flipper |  Tattletail Pinball's table as flat 2D physics (the 3D machine just draws it): walls, slingshots, the star targets, three pop bumpers, the House's door, the ramp lane, the shooter lane with its plunge |
| `PortalFx.cs` | 258 | PortalFx |  The Kaleidoscope portal (the Endless easter egg): a swirling vortex (two spiral layers turning against each other), a glowing rainbow rim, sparks spiraling in, a breathing glow. Upright in the local  |
| `PowerUpObjective.cs` | 459 | PowerUpAbility,PowerUpObjective |  "Power Ups" run modifier — each round (each new objective) there's a 20% chance one coin spawns at a random hand-marked spot with a random ability, floating just above the ground and spinning in pl |
| `PreviewBaker.cs` | 361 | PreviewLibrary,PreviewBaker |  public static class PreviewLibrary { private static readonly Dictionary _cache = new Dictionary(); private static string Dir { get { return Path.Combine(BepInEx.Paths.GameRootPath, "ShopPreviews"); } |
| `PreviewStage.cs` | 118 | PreviewStage |  A live 3D preview for menus: a model far above everything on its own layer, lit by lights that only touch that layer, filmed by its own camera into a texture that a RawImage shows. Used by the OUTFIT |
| `Recalled.cs` | 1211 | RecalledMode,Rc,RcUse,RecalledController,RcLamps |  Recalled (docs/RECALLED_DESIGN.md): the co-op RPG in the town the recalled toys took over. This is the solo game for World 1. Like the beach and the factory, the Night 6 level loads (real player, con |
| `RecalledAtmos.cs` | 458 | RcAtmos |  What makes the night look like a night: a sky with stars and the moon (where the moonlight comes from), motes and fireflies drifting through the air round you, embers over the junkyard, and a color g |
| `RecalledChar.cs` | 437 | RcStack,RcCharacter |  public class RcStack { public int Id, Count; public RcStack(int id, int count) { Id = id; Count = count; } } A Recalled character: experience, what you carry, wear and keep, and where you are in the  |
| `RecalledData.cs` | 484 | RcKind,RcAbility,RcItem,RcCost,RcRecipe |  public static string[] ZoneNames { get { return ZoneNamesTown; } } public static string[] BossNames { get { return BossNamesTown; } } public static string[] ZoneNamesOf(int world) { return ZoneNamesT |
| `RecalledEnemies.cs` | 1494 | RcEnemy,St,RcEnemies,RcLooks |  One toy. Not a MonoBehaviour: RcEnemies ticks them all (cheaper, and their order is fixed). Positions are in town meters; they walk RcNav's spots (the ground, floors and stairs), so they need no coll |
| `RecalledFx.cs` | 174 | RcBubble,RcRoll,RcFlicker |  The guard: a ball of blue light round a kid while they hold a block. It pops open when it's raised, gleams brighter for the moment when a hit would be a perfect block, flashes when something lands on |
| `RecalledGuide.cs` | 83 | RcGuide |  Where your current job is: the place the HUD's arrow line, the objective screen (J) and the map (M) point you to. Pure (no Unity types), so the headless tests can check every quest has somewhere to s |
| `RecalledLines.cs` | 83 | RcSay,RcLines |  public struct RcSay { public string Who, Text; } public static class RcLines { private static readonly Dictionary> _lines = new Dictionary>(); private static readonly List _none = new List(); public  |
| `RecalledMount.cs` | 168 | RcMountView |  What a mount looks like: a toy you ride (a wagon, a ride-on RC car, a rocking horse), built from plain shapes. The same view draws yours and your friends'. Wheels turn as it goes and the horse rocks. |
| `RecalledMusic.cs` | 443 | RcMusic |  Recalled's music and sounds, all written in code (no audio files, nothing copyrighted), in toy instruments: a music box, ticking like a wind-up mechanism, a low drone. Each zone has one loop in three |
| `RecalledNet.cs` | 1311 | RcPlay,RcLobby,RcPeer,RcNet,RcRemote |  Recalled's own Steam lobby: up to four players in one town. Separate from the two-player lobby Versus and Air Hockey use (MpLobby hands this class every Steam lobby event that belongs to it; a player |
| `RecalledNoticeUI.cs` | 253 | RcScenes,RcNoticeMenu,RcBoardMenu |  public static class RcScenes { public static readonly string[] Call = { "TAPE-WG-1", "TAPE-WG-2", "TAPE-WG-3", "TAPE-WG-4", "TAPE-WG-5", "TAPE-WG-6", "CALL-NN-1", "CALL-NN-2", "CALL-NN-3", "CALL-NN-4 |
| `RecalledNotices.cs` | 59 | RcNotice,RcNotices |  public class RcNotice { public int Zone; public string Title, Text, Note; // Note: a line after the text ("" for none) } public static class RcNotices { public const int COUNT = 15; public static rea |
| `RecalledPlayer.cs` | 837 | RcPlayer |  You, in Recalled: your kid in third person (over the shoulder, always facing where you look, so the crosshair is your aim), your health, your two weapons (melee, ranged or a gadget), charging and blo |
| `RecalledProps.cs` | 693 | RcTown |  The town's dressing: the small things that make a place look lived in and then abandoned: mailboxes, toys left on lawns, Waygetter's parcels on porches with a Tattletail sitting on them, recall poste |
| `RecalledRules.cs` | 86 | RecalledZone,RecalledRules |  public enum RecalledZone { Suburbs = 0, Mall = 1, School = 2, Junkyard = 3, Warehouse = 4 } Recalled's rules as plain numbers (no scene, no saves, nothing calls it yet): levels and experience, how en |
| `RecalledSave.cs` | 255 | RecalledSave |  Recalled's save: one file, Recalled.sav, next to the mod's other saves, but not a text file. It's encrypted (AES), signed (HMAC-SHA256) with a key made from a secret inside the mod and your Steam acc |
| `RecalledSecret.cs` | 11 | RecalledSecret |   |
| `RecalledTalk.cs` | 109 | RcTalk |  The text box at the bottom of the screen: who is talking, and what they say, typed out a few letters at a time. Click it or press SPACE: the first press shows the whole line, the next one goes to the |
| `RecalledTown.cs` | 2318 | RcMat,RcSink,RcSpawn,RcSpot,RcTown |  public abstract void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1); public abstract void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide) |
| `RecalledTracks.cs` | 84 | RcTracks |  Recalled's recorded music: seven PCM WAVs in Sounds/ next to the DLL (this Unity can't decode MP3 at runtime, so the mp3s are converted with tools/convert_to_wav.ps1). The clips are 16-bit PCM turned |
| `RecalledUI.cs` | 1507 | RcHud,RcMenu,RcBagMenu,RcStashMenu,RcCraftMenu |  Recalled's HUD: your health and experience (bottom left), your two weapons (bottom right), the quest you're on and which way it is (top right), what you can use here (middle), what you just picked up |
| `RecalledVoice.cs` | 322 | RcVoice |  Everyone who talks in the town, as text in the box at the bottom of the screen: the kids when you get back to the treehouse, Waygetter's announcements, the toys, the bosses. It only watches what the  |
| `RecalledVoiceData.cs` | 366 | RcVoiceData |  public static class RcVoiceData { private static readonly Dictionary _lines = new Dictionary(); private static readonly Dictionary> _groups = new Dictionary>(); private static readonly List _none = n |
| `RecalledWorlds.cs` | 32 | RcWorlds |  Recalled has two worlds, one at a time: 0 is the town (part 1), 1 is Egypt (part 2). A session is in one of them; everything that differs (the plan, the toys, the names, the jobs, the gear) looks her |
| `RecipeCard.cs` | 72 | RecipeCard |  Papa's 10th Birthday!: a recipe card on the kitchen wall (the user's E-mark), listing every food and its two ingredients (PartyItems). |
| `RoomHints.cs` | 77 | RoomHints |  Rough "where is it" hints for hidden objectives ("Look in: Kitchen, Garage"). Rooms come from the house's own HouseLight components, each tagged with the room it lights: a spot belongs to the nearest |
| `RpgPanel.cs` | 115 | OverlayPanel |  The RPG look for Recalled's menus (a panel can opt in with Rpg): a wooden frame with a gold edge and a title banner, buttons drawn as plates that light up under the mouse, and framed boxes for text.  |
| `RunCustomizationSubmenu.cs` | 194 | RunCustomizationSubmenu |  Nested "submenu in the submenu" off MultiplayerSubmenu — the shared-run-settings counterpart to EndlessSubmenu's per-player cosmetic picks (Tattletail color/voice/Mama-voice stay local to each play |
| `RunModifiers.cs` | 112 | RunModifiers |  One-shot application of the three need-drain-rate Run Modifiers ("Batteries Not Included", "Always Hungry", "Brushing is Key"). TattletailNeeds (confirmed via decompile) computes hungerDecreasePerSec |
| `RunModifiersSubmenu.cs` | 232 | RunModifiersSubmenu |  Nested "submenu in the submenu" — opened from EndlessSubmenu's "Run Modifiers" button. Follows EndlessSubmenu.cs's exact established recipe: clone real vanilla widgets, plain Text labels, manual to |
| `SaveFiles.cs` | 81 | SaveFiles |  Where the mod's save files live: Documents\Tattletail After Hours\, so progress survives mod updates and even reinstalling the game. Saves from the older locations (Documents\Kaleidoscope Endless\, f |
| `SceneBootstrap.cs` | 962 | Patch_GameManager_OnSceneLoadComplete_Inject,Patch_MainMenu_Start_ResetInject,Patch_BriansEngine_IsItUnlocked,Patch_InteractableActions_FlashlightCarry,SceneMonitor |  player to touch the first door. public static bool WaitingForDoorTrigger { get; set; } Endless run (PendingEndlessRun consumed) through to returning to the main menu — wider than EndlessRunManager. |
| `Scrapbook.cs` | 243 | StickerRarity,StickerEntry,Scrapbook |  "Mama's Scrapbook" — a purely cosmetic sticker collection, separate from both the shard Shop and Party Shop. Six stickers match a real Tattletail color and unlock automatically the moment you buy t |
| `ScrapbookSubmenu.cs` | 376 | ScrapbookSubmenu |  "Mama's Scrapbook" — a purely cosmetic sticker collection reached from the Endless submenu. A paged grid (no real page-turn animation — see the conversation that led here: not worth it for this e |
| `Shards.cs` | 151 | Shards,ShardMenuIcon |  public static class Shards { public static int Balance { get { return Shop.Get(Shop.SHARDS_KEY); } } public static void Add(int amount) { Shop.AddShards(amount); } } public static class ShardMenuIcon |
| `Shop.cs` | 403 | ShopCategory,ShopItem,Shop,Passives,RunPowerUps |  Everything the player owns, in one plain-text file (Shop.txt, in Documents\Kaleidoscope Endless\ — see SaveFiles). One "Key = value" line per item: Shards = 340 Power Up Bigger Pockets = 1 (bought; |
| `ShopPanel.cs` | 240 | ShopPanel |  The hub's shop screens (ENDLESS SHOP machine = shards, PAPA SHOP machine = Party Tokens, VERSUS SHOP machine = Versus Tokens). Category tabs on the left, that category's items in the middle (click to |
| `ShopSubmenu.cs` | 410 | ShopSubmenu,ShopMenu,ShopCategoryPanel |  Shop: a root panel with one button per category (Power Ups, Tattletail Colors, Flashlight Beams, Voice &amp; Mama); each category is its own panel of buyable rows plus a preview pane (the 3D render + |
| `SpeakerObjective.cs` | 231 | SpeakerObjective |  "Silence the Speaker" objective — a single fixed location, same shape as WashingMachineObjective.cs (not a pick-1-of-N spot like the tapes). Originally scoped as "Silence the Radio," but the real p |
| `StatsSubmenu.cs` | 163 | StatsSubmenu |  Persistent, menu-accessed stats panel — replaces the old auto-popup StatsUI death screen entirely. Just 4 short all-time facts (see EndlessStats.cs for persistence), so unlike StatsUI's original pa |
| `StickerImages.cs` | 90 | StickerImages |  Loads the mod's sticker art (HUD event badges, objective icons, and Mama's Scrapbook collectibles) from Stickers/ next to the DLL — plain PNGs, same "read a real asset file off disk" approach as Pa |
| `StorySave.cs` | 62 | StorySave,Patch_GD_SaveLevel_StoryOnly,Patch_MainMenu_Start_StorySave |  Keeps the STORY's save point safe from the mod's own modes. The hub, Endless and Papa's 10th Birthday! all load Night 6, and the game's GameManager.PlayLevel() saves whatever level it loads as "where |
| `TapeObjective.cs` | 440 | TapeObjective |  "Rewind the Tapes" objective — NOT wired into the real rotation yet. This is the position-verification phase: SpawnAllForTesting() places all 6 hand-marked spots at once (the real objective will on |
| `TattletailFriendObjective.cs` | 165 | TattletailFriendObjective |  "Find Tattletail's friend" objective. User marked 6 exact spots directly in-game (DebugLocationMarker, E key), same workflow already used for VaseObjective/CandleObjective — full control over place |
| `ToyParrot.cs` | 163 | ToyParrot,RainbowCycler |  The Toy Parrot gadget's placed toy: squawks a pitched-up imitation of Tattletail crying, loudly and nonstop, for Gadgets.PARROT_DURATION seconds. Mama is lured to the patrol spot nearest it (again ev |
| `Trailer.cs` | 1066 | TrailerDirector,CartoonText,TrailerMusic |  A self-playing trailer, for recording (dev builds only: F9 in the Arcade). It takes over the player's camera (so the game's own post effects stay), hides every HUD, and plays a scripted sequence with |
| `TrailerRecalled.cs` | 481 | TrailerDirector |  The 3.5 trailer (THE RECALLED UPDATE): shot in the real town. F9 in the Arcade (dev builds) goes through the door and the trailer rolls as soon as the town is up. The director takes the camera and th |
| `TraitPickerMenu.cs` | 155 | TraitPickerMenu |  The PROFILE screen (3.7), opened from the Endless setup screen. It is drawn as Waygetter's file on you: a headline, the two slots as cards, the 34 traits in two groups (steady in green, conditions in |
| `TrueEndingEasterEgg.cs` | 529 | TrueEndingEasterEgg |  Version 2.0 "True Ending" easter egg — steps 1-6 of a hand-marked sequence (positions from DebugLocationMarker, same convention as every other objective in this project). Steps 7+ (the kaleidoscope |
| `VaseObjective.cs` | 347 | VaseObjective |  "Break 3 vases" objective. User marked 6 exact spots directly in-game (DebugLocationMarker, E key) rather than using the 6 real vanilla vase positions — full control over placement instead of being |
| `VersionTag.cs` | 90 | Support,VersionTag |  "EARLY ACCESS V3.1" / "PUBLIC VERSION 3.1" in the bottom-right corner, only while standing in the Arcade (it lives on the hub controller, so it goes away with the hub). Flip EARLY_ACCESS to false whe |
| `VersusLobby.cs` | 141 | VersusSeat,VersusLobby |  Who sits where on the Versus screen, shared between both games. The lobby host owns the seats: the guest asks (VersusClaim), the host decides and sends everyone the result (VersusState). Solo (no lob |
| `VersusMama.cs` | 989 | VersusMamaController |  Versus, the hunter's side: Mama, or Papa (bought). The hunter can't walk: seen in third person at one of Mama's real spawn spots (the level's patrol nodes). 1 MAP: top-down view of the house or basem |
| `VersusMenu.cs` | 122 | VersusMenu |  The VERSUS machine's screen: a MAMA seat and a SURVIVOR seat, shared with the friend in your Steam lobby (VersusLobby). JOIN takes a seat (LEAVE gives it back); the empty seat then offers INVITE PLAY |
| `VersusMode.cs` | 249 | VersusRole,VersusMode,VersusHunters,Skin,VersusBots |  Versus (step 1: solo playtest). The VERSUS machine's screen picks a side; PLAYTEST fills the other side with a bot that does nothing. - Survivor: a normal Endless run, but Mama's AI is replaced by an |
| `VersusNet.cs` | 519 | VersusNet,Over,KaleidoscopePluginRunner,VersusAvatars |  Versus online sync (on top of the Steam lobby): - Poses, ~10 a second, unreliable: where each player is and which way they face, in the Arcade or in a match. VersusAvatars draws the other player from |
| `WashingMachineObjective.cs` | 304 | WashingMachineObjective |  Real, self-contained "find and turn off the washing machine" objective — same from-scratch Interactable recipe as ChargingStation.cs and NoiseObjective.cs (proven reliable this session): a fresh Ga |
| `WorldRays.cs` | 59 | WorldRays |  Raycasts that see through the local player's own body. Finding "the floor" with a plain raycast hits the player's capsule when it's in the way: the other player's kid ended up standing on your head w |

---

## 24. ΠΑΡΑΡΤΗΜΑ Β: τα marks του Facility και οι patrol nodes της Mama

### Β.1 Οι θέσεις των στόχων (marks του owner, `docs/facility_marks.txt`)

Συντεταγμένες κτιρίου (x, z) και ύψος δαπέδου (0 ισόγειο, 4 πάνω όροφος, -4 υπόγειο), γωνία yaw. Ζουν στο `FacilityMarks` (`FacilityEndless.cs`), και προστίθενται εύκολα με το εργαλείο marks (`DevTools`).

| Αντικείμενο | Πλήθος | Σημειώσεις |
|---|---|---|
| Vase | 6 | Παίρνεις 3 τυχαία ανά στόχο |
| Candle | 5 | Παίρνεις 3 |
| Friend | 3 | Ένα |
| Tape | 5 | Ένα |
| Washer | 3 | Και τα 3 στέκονται όλο το run, ο στόχος ενεργοποιεί ένα |
| Speaker | 3 | Το ίδιο |
| FuseBox | 1 | Στον δυτικό τοίχο, x ≈ 0,13, κοιτά ανατολικά |
| Coin | 9 | |
| Phone | 1 | |
| Care stations | 9 | 4 ψυγεία (Feed), 2 βούρτσες + 1 γραφείο (Groom), 2 docks (Charge) |

### Β.2 Οι 54 patrol nodes της Mama

Στο `FacilityMama.Prepare()`: ισόγειο (lobby, hall, break room, γωνίες του assembly, testing lab, δρόμος προς R&D, warehouse, μπροστά από το κτίριο), πάνω όροφος (hall, foreman, records, manager, PA room, conference, catwalk), υπόγειο (σκάλες, διάδρομος, generator, boiler, storage, ακμή του pit, R&D). Προέρχονται από τον κώδικα του παλιού Night Shift (`FactoryTapeProps.MamaPatrol`, στο ιστορικό git πριν το commit `1383bc8`).

---

## 25. ΠΑΡΑΡΤΗΜΑ Γ: όλα τα daily objectives

| Τιέρ | Ανταμοιβή | Στόχος | Μετρητής | Στόχος τιμή |
|---|---|---|---|---|
| Εύκολο | 25 | Complete 3 objectives in Endless | `endless_obj` | 3 |
| Εύκολο | 25 | Break 4 vases in Endless | `vases` | 4 |
| Εύκολο | 25 | Light 4 candles in Endless | `candles` | 4 |
| Εύκολο | 25 | Serve 8 customers in Summer Vacation | `served` | 8 |
| Εύκολο | 25 | Survive 2 minutes at Papa's party | `party_minutes` | 2 |
| Εύκολο | 25 | Defeat 10 toys in Recalled | `toys` | 10 |
| Εύκολο | 25 | Spend 50 shards in the shops | `spent` | 50 |
| Μεσαίο | 60 | Complete 6 objectives in Endless | `endless_obj` | 6 |
| Μεσαίο | 60 | Survive 4 minutes in one Endless run | `run_time` (best) | 240 |
| Μεσαίο | 60 | Escape Endless by answering the phone | `exfil` | 1 |
| Μεσαίο | 60 | Light 8 candles in Endless | `candles` | 8 |
| Μεσαίο | 60 | Play 1 day of Summer Vacation | `beach_days` | 1 |
| Μεσαίο | 60 | Defeat 25 toys in Recalled | `toys` | 25 |
| Μεσαίο | 60 | Survive 4 minutes at Papa's party | `party_minutes` | 4 |
| Δύσκολο | 120 | Complete 10 objectives in one Endless run | `run_obj` (best) | 10 |
| Δύσκολο | 120 | Survive 8 minutes in one Endless run | `run_time` (best) | 480 |
| Δύσκολο | 120 | Complete 5 objectives on Hard or Impossible | `hard_obj` | 5 |
| Δύσκολο | 120 | Play 3 days of Summer Vacation | `beach_days` | 3 |
| Δύσκολο | 120 | Defeat 60 toys in Recalled | `toys` | 60 |
| Δύσκολο | 120 | Escape Endless twice | `exfil` | 2 |
| Δύσκολο | 120 | Earn 150 shards today | `shards_today` | 150 |

*Τέλος εγγράφου.*

---

## 26. ΕΠΙΛΟΓΟΣ: 3.9 (αυτόματα updates και Linux, σε release)

Μετά τη γραφή αυτού του εγγράφου, η έκδοση ανέβηκε σε **3.9**. Το 3.9 είναι η έκδοση των **αυτόματων updates**. Οι υπόλοιπες ενότητες περιγράφουν το 3.8, που περιέχεται ολόκληρο στο 3.9. Το Linux support του installer (ενότητα 19) είναι πλέον μέρος του 3.9.

**Αυτόματα updates (νέα αρχεία):** `src/AutoUpdate.cs` (έλεγχος και κατέβασμα στο mod, `UnityWebRequest`), `src/UpdateCore.cs` (manifest, υπογραφή, hash, ασφάλεια διαδρομών), `src/UpdateKey.cs` (το δημόσιο κλειδί), `tools/updater/` (το BepInEx patcher `AfterHoursUpdater.dll` που εφαρμόζει το update πριν φορτώσουν τα plugins), `tools/release/` (`make_keys.ps1`, `make_release.ps1`, `publish_release.ps1`), `tools/update_test/` (headless δοκιμές: υπογραφή, manifest, hash, διαδρομές και ο πραγματικός updater πάνω σε προσωρινό φάκελο), `tools/installer/make_payload.ps1` (στο repo πλέον, με τον updater μέσα). Config: `Updates/AutoUpdate` (true), `Updates/ManifestUrl` (κενό). Build χωρίς αντιγραφή στο παιχνίδι: `-p:SkipDeploy=true`. Όλες οι λεπτομέρειες, η ασφάλεια και η διαδικασία release: **`docs/AUTO_UPDATE.md`**. Το κλείσιμο του check γίνεται σε κάθε εκκίνηση (6 δευτερόλεπτα μετά), από το δημόσιο repo `ImPoPzzZ/after-hours-releases`, branch `public`.

**Έκδοση:** `VersionTag.VERSION = "3.9"`, `[BepInPlugin]` 3.9.0, installer 3.9, changelog στο `docs/release_3.9/devlog_changelog.md`.
