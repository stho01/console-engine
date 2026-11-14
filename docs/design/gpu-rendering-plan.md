# Plan: GPU-rendering av 3D i ConsoleEngine

Navn: `Examples/Kartster` er konsollversjonen i dette repoet (het `KartViewer` til 2026-11-14). «Kartster» alene
betyr originalspillet (MonoGame) i `C:\Forse\kartster-server`.

Status (2026-11-14): fase 1 er ferdig (`ConsoleEngine.Rendering3D` og testprosjektet, Kartster tegner gjennom
`IRenderer3D`). Fase 2 og 3 er ikke startet.

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

- Teksturer, per-piksel-lys, skygger og etterbehandling. Vi gjør det samme som CPU-rasterizeren gjør i dag.
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

## 5. Testing

- Tester som trenger OpenGL er merket `[Category("Gpu")]` og hopper over seg selv (`Assert.Ignore`) når konteksten
  ikke kan lages. GitHub sin `windows-latest` har ikke brukbar OpenGL 3.3 uten GPU, så der hoppes de over. Det står i
  CI-oppsummeringen.
- **GPU mot CPU:** de samme testscenene (trekanter, overlapp, gulv som krysser near-planet, gjennomsiktig quad,
  ubelyst mesh) rendres på begge, og minst 97 % av pikslene skal være like. Forskjeller kommer bare fra kantpiksler
  (ulike rasteriseringsregler).
- CPU-testene flyttes uendret og kjører alltid.

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
