# Plan: asset loader i ConsoleEngine

Status (2026-11-12): **fase 0–3 er ferdige.** Fase 0: `ConsoleEngine/Assets/`, `GameBase.Assets`. Fase 1:
TerraForM og TerraForM.Ecs laster kartene gjennom `Assets`, TerraForM.Ecs med sin egen `MapLoader : IAssetLoader<Map>`,
testet i det nye `Examples/TerraForM.Ecs.Tests`. Fase 2: `SpriteLoader` (`.sprite`), `JsonLoader<T>`, `Sprite.Clone()`;
TerraForM-skjermene og hele Platformer.Ecs-innholdet (kart, spiller, flis) ligger som filer. Fase 3: referansetelt
cache, `AssetScope`, `Scene.Assets` (slippes når scenen lastes ut), `AssetHandle<T>` og `AssetStore<T>`; eksempelscenene
laster i sitt eget scope. Tester i `ConsoleEngine.Tests/Assets/` og `Infrastructure/Scenery/SceneAssetsTests`, 15 i
`TerraForM.Ecs.Tests`. Neste steg er fase 8 (analyzer) eller fase 4 (flere kilder). § 2 beskriver utgangspunktet før
fase 0 og oppdateres ikke.
Beslutninger som er tatt står i § 11, åpne spørsmål i § 12.
Oppdater status, § 10 (faser) og § 11 (beslutningslogg) etter hvert som arbeidet går.

Planen er skrevet mot industristandarden for asset-systemer: MonoGame/XNA `ContentManager`, Godot `ResourceLoader` +
`ResourceFormatLoader`, Bevy `AssetServer` + `AssetLoader`, Unity Addressables og Unreal `FStreamableManager`. Der et løst
problem har en standardløsning, bruker vi den. Avvik står med begrunnelse i § 9.

## 1. Mål og ikke-mål

**Mål**

- Én felles, liten asset-tjeneste i motoren: `GameBase.Assets`. Spill ber om en asset med en sti og en type
  (`Assets.Load<Map>("Maps/map1.txt")`) og får et ferdig, typet objekt.
- **Utvidbar.** Et spill registrerer egne loadere for egne formater (kart, nivåer, dialog, tabeller) uten å endre
  motoren. Motorens egne loadere bruker samme API som spillenes.
- **Ytelse:** lasting skjer utenfor frame-løkka (i `OnInitialize`/`OnLoad`). Oppslag fra hot path (`Get(handle)`) er en
  array-indeks: 0 allokeringer, ingen `Dictionary`-oppslag per frame.
- **Uavhengig av arbeidskatalog.** Stier løses mot exe-mappen (`AppContext.BaseDirectory`), ikke mot CWD. I dag må
  TerraForM startes fra output-mappen fordi `Assets/Maps/*.txt` leses relativt.
- **Testbar uten disk og uten Win32.** `AssetManager` kan konstrueres alene i tester, med en kilde i minnet.
- Additivt API: eksisterende spill fungerer uendret til de flyttes over (§ 8).

**Ikke-mål (v1)**

- Byggetids-pipeline som «koker» assets til binærformat (MonoGame MGCB/`.xnb`, Unity import, Unreal cook). Våre assets
  er små tekstfiler. Vi leser kildeformatet direkte, slik Godot og Bevy gjør i utvikling.
- GUID-baserte asset-ID-er og `.meta`-filer (Unity, Bevy). Vi har ingen editor som kan holde dem i synk. Stien er ID-en.
- Asynkron/streaming lasting i v1 (kommer som fase 6, § 10).
- Lagring av spilltilstand. Savefiler (`EternalDigger/Saving/SaveFile.cs`) er brukerdata som skrives, ikke assets som
  leses fra spillets innhold. De forblir egne.
- Lyd, bilder og fonter. Konsollen har bare tegn og 16 farger.

## 2. Utgangspunkt i dag

Hvert spill løser innlasting selv:

| Sted | Hva det gjør | Problem |
|---|---|---|
| `Examples/TerraForM/Scenes/GameScene.cs` → `WorldLoader.LoadWorld` | Leser `Assets/Maps/{name}.txt` med relativ sti | Avhenger av CWD. Ingen caching. Feil gir rå `FileNotFoundException` |
| `Examples/TerraForM.Ecs/MapLoader.cs` | `File.ReadAllLines(path)` → `Map` | Samme. Parseren er bundet til `File`, så den kan ikke testes uten disk |
| `Examples/TerraForM/TerraForM.csproj` | Én `<None Update>` per kartfil | Nye kart må legges inn for hånd. TerraForM.Ecs bruker allerede en glob med `Link` |
| `Examples/TerraForM/Assets/Sprites.cs`, `Examples/Platformer.Ecs/Assets/Sprites.cs` | Sprites som `static readonly` strengarrays i kode | Innhold i kode: rekompilering for hver endring, ingen hot reload |
| `Examples/Platformer.Ecs/Assets/Maps.cs` | Kart som `string[]` i kode | Samme |
| `Examples/PhysicsShowcase/Level.cs` | Nivå bygget i kode (`Fill(...)`) | Greit for en demo. Ikke et mål å flytte |
| `ConsoleEngine/Ecs/Rendering/SpriteLibrary.cs` | `List<Sprite>`, `int`-ID-er for `SpriteRef` | Det nærmeste vi har en asset-handle. Fylles for hånd |
| `ConsoleEngine/Infrastructure/Logging/Log.cs` | Finner logger-exe via `AppContext.BaseDirectory` | Riktig mønster. Asset-stier skal gjøre det samme |

`Examples/TerraForM/Assets/Story/*.txt` finnes, men brukes ikke.

## 3. Hva industrien gjør

| Motor | Oppslag | Utvidelse | Levetid | Hot reload |
|---|---|---|---|---|
| MonoGame/XNA | `ContentManager.Load<T>(name)`, cache per manager | `ContentTypeReader<T>` (+ importer/processor i pipeline) | `Unload()` frigjør alt managern lastet. Én manager per nivå | Nei |
| Godot | `ResourceLoader.Load(path, typeHint)`, `res://`-stier, global cache | `ResourceFormatLoader`: gjenkjente filendelser + `_load` | Refcount på `Resource` | Ja, i editor |
| Bevy | `AssetServer.load(path)` → `Handle<T>`, `Assets<T>` lagrer data | `AssetLoader`: `extensions()` + `load(reader, settings, ctx)`. `ctx` laster avhengigheter | Sterke handles teller, svake gjør ikke | Ja, `AssetEvent::Modified` |
| Unity Addressables | Adresse/label → `AsyncOperationHandle<T>` | `ScriptedImporter` (import), providers (runtime) | Eksplisitt `Release`, refcount | Editor |
| Unreal | `FSoftObjectPath` → `FStreamableManager` | `UFactory` (import) | GC + `FStreamableHandle` | Live coding/reimport |

Fellesmønsteret vi tar med oss:

1. **Virtuell sti som ID**, uavhengig av hvor bytes faktisk ligger (Godot `res://`, Bevy `AssetPath`).
2. **Kilde atskilt fra format:** en kilde gir en `Stream` (mappe, innebygd ressurs, pakke). En loader gjør `Stream` → `T`.
   Bevy kaller det `AssetReader`/`AssetSource` og `AssetLoader`. Det er det som gjør loadere testbare og formater
   gjenbrukbare.
3. **Loadere registreres på filendelse + type** (Godot, Bevy).
4. **Cache:** samme sti og type gir samme instans (alle fem).
5. **Levetid styres av en eier, ikke av hvert oppslag:** én `ContentManager` per nivå (MonoGame), eller refcount
   (Godot/Bevy/Addressables).
6. **Lastekontekst** som lar en loader laste avhengigheter (Bevy `LoadContext`), slik at et kart kan peke på et
   tileset.
7. **Handles i data, objekter i et lager:** komponenter holder en liten ID, ikke en referanse (Bevy `Handle<T>`). Vi har
   det samme kravet: komponenter må være `unmanaged`.

## 4. Arkitektur

Alt ligger i `ConsoleEngine`, namespace `ConsoleEngine.Assets` (ny mappe `ConsoleEngine/Assets/`). Ikke en egen
assembly: `SpriteLoader` trenger `Sprite`, og alle spill trenger asset-lasting (i motsetning til fysikk).

```
 Spill/scene                 AssetManager                          IAssetSource
 ───────────                 ────────────                          ────────────
 Assets.Load<Map>(path) ──►  normaliser AssetPath
                             cache-oppslag (path, typeof(T)) ─ treff ─► returner
                             finn IAssetLoader<Map> for ".txt"
                             source.Open(path) ──────────────────────►  DirectoryAssetSource / Embedded / Memory
                             loader.Load(stream, context)
                               └─ context.Load<Tileset>(...)  (avhengighet, rekursivt)
                             lagre i cache, registrer eier (scope)
                        ◄──  Map
```

### 4a. Typer

| Type | Ansvar | Standard |
|---|---|---|
| `AssetPath` | `readonly struct`. Normalisert relativ sti: `/` som skilletegn, ingen `.`/`..`-segmenter, ingen rot. `Extension` uten punktum, små bokstaver. Sammenlignes ordinal (skiller store/små bokstaver, likt på Windows og macOS) | Godot `res://`, Bevy `AssetPath` |
| `IAssetSource` | `bool Exists(AssetPath)`, `Stream Open(AssetPath)`, `IEnumerable<AssetPath> Enumerate(AssetPath directory, string extension)` | Bevy `AssetReader`, MonoGame `TitleContainer` |
| `DirectoryAssetSource` | Rot = `Path.Combine(AppContext.BaseDirectory, "Assets")` som standard. Avviser stier som havner utenfor roten | — |
| `MemoryAssetSource` | `Dictionary<AssetPath, byte[]>`. For tester | — |
| `EmbeddedAssetSource` (fase 4) | Innebygde ressurser i en `Assembly` | .NET `EmbeddedResource` |
| `CompositeAssetSource` (fase 4) | Flere kilder i prioritert rekkefølge. Første treff vinner. Gir overstyring (mods, patcher) | Godot resource packs, Bevy flere `AssetSource`-er |
| `IAssetLoader<T>` | `IReadOnlyList<string> Extensions`, `T Load(Stream stream, AssetLoadContext context)` | Bevy `AssetLoader`, Godot `ResourceFormatLoader` |
| `AssetLoadContext` | `AssetPath Path`, `T Load<T>(AssetPath)` for avhengigheter (relativ til `Path`s mappe), registrerer avhengigheten | Bevy `LoadContext` |
| `AssetManager` | Loader-register, cache, kilde. `Load<T>`, `TryLoad<T>`, `Get<T>(AssetHandle<T>)`, `CreateScope()`, `RegisterLoader<T>` | MonoGame `ContentManager`, Bevy `AssetServer` |
| `AssetScope` | `IDisposable`. Eier asset-ene som lastes gjennom den. `Dispose` slipper dem, og en asset uten eiere fjernes fra cachen (og `Dispose`-es om den er `IDisposable`) | MonoGame én `ContentManager` per nivå |
| `AssetHandle<T>` | `readonly struct` med `int Id`. Kan ligge i en `unmanaged` komponent (via en ikke-generisk `AssetId` om nødvendig, se § 12) | Bevy `Handle<T>` (svak) |
| `AssetLoadException`, `AssetNotFoundException`, `AssetLoaderNotFoundException` | Feil med sti, type og loader i meldingen, opprinnelig unntak som `InnerException`. De to siste arver `AssetLoadException`, så én `catch` fanger alle asset-feil | — |

### 4b. API-skisse

```csharp
// Oppsett, i GameBase-ctor (motoren) eller OnInitialize (spillet)
Assets.RegisterLoader(new MapLoader());               // spillets eget format

// Lasting, i OnLoad / OnCreate
var map = Assets.Load<Map>("Maps/map1.txt");          // cachet, eid av scenens scope
var maps = Assets.Enumerate("Maps", "txt");           // erstatter hardkodet TerraformGame.Maps

// ECS: handle i komponent, oppslag i system
var rover = Assets.LoadHandle<Sprite>("Sprites/rover.sprite");
world.Create(new Position(...), new SpriteAsset(rover));
// i systemet: var sprite = Assets.Get(handle);   // array-indeks, 0 allokeringer

// En spill-loader
public sealed class MapLoader : IAssetLoader<Map>
{
    public IReadOnlyList<string> Extensions { get; } = ["txt"];

    public Map Load(Stream stream, AssetLoadContext context)
    {
        using var reader = new StreamReader(stream);
        return Parse(reader);                          // ren parser, testbar med en MemoryStream
    }
}
```

### 4c. Oppslag og registrering

- Nøkkelen i loader-registeret er `(typeof(T), extension)`. To loadere for samme nøkkel kaster ved registrering
  (ikke «siste vinner» i stillhet).
- Samme fil kan lastes som flere typer (`string` og `Map`). Cache-nøkkelen er derfor `(AssetPath, typeof(T))`, som i
  Godot (sti + type hint).
- Innebygde loadere registreres av `GameBase` før `OnInitialize`. Spill kan registrere egne for samme endelse og en
  annen type uten konflikt.

### 4d. Levetid

Valgt modell: **scope eier, handles teller ikke** (MonoGame-modellen, med delt cache).

- `GameBase.Assets` har et rot-scope for globale assets (HUD-tekster, felles sprites). Slippes aldri før spillet slutter.
- Hver `Scene` får `Assets` (et `AssetScope`) som opprettes før `OnLoad` og `Dispose`-es i `Scene.Unload`, etter
  `OnUnload`. `EcsScene` arver det.
- Cachen er delt: laster to scener samme kart, finnes det én gang. Hvert scope teller én gang per asset. Asset-en fjernes
  når siste scope er borte.
- `AssetHandle<T>` er en svak ID: den holder ikke asset-en i live. Å bruke en handle etter at eier-scopet er borte kaster
  (generasjonsteller i handle-en, samme idé som `Entity`-versjonen i ECS). Bygget: sjekken er på i alle bygg, ikke
  bare Debug; den koster én sammenligning (§ 7).
- Bygget i fase 3, i tillegg: avhengigheter (`AssetLoadContext.Load`) eies av asset-en som lastet dem og slippes med
  den, og en loader som feiler slipper avhengighetene den rakk å laste. `IDisposable`-assets disposes når de slippes.

Hvorfor ikke refcount per handle (Bevy/Addressables)? Handles må ligge i `unmanaged` komponenter og kopieres fritt. Uten
destruktører på structs blir refcount per handle manuell `Release`, og det glemmes. Scope per scene passer scene-stakken
vi allerede har.

### 4e. Feil

- Manglende fil: `AssetNotFoundException` med virtuell sti **og** full fysisk sti (`DirectoryAssetSource` vet roten). Den
  vanligste supporthenvendelsen («kartet lastes ikke») blir da selvforklarende.
- Ingen loader: `AssetLoaderNotFoundException` med type, endelse og listen over registrerte endelser for typen.
- Parsefeil: loaderen kaster det den vil. `AssetManager` pakker det inn i `AssetLoadException` med sti og loader-type.
- `TryLoad<T>` for valgfrie assets (returnerer `false` bare for manglende fil; parsefeil kaster fortsatt).
- Ingen stille reserve-asset i v1 (Godot/Unreal viser en «missing»-tekstur). Kan komme senere, se § 12.

### 4f. Tråder

Som resten av motoren: alt kjører på spill-løkkas tråd, ingen låser. Async (fase 6) gjør I/O og parsing på en
bakgrunnstråd, men legger resultatet inn i cachen på spilltråden ved starten av neste frame (Bevy gjør det samme).

## 5. Innebygde loadere

| Loader | Endelser | Gir | Fase |
|---|---|---|---|
| `TextLoader` | `txt` | `string` | 0 |
| `LinesLoader` | `txt` | `string[]` | 0 |
| `SpriteLoader` | `sprite` | `Sprite` | 2 |
| `JsonLoader<T>` | `json` | `T` via `System.Text.Json` med `JsonTypeInfo<T>` (source generator, trygt for trimming/AOT) | 2 |
| Tiled (`.tmj`) | `tmj` | Tile-lag som `int[,]` + properties | Valgfri, § 12 |

`SpriteLoader` registreres av `AssetManager`-ctoren som de to tekst-loaderne. `JsonLoader<T>` gjør ikke det: den
trenger spillets `JsonTypeInfo<T>` og registreres én gang per type.

**Sprite-format (`.sprite`)**, bygget i fase 2. Samme header-stil som kartene (`!key: value`), så det føles kjent:

```
!fg: Yellow
!bg: Black
---
 /\_/\
( o.o )
 > ^ <
```

Én farge per sprite i v1, akkurat som `Sprite.FromStringArray` i dag. Fargelag per celle (en andre blokk med tegn som
peker på en palett) er et åpent spørsmål, § 12. Filene skal være UTF-8 uten BOM, så boks-tegn fungerer.

Regler som ble bestemt under byggingen: headeren finnes bare hvis første linje starter med `!` eller er `---`; ellers
er hele fila rader. Bare `fg` og `bg` er gyldige nøkler, og en ukjent nøkkel er en feil (fanger skrivefeil). Radene
beholdes tegn for tegn, også mellomrom på slutten, og `.editorconfig` slår av trimming for `*.sprite`.

## 6. Bygg og distribusjon

- Hvert spill kopierer `Assets/**` til output med én glob, i stedet for én linje per fil:

  ```xml
  <None Include="Assets\**\*" Exclude="Assets\**\*.cs" CopyToOutputDirectory="PreserveNewest" />
  ```

- Fase 7: `ConsoleEngine`-pakken leverer dette som `buildTransitive/ConsoleEngine.targets`, så et spill som refererer
  NuGet-pakken får det uten å skrive noe (samme idé som MonoGames content-targets).
- `.cs`-filer under `Assets/` (`Sprites.cs`, `HudTexts.cs`) er kode, ikke assets, og ekskluderes. På sikt kan
  `Assets/`-mappen bare inneholde datafiler.

## 7. Ytelse

- `Load` er **ikke** hot path. Den allokerer (strøm, parser, objekt) og er ment for `OnInitialize`/`OnLoad`/`OnCreate`.
  I Debug logger `AssetManager` en advarsel via `Log` hvis `Load` gir cache-miss etter første frame i en scene, så
  uventede lastinger midt i spillet synes.
- `Get(AssetHandle<T>)` er en indeks inn i en `T[]` per type (`AssetStore<T>`, samme mønster som `ComponentType<T>`):
  ingen `Dictionary`, ingen boksing, ingen typesjekk på hot path.
- **Målt** (2026-11-12, `AssetBenchmarks`, `--job short --affinity 1 --inProcess`, 10 000 oppslag, 0 B allokert i
  alle): `SpriteLibrary.Get` 22,6 µs (grunnlinje), `AssetStore<T>.Get` 29,1 µs (1,29×), `AssetManager.Get` 45,0 µs
  (2,0×), `Load` med cache-treff 1 764 µs (78×). `AssetManager.Get` er tregere fordi `Sprite` er en referansetype:
  delt generisk kode slår opp `AssetTypeId<T>` via en generisk ordbok. Derfor `GetStore<T>()` (som Bevys
  `Assets<T>`): et system henter lageret én gang i `OnCreate`. Resten av forskjellen mot `SpriteLibrary` er
  generasjonssjekken.
- Benchmark i `ConsoleEngine.Benchmarks/Assets/`: `Get` per handle mot `SpriteLibrary.Get`, og en cache-treff-`Load` for
  å vise at den ikke skal kalles per frame.
- `GC.Collect()` i `Scene.Unload` og `SceneManager.Cleanup` (kjent gjeld) er urørt av denne planen.

### 7a. Analyzer: varsel ved lasting fra hot path (forslag, fase 8)

Status: **foreslått** 2026-11-12, ikke besluttet. Kan bygges når som helst etter fase 0.

En Roslyn-analyzer som gir en kompileringsadvarsel når `Load` kalles fra kode som kjører hver frame. Standarden å
følge er Rider/ReSharper sin «performance-critical context» for Unity, som markerer dyre kall (`GetComponent`, `Find`)
i `Update`. Vi gjør det samme med en vanlig `DiagnosticAnalyzer`, slik .NET-analyzerne (CA-reglene) er bygget.

- **Generisk, ikke asset-spesifikk.** Motoren merker dyre API-er med et attributt, for eksempel `[NotInFrameLoop]`
  (navn ikke bestemt). Analyzeren kjenner ikke `AssetManager`; den ser bare etter attributtet. Da kan samme regel senere
  dekke `Sprite.FromString`/`FromStringArray` og annet AGENTS.md § Performance forbyr i hot path.
- **Hot path = metoder som overstyrer en kjent liste:** `GameBase.OnUpdate`/`OnRender`, `Scene.OnUpdate`/`OnRender`,
  `EntitySystem.OnUpdate`, `IForEach<…>.Execute`, pluss lambdaer og lokale funksjoner inne i dem. Et attributt (for
  eksempel `[FrameLoop]`) lar et spill merke egne metoder.
- **Diagnostikk:** én regel, `CE0001`, `Warning`. I dette repoet blir den en feil (`TreatWarningsAsErrors`). Bevisst
  lasting midt i spillet slås av med `#pragma warning disable CE0001` eller `[SuppressMessage]`, som andre analyzere.
- **Begrensning:** bare direkte kall. Kall via en hjelpemetode (`OnUpdate` → `LoadLevel()` → `Assets.Load`) krever
  analyse av kallgrafen over hele prosjektet, og det gjør vi ikke (Rider gjør det, CA-reglene gjør det ikke). Derfor
  beholdes runtime-advarselen over i Debug: analyzeren tar de direkte kallene ved kompilering, runtime-sjekken resten.
- **Bygg:** eget prosjekt `ConsoleEngine.Analyzers` (`netstandard2.0`, `Microsoft.CodeAnalysis.CSharp`, ingen
  runtime-avhengighet). Pakkes inn i `ConsoleEngine`-pakken under `analyzers/dotnet/cs`, så NuGet-brukere får den
  automatisk. Eksemplene i repoet refererer den med `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`, gjerne
  samlet i en `Directory.Build.props` under `Examples/`.
- **Tester:** `ConsoleEngine.Analyzers.Tests` med `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` (NUnit-varianten):
  kode med kall i `OnUpdate` gir `CE0001`, kall i `OnLoad`/`OnCreate`/`OnInitialize` gir ingenting, `#pragma` virker.

## 8. Migrering av eksempelspill

| Spill | Endring | Fase |
|---|---|---|
| TerraForM.Ecs ✅ | `MapLoader` er `IAssetLoader<Map>`, registrert i `TerraformEcsGame.OnInitialize`. `GameScene` bruker `Game.Assets.Load<Map>`. Kartlisten er **ikke** flyttet til `Enumerate`, se § 11 | 1 (pilot) |
| TerraForM ✅ | `WorldLoader.LoadWorld(game, string[] lines)`. `GameScene` henter linjene med `Game.Assets.Load<string[]>`. Glob i `.csproj` | 1 |
| Platformer.Ecs ✅ | `Assets/Maps.cs` → `Assets/Maps/level.txt` via `LinesLoader`. `Assets/Sprites.cs` → `player.sprite`, `tile.sprite`. Stiene er konstanter i `PlatformerScene` | 2 |
| TerraForM, TerraForM.Ecs ✅ | `Assets/Sprites.cs` (fire skjermer) → `Assets/Sprites/*.sprite`, stier i `SpritePaths` (lenket inn i TerraForM.Ecs som før). Lastes i `OnLoad`/ctor og holdes i et felt | 2 |
| EternalDigger | Ingen i v1. Savefiler er ikke assets | — |

Pilot-kriteriet: TerraForM.Ecs startet fra en annen mappe enn output-mappen laster kartene, og `MapLoader`-tester kjører
mot `MemoryAssetSource` uten disk.

## 9. Avvik fra standarden

| Avvik | Standard | Grunn |
|---|---|---|
| Sti, ikke GUID, som ID | Unity, Bevy (`.meta`) | Ingen editor som holder GUID-er i synk. Godot og MonoGame bruker også sti |
| Ingen byggetids-pipeline | MonoGame MGCB, Unity import | Små tekstfiler. Parsing ved lasting tar mikrosekunder |
| Scope eier, handles teller ikke | Bevy sterke handles, Addressables `Release` | Handles må være `unmanaged` og kopieres fritt. Scope per scene passer scene-stakken (§ 4d) |
| Synkron i v1 | Bevy/Addressables er async-først | Konsollspill laster på millisekunder. Async kommer når et spill trenger lasteskjerm |
| Ordinal (store/små bokstaver teller) sammenligning av `AssetPath` og cache-nøkkel, også på Windows | Windows-filsystemet ignorerer store/små | Lik oppførsel på alle plattformer for alt `AssetManager` selv styrer. `DirectoryAssetSource` følger filsystemet, så `maps/map1.txt` åpnes på Windows men ikke på macOS/Linux. Å håndheve store/små bokstaver på Windows krever en mappelisting per åpning; vurderes i fase 5 (Debug-advarsel) |

## 10. Faser

Hver fase er én PR med tester og dokumentasjon. `dotnet build` uten advarsler og `dotnet test` grønn.

| Fase | Innhold | Ferdig når |
|---|---|---|
| **0. Fundament** ✅ 2026-11-12 | `AssetPath`, `IAssetSource`, `DirectoryAssetSource`, `MemoryAssetSource`, `IAssetLoader<T>`, `AssetLoadContext`, `AssetManager` (register, cache, `Load`/`TryLoad`/`Enumerate`), `TextLoader`, `LinesLoader`, unntakene, `GameBase.Assets`. Tester i `ConsoleEngine.Tests/Assets/` | Et spill kan laste en tekstfil med virtuell sti uansett CWD |
| **1. Pilot: kart** ✅ 2026-11-12 | TerraForM.Ecs og TerraForM over på `Assets.Load`. Glob i `.csproj` | Pilot-kriteriet i § 8 |
| **2. Sprites og JSON** ✅ 2026-11-12 | `.sprite`-format + `SpriteLoader`, `JsonLoader<T>`. Platformer.Ecs-kart og store sprites flyttes til filer | Ingen innholds-strenger igjen i de flyttede `Assets/*.cs` |
| **3. Levetid og handles** ✅ 2026-11-12 | `AssetScope`, `Scene.Assets`, `AssetHandle<T>`, `AssetStore<T>`, `Get(handle)`. Vurder om `SpriteLibrary` skal bli en tynn fasade over `AssetStore<Sprite>`. Benchmark | Scene-bytte slipper scenens assets. `Get` 0 B allokert |
| **4. Flere kilder** | `EmbeddedAssetSource`, `CompositeAssetSource` (overstyring) | Et spill kan bygge inn assets i exe-en og la filer på disk overstyre dem |
| **5. Hot reload (Debug)** | `FileSystemWatcher` på `DirectoryAssetSource` → kø → spilltråden laster på nytt ved frame-start, oppdaterer cachen på plass (samme handle), varsler avhengige. `AssetManager.Reloaded`-event | Endre et kart i output-mappen og se det i spillet uten restart |
| **6. Async og forhåndslasting** | `LoadAsync<T>`, `Preload(paths)` med fremdrift for lasteskjerm | Bare når et spill trenger det |
| **7. Pakke-integrasjon** | `buildTransitive`-targets i NuGet-pakken (§ 6) | Et nytt spill fra NuGet får `Assets/**` kopiert uten egen `.csproj`-linje |
| **8. Analyzer** (foreslått) | `ConsoleEngine.Analyzers` med `CE0001`, attributtene, pakking og tester (§ 7a). Uavhengig av fase 2–7 | `Assets.Load` i et `OnUpdate` gir advarsel ved bygg; i `OnLoad` gir det ingenting |

## 11. Beslutningslogg

Legg til en linje per beslutning, nyeste nederst.

| Dato | Beslutning | Grunn |
|---|---|---|
| 2026-11-12 | Asset-systemet ligger i `ConsoleEngine` (`ConsoleEngine.Assets`), ikke i egen assembly | Alle spill trenger det, og `SpriteLoader` trenger `Sprite` |
| 2026-11-12 | Kilde (`IAssetSource`) og format (`IAssetLoader<T>`) er atskilt | Standard i Bevy/Godot. Loadere testes mot en `MemoryStream` |
| 2026-11-12 | Stier løses mot `AppContext.BaseDirectory/Assets`, ikke CWD | Fjerner kravet om å starte fra output-mappen |
| 2026-11-12 | Savefiler er utenfor scope | De skrives av spilleren, de er ikke innhold |
| 2026-11-12 | `AssetManager`-ctoren registrerer `TextLoader` og `LinesLoader` (`txt`) | Som Godot: innebygde formater virker uten oppsett. Begge tar egne endelser (`new LinesLoader("map")`) |
| 2026-11-12 | `string` → `AssetPath` er en implisitt konvertering som kaster ved ugyldig sti | `Assets.Load<Map>("Maps/map1.txt")` uten støy, som Bevy (`From<&str>`). Feil sti er en programmeringsfeil |
| 2026-11-12 | En loader-feil pakkes alltid i `AssetLoadException`, også når årsaken er en avhengighet; ingenting caches | Kjeden av `InnerException` viser hvilken asset som trengte hvilken. Neste `Load` prøver på nytt |
| 2026-11-12 | `Enumerate` ser bare i én mappe, ikke undermapper, og filtrerer endelsen selv | Windows' `*.txt`-mønster treffer også `.txt2`. Rekursiv listing kan legges til når et spill trenger det |
| 2026-11-12 | Fase 0 har ingen scopes: cachen lever til `AssetManager.Clear()` | Scopes og handles hører til fase 3 |
| 2026-11-12 | TerraForM (OOP) cacher linjene (`string[]`), ikke `World` | `World` og flisene endres mens nivået spilles og trenger spillet i ctoren. En asset må være uforanderlig data; tilstand bygges av den hver gang |
| 2026-11-12 | Kartrotasjonen bruker fortsatt fast antall (`Maps.Length`/`MapCount` = 4), ikke `Enumerate` | Med `Enumerate` ville map4 kommet med i rotasjonen. Det endrer spillet, og fase 1 skal ikke endre oppførsel |
| 2026-11-12 | Nytt testprosjekt `Examples/TerraForM.Ecs.Tests` i stedet for å utvide `TerraForM.Tests` | Samme mønster som `EternalDigger.Tests`; to spill-exe-er med like navnerom i ett testprosjekt blir rotete |
| 2026-11-12 | `TerraForM.csproj` kopierer `Assets\**` med `None Update` + `PreserveNewest` | `.cs`-filene under `Assets/` er `Compile`, ikke `None`, så de kopieres ikke. `Story/*.txt` (ubrukt) blir nå også kopiert |
| 2026-11-12 | Pilot-kriteriet «startet fra en annen mappe» er bevist med test, ikke i konsoll | `ShippedMaps_FromExeFolder_AllLoadWithNameAndStart` leser kartene via `AppContext.BaseDirectory` slik spillet gjør. Spillet selv er ikke kjørt (krever ekte konsoll) |
| 2026-11-12 | `SpriteLoader` er innebygd; `JsonLoader<T>` er ikke det | Sprite er en motortype. JSON trenger spillets source-genererte `JsonTypeInfo<T>` (ingen refleksjon, trygt for trimming) |
| 2026-11-12 | Nytt motor-API `Sprite.Clone()` (additivt) | TerraForMs `MenuScene` skriver spillernavnet inn i story-spriten. Med en delt, mellomlagret sprite må den skrive i en kopi |
| 2026-11-12 | Hele `Sprites.cs` i TerraForM flyttet, også ubrukte `TopMenu` | Kriteriet for fasen er at innholds-strenger ikke ligger igjen i flyttede `Assets/*.cs`. Filene ble sjekket piksel for piksel mot den gamle klassen før den ble slettet |
| 2026-11-12 | Platformer.Ecs-spritene ble også flyttet, ikke bare kartet | Da er alt innhold i spillet data, og `Assets/` har ingen `.cs` igjen |
| 2026-11-12 | Ingen JSON-bruk i eksempelspillene ennå | Ingen av dem har et format som passer JSON bedre enn tekst. `JsonLoader<T>` er testet i `ConsoleEngine.Tests` |
| 2026-11-12 | `AssetManager.Load` = manager-ens rot-scope (slippes aldri før `Clear`); `Scene.Assets` = scenens scope | `Game.Assets` er for det hele spillet deler, `Scene.Assets` for scenens eget. Fase 0–2-kode fortsetter å virke uendret |
| 2026-11-12 | `Scene.Assets` opprettes ved første bruk, disposes i `Scene.Unload` etter `OnUnload` | En scene som ikke laster noe får ikke noe scope. Siden `Set` laster ny scene før den gamle lastes ut, leses en restartet banes kart ikke på nytt |
| 2026-11-12 | `SceneManager` får `AssetManager` i en intern ctor | Tester kan ikke lage `GameBase` (Win32); `new SceneManager(null, assets)` gir scener med ekte scopes |
| 2026-11-12 | `SpriteLibrary` beholdes uendret | Brukes av alle ECS-eksemplene, og å fjerne den er en breaking change. Den er fortsatt raskest for sprites laget i kode. `AssetHandle<Sprite>` er for lastede sprites. En egen render-system for handles venter til et spill trenger det |
| 2026-11-12 | `AssetStore<T>` er offentlig, med `AssetManager.GetStore<T>()` | Målingen i § 7: oppslag via manageren er 2× `SpriteLibrary` for referansetyper, via et beholdt lager 1,3× |
| 2026-11-12 | Generasjonssjekken i `Get` er på i alle bygg | Én sammenligning; uten den ville en gammel handle stille gi en annen asset etter at slotten er gjenbrukt |
| 2026-11-12 | TerraForM.Ecs `HudSystem` laster game-over-skjermen i rot-scopet | Samme skjerm i alle baner. Viser bruken av begge scopene |

## 12. Åpne spørsmål

- **Sprite-farger:** én farge per sprite (v1-forslaget) eller et fargelag per celle? Alternativt REXPaint `.xp`, som er
  de facto standard for ASCII-kunst, men bruker CP437-tegn og RGB som må mappes til `char` og 16 `ConsoleColor`.
- ~~**`AssetHandle<T>` i komponenter**~~: avklart i fase 3. En komponent med et `AssetHandle<Sprite>`-felt virker i
  `World` (`AssetScopeTests.Handle_InEcsComponent_StoresAndReadsBack`).
- ~~**`SpriteLibrary`**~~: avklart i fase 3, beholdes uendret (§ 11).
- **Render-system for handles:** skal motoren få en `SpriteAssetRenderSystem` som tegner `AssetHandle<Sprite>`-
  komponenter, ved siden av `SpriteRenderSystem`/`SpriteRef`? Først når et spill trenger det.
- **Reserve-asset** (en synlig «MISSING»-sprite i stedet for unntak) i Release-bygg?
- **Tiled-støtte** (`.tmj`): verdt det for eksempelspillene, eller bare dokumentere hvordan et spill skriver loaderen selv?
- **Analyzer (§ 7a):** bygge den? Hvis ja: bare `Load`/`TryLoad`/`Enumerate` først, eller med en gang flere dyre
  API-er (`Sprite.FromString`, `new List<T>` i `OnRender`)? Og hvilken alvorlighet: `Warning` (feil i dette repoet)
  eller `Info`?
- **Navn:** `AssetManager` (MonoGame-aktig) eller `AssetServer` (Bevy)? Planen bruker `AssetManager` fordi det passer
  `SceneManager`.
