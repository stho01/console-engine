# Plan: Kartster-lik kart-fysikk i KartViewer

Status (2026-11-14): steg 3 (kart-modellen, § 6) finnes som ren planmodell i `Examples/KartViewer/Driving/`, med
tester, men er ikke koblet inn i spillet ennå. Dagens `Examples/KartViewer/KartDriving.cs` er fortsatt en leke-modell
(fast akselerasjon, styring som roterer kursen direkte) og skal erstattes.

Kilde for alt om originalen: `C:\Forse\kartster-server` (Kartster, MonoGame). Stier som begynner med `Kartster/` under
er relative til `src/Kartster/` der. Linjenumre er fra 2026-11-14 og kan ha flyttet seg.

## 1. Mål og ikke-mål

**Mål**

- Karten i KartViewer skal kjøre som i Kartster på flat bakke: samme toppfart, akselerasjon, bremselengde,
  svingradius, skrens og håndbrekk-drift, innenfor en toleranse vi måler (§ 7).
- Vi skriver vår egen modell etter Kartsters formler og tuning, ikke kopierer koden. Feltnavn og enheter følger
  Kartsters `RigidBodyKartTuning`, så tall kan flyttes over én til én.
- Det som er generelt (krefter og moment på en kropp) går inn i `ConsoleEngine.Physics` og følger industristandard
  (Box2D v3 først, så Unity/Godot). Det som er bilspesifikt blir i spillet.
- Samme regler som resten av motoren: 0 allokeringer per steg, fast tidssteg, tester i `Method_State_Expected`-stil,
  docs i samme commit.

**Ikke-mål (nå)**

- 3D-fysikk: hopp, bakketopper, doserte svinger, fly, glide, stuping, velting, enhjulinger (`KartFlight`, `KartGlide`,
  `KartUnicycle`, `KartPitch`). Banen er flat.
- Vegger og kollisjoner mellom karter. Planen gjør dem enkle å legge til senere (§ 9), men de er ikke med nå.
- Power-ups, boost, turbo, downforce (bare McLuring bruker det), bremse-lås (`CanLockWheels`).
- Bit-for-bit likhet med Kartster. Vi sikter på samme oppførsel målt i tall, ikke samme flyttall.
- Å kjøre Kartsters egen kode (`RaceSimulation`). Det ble vurdert og valgt bort: det krever en referanse på tvers av
  repoene og kolliderer med vår kopi av `Microsoft.Xna.Framework`.

## 2. Utgangspunkt

| Hva | Status |
|---|---|
| `ConsoleEngine.Physics` | 2D rigid body-motor i Box2D v3-stil: substeg, Soft Step-kontaktløser, rotasjon, demping, sleeping, boks-collider med riktig treghetsmoment |
| Krefter på en kropp | **Mangler.** Spillkode kan bare sette `Velocity`/`AngularVelocity` direkte |
| KartViewer | `GameBase`-spill uten ECS. Banemodus kjører `KartDriving` |
| Kartster | Rigid body på 120 Hz, dekkrefter per aksel, motor med effekt og girkasse, vektoverføring, underlag |

Bil på flat bakke er et 2D-problem sett ovenfra. Det passer motoren: verdens (x, z) blir fysikkens (x, y).
Treghetsmomentet til en boks-collider er m·(b² + l²)/12, samme formel som Kartster bruker
(`Kartster/Models/Karts/Physics/KartRigidBodyFactory.cs`).

## 3. Slik kjører Kartster (det vi skal gjenskape)

Fast steg dt = 1/120 s, maks 8 steg per frame (`Kartster/Physics/Simulation/PhysicsTuning.cs`). Alle krefter
samles i akkumulatorer og integreres med semi-implisitt Euler (`Kartster/Physics/Dynamics/RigidBodyIntegrator.cs`).
Krefter «i et punkt» virker med armen `aksel - tyngdepunkt`.

### 3a. Rekkefølgen i ett steg (`ArcadeKartPhysics.Update`, linje 147–407)

1. Input: `throttle = (Throttle - Brake)` i [-1, 1], `steer` i [-1, 1] (positiv = høyre). Håndbrekk er en bool.
2. Akseltrykk fra **forrige stegs** målte akselerasjon (vektoverføring, § 3e).
3. Girkasse oppdateres med drevet hjulfart (§ 3c).
4. Styrevinkel δ (§ 3d) og forhjulenes retning.
5. Lengdekrefter: revers, drivkraft og brems per aksel, i hjulets retning.
6. Rullemotstand og luftmotstand langs kartens retning.
7. Sidekrefter per aksel (dekkmodellen, § 3b).
8. Gir-demping (yaw) når karten sklir.
9. Vinkeldemping: `ω *= max(0, 1 - AngularDamping·dt)`.
10. Integrer, og lagre `a = (v - v0)/dt` til neste stegs vektoverføring.

### 3b. Dekk: sidefarten kanselleres, begrenset av friksjon

Ingen slip-vinkelkurve (Pacejka). Hver aksel ber om akkurat den kraften som nuller ut sin sidefart dette steget, og
får den begrenset av friksjonen (`ArcadeKartPhysics.cs` linje 923, 983):

```
vlat  = (v + ω × arm) · hjulHøyre
mEff  = 1 / (1/m + ((I⁻¹(arm × dir)) × arm) · dir)
Fdem  = -vlat · mEff / dt
F     = clamp(Fdem, ±μ · N_aksel)
slip  = |vlat + F·dt/mEff|
```

- Foran: `μf = gripScale · gripShare_f · TyreFriction · (1 - LateralLoadSensitivity · |NL - NR| / N)`.
- Bak: samme, ganger `RearGripScale` og `slidingGrip`, der
  `slidingGrip = lerp(1, RearSlidingGripScale, clamp((α_r - 12°) / 8°))` og
  `α_r = atan2(|vlat_r|, max(|vlong_r|, 1))`.
- Friksjonssirkel mot lengdekraften (`KartTraction.GetSidewaysGripShare`):
  `share = max(sqrt(1 - (|Flong| / Fmax)²), 0.22)`.
- Karten sklir når en aksel har slip > 0,1 m/s (`KartAxleSlip.cs`). Da virker gir-demping
  `τ = -ω · min(rate, 1/dt) / I⁻¹`, med `rate` = `SlideYawDamping`, eller `HandbrakeYawDamping` med håndbrekk.
- Håndbrekk låser bakakselen: friksjon `TyreFriction × 0.25`, og motorkraften halveres.
- Caster: `δ += clamp(CasterShare · atan2(vlat_r, vlong_r), ±45°) · fade`, der `fade` går fra 0 til 1 når bakakselens
  lengdefart går fra 3 til 6 m/s.

### 3c. Motor, girkasse, brems, revers, motstand

- Motor (`KartEngine.cs`):
  `ratio = gir[i] · FinalDrive / WheelRadius`,
  `rpm = max(|v_hjul| · ratio · 30/π, IdleRpm)`,
  `T = P / (max(rpm, TorquePeakRpm) · π/30) · clamp((Redline - rpm) / (0.08 · Redline), 0, 1)`,
  `Fdrive = throttle · T · ratio`. Altså konstant kraft under dreiemomenttoppen og P/v over den.
  Motorbrems uten gass: `0.05 · Tpeak · ratio · turtall01`. Ingen drivkraft under giring (0,05 s).
- Drevet aksel (`KartTraction.Step`): hjulet har egen fart (hjulmasse 20 kg), `Fdem = Fdrive + spinn·20/dt`, begrenset
  til `±TractionFriction · N_aksel`. Hjulspinn bygger seg opp når grensen nås. Firehjulstrekk deler 50/50.
- Girkasse (`KartGearbox.cs`): giret opp når neste gir gir > 1,02 × kraften, ned når lavere gir har rpm < 0,85 ·
  Redline og > 1,1 × kraften. Ikke under hjulspinn (slip > 1,5 m/s).
- Brems (`KartTraction.Brake`): `F = -sign(v) · min(Brake · BrakeForce · andel, Fmax · gripUse)`, der
  `gripUse = lerp(0.9, 0.5, min(|steer| / 0.5, 1))`. Bremsing er når `|v_fwd| > 0.1` og fortegnet på throttle ≠ fartens.
- Revers: `Fmax = -throttle · P / 8`, `F = clamp((8.33 + v_fwd) · m/dt, ±Fmax)`. Maks 30 km/t.
- Motstand: `F = -v_fwd · RR · m - sign · Cd · v_fwd²`, bare langs kartens retning (sidefarten tar dekkene).
  Ingen hard toppfart: den er der `P = (RR·m·v + Cd·v²) · v`.

### 3d. Styring

Sykkelmodell med ekte akselposisjoner. Svingingen kommer bare fra dekkreftene, aldri et direkte moment, og det er
ingen utjevning av styringen i fysikken.

```
lock = lerp(LowSpeedSteerAngle, MaxSteerAngle · lerp(1, HighSpeedSteerScale, f2), f1)
f1 = clamp((|v| - 5) / 10),  f2 = clamp((|v| - 15) / 15)
δ  = lock · steer
```

Bakhjulene peker alltid rett fram.

### 3e. Vektoverføring

`KartLoadTransfer.cs`, med h = RideHeight (0,5) + tyngdepunktets høyde:

- Statisk fordeling etter tyngdepunktets plass mellom akslene.
- `ΔN_lengde = m · a_fwd · h / L · LongitudinalLoadTransferScale`, begrenset og bevart.
- Per aksel: `ΔN_side = (N_aksel / g) · a_lat · h / sporvidde · LateralLoadTransferScale`.
- Virker på grepet og trekkgrensen per aksel, og på straffen for skjev last venstre/høyre.

### 3f. Underlag

Tuningen ganges med underlaget før hvert steg (`Kartster/Models/World/PowerUps/KartEffectTuning.cs`, verdier i
`Kartster/Models/Tracks/Surfaces/SurfacePalette.cs`):

| Underlag | Grep (× `TyreFriction`, `TractionFriction`) | Motstand (× `RollingResistance`) | Toppfart (andel) |
|---|---|---|---|
| Asfalt | 1 | 1 | – |
| Kantstein | 0,85 | 1 | – |
| Grus | 0,7 | 3 | 0,4 |
| Gress | 0,6 | 2 | 0,5 |

Toppfartsandelen begrenser motoreffekten til det som trengs for `toppfart · andel`.

## 4. Steg 1: krefter i `ConsoleEngine.Physics`

Industristandard: Box2D v3 (`b2Body_ApplyForce`, `b2Body_ApplyForceToCenter`, `b2Body_ApplyTorque`), Unity
(`Rigidbody2D.AddForce`, `AddForceAtPosition`, `AddTorque`) og Godot (`apply_force`, `apply_torque`). Alle har samme
modell: krefter samles mellom to steg, holdes konstante gjennom steget og nullstilles etterpå.

**API (forslag)**

- Ny komponent `Force` (unmanaged): `Vector2 Value` (newton, verdens akser) og `float Torque` (N·m, med klokka som
  `Rotation`).
- Hjelpere på `Force`: `Add(Vector2 force)`, `AddTorque(float torque)` og `AddAtPoint(Vector2 force, Vector2 point,
  Vector2 center)`, der moment = (point - center) × force.
- `PhysicsSystem` legger til `Force · InverseMass` i fartsendringen og `Torque · InverseInertia` i vinkelfarten i
  hvert substeg (der tyngdekraften legges til i dag, `ContactSolver.IntegrateVelocities` og
  `IntegrateBodiesWithoutCollider`), og nullstiller `Force` etter steget.
- Gjelder bare dynamiske kropper. En kraft på en sovende kropp vekker den (som Box2D med `wake = true`).

**Rekkefølge:** spillets kraft-system kjører i `FixedSimulation` før `PhysicsSystem` (`[UpdateBefore]`).

**Tester:** konstant kraft gir `a = F/m`; moment gir `α = τ/I`; kraft i et punkt gir både fart og spinn; kraften
nullstilles etter steget; sovende kropp vekkes; ingen allokering.

**Åpent spørsmål:** Kartster regner dekkreftene på nytt hvert 1/120 s. Vår motor holder kraften konstant gjennom
alle substeg. Med 120 Hz fast steg og 1 substeg (eller dekkene regnet per steg med flere substeg) blir det det samme.
Skal vi trenge å regne krefter per substeg, er det et avvik fra Box2D og må begrunnes. Foreslått: start med 120 Hz og
mål.

## 5. Steg 2: KartViewer over på ECS

- Banemodus blir en `EcsScene` med egen `World`, `FixedSimulation`-gruppe på 120 Hz og `PhysicsSystem` uten
  tyngdekraft.
- Karten blir en entitet: `Position`, `Rotation`, `Velocity`, `AngularVelocity`, `RigidBody.Dynamic(Mass)`, `Collider`
  (boks med kartens chassismål), `Force` og en `KartState`/`KartTuning`-komponent.
- Akser: verdens x = fysikkens x, verdens z = fysikkens y. Kartsters forover er -z, altså -y i fysikken. Omregningen
  ligger på ett sted.
- Rasterizer, kamera og palett er uendret. Garasjen kan forbli som i dag.
- `Interpolation` på kartkroppen, så tegningen er jevn mellom fysikkstegene.

## 6. Steg 3: kart-modellen

**Status (2026-11-14):** modellen er bygget som ren C# uten ECS og uten `ConsoleEngine.Physics`, i
`Examples/KartViewer/Driving/` (namespace `KartViewer.Driving`). `KartDynamics.ComputeForces(in KartBody, in
KartControls, Surface, dt)` gir `KartForces` (netto kraft i tyngdepunktet + girmoment), og `KartIntegrator.Step` er
semi-implisitt Euler. Avvik fra tabellen under står i § 10. Testene er `Examples/KartViewer.Tests/Driving/
KartDynamicsTests.cs`. Gjenstår: `KartPhysicsSystem`/`KartInputSystem` (skrive `KartForces` til `Force`, steg 1–2)
og å fjerne `KartDriving`.

Målt med `KartIntegrator` på 1/120 s, full gass på flat asfalt (Joyride):

| Kart | Toppfart asfalt | Toppfart gress | 0–50 km/t | 0–80 km/t | Brems 80 km/t → 0 |
|---|---|---|---|---|---|
| Kart | 32,00 m/s | 16,00 m/s | 0,66 s | 1,68 s | 0,82 s, 8,8 m |
| olabil | 32,00 m/s | 16,00 m/s | 0,66 s | 1,68 s | 0,82 s, 8,8 m |
| wagon | 32,21 m/s | 16,11 m/s | 0,68 s | 1,87 s | 0,70 s, 8,0 m |

Et `KartPhysicsSystem : EntitySystem` i spillet (ikke i motoren), som skriver til `Force` hvert steg etter § 3. Filer
(forslag):

| Fil | Innhold |
|---|---|
| `Driving/KartTuning.cs` | Feltene vi trenger fra `RigidBodyKartTuning`, med Kartsters navn, enheter og standardverdier |
| `Driving/KartCatalog.cs` | Tuning for Kart (standard go-kart), olabil og wagon, pluss standard fartsklasse |
| `Driving/KartEngine.cs` | Dreiemoment, turtall og girkasse (§ 3c) |
| `Driving/AxleGrip.cs` | Dekkmodellen per aksel (§ 3b) |
| `Driving/LoadTransfer.cs` | Akseltrykk (§ 3e) |
| `Driving/Surface.cs` | Asfalt, kantstein, grus og gress (§ 3f) |
| `Systems/KartPhysicsSystem.cs` | Steget i § 3a |
| `Systems/KartInputSystem.cs` | Taster til throttle/brake/steer/håndbrekk |

Tuning å hente fra `Kartster/Models/Karts/KartCatalog.cs` (rundt linje 693, 1226, 1378) og `SpeedClasses.cs`:

| Kart | Viktigste verdier |
|---|---|
| Standard (alle felt) | Mass 200 kg, EnginePowerWatts 32 000, TorquePeakRpm 1 600, Redline 12 000, ett gir, FinalDrive 4,19, hjulradius 0,2, bakhjulstrekk, TractionFriction 5, TyreFriction 1,9, BrakeForce 5 000 N (0,7 foran), AerodynamicDrag 0,62, RollingResistance 0,1, SlideYawDamping 2, AngularDamping 0,3, MaxSteer 30°, LowSpeedSteer 45°. Chassis 1,6 × 2,8 m, aksler ±1,0 m, sporvidde 1,6 |
| Kart (go-kart) | Standard, men BrakeFrontShare 0,4, HandbrakeYawDamping 4 |
| olabil | Som go-kart, men MaxSteer 45°, sporvidde 1,05, LateralLoadTransferScale 1,05/1,6 |
| wagon | Mass 300, P 48 000, RR 0,13, firehjulstrekk, seks gir [1, .77, .59, .455, .35, .25], FinalDrive 9,16, TorquePeak 3 500, Redline 7 000, Traction 3, Tyre 1,8, Brake 9 000, HandbrakeYawDamping 3, LowSpeedSteer 38°, MaxSteer 25° |
| Fartsklasse (standard «Joyride») | Effekt × 1,275, dekk/trekk/brems × 1,2. Gir cirka 32 m/s toppfart for standardkarten |

Tastene blir som i Kartster: gass, brems/revers, styring, håndbrekk (Space i banemodus).

## 7. Steg 4: sammenligning med originalen

Kartster måler hva hver kart klarer med `Kartster.BotBench envelope --karts olabil,wagon` (se
`Docs/race-simulation.md` der). Vi kjører den én gang, skriver tallene inn i testene og sjekker modellen vår mot dem:

| Mål | Toleranse (forslag) |
|---|---|
| Toppfart på asfalt | ±3 % |
| 0–50 km/t og 0–80 km/t | ±10 % |
| Bremselengde fra 80 km/t | ±10 % |
| Svingradius ved full lås, 20 km/t og 50 km/t | ±10 % |
| Toppfart på gress | ±5 % |

Testene kjører modellen uten konsoll, som `KartDrivingTests` i dag. Hvis `envelope` ikke gir alle målene, må vi enten
legge til målinger i BotBench (endring i kartster-repoet) eller regne referansen fra formlene.

## 8. Leveranser

| Steg | Innhold | Commit |
|---|---|---|
| 1 | `Force` i `ConsoleEngine.Physics`, tester, docs (`03-rules` § Physics, `physics-plan.md`) | `feat(physics): …` |
| 2 | KartViewer banemodus på ECS og `PhysicsSystem`, foreløpig med dagens enkle modell over krefter | `refactor: …` |
| 3 | Kart-modellen og tuning for de tre kartene, `KartDriving` fjernes | `feat: …` |
| 4 | Referansetester mot `BotBench envelope` | `test: …` |

Hvert steg bygges, testes og prøvekjøres for seg.

## 9. Senere (ikke i denne planen)

- Vegger fra ribbon-eksporten (`*_wall_*`-objektene) som statiske kropper: kontaktløseren tar dem.
- Flere karter (bots) som kolliderer med hverandre.
- Kantstein og grus fra Kartsters underlagskart (`SurfaceMap`), ikke bare vei/gress.
- Om modellen viser seg nyttig for flere spill: flytte de generelle delene (aksel, dekk, motor) til en valgfri
  assembly, som Unitys `WheelCollider` eller Godots `VehicleBody`.

## 10. Avvik og åpne spørsmål

- **Dekkmodell:** Kartsters «kanseller sidefarten, begrens med friksjon» er ikke den vanlige slip-vinkelmodellen
  (Pacejka/lineær med metning). Vi følger Kartster, fordi målet er Kartsters følelse.
- **Krefter per substeg** (§ 4): avklares med måling.
- **Hjulfart som egen tilstand** (hjulspinn): trengs for trekkgrensen og girkassen. Lagres i kartens komponent, ikke i
  motoren.
- **Hvor mye av vektoverføringen** som trengs for å treffe tallene i § 7: start med lengde og side, uten
  karosseri-rulling.

**Avvik i steg 3 slik det er bygget (`Examples/KartViewer/Driving/`):**

- **Filer:** ingen `AxleGrip.cs`. Dekkmodellen er `KartDynamics.ApplyTyre`/`ApplySteering`, lengdekreftene per aksel
  er `KartTraction` (Kartsters navn), motor, turtall og girvalg er `KartEngine`, og `KartTuning` har også
  chassisgeometrien (`KartChassisGeometry`: boksmål, akselavstander, sporvidde). `KartBody`, `KartControls` og
  `KartForces` ligger i `KartBody.cs`.
- **Alt som endrer fart er kraft eller moment.** Kartsters vinkeldemping `ω *= k` blir momentet
  `ω·(k − 1)·I/dt`, som gir nøyaktig samme ω etter et semi-implisitt Euler-steg. Alle krefter regnes fra tilstanden
  ved stegets start, som i Kartster (akkumulatorer), så steget er Kartsters.
- **Planart:** armene er vannrette. Wagonens tyngdepunkt 0,1 m ned (`CenterOfMassOffset`) senker bare høyden i
  vektoverføringen. Kartster har også den loddrette armen med i effektiv masse (rulle-treghet, omtrent 1,5 % for
  wagon) og gir rulle-/stupemoment; det er utelatt.
- **Girkasse:** bare automatisk. Kartsters standard `SemiAutomatic` uten førerønsker oppfører seg likt.
- **Underlag:** de fire effektive tuningene (grep, rullemotstand, effektgrense) lages én gang i konstruktøren, og
  kartens egen toppfart finnes med halvering én gang (`KartEngine.GetTopSpeed`, som `KartEffectTuning.GetTopSpeed`).
  Ingen allokering per steg.
- **Utelatt som i § 1:** bremse-lås, downforce, boost, kroppsrulling (olabil skiller seg derfor fra Kart bare i
  styrelås, sporvidde og `LateralLoadTransferScale`), stuping, fly/glide.
- **Første steg** har ingen målt akselerasjon (forrige fart mangler), så vektoverføringen starter statisk.
