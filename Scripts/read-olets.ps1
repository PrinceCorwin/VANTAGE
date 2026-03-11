$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $workbook = $excel.Workbooks.Open("C:\Users\steve.amalfitano\source\repos\PrinceCorwin\VANTAGE\Plans\FittingMakeup_v3.xlsx")
    $sheet = $workbook.Sheets.Item(1)
    $usedRange = $sheet.UsedRange
    $rows = $usedRange.Rows.Count

    # Find olet rows (TOL, WOL, SOL, ELB, LOL, NOL)
    for ($r = 2; $r -le $rows; $r++) {
        $component = $sheet.Cells.Item($r, 2).Text
        if ($component -match "^(TOL|WOL|SOL|ELB|LOL|NOL)$") {
            $connType = $sheet.Cells.Item($r, 1).Text
            $class = $sheet.Cells.Item($r, 3).Text
            $runSize = $sheet.Cells.Item($r, 4).Text
            $outletSize = $sheet.Cells.Item($r, 5).Text
            $makeupRun = $sheet.Cells.Item($r, 6).Text
            $makeupOutlet = $sheet.Cells.Item($r, 7).Text
            Write-Host "$connType|$component|$class|$runSize|$outletSize|$makeupRun|$makeupOutlet"
        }
    }

    $workbook.Close($false)
}
finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
