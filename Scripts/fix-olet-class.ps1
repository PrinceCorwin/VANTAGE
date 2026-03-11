$jsonPath = "C:\Users\steve.amalfitano\source\repos\PrinceCorwin\VANTAGE\Resources\FittingMakeup.json"

# Read JSON as text and fix the Class values
$content = Get-Content $jsonPath -Raw

# Remove Class entries with non-numeric values (STD, XS, 160 as strings)
$content = $content -replace '"Class":\s*"STD",?\s*\r?\n\s*', ''
$content = $content -replace '"Class":\s*"XS",?\s*\r?\n\s*', ''
$content = $content -replace '"Class":\s*"160",?\s*\r?\n\s*', ''

# Clean up any trailing commas before closing braces
$content = $content -replace ',(\s*\r?\n\s*\})', '$1'

[System.IO.File]::WriteAllText($jsonPath, $content, [System.Text.Encoding]::UTF8)

Write-Host "Fixed Class values in JSON"
