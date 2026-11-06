# Plan: ECS i ConsoleEngine

Status (2026-11-05): fase 0–4 er ferdige. Motoren ligger i `ConsoleEngine/Ecs/`, målingene i `ConsoleEngine.Benchmarks/`,
alle fem eksemplene har en ECS-port (`Examples/<Game>.Ecs`), og `Examples/Swarm` er stresstesten. Fase 5 gjenstår.

## 1. Mål og ikke-mål

**Mål**

- Simulere svært mange entiteter per frame (mål: 1 000 000 entiteter med `Position` + `Velocity` oppdatert på
  < 2 ms på en moderne desktop-CPU, single-threaded). Tallene er **mål som skal måles**, ikke løfter.
- **0 bytes allokert per frame** i steady state (ingen GC i frame-loopen).
- Cache-vennlig minnelayout: lineær, prefetch-bar lesing av kun de komponentene en system faktisk bruker.
- Additivt API. Eksisterende spill (`Scene`, `GameObjects/`) skal fortsette å virke uendret. Ikke `!`-commit.
- Win32-fritt: ECS-koden ligger i `ConsoleEngine`, avhenger kun av `RenderConsole` og `GameTime`.

**Ikke-mål (v1)**

- Managed komponenter (klasser, strenger, `Sprite`-referanser direkte i komponent).
- Relasjoner/hierarki (parent/child), prefabs, serialisering, nettverk.
- Multithreading (kommer i fase 5, men designet tar høyde for det fra start).

Merk: konsollet er maks noen hundre × hundre celler. Det er **simulering** (fysikk, partikler, AI, kollisjon) som
skalerer med ECS. Rendering er bundet av skjermstørrelse og blir et culling-problem, ikke et throughput-problem.

## 2. Valg av arkitektur: arketyper + chunks

Industrien har to hovedmodeller:

| Modell | Eksempler | Iterasjon | Add/remove komponent |
|---|---|---|---|
| **Arketype + chunk (SoA)** | Unity DOTS/Entities, Bevy (tables), Flecs, Arch (C#) | Best: helt lineær, ingen indireksjon | Dyrere: entitet flyttes mellom arketyper |
| Sparse set | EnTT, DefaultEcs | God for én komponent, svakere for joins | Billig: O(1) |

**Valg: arketype + chunk.** Målet er mange entiteter og rå iterasjonshastighet. Strukturelle endringer
(add/remove) er sjeldnere enn iterasjon og batches via `CommandBuffer`. Arketype-graf med cachede kanter gjør
overgangene O(1) i oppslag + én `memcpy` per komponent.

### Minnelayout

```
Archetype {Position, Velocity, Glyph}
 └─ Chunk 0 (16 KiB, 64-byte aligned, NativeMemory)
    ┌───────────────┬──────────────────────┬──────────────────────┬──────────────┐
    │ Entity[cap]   │ Position[cap]        │ Velocity[cap]        │ Glyph[cap]   │
    └───────────────┴──────────────────────┴──────────────────────┴──────────────┘
      hver kolonne starter på en 64-byte grense (cache line)
 └─ Chunk 1 ...
```

- **Chunk-størrelse 16 KiB** (samme som Unity DOTS): passer i L1d (32–48 KiB) sammen med andre data, og flere
  kolonner strømmes samtidig uten å kaste ut hverandre. Gjør størrelsen til en `const` slik at den kan benchmarkes
  (8/16/32/64 KiB).
- **SoA innenfor chunk**: et system som bare leser `Position` + `Velocity` berører aldri `Glyph`-bytes. Hver
  cache line er 100 % nyttige data. Hardware-prefetcheren ser rene sekvensielle strømmer.
- **Native minne** via `NativeMemory.AlignedAlloc(ChunkSize, 64)`: utenfor GC-heapen, ingen GC-scanning, ingen
  flytting, garantert alignment. `World : IDisposable` frigjør alt.
- **Kapasitet** = `(ChunkSize − header − padding) / sum(sizeof(T))`, beregnet én gang per arketype.
- **Tette arrays**: fjerning gjør *swap-back* (siste rad flyttes inn i hullet). Ingen hull, ingen "alive"-flagg å
  sjekke i hot loop.
- **Chunk-pool**: frigjorte chunks gjenbrukes (free list), så spawn/despawn-bølger ikke allokerer.

### Komponenter

- `where T : unmanaged` overalt. Det gir blittable data, `sizeof(T)` kjent, `memcpy` ved flytting, og ingen
  GC-referanser i native minne.
- Små og spesifikke komponenter (data, ingen logikk). Eksempel: `Position { Vector2 Value; }`,
  `Velocity { Vector2 Value; }`.
- **Tags** (tomme structs, f.eks. `Dead`, `Player`) får ingen kolonne, bare en bit i arketype-signaturen.
- Sprites/strenger refereres med **håndtak** (`SpriteId { int Value; }`) inn i en `SpriteRegistry` som eier
  `Sprite`-objektene. Det er DOTS sin "blob/managed-side-table"-modell.
- Ny 4-byte render-komponent `Glyph { char Char; byte Foreground; byte Background; }`. Til sammenligning er dagens
  `Pixel` 12 bytes (to `ConsoleColor`-enums à 4 bytes + `char`). Se §8 om å pakke `Pixel` også.

### Entitet

```csharp
public readonly struct Entity : IEquatable<Entity>   // 8 bytes
{
    public readonly int Id;       // indeks i entity-tabellen
    public readonly int Version;  // generasjon; øker ved destroy → gamle håndtak blir ugyldige
}
```

- Generational index med free list (standard i EnTT, Bevy, Flecs). Gjenbrukte id-er gir ikke "dangling" håndtak.
- Entity-tabell som **AoS**: `EntityRecord { Version, ArchetypeIndex, ChunkIndex, Row }` (16 bytes, fire per cache
  line). Alle fire feltene leses sammen ved hvert oppslag, så AoS gir én cache miss der SoA ville gitt fire.
  Random access `world.Get<T>(entity)` = 1 record-oppslag + 1 chunk-peker + offset.

### Komponenttyper og signaturer

- `ComponentType<T>.Id`: statisk generisk klasse som tildeler id ved første bruk. JIT-en gjør dette til en
  konstant i spesialisert kode, null kostnad i hot path.
- `ComponentMask`: `struct` med fast 256-bit bitset (`fixed ulong[4]` eller `Vector256<ulong>`). Query-matching er
  `(mask & all) == all && (mask & none) == 0`, noen få instruksjoner med SIMD. Maks 256 komponenttyper i v1
  (klar feilmelding hvis overskredet).
- Arketyper slås opp på signatur i en `Dictionary<ComponentMask, Archetype>` (kun ved strukturelle endringer,
  aldri i iterasjon).
- **Arketype-graf**: hver arketype cacher `Add<T>`/`Remove<T>`-kanter (`ComponentType.Id → Archetype`), slik at
  gjentatte overganger ikke hasher signaturer.

## 3. Queries og iterasjon

```csharp
var query = world.Query(Query.With<Position, Velocity>().Without<Dead>());
```

- **Cachet**: en `Query` holder listen over matchende arketyper og oppdateres *inkrementelt* når nye arketyper
  opprettes (sammenligner `world.ArchetypeCount` med sist sett). Ingen matching per frame.
- Tomme chunks/arketyper hoppes over.

To iterasjons-API-er, begge uten allokering og uten delegater/closures (jf. AGENTS.md § Performance):

**a) Chunk-iterasjon (lavest nivå, raskest, SIMD-vennlig)**

```csharp
foreach (ref readonly var chunk in query.GetChunks())
{
    var positions = chunk.GetSpan<Position>();
    var velocities = chunk.GetSpan<Velocity>();
    for (var i = 0; i < chunk.Count; i++)
        positions[i].Value += velocities[i].Value * dt;
}
```

`GetChunks()` returnerer en `ref struct`-enumerator. Kolonne-offset per komponent slås opp én gang per chunk,
ikke per entitet.

**b) Struct-callback (ergonomisk, like raskt etter inlining)**

```csharp
struct MoveJob : IForEach<Position, Velocity>
{
    public float Dt;
    public void Execute(ref Position position, ref Velocity velocity) => position.Value += velocity.Value * Dt;
}

query.ForEach<MoveJob, Position, Velocity>(ref job);
```

`TJob : struct, IForEach<...>` gjør at JIT spesialiserer og inliner `Execute`, i motsetning til `Action<...>`
(indirekte kall + closure-allokering). Samme mønster som Arch og Unity `IJobEntity`. Generer overloads for 1–6
komponenter med en T4/source generator, eller skriv dem for hånd i v1.

Inne i loopene: `ref T` via `MemoryMarshal.GetReference` + `Unsafe.Add` for å unngå bounds checks der JIT ikke
fjerner dem selv. Verifiser med disassembly (`[DisassemblyDiagnoser]`).

## 4. Strukturelle endringer: CommandBuffer

Å legge til/fjerne komponenter eller ødelegge entiteter **mens** man itererer en query invaliderer chunk-layouten.
Standardløsningen (DOTS `EntityCommandBuffer`, Bevy `Commands`, Flecs deferred mode):

- `CommandBuffer` med **forhåndsallokert** byte-buffer (vokser sjelden, nullstilles per frame). Kommandoer lagres
  som `(opcode, entity, componentTypeId, payload-bytes)`.
- `Create` i buffer gir en midlertidig entitet som løses ved playback.
- Playback ved et definert **sync point**: etter hver `SystemGroup` (se §5). Playback sorterer/grupperer per
  arketype-overgang for batch-flytting.
- Direkte `world.Add/Remove/Destroy` er tillatt utenfor iterasjon. Under iterasjon kaster debug-builds
  `InvalidOperationException` (en `_iterationDepth`-teller), mens release hopper over sjekken.

- `Create` i bufferet gir et **deferred håndtak** tilbake: en `Entity` med negativ id og bufferets generasjon som
  versjon (samme modell som Unity sin `EntityCommandBuffer`). Det kan brukes i senere kommandoer på samme buffer og
  byttes ut med den ekte entiteten ved playback. Håndtak fra et annet buffer eller fra før siste playback kaster.
- Playback kjører kommandoene i rekkefølge, uten sortering per arketype. `CommandBufferBenchmarks` viser at buffering
  koster cirka 2× for create+destroy (54 mot 27 ns per entitet) og 1,25× for add/remove. Arketype-oppslaget gjøres
  uansett per Create, så batching ville ikke fjernet ekstrakostnaden. Den kommer fra å skrive og lese bytes. Ikke
  verdt kompleksiteten nå.
- Vakten er på i **alle** builds. Repoet har ingen `#if DEBUG`, og NuGet-pakken er Release, så en debug-only sjekk ville
  aldri slått inn hos brukerne. Den koster én int-sammenligning per strukturell endring, ikke per entitet.
- `Destroy`/`Add`/`Remove` for døde entiteter hoppes over ved playback. Da kan to systemer trygt ødelegge samme
  entitet (to kuler treffer samme asteroide).

## 5. Systemer og integrasjon i frame-loopen

```csharp
public abstract class EntitySystem
{
    protected World World { get; }
    public bool Enabled { get; set; } = true;
    protected virtual void OnCreate() {}
    protected abstract void OnUpdate(float deltaSeconds);
    protected virtual void OnDestroy() {}
}
```

- Systemer er klasser (ett virtuelt kall per system per frame er neglisjerbart). Arbeidet per entitet skjer i
  struct-jobs/chunk-loops.
- `SystemGroup`: ordnet liste. To standardgrupper: **Simulation** og **Presentation**. Rekkefølge er eksplisitt
  (registreringsrekkefølge, justert av `[UpdateBefore]`/`[UpdateAfter]` med stabil topologisk sortering), ikke
  implisitt.
- Sync point (CommandBuffer-playback) etter hver gruppe.

**Integrasjon uten å bryte noe:**

- Ny `EcsScene<TGame> : Scene<TGame>` som eier en `World` og to `SystemGroup`-er. Den mapper `OnUpdate` →
  Simulation og `OnRender` → Presentation, og `OnUnload` → `World.Dispose()` (frigjør native minne
  deterministisk).
- **Ikke** legg en `World`-property på basis-`Scene`: TerraForM har allerede `GameScene.World` (egen klasse), og
  det ville kollidert.
- Frame-rekkefølgen i `GameBase.Start` forblir uendret (se `docs/agent/03-rules.md#frame-loop`).
- `deltaSeconds` = `(float)GameTime.Delta.TotalSeconds`, beregnet én gang per frame av gruppen. Systemer skal
  skalere med delta (loopen er uncapped).

**Innebygde systemer/komponenter (v1):** `Position`, `Velocity`, `Glyph`, `SpriteId`, `MovementSystem`,
`GlyphRenderSystem`, `SpriteRenderSystem`.

- Render-systemene skriver direkte i frame-bufferen. Dette krever en ny `RenderConsole`-metode som eksponerer
  bufferen som `Span<Pixel>` (internal eller public, avgjøres i fase 3), slik at vi slipper ett metodekall + fire
  bounds-sjekker per celle.
- Culling: ett billig AABB-test mot skjerm/kamera per entitet før skriving.

## 6. Ytelsesprinsipper (sjekkliste for alle PR-er)

| Prinsipp | Konkret |
|---|---|
| Ingen allokering i loop | `ref struct`-enumeratorer, struct-jobs, forhåndsallokerte buffere, chunk-pool |
| Kun nødvendige bytes i cache | SoA per chunk, små komponenter, tags uten kolonne, 4-byte `Glyph` |
| Ingen indireksjon i indre loop | Kolonnepeker hentes per chunk, `ref`-iterasjon, ingen interface-kall per entitet |
| Alignment | Chunks og kolonner 64-byte aligned, ingen false sharing når fase 5 deler chunks mellom tråder |
| Forutsigbare branches | Tette arrays (ingen "alive"-sjekk), filtrering skjer på arketype-nivå, ikke per entitet |
| SIMD der det lønner seg | `Vector2`-kolonner er sammenhengende `float`-par, så `Vector256<float>` kan prosessere 4 entiteter per instruksjon. Vurder i fase 5 |
| Mål, ikke gjett | BenchmarkDotNet med `[MemoryDiagnoser]` og `[HardwareCounters(CacheMisses, BranchMispredictions)]` (krever admin på Windows) |

## 7. Faser

Hver fase er én PR med Conventional Commits, `dotnet build` uten warnings og `dotnet test` grønn.

### Fase 0: Måleoppsett (`build:`, `perf:`)

- Nytt prosjekt `ConsoleEngine.Benchmarks/` (BenchmarkDotNet, Release, ikke del av CI-testkjøring).
- Baseline: dagens OOP-mønster (`List<GameObject>` med virtuelle `Update`) for 10k/100k/1M objekter.
- Valgfritt referansepunkt: samme benchmark mot [Arch](https://github.com/genaray/Arch) (Apache-2.0) **kun som
  benchmark-avhengighet**, ingen kode kopieres inn (jf. AGENTS.md om tredjepartskode, spør eier først).

### Fase 1: Kjerne-lagring (`feat(engine):`)

- `ConsoleEngine/Ecs/`: `Entity`, `ComponentType<T>`, `ComponentMask`, `Chunk`, `Archetype`, `World`.
- API: `Create(...)`, `Destroy`, `IsAlive`, `Add<T>`, `Remove<T>`, `Has<T>`, `Get<T>` (returnerer `ref T`), `Set<T>`.
- Bulk-create: `world.Create<T1,T2>(count, Span<Entity> output)`, som fyller chunks direkte.
- Tester i `ConsoleEngine.Tests/Ecs/` (`Method_State_Expected`): versjonering, swap-back-korrekthet,
  overganger mellom arketyper bevarer data, kapasitet/alignment, `Dispose` frigjør, gjenbruk av chunk-pool.

### Fase 2: Queries og iterasjon (`feat(engine):`)

- `QueryDescription`, `Query` (cachet), chunk-enumerator (`EntityChunk`), `IForEach<...>` med 1–3 komponenter. **Ferdig.**
- Test: allokering i steady state er 0 (`GC.GetAllocatedBytesForCurrentThread()` før/etter N iterasjoner).
- Benchmark mot fase 0-baseline.

### Fase 3: CommandBuffer, systemer, scene-integrasjon (`feat(engine):`)

- `CommandBuffer`, `EntitySystem`, `SystemGroup`, `EcsScene<TGame>`. **Ferdig.**
- `RenderConsole`-tilgang til frame-buffer, `GlyphRenderSystem`, `SpriteRenderSystem`, `SpriteRegistry`.
  **Delvis:** `GlyphRenderSystem` og `OutlineRenderSystem` (med `OutlineLibrary` og `Camera`) er ferdige og tegner via
  `RenderConsole.Draw`/`DrawLine`. `SpriteRenderSystem` (med `SpriteLibrary` og `SpriteRef`, inkludert `Offset`
  og et lagfilter) ble flyttet inn etter at både `Platformer.Ecs` og `TerraForM.Ecs` hadde laget hver sin. Frame-bufferen
  er `internal` (`RenderConsole.Pixels`). `GlyphRenderSystem` og `SpriteRenderSystem` skriver rett i den med én
  klipp-sjekk per entitet. Det gjør glyphs 1,8× og sprites 2,7× raskere (se §7b). `OutlineRenderSystem` bruker fortsatt
  `DrawLine`.
- `[UpdateBefore]`/`[UpdateAfter]` på systemklasser, sortert stabilt i `SystemGroup.RunOrder`. **Ferdig.**
- Debug-guard mot strukturelle endringer under iterasjon. **Ferdig**, og på i alle builds (se §4).
- Innebygde komponenter `Position`, `Velocity`, `Rotation`, `Glyph` (4 bytes), `Outline` og `MovementSystem`.
  `Asteroids.Ecs` bruker dem og har bare spillspesifikke systemer igjen.

### Fase 4: Eksempel og dokumentasjon (`feat:`, `docs:`)

- **Ferdig:** `Examples/Asteroids.Ecs` er `Asteroids` portet til ECS (komponenter i `Components/`, systemer i
  `Systems/`, antall asteroider som første argument). Den har i tillegg skyting, kollisjon, drift og wrap, så den bruker
  create/destroy hver frame. Senere portert: `Snake.Ecs`, `ConsoleGame.Ecs`, `Platformer.Ecs` og `TerraForM.Ecs`. Begge sprite-spillene laget sin egen `SpriteRenderSystem`, som så ble flyttet inn i motoren. `Examples/Swarm` (stresstest, 100 000 partikler som standard) er ferdig, se §7b.
- Oppdater docs per AGENTS.md § Keeping agent docs up to date:
  `AGENTS.md` (prosjekttabell, mappestruktur, hot-path-listen, `Ecs/`-navnekonvensjoner),
  `docs/agent/01-system-map.md` (Benchmarks-prosjekt, Swarm), `03-rules.md` (system-/sync-point-rekkefølge),
  `04-flows.md` + `06-diagnostics.md` (exceptions ordrett), `09-glossary.md` (entity, archetype, chunk, query,
  sync point, command buffer).
- Bump `PackageVersion` til 0.2.0 (additivt API). **Ferdig.**

### Fase 5: Avansert (egne PR-er, kun når benchmarks viser behov)

- **Endringsdeteksjon**: versjonsnummer per chunk-kolonne (DOTS `ChangeVersion`, Bevy `Changed<T>`). Systemer
  hopper over chunks som ikke er endret siden sist.
- **Parallell iterasjon**: chunk-granulerte jobs på en egen worker-pool. Systemer deklarerer read/write per
  komponent, og jobber joines før systemet returnerer, så engine-state forblir på game-loop-tråden (jf. AGENTS.md
  § Threading). Render-systemer forblir single-threaded.
- **SIMD**-varianter av `MovementSystem`.
- **Enableable components** (bitmaske per chunk) for å slå av/på uten arketype-flytting.
- **Source generator** for `IForEach`-overloads og query-boilerplate.

## 7b. Målinger etter fase 0–1

Kjørt med BenchmarkDotNet `--job short`, Release, .NET 10.0.14, i7-11800H. `short`-jobben gir store feilmarginer, så
tallene er grove. Alle radene allokerte 0 B.

| Benchmark (1 000 000 elementer per kall) | Tid | Per element |
|---|---|---|
| `OopUpdateBenchmarks.ObjectList_Sequential` | 3,2 ms | 3,2 ns |
| `OopUpdateBenchmarks.ObjectList_Shuffled` (spredt heap) | 13,4 ms | 13,4 ns |
| `OopUpdateBenchmarks.StructArrays` (nedre grense for fase 2-iterasjon) | 0,93 ms | 0,9 ns |
| `WorldBenchmarks.Get_InCreationOrder` | 3,7 ms | 3,7 ns |
| `WorldBenchmarks.Get_Shuffled` | 24,9 ms | 25 ns |
| `WorldBenchmarks.CreateThenDestroy_OneByOne` | 33 ms | 33 ns per par |
| `WorldBenchmarks.CreateThenDestroy_Bulk` | 19,7 ms | 20 ns per par |
| `WorldBenchmarks.AddThenRemoveTag` | 69 ms | 34 ns per overgang |

Hva tallene sier:

- `World.Get` per handle er omtrent like dyrt som OOP-lista. Det er forventet: ECS vinner på query-iterasjon
  (fase 2), som skal ligge nær `StructArrays`.
- Arketype-overganger (34 ns) domineres trolig av `Dictionary`-oppslag for kantene. Kandidat for optimalisering:
  array indeksert på komponent-id i stedet for `Dictionary<int, Archetype>`. Mål før endring. **Gjort etter
  review, se «Review-rettinger» nedenfor.**

### Fase 2: queries (2026-11-05, samme maskin og oppsett)

| Benchmark (`position += velocity * delta`) | 10 000 | 100 000 | 1 000 000 |
|---|---|---|---|
| `OopUpdateBenchmarks.ObjectList_Sequential` | 14,9 µs | 164 µs | 3,57 ms |
| `OopUpdateBenchmarks.ObjectList_Shuffled` | 23,5 µs | 356 µs | 16,1 ms |
| `OopUpdateBenchmarks.StructArrays` (nedre grense) | 7,6 µs | 75 µs | 0,89 ms |
| `QueryBenchmarks.GetChunks` | 7,5 µs | 79 µs | 0,98 ms |
| `QueryBenchmarks.ForEach` | 8,5 µs | 89 µs | 1,18 ms |

- Chunk-iterasjon ligger på nivå med rene struct-arrays og er 3,6× raskere enn OOP-lista (16× mot en spredt heap).
  Målet fra §1 (1M entiteter < 2 ms) er nådd for denne operasjonen.
- `ForEach` kjører jobben på en lokal kopi. Gjennom `ref job` må JIT anta at komponentskriving kan endre jobben, og
  leste feltene på nytt for hver entitet. Det kostet 2× ved 10k (16,6 → 8,5 µs).

### Fase 3: kostnaden av iterasjonsvakten

Med vakten på (sjekk ved hver strukturelle endring, teller ved start og slutt av hver query-løkke) ligger tallene for
10k og 100k innenfor feilmarginen fra fase 1 og 2. For eksempel tar `CreateThenDestroy_OneByOne` 332 µs mot 345 µs,
`AddThenRemoveTag` 668 µs mot 650 µs, og `GetChunks` 7,6 µs mot 7,5 µs. 1M-radene var for støyete i denne kjøringen
(feilmargin over 100 %) til å si noe.

### Fase 3, del 2: command buffer og tegning (2026-11-05, samme oppsett)

| Benchmark | 10 000 | 100 000 |
|---|---|---|
| `CommandBufferBenchmarks.CreateThenDestroy_Direct` | 266 µs | 2,75 ms |
| `CommandBufferBenchmarks.CreateThenDestroy_Buffered` | 538 µs | 5,21 ms |
| `CommandBufferBenchmarks.AddThenRemoveTag_Direct` | 651 µs | 6,64 ms |
| `CommandBufferBenchmarks.AddThenRemoveTag_Buffered` | 812 µs | 7,83 ms |

| `RenderBenchmarks` (200×200 konsoll) | 1 000 | 10 000 | 100 000 |
|---|---|---|---|
| `Glyphs` via `RenderConsole.Draw` (før) | 6,0 µs | 54 µs | 543 µs |
| `Glyphs` rett i frame-bufferen (etter) | 3,2 µs | 30 µs | 286 µs |
| `Sprites` 4×3 via `RenderConsole.Draw` (før) | 67 µs | 632 µs | 6,18 ms |
| `Sprites` 4×3 rett i frame-bufferen (etter) | 24 µs | 233 µs | 2,29 ms |

Alle radene allokerte 0 B.
### Fase 4: Swarm, en hel frame (2026-11-05)

`SwarmScene` kjørt headless i Release med fast delta på 16 ms: alle systemene og tegning i frame-bufferen, men uten
skriving til konsollet (`WriteConsoleOutput`).

| Partikler | Tid per frame | FPS uten konsollskriving |
|---|---|---|
| 100 000 | 1,4 ms | cirka 700 |
| 1 000 000 | 14,6 ms | cirka 68 |

Allokering ved stabil drift: cirka 0 B (40 B over 500 frames utenom testharnessens egen reflection).

### Review-rettinger: kanter som array, Create via kanter (2026-11-06)

To endringer fra review av PR #16:
- Add/remove-kanter er nå `Archetype[]` indeksert på komponent-id i stedet for `Dictionary<int, Archetype>`.
- `Create` finner arketypen ved å gå langs add-kanter fra den tomme arketypen, i stedet for å bygge og hashe en
  `ComponentMask` per kall.

`AddThenRemoveTag` legger nå til tagger bakfra, slik at swap-back ikke stokker radene mellom iterasjonene, som
tidligere gjorde tallene ustabile. Før og etter er målt med de samme rettede benchmarkene, `--job short --inProcess`
(in-process fordi det auto-genererte prosjektet ikke bygde fra en midlertidig worktree med lang sti).

| Benchmark | Før | Etter |
|---|---|---|
| `WorldBenchmarks.CreateThenDestroy_OneByOne` 10 000 | 327 µs | 241 µs |
| `WorldBenchmarks.CreateThenDestroy_OneByOne` 1 000 000 | 34,8 ms | 27,3 ms |
| `WorldBenchmarks.CreateThenDestroy_Bulk` 10 000 | 155 µs | 160 µs |
| `WorldBenchmarks.AddThenRemoveTag` 10 000 | 548 µs | 475 µs |
| `WorldBenchmarks.AddThenRemoveTag` 100 000 | 5,5 ms | 4,7 ms |
| `CommandBufferBenchmarks.AddThenRemoveTag_Direct` 10 000 | 820 µs | 481 µs |

Bulk er uendret, som ventet: der skjer arketype-oppslaget én gang per kall, ikke per entitet. Raden for
`AddThenRemoveTag_Direct` har høy spredning (feilmargin over 100 % før endringen).

## 8. Relaterte forbedringer (utenfor ECS, men påvirker resultatet)

- `Pixel` er 12 bytes, mens `CharInfo` (Win32) er 4. Å pakke `Pixel` til `char` + `byte` + `byte` reduserer
  frame-bufferen 3× og gjør `Display` billigere. Det er en endring i `ConsoleEngine.Abstractions` (`!`-commit), så
  den bør tas separat.
- Kjent gjeld i AGENTS.md (`GameTime.Update` bruker `List.ForEach` med lambda, `SceneManager` kaller
  `GC.Collect()`, `InputHandler` bruker LINQ) koster per frame uavhengig av ECS. Disse bør fikses før benchmarks
  av hele frame-tiden tolkes.

## 9. Åpne spørsmål til eier

1. ~~Egen pakke eller namespace?~~ Fase 1 bruker namespace `ConsoleEngine.Ecs` i samme pakke.
2. ~~BenchmarkDotNet?~~ Lagt til (0.15.8, MIT) i `ConsoleEngine.Benchmarks`. Arch som referanse-benchmark er ikke
   lagt til: krever eierens ja.
3. ~~Skal `RenderConsole`-bufferen eksponeres `public` eller `internal`?~~ `internal` (`RenderConsole.Pixels`): bare
   motorens render-systemer skriver direkte. Spill bruker Draw-API-et.
4. ~~Hvilket eksempel?~~ Port av Asteroids: `Examples/Asteroids.Ecs`. `Swarm` er ikke laget.
