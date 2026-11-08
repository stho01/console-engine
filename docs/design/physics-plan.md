# Plan: 2D-fysikk i ConsoleEngine

Status (2026-11-07): fase 0–5 er ferdige: fast tidssteg, deteksjon og hendelser, Soft Step-løseren, `TileGrid`,
spørringer og `CharacterMover`, og i fase 5 rotasjon og konvekse polygoner, kropper mot tilegrid, sleeping med øyer
og render-interpolering. Pilotene er Asteroids.Ecs, `Examples/PhysicsSandbox`, Platformer.Ecs og
`Examples/PhysicsShowcase` (alt på én gang). Det som gjenstår står i § 12c. Målingene står i § 12b. Planen er gjennomgått
mot industristandard (Box2D v3, «Fix Your Timestep», Ericson, Godot/Unity 2D). Avvik fra standarden er markert og
begrunnet i § 10.

## 1. Mål og ikke-mål

**Mål**

- En egen, liten 2D-fysikkmodul i en egen assembly, `ConsoleEngine.Physics` (se § 4b), **uten
  tredjepartsbiblioteker**. Vi følger publiserte design (Box2D v3 først), men skriver koden selv.
- **Ytelse er prioritet 1.** 0 bytes allokert per fysikksteg i steady state. Lineær lesing av chunk-data, tette arrays
  i løseren, ingen `World.Get` per kontakt.
- **Ikke vær kreativ.** Der et løst problem har en standardløsning, bruker vi den. Avvik krever en grunn i § 10.
- Additivt API: eksisterende spill og systemer oppfører seg som før.
- Erstatte den håndskrevne fysikken i eksemplene der det gir mening (§ 9).

**Ikke-mål (v1)**

- ~~Rotasjon og vinkeldynamikk~~, ~~konvekse polygoner~~, ~~sleeping og øyer~~, ~~render-interpolering~~: kom i
  fase 5.
- Ledd (joints), kapsler, avrundede polygoner, kontinuerlig kollisjon (TOI) mellom dynamiske kropper.
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

Alle er `unmanaged` structs i `ConsoleEngine.Physics`. Posisjon og hastighet gjenbruker motorens `Position` og
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
- **`MovementSystem`** flytter ikke fysikkropper. Spill med fysikk legger til
  `new MovementSystem(Query.Without<RigidBody>())` (samme regel som Unity: ikke flytt en rigidbody direkte). Se § 4b.

## 4b. Egen assembly og grensesnitt (2026-11-07)

Fysikken ligger i `ConsoleEngine.Physics` (egen DLL og NuGet-pakke), ikke i `ConsoleEngine`. Et spill uten fysikk
refererer ikke til den. Avhengigheten går bare én vei: fysikken bruker motorens offentlige ECS-API (`EntitySystem`,
`Query`, `EntityChunk`, `Position`, `Velocity`), og motoren vet ingenting om fysikken.

- **`MovementSystem` blir i motoren.** Før flyttingen hadde den `Without<RigidBody>`. Nå tar den et valgfritt filter i
  konstruktøren (`Position` og `Velocity` kreves alltid), og spill med fysikk sender `Query.Without<RigidBody>()`.
  Glemmer et spill filteret, flyttes rigid bodies to ganger. Det står i `05-triage.md` P17.
- **Grensen mot andre fysikkmotorer er dataene, som i Unity DOTS** (Unity Physics og Havok bruker samme komponenter):
  `RigidBody`, `Collider`, `Position`, `Velocity` og hendelsene. En adapter er et annet system som leser de samme
  komponentene og skriver tilbake posisjon, fart og hendelser. Spillkoden leser hendelsene gjennom `IPhysicsEvents`,
  ett virtuelt kall per steg, ikke per kropp. Et objekt-API i Godot-stil (`IPhysicsWorld.CreateBody` …) er valgt bort:
  virtuelle kall per kropp og synkronisering av håndtak går imot ytelseskravet.
- `IPhysicsQueries` (`Raycast`, `Overlap*`, `CastBox`) kom i fase 4. `CharacterMover` tar et `IPhysicsQueries`, så
  den virker med en annen fysikkmotor bak samme grensesnitt.

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

- `TileGrid` (planlagt som `CollisionGrid`): én `bool` per tile, oppslag i O(1), som `TileMap` i Platformer.Ecs.
  Vegger blir **ikke** kropper.
- Raycast går gjennom gridet med DDA (Amanatides–Woo, `GridWalker`). En box cast tester tilene i den sveipede boksen;
  det holder fordi slike sveip er korte (ett steg).
- **Fase 5:** dynamiske kropper mot tilene i løseren. Hver våken dynamisk kropp får kontakter med de solide tilene
  under sine voksne bounds; en tile er en statisk boks. Parcachen har 64-bits nøkler, så par kropp–tile
  (`PairCache.KeyOfTile`) lagres ved siden av par entitet–entitet, og alle tiles deler én statisk løserkropp. En
  tileflate som grenser til en annen solid tile, dytter aldri, så kropper glir over skjøter. Bokser og polygoner bruker
  bare tilens åpne flater som referanseflate (`Collision.PolygonTile`): mot én tile gir polygonets egne, skrå flater ofte
  størst separasjon, og den normalen pekte inn i nabotilen, så kontakten ble kastet og spissen av en trekant gikk rett
  gjennom et tynt tak. Sirkler hoppes over når de ville dyttes ut gjennom en lukket flate. Tile-kontakter har ingen
  hendelser.

## 7. Narrowphase

- Tester: sirkel–sirkel, polygon–sirkel og polygon–polygon (bokser og tiles er polygoner). Fra fase 5 gir de en
  manifold med normal og opptil **to punkter** (Box2D): uten to punkter kan ikke en dreid boks ligge stabilt flatt.
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
| Rotasjon er valgfri per kropp | Box2D: alle dynamiske kropper roterer (med mindre fixed rotation) | Kropper uten `Rotation` + `AngularVelocity` har fast rotasjon, så eksisterende spill er uendret. Kontakter der ingen kropp kan rotere, løses med en lineær snarvei med samme resultat |
| Rotasjon rundt `Position` | Box2D roterer rundt massesenteret | Enklere for spill (posisjon = rotasjonspunkt). Legg posisjonen i formens sentrum for naturlig bevegelse |
| Øyer bygges per steg (union-find) | Box2D v3 holder øyer vedlike og flytter sovende kropper til egne «solver sets» | Mye enklere. Sovende kropper koster fortsatt gather og broadphase hvert steg (§ 12c); løser og narrowphase hoppes over |
| Polygoner i et bibliotek, ikke i komponenten | Box2D har polygonen i formen | ECS-komponenter må være små og `unmanaged`; samme mønster som motorens `OutlineLibrary` |

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
- `MovementSystem` har `Without<RigidBody>` (etter § 4b: et filter spillet sender inn).
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

### Fase 4: Tilegrid, spørringer, karakterkontroller (`feat(physics):`) — ferdig

- `TileGrid`, `IPhysicsQueries` (`Raycast`, `CastBox`, `OverlapBox`, `OverlapCircle`, resultat `CastHit`) implementert
  av `PhysicsSystem` (`PhysicsSystem.Queries.cs`), og `CharacterMover.MoveAndSlide` med `SlideCollisions`.
- Spørringene ser verden slik den var på slutten av forrige steg, som i Box2D. Den første spørringen etter et steg
  flytter proxyene til sluttposisjonene og bygger gridet på nytt uten marginer; et spill som ikke spør betaler ingenting.
  Hver kropp testes én gang per spørring (stempel per kropp, ingen allokering). Sensorer rapporteres aldri.
- Box cast er en raycast mot formen utvidet med boksen (Minkowski-sum): boks blir større boks, sirkel blir boks med
  avrundede hjørner. Starter boksen inni en form, treffes den ved fraction 0 bare hvis bevegelsen går videre inn.
- Indre kanter i tilegridet (en flate som grenser til en annen solid tile) treffes aldri, så en boks som glir langs en
  rad med tiles ikke hekter seg på skjøtene. Det er den vanlige løsningen for tilekart; Box2D bruker kjedeformer.
- `MoveAndSlide` følger Godot: sveip, stopp `SkinWidth` (0,01) før treffet, fjern bevegelsen og farten inn i flaten,
  gli videre, maks 4 treff. Gulv/tak/vegg skilles med 45° mot `up`.
- **Pilot: Platformer.Ecs.** `PlayerPhysicsSystem` beholder spillreglene (gravitasjon, hopp, drag, `MinSpeed`, at et
  hopp i lufta skjer ved landing) og bruker `MoveAndSlide` mot `TileMap.ToTileGrid()`. Den gamle kollisjonskoden med
  hjørneprober, `MaxStep`-substeg og `RectangleF` er borte. Spilleren går nå i det faste steget; input låses hver frame
  i `PlayerBody.RunDirection` (erstatter `Acceleration`, som summerte per frame) og `JumpRequested`.
- TerraForM.Ecs er ikke flyttet: roveren går i ruter med kommandoer, ikke med fysikk.

### Fase 5: Rotasjon, polygoner, tiles i løseren, sleeping, interpolering (`feat(physics):`) — ferdig

- **Rotasjon og polygoner** (Box2D v3): `AngularVelocity`, konvekse polygoner i `PolygonLibrary` (hull, maks 8 hjørner,
  treghet fra `b2ComputePolygonMass`), og manifolder med opptil 2 punkter fra SAT og klipping (`b2CollidePolygons`).
  Løseren har vinkelledd, faste ankre og separasjon fra endret posisjon og rotasjon (`b2SolveContact`). Rotasjon
  integreres med `b2IntegrateRotation` (ingen sin/cos per substeg). Spørringene håndterer dreide bokser og polygoner
  (raycast mot polygon, box cast mot Minkowski-summen).
- **Kropper mot tiles**: se § 6.
- **Sleeping**: øyer av dynamiske kropper som berører hverandre (union-find over kontaktene). En øy der alle har vært i
  ro i `TimeToSleep` (0,5 s; 0,05 celler/s, 2°/s), sover. Vekkes av berøring fra en kropp som beveger seg, en tapt
  kontakt, eller en fart satt av spillet. Par av to sovende (eller sovende og statisk) gjenbruker forrige stegs
  cache-oppføring uten narrowphase. Våkner den ene, måles paret likevel, og vekkingen sprer seg gjennom hele øya
  samme steg.
- **Interpolering**: `Interpolation` + motorens nye `RenderOffset` + `InterpolationSystem` (Fiedler).
  Motorens tre tegnesystemer legger til `RenderOffset` når entiteten har en.
- **Ytelse**: kroppen er delt i `BodyState` (32 bytes, det eneste løkkene i substegene leser) og `BodySim`, som
  Box2D. Masser kopieres inn i kontakten. Kontakter bygges på plass i bufferen. Gridet lagrer bounds i oppføringene og
  regner hash én gang per oppføring. Se målingene i § 12b.
- **Pilot: `Examples/PhysicsShowcase`**, i launcheren: alle funksjonene i én scene.

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

### Fase 4: spørringer, og stabile målinger (2026-11-07)

To ting gjorde fysikkmålingene ustabile; begge er rettet:
- `PhysicsStepBenchmarks` startet med overlappende kropper, og tilstanden endret seg mens BenchmarkDotNet varmet opp.
  Resultatet hang da av antall oppvarmingssteg (2,0 eller 3,2 ms for 10 000). Nå får kroppene sette seg i 600 steg
  (10 s) i oppsettet, og målingen er en stabil tilstand.
- På denne maskinen havner prosessen på raske eller trege kjerner (hybrid-CPU): 100 og 1 000 kropper varierte opptil
  1,8× mellom kjøringer. Med `--affinity 1` er kjøringene like innen få prosent. Bruk det når to målinger skal
  sammenlignes.

`--job short --affinity 1`, samme maskin:

| Kropper | Fysikksteg (stabil tilstand) | Allokert |
|---|---|---|
| 100 | 7,0 µs | 0 B |
| 1 000 | 136 µs | 0 B |
| 10 000 | 2,1 ms | 1 B per steg i snitt (BenchmarkDotNet runder opp en sjelden allokering; 0 B uten affinity) |

`PhysicsQueryBenchmarks` (uten affinity), 1 000 spørringer per operasjon blant statiske sirkler:

| Kropper | 1 000 raycasts (20 celler) | 1 000 box casts (2 celler) | Steg + første spørring | Allokert |
|---|---|---|---|---|
| 1 000 | 85 µs | 125 µs | 89 µs | 0 B |
| 10 000 | 159 µs | 226 µs | 1,87 ms | 0 B |

En spørring koster 0,1–0,2 µs. Den første spørringen etter et steg bygger gridet på nytt; med statiske kropper er
det det meste av «steg + første spørring».

### Fase 5: kostnaden av funksjonene, og hva sleeping sparer (2026-11-07)

Målt A/B rett etter hverandre på samme maskin (Release, egen testløkke, ikke BenchmarkDotNet, som var urolig under
denne økten): sirkler uten tyngdekraft med demping 1/s, satt i ro i 600 steg, deretter tid per steg. Fase 4 er commit
`6810a5d`.

| Kropper | Fase 4 | Fase 5, sleep av | Fase 5, sleep på |
|---|---|---|---|
| 100 | 17,7 µs | 20,1 µs (+13 %) | 6,9 µs |
| 1 000 | 100 µs | 117 µs (+17 %) | 84 µs |
| 10 000 | 1,47 ms | 1,56 ms (+6 %) | 1,10 ms |

- Første versjon av fase 5 var 35–60 % tregere. Profilering per del av steget viste at løseren og gather/kontakter
  hadde vokst. Delingen `BodyState`/`BodySim`, den lineære snarveien, kontakter bygd på plass og grid-oppføringer med
  bounds tok det ned til 6–17 %.
- Med sleeping er en scene i ro raskere enn i fase 4. Ved 10 000 sovende kropper går mesteparten av tiden til gather og
  broadphase, som fortsatt kjøres for sovende kropper (§ 12c).
- 0 B allokert per steg i alle tilfellene.

### 12c. Det som gjenstår

- Sovende kropper koster fortsatt gather og broadphase hvert steg. Box2D unngår det med «solver sets» og et
  vedvarende tre; her ville det kreve et grid som overlever mellom stegene.
- Ledd (joints), kapsler, avrundede polygoner, kontinuerlig kollisjon (TOI) mellom dynamiske kropper, multithreading.
- `IPhysicsQueries` med flere treff per spørring (Box2D sine callback-varianter), hvis et spill trenger det.
- Hendelser for kontakter med tiles.

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
