$Files = $(Get-ChildItem -Path *.nupkg -R).FullName

Write-Output $Files

Write-Output Remove y/N?
$KeyPresed = [System.Console]::ReadKey().Key.ToString();
Write-Output ''

if ($KeyPresed -eq 'y') {
    Get-ChildItem -Path *.nupkg -R | Remove-Item
} else {
    Write-Output Skipped..
}