dotnet pack -c "Release"

Write-Output '=============================================='

$Files = $(Get-ChildItem -Path *.1.0.0.nupkg -R).Fullname
$Path = $(Get-Location).Path
$Destination = $Path + '\nugetpkgs'

Write-Output '=============================================='
Write-Output COPYING FILES: 
Write-Output $Files
Write-Output '=============================================='
Write-Output DESTINATION:
Write-Output $Destination
Write-Output '=============================================='

foreach ($file in $Files) {
    Copy-Item $file $Destination    
}

Write-Output ''
Write-Output Done...
