# Plan: GPU-rendering av 3D i ConsoleEngine

Navn: `Examples/Kartster` er konsollversjonen i dette repoet (het `KartViewer` til 2026-11-14). «Kartster» alene
betyr originalspillet (MonoGame) i `C:\Forse\kartster-server`.

Status (2026-11-14): fase 1–3 er ferdige (`ConsoleEngine.Rendering3D` med testprosjekt, Kartster tegner gjennom
`IRenderer3D`; `ConsoleEngine.Rendering3D.OpenGL` med `GlRenderer` og GPU-testene). Fase 3: Kartster bytter med G eller `--gpu` og faller tilbake til CPU. Første måling i conhost (Debug): 3D-delen 6,2 ms på CPU mot 1,5 ms på GPU.
Sant fargemodus (24-bit, § 4b) er ferdig i begge rendererne og i konsollen (`RenderConsole.TrueColor`/`DrawRgb`, på
Windows VT-sekvenser): Kartster bruker det med `--truecolor` eller C. I conhost er en jevn 24-bit-frame like rask som
palettet, men mange fargeskift per rad (dithering, støy) koster 5–7 ganger så mye (tall i `AGENTS.md` § Performance).
Lys (§ 4c) er ferdig i begge rendererne: ambient + sol som kan settes, og opptil 16 punkt- og spotlys per piksel.

## 1. Mål og ikke-mål

**Mål**

- 3D-scener kan tegnes på GPU-en med **OpenGL via Silk.NET**, samme API som Kartster (MonoGame DesktopGL). Resultatet
  er det samme som fra CPU-rasterizeren: én konsollfarge per piksel, med samme lys, dithering, palett,
  screen-door-transparens og near-plane-klipping.
- 3D-koden blir et **valgfritt bibliotek i motoren**, som `ConsoleEngine.Physics`: spill som ikke tegner 3D, får ingen
  ny avhengighet. GPU-delen ligger i et eget bibliotek med de native avhengighetene, så CPU-rendering ikke drar med seg
  OpenGL.
- CPU og GPU bak samme grensesnitt. Spillet velger, og faller tilbake til CPU hvis OpenGL ikke er tilgjengelig.
- Samme regler som resten av motoren: ingen allokering per frame, `TreatWarningsAsErrors`, tester og docs i samme
  commit.

**Ikke-mål (nå)**

- Teksturer, skygger fra lys og etterbehandling. Vi gjør det samme som CPU-rasterizeren gjør (per-piksel-lys kom
  senere, § 4c).
- Å tegne rett til skjermen med GPU-en. Konsollen er fortsatt utgangen: GPU-en rendrer til et bilde utenfor skjermen,
  og bildet leses tilbake.
- Kryssplattform-test. Koden bør virke på macOS og Linux (OpenGL 3.3), men vi verifiserer bare Windows.

## 2. Forventet gevinst

Målt i dag (320×180): cirka 2 ms per frame til 3D-tegning på CPU, og cirka 11 ms per frame totalt (93 FPS). Det meste
går til konsoll-skrivingen. **GPU-en gjør ikke Kartster merkbart raskere i dag.** Den gjør det mulig med mye tyngre
scener (mange karter, trær) og høyere oppløsning uten at CPU-tiden vokser. Fase 3 måler det.

## 3. Arkitektur

| Prosjekt | Innhold | Avhengigheter |
|---|---|---|
| `ConsoleEngine.Rendering3D` (nytt, valgfritt) | `Mesh`, `ObjFile`, `ConsolePalette`, `IRenderer3D`, `Rasterizer` (CPU), flyttet fra `Examples/Kartster/Rendering/` | Bare .NET (`System.Numerics`) |
| `ConsoleEngine.Rendering3D.OpenGL` (nytt, valgfritt) | `GlRenderer : IRenderer3D` | `ConsoleEngine.Rendering3D`, Silk.NET.OpenGL, Silk.NET.Windowing (skjult GLFW-vindu for kontekst) |
| `ConsoleEngine.Rendering3D.Tests` (nytt) | Rasterizer- og palett-testene flyttet fra Kartster.Tests, pluss GPU-mot-CPU-tester | Begge |

**Grensesnittet** (forslag, finjusteres i fase 1):

```csharp
public interface IRenderer3D : IDisposable
{
    int Width { get; }
    int Height { get; }
    ConsoleColor[] Colors { get; }                         // resultatet, index = y * Width + x
    void Clear(ReadOnlySpan<ConsoleColor> background);    // starter en frame
    void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight);
    void Finish();                                         // resultatet er klart i Colors (GPU: tilbakelesing)
}
```

`Mesh` får en `Version` som spillkoden øker når den har endret arrayene (`MarkChanged()`). GPU-en laster opp en mesh
én gang og på nytt bare når versjonen har endret seg (bremsespor, røyk). CPU-en bryr seg ikke.

**Slik ble fase 1:** grensesnittet er som over. `ConsoleEngine.Rendering3D` pakkes som NuGet (`GeneratePackageOnBuild`,
0.2.0) akkurat som `ConsoleEngine.Physics`, og ligger i solution-mappa `Engine`. Testprosjektet ligger i roten av
solution, som `ConsoleEngine.Physics.Tests`. Det fantes ingen egne palett-tester; `ScreenDoorTests` ble flyttet
sammen med `RasterizerTests` og OBJ-testene, mens `Load_ShippedKart_SkipsMarkerParts` ble i Kartster.Tests
(`KartModelTests`), fordi den trenger kart-filene. `SkidMarks` og `SlideSmoke.BuildQuads` kaller `MarkChanged()`.

## 4. Slik tegner GPU-en

- **Kontekst:** OpenGL 3.3 core i et skjult GLFW-vindu (Silk.NET.Windowing, `IsVisible = false`). Lager vi ikke
  konteksten, kaster konstruktøren, og spillet faller tilbake til CPU.
- **Mål:** et framebuffer-objekt på `Width × Height` med én `R8UI`-fargebuffer (palettindeks) og en 24-bits
  dybdebuffer.
- **Mesher:** flat skyggelegging trenger egenskaper per trekant (normal, farge, gjennomsiktighet, ubelyst). Hver mesh
  lastes derfor opp ekspandert, med tre hjørner per trekant som alle bærer trekantens egenskaper (`flat`-attributter
  i GLSL).
- **Hjørneshader:** `clip = worldViewProjection · position`. `System.Numerics`-projeksjoner gir z i [0, 1] (D3D-stil),
  mens OpenGL klipper z i [-w, w]. Shaderen regner om med `z' = 2z - w`, så near- og far-planet blir de samme.
  OpenGL klipper mot near-planet selv.
- **Fragmentshader:** lys = ambient 0,35 + diffus 0,65 (som CPU-en), Bayer 4×4-dither, nærmeste av de 16
  palettfargene (en uniform), og `discard` for screen-door når Bayer-terskelen ≥ gjennomsiktigheten. Ut kommer
  palettindeksen.
- **Bakgrunn:** fargebufferen tømmes til 255 («ingen geometri»). Etter tilbakelesing erstattes 255 med bakgrunnen fra
  `Clear`. Himmelgradienten blir dermed lik på CPU og GPU uten noen ekstra tegning.
- **Tilbakelesing:** `glReadPixels` til en forhåndsallokert `byte[]` (57,6 KB i 320×180), radene snus (OpenGL har
  origo nede til venstre), og verdiene blir til `ConsoleColor`. Én synkron tilbakelesing per frame. Pixel buffer
  objects (asynkront) er et senere steg om målingen viser behov.
- **Bakflate-culling:** `GL_CULL_FACE` med samme vinding som CPU-en (mot klokka sett utenfra er forsiden).

**Slik ble fase 2:** `GlRenderer(width, height, palette)` kaster `InvalidOperationException("OpenGL 3.3 is not
available: …")`, og `GlRenderer.TryCreate` gir `false` i stedet. Avvik fra planen over:

- **Palett:** i stedet for «nærmeste av de 16 farger» i shaderen laster vi opp `ConsolePalette` sin egen 32³-tabell
  (3D-tekstur `R8UI`) og dither-forskyvningene (uniform-array), og regner samme celle som `ToConsoleColor`. Da blir
  GPU-en lik CPU-en også der to farger er like nære. `ConsolePalette` fikk `internal` `Nearest`, `Dither` og
  `LookupSize` (`InternalsVisibleTo` for OpenGL-prosjektet).
- **Lys i hjørneshaderen**, ikke fragmentshaderen: fargen er flat per trekant, så den regnes én gang per hjørne og
  sendes som `flat`.
- **Ubelyst** er en uniform per mesh (`Mesh.IsUnlit` gjelder hele meshen), ikke et hjørne-attributt.
- **Bayer-cella** telles fra øverste venstre hjørne (`y = height − 1 − gl_FragCoord.y`), så mønstrene treffer CPU-en.
- **Mesher** blir liggende på GPU-en til `Dispose` (ingen utkastelse). Endringer krever `MarkChanged()`.
- **Tråd:** konteksten hører til tråden som lagde rendereren; `Clear` gjør konteksten gjeldende igjen hvis en annen
  renderer på samme tråd har tatt den.
- **Målt (Intel Arc, Release, 320×180):** 3 201 trekanter, `Clear` + 2 `Draw` + `Finish` 0,43 ms per frame, 0 byte
  allokert. Testscenene (64×64) er 100 % like CPU-en.

## 4b. Sant fargemodus (24-bit)

`IRenderer3D.TrueColor` velges i konstruktøren (`trueColor: true` på `Rasterizer`, `GlRenderer` og `GlRenderer.TryCreate`). Da er bildet `Rgb` (`0xRRGGBB` per piksel), `Colors` er tom, og en frame starter med `Clear(ReadOnlySpan<uint>)`. Feil `Clear` for modusen kaster `InvalidOperationException`. Palettmodus er uendret.

- **Farge:** samme lys som før, ingen dither, ingen palett: `clamp(farge, 0, 1)` × 255, avrundet.
- **Gjennomsiktighet:** ekte alfablanding, `dst = src · o + dst · (1 − o)`. Blandede trekanter dybdetestes, men skriver ikke dybde. Per mesh tegnes de ugjennomsiktige trekantene først og de gjennomsiktige etterpå, på begge rendererne, så rekkefølgen inne i en mesh ikke spiller noen rolle. Mellom mesher gjelder tegnerekkefølgen: en fjernere ugjennomsiktig mesh som tegnes senere, dekker blandingen. Tegn gjennomsiktige mesher sist.
- **CPU:** en `Vector3`-buffer (0..1) som pakkes til `Rgb` i `Finish`.
- **GPU:** egen fragmentshader (utdata `vec4` i stedet for `uint`, så det må være et eget program; bare ett kompileres per renderer) og et `RGBA8`-framebuffer. Bakgrunnen lastes opp til en tekstur i `Clear` og blittes inn (radene snus), så blandingen ser den ekte bakgrunnen. Et «ingen geometri»-merke i alfa hadde ikke virket: en blandet piksel over tom bakgrunn ville fått feil farge. Gjennomsiktige trekanter tegnes i et andre `DrawArrays` med `SRC_ALPHA, ONE_MINUS_SRC_ALPHA` og `DepthMask(false)` (bare mesher med `TriangleOpacity`). `Finish` leser `BGRA`/`UnsignedInt8888Rev` til en `uint[]`, snur radene og fjerner alfa.
- **Målt (Intel Arc, Release, 320×180, 3 202 trekanter, én gjennomsiktig quad):** GPU 0,74 ms per frame (palettmodus 0,36 ms: fire ganger så mye å lese tilbake og en blit), CPU 1,2 ms. 0 byte allokert på begge. Testscenene (64×64) er innenfor 1 per kanal på 100 % av pikslene.

## 4c. Lys

`IRenderer3D.Lighting` (en `SceneLighting`, null = lyset fra før: ambient 0,35 og sol 0,65, hvitt) gjelder de neste
`Draw`-kallene. Begge `Clear` setter den tilbake til null, så den settes etter `Clear` hver frame. Uten lys er bildet
byte for byte det samme som før (sjekket mot den gamle `Rasterizer` på 100 Kartster-frames i begge fargemodusene, og
like raskt). Medlemmet har en standardimplementasjon i grensesnittet (gjør ingenting), så eldre renderere kompilerer.

- **Modell:** flat `albedo · (Ambient + SunColor · max(0, n·toLight))` per trekant som før (`toLight` er fortsatt
  argumentet til `Draw`), pluss per piksel `albedo · Σ lys` for opptil `SceneLighting.MaxLights` = 16 `PointLight`
  (posisjon, farge × styrke, radius, retning, kjeglecosinus). Hvert lys gir
  `farge · (1 − d²/r²)² · clamp(n·l) · kjegle`,
  kjegle = 1 for punktlys (`ConeCosine` < −0,5), ellers `smoothstep(cos, cos + 0,08, …)`: Kartsters `PointLightPS`.
  Ubelyste mesher bryr seg ikke. Farger over 1 kuttes per piksel i 24-bit; i palettmodus ditheres den belyste fargen
  per piksel som før.
- **Per piksel, ikke per hjørne:** trekantene er flate og store nær kameraet (veien), så en lyskjegle per hjørne ville
  ikke synes. CPU-en regner verdensposisjonen per piksel perspektivriktig (verden/w og 1/w interpolert).
- **CPU-kostnad holdes nede:** et lys tas bare med på trekanter det kan nå (foran trekantens plan, og kulen rundt lyset,
  `SceneLighting.Bounds`, smalere for spotlys, rører trekantens kule), og bare i 8×8-pikselfliser som kulen dekker på
  skjermen (bygges på nytt når lysene eller kameraet endres). En piksel utenfor kjeglen avvises før kvadratroten.
- **GPU:** samme formel i begge fragmentshaderne (`LightsShader`), hjørneshaderen sender albedo, normal og
  verdensposisjon videre. Lysene lastes opp som uniform-arrayer på hvert `Draw` (antall 0 for ubelyste mesher).
- **Likhet:** GPU-mot-CPU-testene med punktlys, spotlys og fargede lys (`LitScenes`) er 100 % like i palettmodus og
  innenfor 1 per kanal i 24-bit.
- **Målt (Release, 320×180, Star Circuit med seks karter på rad, alle i hverandres lyskjegler: et verste tilfelle):**
  CPU 1,6 ms uten lys, 4,5–5 ms om natten med 16 lys (12 frontlys over hele veien); GPU 0,55 → 0,6 ms (palett) og
  0,7 → 0,8 ms (24-bit). 0 byte allokert. Målingen er `KartLightsTests.Frame_OnStarCircuit_ReportsTheRenderTimes`
  (`[Explicit]`, kjør i Release).

## 5. Testing

- Tester som trenger OpenGL er merket `[Category("Gpu")]` og hopper over seg selv (`Assert.Ignore`) når konteksten
  ikke kan lages. GitHub sin `windows-latest` har ikke brukbar OpenGL 3.3 uten GPU, så der hoppes de over. Det står i
  CI-oppsummeringen.
- **GPU mot CPU:** de samme testscenene (trekanter, overlapp, gulv som krysser near-planet, gjennomsiktig quad,
  ubelyst mesh) rendres på begge, og minst 97 % av pikslene skal være like. Forskjeller kommer bare fra kantpiksler
  (ulike rasteriseringsregler).
- CPU-testene flyttes uendret og kjører alltid.
- **Slik ble det:** eget testprosjekt `ConsoleEngine.Rendering3D.OpenGL.Tests`, så CPU-testprosjektet ikke får de
  native avhengighetene. Testene hopper også over seg selv når OS-et ikke er Windows (GLFW vil ha hovedtråden på
  macOS, og NUnit kjører ikke tester der). CI-matrisen (Windows, macOS, Ubuntu) har ingen GPU, så der er de alltid
  ignorert.

## 6. Faser

| Fase | Innhold | Hvem |
|---|---|---|
| 1 | `ConsoleEngine.Rendering3D` + testprosjekt. 3D-koden flyttes fra Kartster, `IRenderer3D`, `Mesh.Version`. Kartster bruker biblioteket, uten endret oppførsel | Agent |
| 2 | `ConsoleEngine.Rendering3D.OpenGL` med `GlRenderer` og GPU-testene | Agent (etter fase 1) |
| 3 | Kartster: `G` bytter CPU/GPU, `--gpu`-argument, rendereren står i tittelen, fallback til CPU. Benchmark av CPU mot GPU i `ConsoleEngine.Benchmarks` | Prosjektleder |

## 7. Åpne spørsmål og risiko

- **Native avhengighet:** Silk.NET.Windowing trenger `glfw3.dll`, som pakkene leverer per plattform. Den må komme med
  i output-mappen til spillene som bruker GPU-en.
- **Driverforskjeller:** noen Intel-drivere har feil med `R8UI`-rendermål. Faller vi over det, er `RGBA8` med indeksen
  i rødkanalen en reserve.
- **GPU-tester i CI:** de kjører ikke i CI (§ 5), bare lokalt. Mesa på Windows (llvmpipe) kunne gitt CI-dekning, men
  det er en egen oppgave.
