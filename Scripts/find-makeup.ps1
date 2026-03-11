param([string]$Component, [double]$Size = 0, [double]$Class = 0)

$json = Get-Content "C:\Users\steve.amalfitano\source\repos\PrinceCorwin\VANTAGE\Resources\FittingMakeup.json" | ConvertFrom-Json

$matches = $json | Where-Object {
    $_.Component -eq $Component -and
    ($Size -eq 0 -or $_.Run_Size -eq $Size) -and
    ($Class -eq 0 -or $_.Class -eq $Class -or $_.Class -eq $null)
}

if ($matches.Count -eq 0) {
    Write-Host "No matches found for Component=$Component, Size=$Size, Class=$Class"
} else {
    $matches | ForEach-Object {
        Write-Host "ConnType=$($_.Connection_Type), Comp=$($_.Component), Class=$($_.Class), Size=$($_.Run_Size), Outlet=$($_.Outlet_Size), Makeup=$($_.Makeup_Run_In)"
    }
}
