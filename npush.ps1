$Files = $(Get-ChildItem -Path .\nugetpkgs\*.1.0.0.nupkg -R).Fullname

Write-Output $Files
Write-Output 'Push to packs to github y/N?'
$KeyPresed = [System.Console]::ReadKey().Key.ToString();
Write-Output ''

if ($KeyPresed -eq 'y') {
    foreach ($file in $Files) {
        dotnet nuget push $file -s "github"
    }   
} else {
    Write-Output 'Skipped...'
}