# Plan: Kartster som spillbart spill

Status (2026-11-14): bølge 1 og 2 er ferdige (§ 7), bølge 3 er i gang (§ 8: minikart, pause og avslutning levert).

Navn: `Examples/Kartster` er konsollversjonen i dette repoet. «Kartster» alene betyr originalspillet (MonoGame) i
`C:\Forse\kartster-server`, som vi henter regler, tall og data fra, men aldri endrer.

## 1. Mål

Et spillbart kart-løp i konsollen: menyer, flere karter med bots, løpsregler med runder og resultater, HUD,
kollisjoner og power-ups, alt så nær originalen som konsollen tillater. Samme regler som resten av motoren: ingen
allokering per frame, tester, docs i samme commit.

## 2. Moduler og eierskap

Hver funksjon ligger i egen mappe med egne systemer og filer. Scenene er det eneste stedet funksjonene kobles sammen.
Det gjør prosjektlederen når en bølge flettes inn.

| Modul | Mappe i `Examples/Kartster` | Eier (bølge 1) | Leverer |
|---|---|---|---|
| UI-sett | `Ui/` | Agent U | `PixelFont`, `IPixelTarget`, HUD-tegning, menyskjerm-rammeverk |
| Menyer | `Scenes/Menus/` | Agent U | Tittel, velg kart, velg bane, resultater, som scener |
| Løpsregler | `Race/` | Agent R | `RaceSession` (ren logikk), `RaceSystem`, banemarkører (`<id>.race.txt`). Ferdig, se § 5 |
| Flere karter og bots | `Bots/`, `KartEntity`, `Systems/` | Agent B | N karter, `BotDriverSystem`, kollisjon mellom karter |
| 24-bit farge | motor + `KartsterGame` | Agent K | `RenderConsole.TrueColor`, VT-utgang, `--truecolor` |
| Kulisser | `Scenery*` | Agent S | `<id>.scenery.txt`, modeller, kulling |
| Power-ups | `PowerUps/` | Bølge 3 | Bokser, plukk opp, bruk |

**Felles regler i bølge 1:** Ingen andre enn K endrer `KartsterGame.cs` og `Program.cs`. B eier `TrackScene`,
`KartPhysicsSystem`, `KartGroundSystem`, `KartRenderSystem` og `KartEntity`, og S legger bare ett tegnekall inn i
`KartRenderSystem`. U og R lager bare nye filer pluss tester.

## 3. Grensesnitt mellom modulene

- **Tegning av UI:** `Ui/IPixelTarget` med `Width`, `Height` og `Set(int x, int y, uint rgb)`. HUD og menyer tegner
  med 0xRRGGBB-farger. En adapter for palett-modus (nærmeste farge i `ConsolePalette`, uten dither) og en for
  24-bit (K sitt API) kobles inn ved fletting. UI-et tegnes over 3D-bildet, etter `Renderer.Finish`.
- **Løpet:** `Race/RaceSession` er ren logikk. Den får kartenes posisjoner per fast steg og gir fase (nedtelling,
  løp, mål), runde, sjekkpunkt, rundetider, plasseringer og resultater. `RaceSystem` (fast steg) mater den fra alle
  entiteter med `Components.Kart`, og legger en `RaceProgress`-komponent på hver kart. HUD og bots leser derfra.
- **Karter:** hver kart er en entitet med `Components.Kart`, `KartControls` og fysikkropp. Spilleren har taggen
  `PlayerKart`. Input skriver `KartControls` for spilleren, `BotDriverSystem` for bots. `KartPhysicsSystem`
  håndterer alle karter.
- **Banedata:** én eksport per bane fra originalen, med originalens egen kode: `ribbon.obj`, `spline.obj`,
  `scenery.txt` (S) og `race.txt` (R: start og mål, sjekkpunkter, startrutenett, antall runder). Eksportverktøyet er
  `tools/KartsterExport/` (`list`, `export <outDir> <trackId...>`, én `ITrackExporter`-klasse per fil; bruk i
  `tools/KartsterExport/README.md`). Det står ikke i løsningen, fordi det trenger originalens kildekode.

### Status: UI-sett og menyer (U)

Levert i bølge 1, koblet inn i bølge 2 (`KartsterGame` starter på `TitleScene`, se § 7):

- `Ui/IPixelTarget` (`Width`, `Height`, `Set(x, y, rgb)`, ignorerer piksler utenfor). `PaletteTarget` regner ut
  nærmeste palettfarge selv (kvadratisk RGB-avstand, uten dither), så `ConsoleEngine.Rendering3D` er urørt.
  `RgbTarget` skriver `0xRRGGBB` rett i en `uint[]`. Begge har `Frame` som kan byttes når rendereren byttes.
- `Ui/PixelFont`: 3×5-glyfer, fast bredde (4 piksler per tegn), `DrawText`/`DrawTextOutlined`/`DrawCentered`/
  `MeasureText` på `ReadOnlySpan<char>`. `TextFormat` skriver tall i en buffer som kalleren eier (`Time` = `m:ss.t`).
- `Ui/Hud.Draw(target, in HudState)`: runde og klokke nede til venstre med stillingen over, fartsmåler nede til
  høyre, power-up oppe til høyre, nedtelling/`GO!`/`FINISHED` oppe i midten. Øvre venstre hjørne er
  minikartet (`Ui/Minimap`, bølge 3).
- `Scenes/Menus/`: `MenuScreen` (ren logikk på `MenuKeys`, tegning), `TitleScene`, `SelectKartScene`,
  `SelectTrackScene`, `ResultsScene(RaceResults)`. Alle er `IUiLayer`.

**Kontrakt for bølge 2:**

- `HudState` fylles hver frame av den som eier løpet (R: runde, antall runder, klokke, plassering, stilling,
  nedtelling, `ShowGo`, `Finished`; B: antall karter; spilleren: fart i km/h). `HudStanding.Seconds` er lederens tid
  for posisjon 1, ellers avstand til lederen. `PowerUp` er null til bølge 3.
- `RaceResults(trackName, rows)` med én `ResultRow` per kart i målrekkefølge lages av R når løpet er ferdig, og gis
  til `new ResultsScene(results)`.
- `KartsterGame` implementerer `Ui/IKartsterMenus`: `KartNames`, `KartIndex`, `TrackNames`, `SelectKart(index)`,
  `StartRace(trackIndex, laps)`; `Renderer`, `Projection`, `Kart` og `KartCenter` finnes allerede. Etter
  `Renderer.Finish` i `OnRender` kaller spillet `(Scene as IUiLayer)?.DrawUi(target)`, og `TrackScene` tegner HUD-en
  der. Spillets egne Enter/Esc/Tab/R må bare gjelde på banen, ellers krasjer de med menyene.
- Palett-modus: `ScenePalette` har fått gult (`0xFFD23F`, `UiColors.Selected`) i stedet for mellomgrått `0x949497`
  (bølge 2), så valgt rad, nål og logo er gule også i 16 farger.

## 4. Bølger

| Bølge | Innhold | Status |
|---|---|---|
| 0 | Kjøring, fysikk, GPU, rendering (tidligere planer) | Ferdig |
| 1 | 24-bit farge (K), kulisser (S), UI-sett og menyer (U), løpsregler (R), flere karter og bots (B) | Ferdig |
| 2 | Flette bølge 1, skjermflyt (meny → løp → resultater), HUD inn i løpet | Ferdig (§ 7); vegger og flere baner flyttet til 3 |
| 3 | Power-ups (P), vegger og kollisjoner (V), flere baner og eksportverktøy (T), minikart, pause og avslutning (M), bots som kjører forbi (O) | Ferdig (§ 8) |
| 4 | Bakker og bruer (H), alle karter (K2), resten av power-ups (P2), spillmoduser (G), motor-opprydding (E), lys og stemning (L) | Startet (§ 9) |

## 5. Løpsregler (`Race/`): status

Ferdig i bølge 1, koblet inn i `TrackScene` i bølge 2. Reglene er originalens, med tall og kilder i
`docs/agent/03-rules.md` (Kartster-raden om løpsregler).

- `RaceMarkers.Load(folder, id)` leser `<id>.race.txt`. `RaceSession` er ren logikk: `Register(id, isHuman)`, per
  fast steg `Advance(sekunder)`, `Update(id, forrige, nå)` for hver kart og `UpdatePositions()`. `CanDrive` er false
  under nedtellingen (3 s); den som skriver `KartControls` holder kartene stille. `RaceSystem` gjør stegene for alle
  entiteter med `Components.Kart`, `Position` og `RaceProgress`, og speiler `Racer` inn i `RaceProgress`.
- Avvik fra originalen: plassering etter porter og så rett avstand til neste port (originalen: runder pluss
  ruteavstand rundt banen); klokken går i faste steg (originalen: per frame); ingen
  `Retire`/`Rejoin`, tider per port (gap). Autopilot og avslutning kom i bølge 3 (§ 8). Bare lukkede ruter.
- `race.txt` (format i `Race/RaceMarkers.cs`), meter i planet (x = verdens X, y = verdens Z):
  `laps <n>`, `start x y fremX fremY venstre høyre`, `checkpoint …` (samme felt, i rute-rekkefølge),
  `grid x y heading` (i rutenett-rekkefølge, 8 plasser). Eksportert med originalens `SelectRoute`, `GetLapCount`,
  `RaceGatePlane.Create` og `GetStartingLocation` av `RaceExporter` i `tools/KartsterExport/`.

Kobling i `TrackScene.OnCreate` (B eller prosjektlederen):

```csharp
var markers = RaceMarkers.Load(Path.Combine("Assets", "Tracks"), track.Name);
var race = new RaceSession(markers);
var slot = markers.Grid[0]; // karten står bak startlinjen; Sample(0) er 60 m før den på Star Circuit
_kart = KartEntity.Create(World, Game.Tuning, new Vector2(slot.Position.X, slot.Position.Y), slot.Heading);
World.Add(_kart, new RaceProgress());
race.Register(_kart.Id, isHuman: true);
FixedSimulation.Add(new RaceSystem(race)); // kjører etter KartGroundSystem
// og i KartInputSystem / BotDriverSystem: if (!race.CanDrive) controls = default;
```

## 5b. Flere karter og bots: status

- **Flere karter og bots (B):** levert. `TrackScene` lager spilleren og 5 bots (`DefaultBotCount`) på et
  midlertidig rutenett (`TrackScene.GridSlot`: to i bredden, ±2 m, rader 6 m fra hverandre bak ruteavstand 0).
  `Components/Kart.Index` peker inn i per-kart-tilstand (`KartDynamics`, ruteplan, mesh); `PlayerKart` og `Bot`
  skiller dem. `Bots/BotDriverSystem` (fast steg) er en enkel Earl–Merlin: ruteplan med svingfart og bremsing,
  pure pursuit mot et punkt på egen fil, ferdighet 0,8–0,3, tilbake på veien og rygging når den sitter fast.
  Kollisjon mellom karter går gjennom boksene (restitusjon 0,1, friksjon 0,3). Flettet i bølge 2: rutenettet kommer fra
  `race.txt` (`TrackScene.StartingSlot`; `StandInGridSlot` bare når banen mangler den), `Bot.Progress` er fjernet
  (den var bare en målestokk for testene, som nå leser `RaceProgress`), og botene tar kartmappene fra
  `KartsterGame.KartFolders`. Trafikk og dytting kom i bølge 3 (§ 8, O). Power-ups: se § 8 (P).

## 6. Åpne spørsmål

- Lyd: konsollen har bare `Console.Beep`. Foreløpig ingen lyd.
- Splitscreen for flere spillere: ikke i denne planen.

## 7. Bølge 2: status

Ferdig. Detaljer og tall står i `docs/agent/03-rules.md` (Kartster-radene), tastene i `08-operator-actions.md`.

- **Flyt:** `TitleScene` → `SelectKartScene` → `SelectTrackScene` → `TrackScene` (løpet) → `ResultsScene`.
  `KartsterGame` er `IKartsterMenus` og leser bare G og C selv; alle andre taster hører til scenen. Fra tittelen
  åpner Space garasjen (Esc tilbake, Tab neste kart). I løpet: R starter på nytt, Esc går til tittelen (bølge 3: Enter/Esc gir pause, § 8).
- **Løpet:** rutenettet fra `race.txt`, spilleren først (pole), som originalen stiller tastaturspilleren foran
  botene. `RaceSession` per løp med antall runder fra menyen; kartene registreres med `Kart.Index`. Ingen kjører
  under nedtellingen. 4 s etter at spilleren er i mål (originalens `ResultsDelaySeconds`) stoppes løpet og
  resultatene vises; de som ikke er i mål får DNF (originalen kjører dem ferdig med autopilot og `FinishRemaining`; gjort i bølge 3, § 8).
- **Palett:** gult `0xFFD23F` i `ScenePalette` i stedet for mellomgrått `0x949497` (ditheres fra naboene); gult
  er også banens kantsteiner og dekkstabler og gule kartdeler.
- **HUD:** `RaceReport.FillHud` fyller `HudState` hver frame uten allokering: fart, runde, klokke, plassering,
  topp 3 med navn og avstand per port (`RaceSession.GapToLeader`), nedtelling, `GO!`, `FINISHED`. Tegnes etter
  `Renderer.Finish` gjennom `PaletteTarget` eller `RgbTarget`.

**Igjen til bølge 3:** power-ups (levert, § 8), vegger og kollisjon mot dem, flere baner (menyen tar
alle `*.ribbon.obj` i mappen allerede), minikart (øvre venstre hjørne er holdt av), autopilot for en spiller i mål og
bots som kjøres ferdig, forbikjøring og dytting for botene, pause.

## 8. Bølge 3: eierskap

| Agent | Oppgave | Eier | Rører ellers (minst mulig) |
|---|---|---|---|
| P | Power-ups som i originalen: bokser på banen, plukk opp, bruk, effekter | `PowerUps/` | Én registrering i `TrackScene`, en krok i `KartPhysicsSystem` for effekter på tuning, `HudState.PowerUp` |
| V | Vegger og kollisjon med gjerder, rør og ribbon-vegger | `Collision/` | Én registrering i `TrackScene`; kollisjonsdata i baneeksporten |
| T | Eksportverktøyet inn i repoet (`tools/KartsterExport/`), flere baner, tre-modeller | `tools/`, `Assets/` | Ingen kode i spillet utenom det banene trenger |
| M | Minikart, pause, autopilot for ferdig spiller og avslutning for resten av feltet | `Ui/Minimap*`, `Race/` (avslutning), `Scenes/` (pause) | `TrackScene` for pause, HUD-utfylling |
| O | Bots: forbikjøring, unngå andre karter, dytting | `Bots/` | Ingenting utenfor `Bots/` |

Ingen endrer `KartsterGame.cs` uten å si det i rapporten. Prosjektlederen fletter `TrackScene` og HUD-utfyllingen.

### Status: vegger og kollisjoner (V)

Levert. Regler og tall i `docs/agent/03-rules.md` (Kartster-raden om vegger).

- **Originalen:** alt utenfor veien som karter treffer, ligger i kollisjonsmeshen (`LevelLoader.LoadCollisionMesh`):
  ytterveggen og gjerdene som 0,5 m tykke bokser (`TrackFenceMesh`), rør som åttekantede stolper (`TrackPipes`), og på
  andre baner porter, trær, bygninger osv. `RaceSimulation.ResolveKartVsCollisionMesh` løser dem med kartens egen
  restitusjon 0,1 og friksjon 0,3. `WallFriction` 0,6 gjelder bare kantveggene (`KartWallConstraint`), som Star
  Circuit ikke har (`WallHeight` 0). Ingen egen fartsregel: farten tapes bare gjennom restitusjon og friksjon.
- **Data:** `<id>.collision.txt` (`segment x1 y1 x2 y2 halfThickness`, `circle x y radius`; formatet trengte ingen
  nye linjetyper), eksportert av `CollisionExporter` i `tools/KartsterExport` med originalens `TrackGround`,
  `TrackFenceMesh.ThicknessMeters`, `TrackBoundaryMesh` (grenser), `TrackExportFrameBuilder` (kantvegger på seksjoner
  med `WallHeight` > 0), `RaceMarkings.PlaceGates` + `StartGateMesh` (portstolper) og `TrackPipes.Place`. Alle fire
  baner har fil: Star Circuit 4 yttervegger, 15 gjerder, 10 rør; Prism Loop 2 portstolper (ingen bakke, gjerder eller
  rør); Eight Ball 4 yttervegger og 140 grensesegmenter (3 grenser); Star Rally 4 yttervegger, 19 gjerder,
  22 kantvegg-segmenter (brua) og 8 rør. Verktøyet nekter (`skipped:`) baner med ruteender, veisperringer,
  midtdelere, autovern, tunneltak, gatefall eller retningsskilt til de er støttet; ingen av de åtte har det.
- **Trær, bygninger, lykter og søyler** (de fire bakkebanene): det `LoadCollisionMesh` tar med (i korridoren; ingen
  bane har en): trestammer og lyktestolper (`TrackTreeMesh`/`TrackLampMesh.AddColliders`) som sirkler, husvegger
  (hver kant av `TrackBuilding.Footprint`) som 0,5 m tykke segmenter, gate- og brusøyler som kvadrater. Formatet
  trengte heller ikke nå nye linjetyper. Norbergring: 929 kantvegger, 8 husvegger, 320 trær; Dunsfold Neon: 132
  kantvegger, 976 husvegger (111 hus), 38 lykter; Kinnekulle: 80 husvegger, 1607 trær; Brands Hatch Indy: 32
  husvegger, 1693 trær.
- **Høyde:** fysikken er plan, så det en kart kjører over eller under er utelatt (`RoadDecks`, `DeckClearance` 1 m):
  alt med vei mer enn 1 m over foten (alle brusøyler: 4 og 16; karter under brua kjører gjennom der søylene står),
  alt med foten mer enn 1 m over laveste vei eller terreng (gangbrua på Kinnekulle, bygd som hus), og kant- og
  husvegger der de går over en lavere vei eller under et dekk (testet per ramme / per meter).
- **Kostnad:** et fast steg med 6 karter (Release): Star Circuit 37 µs, Brands Hatch Indy 60 µs (1725 kropper),
  Dunsfold Neon 308 µs (1146). Tiden går i `PhysicsSystem.Gather`, som leser alle statiske kropper hvert steg; en
  dreid boks koster omtrent ti ganger en sirkel der. Ingen trær er fjernet for avstand. Forslag til motoren: hopp
  over statiske kropper i `Gather` når ingen er endret.
- **Boter:** tre boter fullfører en runde på alle åtte med veggene, uten å rygge (69 s Norbergring, 105 s Dunsfold
  Neon, 88 s Kinnekulle, 88 s Brands Hatch Indy).
- **Kode:** `Collision/TrackCollision` (`Load`, `Parse`, `AddTo(World)`): statiske kropper, en dreid boks per segment og
  en sirkel per rør, friksjon 0,3 og restitusjon 0,1 (parets friksjon `sqrt(0,3 × 0,3)` = 0,3). Én linje i
  `TrackScene.OnCreate`. De 29 statiske kroppene koster omtrent 6–10 µs per fysikksteg med 6 karter, ingen allokering.
- **Avvik og hull:** rør er sirkler (originalen: åttekant med hjørnene på radien); høyden er ikke med (ingen hopper
  over gjerdene); motorens kontaktløser er Box2D-myk (60 Hz mot statiske, ett delsteg) i stedet for
  originalens impulser med 4 iterasjoner, og den bygger broadphase for statiske kropper på nytt hvert steg (et forslag
  til motoren: hold statiske kropper i broadphase mellom stegene). Kantveggene (originalen: et plan på veikanten,
  `KartWallConstraint`, friksjon 0,6) er her 0,5 m tykke bokser rett utenfor kanten med vanlig veggfriksjon 0,3;
  grensene (originalen: null tykke) er 0,5 m bokser sentrert på linjen. Ghost-kart som kjører gjennom vegger er ikke
  laget. Gnister ved treff (`KartWallContact`) mangler.

### Status M: minikart, pause og avslutning

Levert. Regler og tall i `docs/agent/03-rules.md` (Kartster-radene Minimap, Pause og Race end), taster i
`08-operator-actions.md`.

- **Minikart** (`Ui/Minimap`): hele banen nordvendt, skalert inn i 48×48 px oppe til venstre, senterlinjen hvit med
  mørk skygge, hvit strek over startlinjen, botene som prikker i karosserifargen, spilleren som gul pil. Polylinjen
  regnes ut én gang per bane; ingen allokering per frame. Avvik: originalen dreier kartet med spilleren og viser
  110 m rundt.
- **Pause** (`Scenes/PauseMenu`): Enter (originalens tast) eller Esc åpner PAUSED med RESUME, RESTART, QUIT TO TITLE
  (`MenuScreen`); Esc fortsetter. Mens det er pause får scenen et `FixedTimestep` som aldri går, så fast steg og
  klokke står og løpets eget tidssteg beholder akkumulatoren (ingen etterslep). Esc går altså ikke lenger rett til
  tittelen (originalen: Esc går til lobbyen).
- **Avslutning** (`Race/AutopilotSystem`, `Race/RaceFinish`): spilleren i mål får en `Bot` (Merlin) og kjøres videre
  av `BotDriverSystem`; `KartInputSystem` hopper over karter med `Bot`. Etter 4 s `FINISHED` kjøres resten av feltet
  ferdig i 20 ms-skiver per frame («FINISHING RACE», «n/6 HOME») til alle er i mål eller tidsgrensen
  min(30 min, 2 × vinnertid + 30 s) stopper løpet (DNF). Resultatene får ekte tider. `Bots/` er urørt.

### Status: T (eksport og baner)

- `tools/KartsterExport/` (`list`, `export`) gjenskaper Star Circuit-filene byte for byte (bortsett fra
  kommentarlinjen øverst i `scenery.txt` og `race.txt`). Power-ups (P) og kollisjon (V) blir hver sin
  `ITrackExporter`-klasse i `Program.Exporters`.
- Nye baner: `star-rally` (veihøyde 0–3,15 m), `prism-loop` (helt flat, 2,0 km runde) og `eight-ball` (0–0,1 m,
  5 runder). Til sammen 1,8 MB nye filer. Ingen av dem trenger nye scenerimodeller: de har ingen trær.
- Valgt bort: baner med bro over egen vei (`norbergring` 7 m, `dunsfold-neon` 8 m, `neon-city` 6 m: `KartGroundSystem`
  tar første veitrekant under karten), vei under y = 0 (`downtown`, `skaevven`: gresset legges under laveste punkt,
  så resten av veien svever), bakkete baner (10–60 m høydeforskjell; fysikken er plan og ser bort fra stigning),
  doserte `speedway` (2 MB skilt) og alle åpne ruter (ingen `race.txt`).
- Eneste kodeendring i spillet: `Track.Load` stopper senterlinjen ved første hopp over 2 m. Spline-eksporten har
  alle grener etter ruten (Prism Loops `north-section-*`), så banen ble ellers ikke lukket.
- Bakker, broer og terreng trenger høyde i fysikken (eller terreng under veien) før flere av originalens baner passer.

### Status: bots i trafikk (O)

Levert, bare i `Bots/`. Tall og regler står i `docs/agent/03-rules.md` (raden «Bot traffic»).

- **Veien:** `BotRoad` lages én gang i `BotDriverSystem.OnCreate`: prøvene hver ~2 m, retning og hvor langt asfalten
  når til hver side (målt på ribbon-meshen med `Track.TryGetRoadHeight`). Alle `BotRoutePlan` deler den.
- **Trafikk:** hvert steg plasseres alle karter (også spilleren) på veien i en forhåndsallokert `TrafficKart[]`.
  `BotTraffic.Avoid` som originalens `BotTraffic`: hold en fil unna en kart ved siden av, kjør forbi en tregere kart
  foran (tidsluke 1,2 s, 8–30 m) på egen side eller den andre, bare på asfalten og bare der det er ledig, og følg
  (fartsgrense) i stedet for å kjøre inn i en kart foran når det ikke er plass. Filen glir dit med 3 m/s.
- **Dytting:** `BotTraffic.Lean`, en mild utgave av originalens `BotBumping`: av og til (sjanse 0–0,4 etter ferdighet,
  hvert 3. sekund) lener boten seg på en kart ved siden av i 1 s. Aldri når den bremser til en sving, aldri utenfor
  kantmarginen. Dytte tilbake (originalens `BumpsBack`, Merlin og over): en bot med ferdighet ≥ 0,5 som blir dyttet
  (kontaktstart mellom to karter, lukkefart ≥ 2 m/s, den andre brakte mest av den) går etter dytteren i 1,2 s og sikter
  0,6 m forbi linjen dens (`BotTraffic.BumpedBy`, kontakthendelsene fra `PhysicsSystem` via `BotDriverSystem.Contacts`).
- **Originalens øvrige:** slingring (`Wanders`, på, Earl/Merlin) og rubber-banding (`RubberBanding`, av som i
  originalen).
- **`BotDriverSystem`:** samme konstruktør og samme `Drive`; nytt er en `Drive`-overlast med fil, fartsgrense og
  tempo, og egenskapene `RubberBanding`, `Wanders`, `Road`, `GetTraffic`, `GetLaneOffset`, senere `Contacts`,
  `BumpsBack` og `IsBumpingBack` (dytte tilbake). Ikke gjort: power-ups.

### Status: power-ups (P)

Levert på grenen til P. Regler og tall står i `docs/agent/03-rules.md` (Kartster-raden om power-ups), tastene i
`08-operator-actions.md`.

- **Med:** puter fra `<id>.powerups.txt` (eksportert med originalens `TrackPowerUpPadResolver`, alle fire baner;
  boksene er tatt ut av `scenery.txt`), plukk ved å kjøre gjennom (1,2 m), 5 s til de er tilbake, én power-up om gangen,
  hjulet (48 trekk, 2,5 s, stopp med et trykk), trekning fra `normal`-settet vektet etter plassering med en seedet
  `Random`; Speed Boost, Engine Tune, Grip Tune, Bananas (slipp og kast), Lapskaus og Homing Joika (enkel og trippel,
  skutt eller sluppet bak); effektene gjennom `KartPhysicsSystem.Effects`; bots etter originalens `Simple`-regler;
  HUD-navnet; E/T/B (B i stedet for G, som er GPU-bryteren).
- **Utenfor:** de 17 andre typene, hopp (planet), vegger for boksene (V), bots som leter etter puter eller kjører
  rundt farer (O eier linjen), lyd og partikler.
- **Rørt utenfor `PowerUps/`:** `TrackScene` (`PowerUpSystem.Add`, `PowerUpView` på `KartRenderSystem`,
  `HudState.PowerUp`), `KartPhysicsSystem.Effects`, `KartRenderSystem.PowerUps`, en `ComputeForces`-overlast med ferdig
  tuning i `KartDynamics`, `Track.PowerUpPads`, `star-circuit.scenery.txt` (9 `model Box`-linjer ut) og
  `star-circuit.powerups.txt`. `PowerUpExporter` i `tools/KartsterExport` skriver nå `powerups.txt` for alle fire baner (Star Circuit 9, Prism Loop 12,
  Eight Ball 9, Star Rally 9 puter), og `SceneryExporter` skriver ikke lenger putene som `Box`.

### Status: høyde (H)

Levert. Design og tall i `docs/design/kart-physics-plan.md` § 11, regler i `docs/agent/03-rules.md` (Kartster-radene
Height og Tracks).

- **Høyde:** den plane fysikkroppen beholdes; hver kart har høyde, loddrett fart, bakkenormal og `Airborne`
  (`Components/Kart`). `KartGroundSystem` gjør tyngdekraft og ensidig støtte som originalens `KartGroundConstraint`;
  `KartPhysicsSystem` gir tyngdekraften langs bakken som plan kraft og lasten (cos θ) til modellen; i lufta virker
  hjulene ikke. Karten tegnes vippet etter bakken, kameraet følger høyden.
- **Nivåer:** høyeste vei-trekant ikke mer enn 0,5 m over karten (`Track.TryGetGround`), gjennom et rutenett på 4 m
  bygd én gang. Under en bru teller ikke dekket, på brua er det høyest.
- **Terreng:** `TerrainExporter` skriver originalens `TrackTerrain` som `<id>.terrain.txt`; `Terrain` kjører og tegner
  på det (fliser, kulling som kulissene). Sjakkbrettet er igjen for baner uten terreng.
- **Nye baner:** `norbergring` (0–9,8 m, bro 7 m over egen vei), `dunsfold-neon` (0–9,2 m, to broer på 8 m),
  `kinnekulle` (0,1–15,8 m) og `brands-hatch-indy` (0,8–25,9 m), 3,8 MB til sammen, pluss fire tremodeller. Tre boter
  fullfører en runde på hver (beste rundetider 65, 101, 85 og 86 s). Vegger, trær, bygninger og lykter kom senere (status V). CPU-tegning med 40 jagekameravinkler: 3,4–7,2 ms per bilde mot 3,6 på Star
  Circuit; GPU 1,5–2,7 ms.
- **Valgt bort nå:** byene (`downtown`, `neon-city`: bygninger, lykter og tunneltak er ikke med i kulisse-eksporten,
  så de blir tomme), fjellbanene (opptil 470 m), doserte `speedway` (2 MB skilt) og åpne ruter.
- **Porter i høyden (bølge 4, etterarbeid):** `dunsfold-neon` har en port ved brua, så portene har nå originalens
  grenser: `RaceGate.StandOn` (i `TrackScene.CreateSession`) setter `Bottom` 2 m under portens vei og `Top` 60 m over,
  eller 1,5 m under en vei som går over porten; `RaceSystem` gir `Kart.Height` til `RaceSession.Update`. Test:
  `RaceMarkersTests.StandOn_ShippedTrack_TakesInItsRoadButNotAnotherLevel`.
- **Rørt utenfor eget område:** `KartRenderSystem` (to linjer: tegnekallet for bakken og kartens verdensmatrise),
  `Scenery.FromMeshes` (for terrengflisene), `KartFrame.ToWorld`, `Components/Kart`, `ShippedTracksTests` (baner uten
  kollisjon er listet).

## 9. Bølge 4: eierskap

| Agent | Oppgave | Eier | Rører ellers (minst mulig, si det i rapporten) |
|---|---|---|---|
| H | Høyde i kartfysikken: helning, veien på flere nivåer, terreng under veien. Deretter flere av originalens baner | `Driving/` (høyde), `Track.cs`, `Systems/KartGroundSystem.cs`, `Systems/KartPhysicsSystem.cs`, terreng-eksport i `tools/` | Ett tegnekall for terreng i `KartRenderSystem` |
| K2 | Alle originalens karter med tuning | `Driving/KartCatalog.cs`, `Assets/Karts/`, `KartModel.cs` | Ingenting |
| P2 | De resterende power-up-typene og usårbarhet | `PowerUps/` | Ingenting |
| G | Tidskjøring med spøkelseskart (ferdig), delt skjerm for to spillere hvis det lar seg gjøre (design, se under) | `Modes/`, `Scenes/Menus/` (modusvalg) | `TrackScene` (registrering), `KartsterGame` (flyt) |
| E | Motor: pause i `EcsScene`, statiske kropper i broadphase, kontakthendelser til bots, 24-bit i cross-platform | `ConsoleEngine*`, `Libs/` | `TrackScene` (pause-bruk), `Bots/` (dytt tilbake) |
| L | Lys og stemning: frontlys, natt, enkle skygger | `ConsoleEngine.Rendering3D*` (lys), `Lighting/` | `KartRenderSystem` |

Prosjektlederen fletter `TrackScene`, `KartRenderSystem` og `KartsterGame`.

### Status G: spillmoduser (tidskjøring ferdig, delt skjerm som design)

Originalen har ingen tidskjøring og ingen opptak av beste runde: `Docs/replays.md` tar opp hele løp som øyeblikksbilder
(30 per sekund, til `~/.kartster/replays`) for avspilling, ikke som spøkelse. Det vi tar fra den: spøkelseskarter
tegnes gjennomsiktige med `KartGhost.GhostOpacity` 0,35 og kolliderer ikke (`Docs/rigid-body-physics.md`, «Ghost on a
boost»). Delt skjerm finnes: `SplitScreenLayout` gir 2 spillere side om side (venstre/høyre halvdel), 3–4 i kvadranter;
HUD-en ankres i spillerens region, men har samme størrelse (`Docs/hud.md`). Tastaturet er én spiller i originalen
(W/S/A/D og piltastene samtidig); spiller 2+ er håndkontroller.

- **Bygd:** `MODE`-rad i `SelectTrackScene` (`RACE` / `TIME TRIAL`), `IKartsterMenus.StartRace(track, laps, mode)`.
  Tidskjøring = `TrackScene(laps, botCount: 0, TimeTrial)`: spilleren alene, rundetider og `BEST` nede til venstre i
  stedet for stillingen, hver runde tatt opp per fast steg i en forhåndsallokert `GhostLap` (ingen allokering per steg,
  testet), beste runde som spøkelse (samme mesh, `TriangleOpacity` 0,35: blandet i 24-bit, skjermdør i 16 farger),
  ingen entitet og derfor ingen kollisjon, lagret straks i `%LOCALAPPDATA%/Kartster/ghosts/<bane>.<kart>.ghost`
  (`GhostFile`, binært med versjon og sjekk; ødelagt fil gir ikke noe spøkelse og kaster ikke). R og RESTART beholder
  spøkelset.
- **Rørt utenfor `Modes/` og `Scenes/Menus/`:** `TrackScene` (ny ctor-parameter `TimeTrial timeTrial = null`,
  `timeTrial?.AddTo(...)` etter `KartRenderSystem`, `timeTrial?.DrawUi(...)` i `DrawUi`, de to omstartene sender den
  videre, `using Kartster.Modes`), `KartsterGame.StartRace` (modus-parameter, velger scene), `Ui/IKartsterMenus`
  (modus-parameter), `KartRenderSystem.ViewProjection` (ny egenskap, satt der den regnes ut).
- **Utenfor:** modusen huskes ikke mellom besøk i menyen; power-ups er på også i tidskjøring; spøkelsets egne
  trekanter ses gjennom hverandre i 24-bit (originalen tegner dybden først); runde 1 starter på startrutenettet, så
  spøkelset fra en senere runde starter ved streken.

**Delt skjerm (ikke bygd; anslått for stort for denne bølgen fordi det rører fire andres filer).** Plan:

1. **Oppsett:** som originalens `SplitScreenLayout` for 2: venstre og høyre halvdel, 160×180 hver (bildeforhold
   160/180, egen `Projection`). `Modes/SplitScreen.Viewports(width, height)` gir to rektangler; test: dekker skjermen
   uten overlapp.
2. **Rendering:** `KartsterGame` får `IRenderer3D[] ViewRenderers` (to CPU-`Rasterizer`e på 160×180, eller to
   `GlRenderer`e; samme G/C-bytte og `MatchConsoleColors` for begge) og en himmel per halvdel. `OnUpdate` tømmer begge,
   `OnRender` kaller `Finish` på begge og kopierer radene inn i hver sin halvdel av konsollen (samme løkke som nå,
   med x-forskyvning). `KartRenderSystem` må ta inn renderer, projeksjon og spiller i stedet for å lese
   `game.Renderer`/`game.Projection` (eies av L/H nå): én `KartRenderSystem` per halvdel. `SlideSmoke.BuildQuads`
   bygges før hver tegning (kamera per visning), `PowerUpView.Draw` får `deltaSeconds` bare fra den første.
3. **Input:** `PlayerKart` får et `Number` (0/1). `KartInputSystem` leser en tastetabell per nummer: spiller 1
   W/S/A/D + Space, spiller 2 piltastene + `Key.NUMPAD0` (håndbrems). Kolliderer ikke med G/C/E/T/B/R/Enter/Esc.
   Power-ups for spiller 2 (`PowerUpInputSystem`, eies av P2): `Key.NUMPAD1` bruk, `NUMPAD2` kast, `NUMPAD3` slipp, med samme
   `RequestUse` per kart-indeks.
4. **Løp:** `RaceSession` støtter flere mennesker allerede (`Register(id, isHuman: true)`, løpet venter på alle).
   `CreateKarts` må ta en liste med spillerindekser (rutenett 0 og 1), to `RaceReport`-er (én per `playerId`),
   autopilot per ferdig spiller (virker allerede per `PlayerKart`), resultatene når begge er i mål.
5. **HUD:** `IPixelTarget` for en delflate (`ViewportTarget`: `Frame`, `X`, `Width`, `Height`, `Set` med forskyvning),
   `Hud.Draw` og `Minimap.Draw` per halvdel (samme størrelse, som originalen: dekker mer av visningen). Pausemenyen
   over hele skjermen.
6. **Tester:** visningsoppsett, input-ruting (spiller 2 påvirker bare sin kart), to mennesker i `RaceSession`
   (venter på begge), `ViewportTarget` klipper til sin halvdel.
### Status K2: alle karter
- **Levert.** `Assets/Karts` har 20 av originalens 22 karter (OBJ med 4 desimaler, uten kommentarer, LF; om lag 10 MB,
  2,5 MB komprimert). `KartCatalog` har tuningen til hver av dem felt for felt (inkludert chassis, girkasse, styring,
  grep, bremser, tyngdepunkt og treghet), med Joyride. `KartModel` kjenner fargeordene til de nye kartene, glass er
  gjennomsiktig (`Mesh.TriangleOpacity`), `muted` følger spillerfargen, og markører (`muffler`, `thrust`, `pose_`,
  `joint_`, `radar_pivot` …) og deler som bare finnes på boost eller i roboten tegnes ikke.
- **Utelatt:** `unicycle` (ett hjul, ingen aksler; trenger originalens `KartUnicycle`) og `mcluring` (grepet kommer
  fra downforce, som `KartDynamics` ikke har; tuningen ligger i `KartCatalog.McLuring`).
- **Ikke modellert** (karten kjører annerledes enn i originalen): krengning og velting, hjullåsing (elephant),
  lightcycle som lener seg og lysveggen, banana som svever, delorelei som flyr, hektor som spøkelse og whosp som robot.
  Detaljer i `docs/agent/03-rules.md` (raden «Kart catalog»).
- **Risiko (løst i bølge 4):** de ekte bilene har 20 000–40 000 trekanter hver (olabil 3 200). `KartLod` bygger tre
  forenklede utgaver per kart ved lasting (vertex clustering på 4, 10 og 25 cm, farger og glass beholdes) og
  `KartRenderSystem` velger etter avstand (spillerens kart alltid full). Seks tunge karter på Star Circuit og
  Norbergring, CPU: 8,7–9,4 til 4,1–4,5 ms om dagen, 20–22 til 8,5–9,2 ms om natten (også fordi `Rasterizer` nå kaster
  baksidene før skyggeleggingen); GPU 1,7–1,8 til 0,8 ms. Natten er fortsatt over 6 ms: 4 ms av den er nattscenen uten
  karter (lys per piksel).
### Status: P2 (resten av power-upene)
Levert på grenen til P2. Regler og tall i `docs/agent/03-rules.md` (Kartster-raden om power-ups).
- **Med, som originalen:** Invulnerable (5 s: farer, bokser, Joika, røyk, magnet, sperrer og tog går forbi; 1000×
  masse i kontakt), Anvil (4× masse i 8 s), Magnet, Lightning (toppfart 0,667), Vampire, Glitch og Earthquake (på
  styringen), Smoke Bomb (smellet; skyen tegnet som fem klumper), Fake Power-up, Roadblock og Concrete Roadblock (egne
  fysikk-kropper), Fake Finish (bare tegnet), Locomotive (langs midtlinjen); alle i trekningen med originalens vekter,
  bots etter `Simple`-reglene. Usårbar kart: blinkende gull-plate under kartet.
- **Utenfor trekningen:** Trampoline (trenger høyde), Bug Splat og Smudge (bare skjermeffekter); de andre beholder
  forholdet seg imellom.
- **Tilnærmet:** ingen hopp (planet), toget går på midtlinjen og er ingen fast kropp, ≥ 0,7 sprett mot ambolt-kart er
  ute, glitchede bots styrer ikke tilbake.
- **Krokene utenfor `PowerUps/` (bølge 4, gjort):** `KartRenderSystem` tegner kartet i `DrawScale` (lyn), rister
  kamerabildet ved `SteeringNoise` (0,35 m ved 0,3, to sinuser av tegnet tid) og setter `KartsterGame.MirrorView`
  (glitch) og `ViewDarkness` (`SmokeCover` ved øyet); `KartsterGame.OnRender` speiler og vasker 3D-bildet før HUD-en
  (`Ui/ViewEffects`, 16 farger og 24-bit). `BotTraffic.Lean` lener seg ikke på karter som er usårbare eller har
  `Mass > 1` (`TrafficKart.IsUnbumpable`, fra `BotDriverSystem.PowerUps`).
- **Rørt utenfor `PowerUps/`:** bare dokumentasjon. `PowerUpSystem.Add` legger nå også til `PowerUpSteeringSystem` og
  `PowerUpContactSystem` (plassert med `UpdateBefore`/`UpdateAfter` rundt `KartPhysicsSystem`).
### Status: motor (E)
- **Pause i `EcsScene`:** `EcsScene.IsSimulationPaused` stopper `FixedSimulation` og `Simulation` uten å samle tid
  (ingen etterslep, `Alpha` står stille); `Presentation` går videre. `TrackScene.SetPaused` bruker den, og
  `FlowSystem` (pausetastene) går nå først i `Presentation`. Omveien med en `FixedTimestep` på `float.MaxValue`
  (`PauseMenu.Timestep`) er borte.
- **Statiske kropper i broadphase:** `PhysicsSystem` holder de statiske kroppene i et eget `SpatialHashGrid` som bare
  bygges på nytt når en statisk kropp legges til, fjernes, flyttes, dreies eller får ny `Collider`.
  `StaticBodiesStepBenchmarks`: 30 statiske 12,1 → 10,8 µs, 150: 21,5 → 14,6 µs, 1000: 95 → 40 µs per steg.
- **Bots dytter tilbake:** `BotDriverSystem.Contacts` (settes i `TrackScene.AddDriving`) leser `ContactBeginEvents`;
  `BotTraffic.BumpedBy`/`Bumped`/`AWasBumper`, `BotDriverSystem.BumpsBack`/`IsBumpingBack`. Regler i
  `docs/agent/03-rules.md` (Kartster-raden om bot-trafikk).
- **24-bit i cross-platform:** `CrossPlatformConsoleHandler.SetTrueColor`/RGB-`Render` på true-colour-terminaler
  (`COLORTERM`), så `--truecolor`/C virker også på macOS og Linux. Regler i `docs/agent/03-rules.md` § True colour.
**Status L (lys og stemning): levert.** Regler og tall i `docs/agent/03-rules.md` (raden «3D (any game)» om lys og
Kartster-raden om lys og stemning), N i `08-operator-actions.md`, motorlyset i `gpu-rendering-plan.md` § 4c.
- Motor: `IRenderer3D.Lighting` (`SceneLighting`: ambient, solfarge, opptil 16 `PointLight` per piksel, punkt- eller
  spotlys, farget i 24-bit). Uten lys er bildet byte for byte som før. GPU og CPU er like i testene.
- Kartster (`Lighting/KartLights`, `Lighting/KartLamps`): blob-skygge under hver kart dag og natt; natt med N (på fra
  start på `prism-loop`, originalens nattbane), mørk himmel, svak måne, frontlys fra `light_front*`, `light_long*` og
  dempet rødt baklys fra `light_rear*` (posisjonene leses fra OBJ-filene, `KartModel.cs` er urørt), lysende linser
  (`lamp_*`) om natten. Om dagen er lyktene av, som i originalen.
- `KartRenderSystem` (å flette): `using Kartster.Lighting;`, feltene `_kartWorlds` og `_lights`, `PlaceKarts()` og
  `_lights.Begin(...)` før bakken, `_lights.DrawShadows(...)` etter bremsesporene, kartene tegnes fra `_kartWorlds` i
  en løkke, `_lights.DrawLenses(...)` etter kartene; `DrawKarts` ble `PlaceKarts` (fyller `_kartWorlds` i stedet for å
  tegne). Høydeagentens kart-verdensmatrise hører hjemme i `PlaceKarts`.
  `KartsterGame` og `TrackScene` er urørt (natthimmelen
  tegnes med en ekstra `Clear` i `KartLights.Begin`).
- Ikke gjort: bremselys som lyser sterkere, skygger under scenerimodeller, lys fra power-ups og hazards.
