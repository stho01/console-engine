# 07 — Read-only diagnostic checks

There is no database. These are the read-only checks to run on (or ask the user to run on) the machine. Each answers
one named question. All commands are PowerShell and **only read**. None changes files, settings or processes.

<a id="q1"></a>
## Q1 — Is this Windows, and which version?

```powershell
[System.Environment]::OSVersion.VersionString; [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
```
Not Windows → [P1](05-triage.md#p1) step 1.

<a id="q2"></a>
## Q2 — Classic console or Windows Terminal? **[external]**

Run **in the same kind of window the game opens in**:
```powershell
if ($env:WT_SESSION) { 'Windows Terminal' } else { 'Not Windows Terminal (likely conhost)' }
```
Windows Terminal sets `WT_SESSION`. A double-clicked exe opens in whatever the Windows "default terminal application"
setting says. Ask the user what that setting is. Windows Terminal → [P2](05-triage.md#p2) step 1.

<a id="q3"></a>
## Q3 — Is the working directory the game's output folder?

From the folder the game is launched in:
```powershell
Get-Location; Get-ChildItem -Name *.exe; Test-Path .\Assets\Maps\map1.txt
```
TerraForM needs `True` on the last line (`GameScene.MapAssetPath` = `Assets/Maps`, relative). → [P1](05-triage.md#p1) step 5.

<a id="q4"></a>
## Q4 — Is the logger exe where `Log.Start` will look?

```powershell
Test-Path .\ConsoleEngine.Logger.exe
```
Run from the launch folder. `False` while `EnableLogger = true` → [P1](05-triage.md#p1) step 4.

<a id="q5"></a>
## Q5 — Is TCP port 11000 already taken?

```powershell
Get-NetTCPConnection -LocalPort 11000 -ErrorAction SilentlyContinue | Select-Object LocalAddress,State,OwningProcess
Get-Process -Id (Get-NetTCPConnection -LocalPort 11000 -ErrorAction SilentlyContinue).OwningProcess -ErrorAction SilentlyContinue | Select-Object Id,ProcessName
```
Any row with the game not running → [P1](05-triage.md#p1) step 3.

<a id="q6"></a>
## Q6 — Is this map file valid for `WorldLoader`?

```powershell
$f = '.\Assets\Maps\map1.txt'
$l = Get-Content $f
$h = $l | Where-Object { $_ -like '!*' }
$g = $l | Where-Object { $_ -notlike '!*' }
"headers:";  $h
"headers without ':'"; $h | Where-Object { $_ -notmatch ':' }
$w = [int](($h | Where-Object { $_ -match '^!width\s*:' }) -replace '.*:\s*','')
$ht = [int](($h | Where-Object { $_ -match '^!height\s*:' }) -replace '.*:\s*','')
"width=$w height=$ht rows=$($g.Count) maxRowLen=$(($g | Measure-Object Length -Maximum).Maximum)"
"S count=$((($g -join '').ToCharArray() | Where-Object { $_ -eq 'S' }).Count)  F count=$((($g -join '').ToCharArray() | Where-Object { $_ -eq 'F' }).Count)"
"last header line index=$([array]::LastIndexOf($l, ($h | Select-Object -Last 1)))  first grid line index=$([array]::IndexOf($l, ($g | Select-Object -First 1)))"
```
Expected for a loadable map ([04 § F6](04-flows.md#f6)):

| Check | Must be | Else |
|---|---|---|
| Headers without `:` | none | `IndexOutOfRangeException` |
| `!power`, `!sequences` | integers > 0 | `FormatException` / instant game over |
| `maxRowLen` ≤ width, `rows` ≤ height | yes | `IndexOutOfRangeException` |
| S count | ≥ 1 (the last one wins) | `NullReferenceException` |
| F count | ≥ 1, not adjacent | level can't be finished / double finish |
| last header index < first grid index | yes | rows silently dropped or wiped |

Key matching in `WorldLoader` lower-cases the key, so `!Width` works. The regex above assumes lower case.

<a id="q7"></a>
## Q7 — Is the .NET 10 runtime present?

```powershell
dotnet --list-runtimes | Select-String 'Microsoft.NETCore.App 10.'
```
All projects target `net10.0`. Nothing → install the .NET 10 runtime.

<a id="q8"></a>
## Q8 — What FPS is the user getting?

No in-game display exists. Options: (a) the logger title `Game FPS: n` in Asteroids/ConsoleGame-style builds with
`ShowFps` + `EnableLogger`, or (b) ask about the machine. Use it in [03 § Power](03-rules.md#power):
power per TerraForM move ≈ `0.64 × (0.1 × FPS + 1)`.

<a id="q9"></a>
## Q9 — Does the code still say what these docs say? (developer checkout)

```powershell
git grep -n "RotateMap" -- Examples/TerraForM
git grep -n "11000\|ConsoleEngine.Logger.exe" -- ConsoleEngine
dotnet test --no-build
```
