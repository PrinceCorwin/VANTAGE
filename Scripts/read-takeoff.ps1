param([string]$Path, [string]$Sheet = "Material", [int]$MaxRows = 30)

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $workbook = $excel.Workbooks.Open($Path)
    $ws = $workbook.Sheets.Item($Sheet)
    $usedRange = $ws.UsedRange
    $rows = $usedRange.Rows.Count
    $cols = $usedRange.Columns.Count

    Write-Host "=== $Sheet TAB ==="
    Write-Host "Rows: $rows, Cols: $cols"
    Write-Host ""

    for ($r = 1; $r -le [Math]::Min($rows, $MaxRows); $r++) {
        $line = @()
        for ($c = 1; $c -le [Math]::Min($cols, 12); $c++) {
            $val = $ws.Cells.Item($r, $c).Text
            $line += $val
        }
        Write-Host ($line -join '|')
    }

    $workbook.Close($false)
}
finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
