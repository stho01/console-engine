# Plan: 2D-fysikk i ConsoleEngine

Status (2026-11-07): fase 0–3 er ferdige: `FixedTimestep` og `EcsScene.FixedSimulation`, deteksjon og hendelser,
og Soft Step-løseren. Pilotene er Asteroids.Ecs (sensorer, lag) og det nye eksempelet `Examples/PhysicsSandbox`
(løseren). Fase 4 (tilegrid, spørringer, karakterkontroller) gjenstår. Målingene står i § 12b. Planen er gjennomgått
mot industristandard (Box2D v3, «Fix Your Timestep», Ericson, Godot/Unity 2D). Avvik fra standarden er markert og
begrunnet i § 10.

## 1. Mål og ikke-mål

**Mål**

- En egen, liten 2D-fysikkmodul i `ConsoleEngine/Ecs/Physics/` (namespace `ConsoleEngine.Ecs.Physics`), **uten
  tredjepartsbiblioteker**. Vi følger publiserte design (Box2D v3 først), men skriver koden selv.
- **Ytelse er prioritet 1.** 0 bytes allokert per fysikksteg i steady state. Lineær lesing av chunk-data, tette arrays
  i løseren, ingen `World.Get` per kontakt.
- **Ikke vær kreativ.** Der et løst problem har en standardløsning, bruker vi den. Avvik krever en grunn i § 10.
- Additivt API: eksisterende spill og systemer oppfører seg som før.
- Erstatte den håndskrevne fysikken i eksemplene der det gir mening (§ 9).

**Ikke-mål (v1)**

- Rotasjon og vinkeldynamikk (treghetsmoment, torque). `Rotation` brukes fortsatt til tegning.
- Ledd (joints), polygoner/konvekse former, kapsler.
- Sleeping og øyer (islands).
- Render-interpolering (men forrige posisjon lagres, se § 3).
- Determinisme på tvers av maskiner. Single-threaded og samme input gir samme resultat på samme maskin, men vi lover
  ikke mer. Dette dokumenteres eksplisitt.
- Multithreading. Designet (tette arrays, ingen delt mutabel tilstand per kropp) lar det komme senere.

## 2. Utgangspunkt i dag

Hvert spill løser fysikk selv, og alt kjører med variabel `delta` i en løkke uten FPS-tak:

| Sted | Hva det gjør |
|---|---|
| `ConsoleEngine/Ecs/Systems/MovementSystem.cs` | `Position += Velocity * delta` |
| `Examples/Asteroids.Ecs/Systems/CollisionSystem.cs` | Sirkel mot sirkel, alle kuler mot alle asteroider: O(kuler × asteroider) |
| `Examples/Platformer.Ecs/Systems/PlayerPhysicsSystem.cs` | Gravitasjon, drag, tilekollisjon med hjørneprober og `MaxStep`-substeg. En håndskrevet karakterkontroller |
| `Examples/Swarm/Systems/BounceSystem.cs` | Speiler hastigheten ved skjermkanten. Ingen kropp-mot-kropp |
| `Examples/TerraForM.Ecs/Systems/RoverPhysicsSystem.cs`, `RoverCollisionSystem.cs` | Roverens bevegelse og kollisjon mot kartet |

Variabel `delta` gir FPS-avhengig og ustabil kollisjonsrespons. Det første steget er derfor et fast tidssteg.

## 3. Fast tidssteg

Standard: Fiedler, «Fix Your Timestep».

- `EcsScene` får en tredje gruppe, `FixedSimulation`, som kjøres fra `OnUpdate` via en akkumulator:

  ```
  accumulator += delta
  steps = 0
  while accumulator >= FixedDelta && steps < MaxStepsPerFrame:
      FixedSimulation.Update(FixedDelta)
      accumulator -= FixedDelta
      steps++
  if steps == MaxStepsPerFrame: accumulator = 0   // kast resten, ellers henger spillet stadig mer etter
  Simulation.Update(delta)
  ```

- `FixedDelta` er konfigurerbar per scene, standard 1/60 s. `MaxStepsPerFrame` er for eksempel 4.
- Ved 1000+ FPS kjører de fleste frames **0** fysikksteg. Det er en ytelsesgevinst, men har to konsekvenser:
  - **Systemer som leser kollisjonshendelser kjører i `FixedSimulation`, etter `PhysicsSystem`.** I den variable
    gruppen ville de sett samme hendelser flere ganger eller ikke i det hele tatt (samme regel som `OnCollision*` i
    Unitys `FixedUpdate`).
  - **Input-kanter må låses** til et fast steg har brukt dem. Et hopp som trykkes i en frame uten fysikksteg skal
    ikke forsvinne. `PlayerBody.JumpRequested` i Platformer.Ecs er allerede dette mønsteret.
- Fysikken skal lagre forrige posisjon per kropp, så interpolering (`alpha = accumulator / FixedDelta`, nå
  `FixedTimestep.Alpha`) kan legges til uten å endre datamodellen. Ikke gjort i fase 2: legges til sammen med
  interpoleringen, når den trengs.

## 4. Komponenter

Alle er `unmanaged` structs i `ConsoleEngine.Ecs.Physics`. Posisjon og hastighet gjenbruker motorens `Position` og
`Velocity`.

| Komponent | Felter | Standard |
|---|---|---|
| `RigidBody` | `BodyType` (`Static`/`Kinematic`/`Dynamic`), `InverseMass`, `GravityScale`, `LinearDamping` | Box2D `b2BodyDef`, Unity/Godot body types |
| `Collider` | `ShapeType` (`Box`/`Circle`), `HalfExtents` eller `Radius`, `Offset`, `Category`, `Mask` (bits), `IsSensor`. `Friction` og `Restitution` kommer i fase 3 | Én komponent med formtype, som Box2D `b2Shape` og Unity DOTS `PhysicsCollider`. Filteret ligger i formen, som `b2ShapeDef.filter` i Box2D |

Endret i fase 2: planen hadde en egen `CollisionFilter`-komponent. Filteret ble lagt i `Collider`, slik Box2D gjør. Det
er én komponent mindre per entitet (Create tar maks 6 komponenter, og en Asteroids-kule trenger 6), og filteret leses
uansett sammen med formen. En `Collider` uten `RigidBody` er en statisk kropp, som en collider uten rigidbody i Unity.

- **Tre kroppstyper:**
  - `Static`: flyttes aldri, uendelig masse, testes aldri mot andre statiske.
  - `Kinematic`: flyttes med hastighet, uendelig masse, skyves ikke av kontakter (bevegelige plattformer,
    skriptstyrte fiender).
  - `Dynamic`: styres av krefter og kontakter.
- **Én `Collider`, ikke én komponent per form.** Én query henter alle formene, en entitet kan ikke ha to former ved et
  uhell, og arketypene splittes ikke per form.
- **Blandingsregler** for et par: friksjon = √(a·b), restitusjon = max(a, b) (Box2D-standard).
- **Navnekollisjon:** `Examples/Asteroids.Ecs/Components/Collider.cs` finnes allerede. Den må få nytt navn eller
  erstattes før piloten, ellers skygger den motorens type (se Gotchas i `AGENTS.md`).
- **`MovementSystem`** får `Without<RigidBody>`, slik at fysikkropper ikke flyttes to ganger (samme regel som Unity:
  ikke flytt en rigidbody direkte). Ingen eksisterende entitet har `RigidBody`, så dette er **ikke** en breaking
  change. Det dokumenteres, men trenger ingen `!`-commit.

## 5. Ett fysikksteg

`PhysicsSystem` kjører først i `FixedSimulation`. Rekkefølgen er standard for Box2D v3:

1. **Gather.** Les chunk-spans (`Position`, `Velocity`, `RigidBody`, `Collider`) lineært inn i forhåndsallokerte
   arrays i `PhysicsSystem`. Hver kropp får en tett indeks, og `Entity` lagres ved siden av. Fase 2 bruker én
   32-byte `BodyProxy` per kropp (AoS) i stedet for SoA: en partest leser alle feltene til to tilfeldige kropper, og
   da gir ett struct per kropp færre cache-linjer enn sju separate arrays.
   Kopien er en lineær kopi fra justert native-minne, så den gjøres hvert steg i stedet for å vedlikeholde en map.
2. **Integrer hastighet:** gravitasjon × `GravityScale`, demping. Semi-implisitt Euler.
3. **Broadphase** (§ 6) → kandidatpar.
4. **Narrowphase** (§ 7) → kontakter, med spekulativ margin.
5. **Oppdater parcachen**: match kontakter mot forrige steg (warm starting, begin/end-hendelser).
6. **Løs** med Soft Step (§ 8).
7. **Integrer posisjon** og kjør den sveipede testen mot tilegridet for raske kropper (§ 8).
8. **Scatter.** Skriv `Position` og `Velocity` tilbake chunk for chunk gjennom spans, aldri `World.Set`, og aldri
   strukturelle endringer.

Spillsystemer etterpå i `FixedSimulation` leser hendelsene (§ 8) og registrerer destroys i `Commands`. De spilles av
etter gruppen, som i dag.

## 6. Broadphase: uniformt grid

- **Valg: uniformt grid bygd med counting sort.** Hvert steg: tell kropper per celle, ta prefikssum, fyll ett flatt
  indeksarray. O(n), ingen allokering, sekvensiell tilgang.
- **Avvik fra Box2D**, som bruker et dynamisk AABB-tre. Grunnen står i § 10.
- **Cellestørrelse** ≥ en typisk kropp. Kropper som er større enn en celle legges i alle cellene de dekker.
- **Duplikate par:** et par rapporteres bare i cellen som inneholder minimumshjørnet av overlappet deres.
- **Utenfor gridet:** posisjonen klemmes inn til kantcellene (eller hashes), så ingenting faller ut.
- Statiske kropper bygges inn i gridet bare når de endres, ikke hvert steg.
- Filteret (`Category`/`Mask`) sjekkes før narrowphase.

### Statisk tilegrid

- `CollisionGrid`: én `byte` per tile (tom/vegg, senere enveisplattform osv.), oppslag i O(1), som `TileMap` i
  Platformer.Ecs. Vegger blir **ikke** kropper.
- Kropper testes mot tilene de dekker. Raske kropper bruker en DDA-gange gjennom gridet (Amanatides–Woo).

## 7. Narrowphase

- Tester: AABB–AABB, sirkel–sirkel, sirkel–AABB. Hver gir høyst **ett kontaktpunkt** med normal og separasjon. Uten
  rotasjon trengs ikke mangepunkts-manifolder.
- **Spekulative kontakter:** et par innenfor `separasjon < SpeculativeDistance` gir en kontakt allerede før det
  overlapper. Det hindrer tunnelering mellom dynamiske kropper uten egen CCD (Box2D v3).
- Sensorer gir bare overlapp-hendelser, ingen kontakt til løseren.
- Kontakter skrives til en forhåndsallokert buffer: `Contact { int A, B; Vector2 Normal; float Separation;
  float NormalImpulse; float Friction, Restitution; }`.

## 8. Løser, CCD og hendelser

### Soft Step

Standard i Box2D v3 (Catto, «Solver2D»). Erstatter Baumgarte-korreksjon fra Box2D Lite/v2.

- Myke kontaktbegrensninger (stivhet og demping uttrykt som frekvens og dempingsforhold).
- Rundt 4 substeg per fysikksteg, 1 iterasjon per substeg, deretter et relax-pass uten bias.
- Restitusjon brukes til slutt, bare over en hastighetsterskel.
- Uten rotasjon er hver begrensning skalar langs normalen (og én for friksjon langs tangenten), så kostnaden er lav.

### Warm starting og parcache

- Parcachen husker `NormalImpulse` fra forrige steg og starter løseren med den. Uten warm starting rister kropper i
  ro og stabler.
- Nøkkel: `(Entity, Entity)` pakket som `ulong`, **aldri** tett indeks (indeksene endres når chunk-layouten endres).
- Implementasjon: open-addressing hashmap over `ulong`, ikke `Dictionary`. Buffere vokser geometrisk og gjenbrukes.
- Døde entiteter (ødelagt via `CommandBuffer` mellom stegene) renskes ved gather og gir en end-hendelse.

### Tunnelering

- Dynamisk mot dynamisk: spekulative kontakter (§ 7).
- Raske kropper mot statisk geometri og tilegrid: sveipet test (time of impact). Mot tilegridet en DDA-gange.
- Substeg alene stopper ikke tunnelering.

### Hendelser

Box2D v3-modellen: lister som leses etter steget, ingen callbacks, delegater eller closures.

- `ReadOnlySpan<ContactBeginEvent>`, `ReadOnlySpan<ContactEndEvent>`, og tilsvarende for sensorer. Hver hendelse har
  de to `Entity`-ene.
- Begin trengs for engangslogikk (Asteroids: kule treffer asteroide). Stay kan leses fra den aktive kontaktlisten.

## 9. Spatiale spørringer og karakterkontroller

### Spørringer

Alle motorer har dem, og karakterbevegelse bygger på dem. Alle tar et lagfilter og går mot både grid og kropper:

- `Raycast(origin, direction, distance, filter)`
- `OverlapBox`, `OverlapCircle`
- `CastBox` (sveip en boks, returner første treff med normal og avstand)

Resultater skrives til en buffer som kalleren gir inn (`Span<T>`), ingen allokering.

### Kinematisk karakterkontroller

Industripraksis er en kinematisk kontroller, ikke en dynamisk kropp, for plattformspillere (Godot
`CharacterBody2D.move_and_slide`, Unity kinematisk `Rigidbody2D` + `Cast`). En dynamisk kropp gir svevende eller
ristende kontroll.

- `MoveAndSlide`: sveip med `CastBox`, stopp ved treff, gli langs normalen med restbevegelsen, gjenta et fast antall
  ganger. Returnerer om kroppen står på bakken, treffer taket eller en vegg.
- `PlayerPhysicsSystem` i Platformer.Ecs er allerede en håndskrevet variant. Reglene den har (hopp, drag, `MinSpeed`)
  blir i spillet; kollisjonsdelen flyttes til motoren.

## 10. Avvik fra standarden

| Avvik | Standard | Grunn |
|---|---|---|
| Uniformt grid | Dynamisk AABB-tre (Box2D) | Ericson, *Real-Time Collision Detection* kap. 7: grid er best når objektene har lik størrelse og verden er avgrenset. Konsollspill er nettopp det, og gridet bygges uten pekerjakt eller allokering |
| Ingen rotasjon | Box2D har full vinkeldynamikk | Konsollgrafikk tegner rotasjon grovt, og ingen av eksemplene trenger torque. Halverer løseren |
| Ett kontaktpunkt per par | Manifolder med opptil 2 punkter (2D) | Holder uten rotasjon, fordi ingen kropp kan vippe |
| Ingen sleeping/øyer | Box2D v3 solver sets og islands | Den mest komplekse delen av Box2D. Scener i gridstørrelse trenger det ikke i v1 |
| Ingen interpolering | Fiedler anbefaler det | Celleoppløsning skjuler det meste. Forrige posisjon lagres, så det kan legges til |

## 11. Ytelsesprinsipper (sjekkliste for alle PR-er)

- 0 B per fysikksteg (`[MemoryDiagnoser]`), også når kontaktantallet vokser i steady state.
- Gather og scatter er lineære over chunk-spans. Ingen `World.Get` i steget.
- Alt arbeid i steget går på tette arrays (`Span<T>` over forhåndsallokerte buffere).
- Ingen LINQ, lambdaer, interfaces eller virtuelle kall i indre løkker. Formtester er `switch` på `ShapeType`.
- Ingen strukturelle endringer i steget.
- Kast i egne throw-helpers, som `EcsThrowHelper`, så sjekkede metoder kan inlines.
- Benchmarks før og etter hver fase (`ConsoleEngine.Benchmarks/Physics/`).

## 12. Faser

### Fase 0: Måleoppsett (`perf:`, `test:`)

- Benchmarks i `ConsoleEngine.Benchmarks/Physics/`: broadphase og fullt steg med 1k og 10k kropper.
  Baseline: dagens brute-force-test som i Asteroids `CollisionSystem`.

### Fase 1: Fast tidssteg (`feat(engine):`)

- `FixedSimulation`, `FixedDelta`, `MaxStepsPerFrame` i `EcsScene`.
- Tester for akkumulatoren (0 steg, flere steg, taket nådd kaster resten).
- Docs: `03-rules.md` (frame-loopen), `04-flows.md` (F9).

### Fase 2: Kropper, kollidere, deteksjon (`feat(engine):`) — ferdig

- `RigidBody`, `Collider` (med filter), `PhysicsSystem`, gather, `SpatialHashGrid`, `Collision`, `PairCache`,
  begin/end-hendelser, sensorer. Ingen kontaktrespons ennå. Scatter trengs først når løseren endrer hastigheter
  (fase 3); i fase 2 skriver integrasjonen rett i chunkene med en `IForEach`-jobb.
- `MovementSystem` har `Without<RigidBody>`.
- **Pilot: Asteroids.Ecs.** Spillets `Collider` er erstattet av motorens. Kuler er kinematiske sensorer, asteroider
  dynamiske, og `CollisionLayers` gjør at bare kule og asteroide testes mot hverandre. `CollisionSystem` leser
  `SensorBeginEvents` i `FixedSimulation`. `Bullet`-taggen er fjernet (sensoren er kula). Endret oppførsel: én kule
  som treffer to asteroider i samme steg, ødelegger begge (før: bare den første), og asteroider og kuler flytter seg i
  faste steg på 1/60 s.

### Fase 3: Løser (`feat(engine):`) — ferdig

- Soft Step som i Box2D v3 (`ContactSolver`): 4 substeg, myk push-out med `ContactHertz` 30 og dempingsforhold 10,
  relax-pass, restitusjon til slutt, warm starting fra `PairCache`, friksjon √(a·b), restitusjon max(a, b).
  Standardverdiene er Box2D sine med 1 celle = 1 lengdeenhet.
- Spekulative kontakter: hver kropp får en margin (fart × steg, pluss tyngdekraft × steg²). Broadphase-boksen vokser
  med den, og et par blir en kontakt når gapet er mindre enn begge marginene. Det gjør at raske kropper ikke tunnelerer,
  uten egen CCD. Den sveipede testen mot tilegrid kommer i fase 4.
- `Collision` gir nå separasjonen også når formene er fra hverandre (gap), ikke bare ved overlapp.
- «Touching» (hendelser) er separasjon under `TouchDistance` = 4 × `LinearSlop` (Box2D sin speculative distance),
  så en kropp som hviler ikke blinker mellom begin og end.
- Dynamiske og kinematiske kropper uten `Velocity` regnes som statiske. Rigid bodies uten `Collider` integreres med
  de samme substegene, uten kontakter.
- **Pilot: `Examples/PhysicsSandbox` i stedet for Swarm.** Swarm har 100 000 partikler på 160 × 100 celler (rundt 6 per
  celle). Kollisjon mellom alle gjør den ubrukelig som stresstest, så eier valgte et nytt lite eksempel: baller og
  bokser som slippes i en boks med to hyller og en kinematisk plattform. Spillet ligger i launcheren.

### Fase 4: Tilegrid, spørringer, karakterkontroller (`feat(engine):`)

- `CollisionGrid`, DDA-gange, `Raycast`, `OverlapBox`/`OverlapCircle`, `CastBox`, `MoveAndSlide`.
- **Pilot: Platformer.Ecs.** `PlayerPhysicsSystem` beholder spillreglene og bruker `MoveAndSlide` for kollisjon.
- Vurder TerraForM.Ecs etterpå.

### Fase 5: Bare når målinger viser behov

- Sleeping/øyer, render-interpolering, flere former, multithreading av narrowphase og løser.

Hver fase oppdaterer `docs/agent/` og `AGENTS.md` i samme PR, og legger målingene inn i denne filen.

## 12b. Målinger

### Fase 0: baseline, alle par (2026-11-07)

`ConsoleEngine.Benchmarks/Physics/BruteForceCollisionBenchmarks.AllPairs`: sirkel mot sirkel for alle n·(n−1)/2 par
på rene arrays, som Asteroids `CollisionSystem` men uten ECS-overhead. Verdenen vokser med antallet (16 celler per
kropp). Release, `--job short`, utviklerens maskin.

| Kropper | Tid per kjøring | Allokert |
|---|---|---|
| 100 | 3,0 µs | 0 B |
| 1 000 | 314 µs | 0 B |
| 10 000 | 31,0 ms | 0 B |

Kvadratisk som ventet: ti ganger flere kropper gir hundre ganger tiden. Ved 10 000 kropper bruker brute force alene
nesten to fysikksteg à 16,7 ms. Gridet i fase 2 skal måles mot disse tallene med samme oppsett.

### Fase 1: fast tidssteg

Ingen egen benchmark. `EcsScene.RunSimulation` legger til ett `FixedTimestep.Advance`-kall per frame (noen få
flyttallsoperasjoner, ingen allokering). En tom `FixedSimulation` koster ett tomt `SystemGroup.Update` per fast steg.

### Fase 2: hele fysikksteget (2026-11-07)

`ConsoleEngine.Benchmarks/Physics/PhysicsStepBenchmarks.Step60Hz`: ett `PhysicsSystem`-steg (integrasjon, gather,
grid, narrowphase, parcache, hendelser) på de samme kroppene som baseline, alle dynamiske og i ro, cellestørrelse 4.
Samme maskin og oppsett.

| Kropper | Alle par (fase 0) | Helt fysikksteg | Allokert |
|---|---|---|---|
| 100 | 3,0 µs | 3,8 µs | 0 B |
| 1 000 | 314 µs | 53 µs | 0 B |
| 10 000 | 31,0 ms | 0,98 ms | 0 B |

Lineært som ventet: ti ganger flere kropper gir rundt ti til tjue ganger tiden, og ved 10 000 kropper er hele steget
32 ganger raskere enn bare partestene i brute force. Ved 100 kropper koster gather og parcachen omtrent det brute force
sparer. 1 000-målingen har stor feilmargin (`--job short`, få iterasjoner).

### Fase 3: med løseren (2026-11-07)

Samme benchmark som fase 2 (`PhysicsStepBenchmarks.Step60Hz`), nå med løseren og 4 substeg. Kroppene starter på
tilfeldige steder; overlappende skyves fra hverandre og blir liggende inntil hverandre, så de fleste kontaktene varer
fra steg til steg. Samme maskin og oppsett.

| Kropper | Fase 2 (deteksjon) | Fase 3 (med løser) | Allokert |
|---|---|---|---|
| 100 | 3,8 µs | 5,1 µs | 0 B |
| 1 000 | 53 µs | 109 µs | 0 B |
| 10 000 | 0,98 ms | 2,0 ms | 0 B |

Løseren omtrent dobler steget. 10 000 kropper i kontakt tar 2 ms av et 16,7 ms-steg ved 60 Hz. Feilmarginene er store
(`--job short`).

## 13. Åpne spørsmål til eier

1. **Celleforhold.** En konsollcelle er omtrent dobbelt så høy som bred, men verdenen regnes i celler. En sirkel med
   radius 3 blir derfor en ellipse på skjermen, og diagonal fart ser skjev ut. Skal fysikken regne i celler (som
   eksemplene gjør i dag), eller i kvadratiske enheter med skalering ved tegning?
   **Besluttet (2026-11-07): celler, som i dag.** Én enhet er én konsollcelle på begge akser. Former og fart ser
   strukket ut loddrett på skjermen. Det er kjent og akseptert.
2. **`FixedDelta`.** 60 Hz som standard, eller høyere (for eksempel 120 Hz) for raske spill?
3. **TerraForM.Ecs:** skal roveren over på motorfysikken, eller beholder den sin turbaserte bevegelse?

## 14. Kilder

Gjennomgangen er gjort fra kjente kilder uten å slå opp hver detalj på nytt. Sjekk API-navn mot kildene før de
siteres i kode eller docs.

- Box2D v3 dokumentasjon og Erin Catto: «Solver2D», «Simulation Islands» (box2d.org/posts)
- Glenn Fiedler: «Fix Your Timestep!» (gafferongames.com)
- Christer Ericson: *Real-Time Collision Detection*, kap. 7 (spatial partitioning)
- John Amanatides, Andrew Woo: «A Fast Voxel Traversal Algorithm for Ray Tracing» (1987)
- Godot `CharacterBody2D`, Unity `Rigidbody2D` dokumentasjon
