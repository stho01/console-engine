<#
.SYNOPSIS
Builds an example game and starts it in the classic console host (conhost.exe).

.DESCRIPTION
Windows Terminal ignores the window size and font that the engine sets, so the games look wrong there. conhost
always opens a classic console window, whatever Windows' default terminal is. The game starts in its output folder,
so relative paths (TerraForM maps, the logger exe) resolve.

.EXAMPLE
./run-game.ps1 Scripted

.EXAMPLE
./run-game.ps1 TerraForM -Configuration Release
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('TerraForM', 'Snake', 'Asteroids', 'Platformer', 'ConsoleGame',
        'TerraForM.Ecs', 'Snake.Ecs', 'Asteroids.Ecs', 'Platformer.Ecs', 'ConsoleGame.Ecs',
        'Swarm', 'PhysicsSandbox', 'PhysicsShowcase', 'EternalDigger', 'Starguard', 'Scripted', 'Kartster')]
    [string] $Game,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

dotnet build (Join-Path $PSScriptRoot "Examples/$Game/$Game.csproj") -c $Configuration --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$output = Join-Path $PSScriptRoot "Examples/$Game/bin/$Configuration/net11.0"
Start-Process conhost.exe -ArgumentList "`"$(Join-Path $output "$Game.exe")`"" -WorkingDirectory $output
