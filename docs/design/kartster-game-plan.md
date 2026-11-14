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
| 3 | Power-ups (P), vegger og kollisjoner (V), flere baner og eksportverktøy (T), minikart, pause og avslutning (M), bots som kjører forbi (O) | Startet (§ 8) |

## 5. Løpsregler (`Race/`): status

Ferdig i bølge 1, koblet inn i `TrackScene` i bølge 2. Reglene er originalens, med tall og kilder i
`docs/agent/03-rules.md` (Kartster-raden om løpsregler).

- `RaceMarkers.Load(folder, id)` leser `<id>.race.txt`. `RaceSession` er ren logikk: `Register(id, isHuman)`, per
  fast steg `Advance(sekunder)`, `Update(id, forrige, nå)` for hver kart og `UpdatePositions()`. `CanDrive` er false
  under nedtellingen (3 s); den som skriver `KartControls` holder kartene stille. `RaceSystem` gjør stegene for alle
  entiteter med `Components.Kart`, `Position` og `RaceProgress`, og speiler `Racer` inn i `RaceProgress`.
- Avvik fra originalen: plassering etter porter og så rett avstand til neste port (originalen: runder pluss
  ruteavstand rundt banen); klokken går i faste steg (originalen: per frame); ingen høydegrenser på portene; ingen
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
  22 kantvegg-segmenter (brua) og 8 rør. Verktøyet nekter (`skipped:`) baner med ruteender, trær, bygninger, lykter,
  veisperringer, midtdelere, autovern, søyler, bru-søyler, tunneltak, gatefall eller retningsskilt til de er støttet;
  ingen av de fire har det.
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
  kantmarginen.
- **Originalens øvrige:** slingring (`Wanders`, på, Earl/Merlin) og rubber-banding (`RubberBanding`, av som i
  originalen).
- **`BotDriverSystem`:** samme konstruktør og samme `Drive`; nytt er en `Drive`-overlast med fil, fartsgrense og
  tempo, og egenskapene `RubberBanding`, `Wanders`, `Road`, `GetTraffic`, `GetLaneOffset`. Ikke gjort: dytte tilbake
  (trenger kontakthendelser), power-ups.

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
