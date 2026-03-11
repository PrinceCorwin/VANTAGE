$json = Get-Content "C:\Users\steve.amalfitano\source\repos\PrinceCorwin\VANTAGE\Resources\FittingMakeup.json" | ConvertFrom-Json
$components = $json | ForEach-Object { $_.Component } | Sort-Object -Unique
$components | ForEach-Object { Write-Host $_ }
