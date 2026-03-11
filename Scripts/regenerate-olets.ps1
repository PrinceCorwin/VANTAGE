$jsonPath = "C:\Users\steve.amalfitano\source\repos\PrinceCorwin\VANTAGE\Resources\FittingMakeup.json"

# Read existing JSON
$entries = Get-Content $jsonPath | ConvertFrom-Json

Write-Host "Total entries before: $($entries.Count)"

# Remove existing olet entries (WOL, SOL, TOL, ELB, LOL, NOL)
$oletTypes = @("WOL", "SOL", "TOL", "ELB", "LOL", "NOL")
$nonOletEntries = @($entries | Where-Object { $_.Component -notin $oletTypes })
Write-Host "After removing olets: $($nonOletEntries.Count)"

# New olet data from Excel (ConnType|Component|Class|RunSize|OutletSize|MakeupRun|MakeupOutlet)
$oletData = @(
    "BW|WOL|STD|0.5||0.748|",
    "BW|WOL|STD|0.75||0.8661|",
    "BW|WOL|STD|1||1.063|",
    "BW|WOL|STD|1.5||1.2992|",
    "BW|WOL|STD|2||1.4961|",
    "BW|WOL|STD|2.5||1.6142|",
    "BW|WOL|STD|3||1.7323|",
    "BW|WOL|STD|4||2.0079|",
    "BW|WOL|XS|0.5||0.748|",
    "BW|WOL|XS|0.75||0.8661|",
    "BW|WOL|XS|1||1.063|",
    "BW|WOL|XS|1.5||1.2992|",
    "BW|WOL|XS|2||1.4961|",
    "BW|WOL|XS|2.5||1.6142|",
    "BW|WOL|XS|3||1.7323|",
    "BW|WOL|XS|4||2.0079|",
    "BW|WOL|160|0.5||1.1024|",
    "BW|WOL|160|0.75||1.2598|",
    "BW|WOL|160|1||1.4961|",
    "BW|WOL|160|1.5||2.0079|",
    "BW|WOL|160|2||2.1654|",
    "BW|WOL|160|3||1.1811|",
    "BW|WOL|160|4||1.1811|",
    "SW|SOL|3000|0.5||0.6299|",
    "SW|SOL|3000|0.75||0.6299|",
    "SW|SOL|3000|1||0.8661|",
    "SW|SOL|3000|1.5||0.9449|",
    "SW|SOL|3000|2||0.9449|",
    "SW|SOL|3000|2.5||0.9843|",
    "SW|SOL|3000|3||1.1811|",
    "SW|SOL|3000|4||1.1811|",
    "SW|SOL|6000|0.5||0.9449|",
    "SW|SOL|6000|0.75||0.9843|",
    "SW|SOL|6000|1||1.1024|",
    "SW|SOL|6000|1.5||1.2598|",
    "SW|SOL|6000|2||1.4567|",
    "SW|SOL|6000|2.5||1.811|",
    "SW|SOL|6000|3||2.0079|",
    "SW|SOL|6000|4||2.2441|",
    "THRD|TOL|3000|0.5||0.9843|",
    "THRD|TOL|3000|0.75||1.063|",
    "THRD|TOL|3000|1||1.2992|",
    "THRD|TOL|3000|1.5||1.378|",
    "THRD|TOL|3000|2||1.4961|",
    "THRD|TOL|3000|2.5||1.811|",
    "THRD|TOL|3000|3||2.0079|",
    "THRD|TOL|3000|4||2.2441|",
    "THRD|TOL|6000|0.5||1.2598|",
    "THRD|TOL|6000|0.75||1.4173|",
    "THRD|TOL|6000|1||1.5748|",
    "THRD|TOL|6000|1.5||1.6929|",
    "THRD|TOL|6000|2||2.0472|",
    "BW|LOL|3000|0.5||1.5354|",
    "BW|LOL|3000|0.75||1.811|",
    "BW|LOL|3000|1||2.2047|",
    "BW|LOL|3000|1.5||2.7953|",
    "BW|LOL|3000|2||3.622|",
    "BW|LOL|6000|0.5||1.811|",
    "BW|LOL|6000|0.75||2.126|",
    "BW|LOL|6000|1||2.4803|",
    "BW|LOL|6000|1.5||3.5039|",
    "BW|ELB|3000|0.5||1.6142|",
    "BW|ELB|3000|0.75||1.8504|",
    "BW|ELB|3000|1||2.2441|",
    "BW|ELB|3000|1.5||2.7559|",
    "BW|ELB|3000|2||3.2283|",
    "BW|ELB|6000|0.5||1.8504|",
    "BW|ELB|6000|0.75||2.2441|",
    "BW|ELB|6000|1||2.4803|",
    "BW|ELB|6000|1.5||3.2283|",
    "THRD|NOL|3000|0.5||3.5039|",
    "THRD|NOL|3000|0.75||3.5039|",
    "THRD|NOL|3000|1||3.5039|",
    "THRD|NOL|3000|1.5||3.5039|",
    "THRD|NOL|3000|2||3.5039|",
    "BW|NOL|3000|0.5||3.5039|",
    "BW|NOL|3000|0.75||3.5039|",
    "BW|NOL|3000|1||3.5039|",
    "BW|NOL|3000|1.5||3.5039|",
    "BW|NOL|3000|2||3.5039|",
    "SW|NOL|3000|0.5||3.5039|",
    "SW|NOL|3000|0.75||3.5039|",
    "SW|NOL|3000|1||3.5039|",
    "SW|NOL|3000|1.5||3.5039|",
    "SW|NOL|3000|2||3.5039|",
    "THRD|NOL|6000|0.5||3.5039|",
    "THRD|NOL|6000|0.75||3.5039|",
    "THRD|NOL|6000|1||3.5039|",
    "THRD|NOL|6000|1.5||3.5039|",
    "THRD|NOL|6000|2||3.5039|",
    "BW|NOL|6000|0.5||3.5039|",
    "BW|NOL|6000|0.75||3.5039|",
    "BW|NOL|6000|1||3.5039|",
    "BW|NOL|6000|1.5||3.5039|",
    "BW|NOL|6000|2||3.5039|",
    "SW|NOL|6000|0.5||3.5039|",
    "SW|NOL|6000|0.75||3.5039|",
    "SW|NOL|6000|1||3.5039|",
    "SW|NOL|6000|1.5||3.5039|",
    "SW|NOL|6000|2||3.5039|"
)

# Parse and create new entries
$newEntries = @()
foreach ($line in $oletData) {
    $parts = $line.Split('|')
    $connType = $parts[0]
    $component = $parts[1]
    $classStr = $parts[2]
    $runSize = [double]$parts[3]
    $makeupRun = [double]$parts[5]

    $newEntry = [ordered]@{
        Connection_Type = $connType
        Component = $component
        Class = $classStr  # Store class as string (STD, XS, 160, 3000, 6000)
        Run_Size = $runSize
        Makeup_Run_In = $makeupRun
    }

    $newEntries += [PSCustomObject]$newEntry
}

Write-Host "New olet entries: $($newEntries.Count)"

# Combine non-olet entries with new olet entries
$allEntries = @($nonOletEntries) + @($newEntries)
Write-Host "Total entries after: $($allEntries.Count)"

# Write back with proper JSON formatting
$json = $allEntries | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($jsonPath, $json, [System.Text.Encoding]::UTF8)

Write-Host "Done! JSON updated with Class values as strings."
