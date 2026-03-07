param(
    [Parameter(Mandatory=$true)][string]$ThemeName,
    [Parameter(Mandatory=$true)][ValidateSet("Dark","Light")][string]$Base,
    [Parameter(Mandatory=$true)][string]$PrimaryHex,
    [Parameter(Mandatory=$true)][string]$AccentHex,
    [Parameter(Mandatory=$true)][string]$SecondaryHex,
    [Parameter(Mandatory=$true)][string]$SurfaceHex
)

# HSL conversion utilities
function HexToRgb($hex) {
    $hex = $hex.TrimStart('#')
    if ($hex.Length -eq 8) { $hex = $hex.Substring(2) } # strip alpha
    $r = [Convert]::ToInt32($hex.Substring(0,2), 16)
    $g = [Convert]::ToInt32($hex.Substring(2,2), 16)
    $b = [Convert]::ToInt32($hex.Substring(4,2), 16)
    return @($r, $g, $b)
}

function RgbToHsl($r, $g, $b) {
    $r1 = $r / 255.0; $g1 = $g / 255.0; $b1 = $b / 255.0
    $max = [Math]::Max($r1, [Math]::Max($g1, $b1))
    $min = [Math]::Min($r1, [Math]::Min($g1, $b1))
    $l = ($max + $min) / 2.0
    if ($max -eq $min) { return @(0, 0, $l) }
    $d = $max - $min
    $s = if ($l -gt 0.5) { $d / (2.0 - $max - $min) } else { $d / ($max + $min) }
    $h = 0
    if ($max -eq $r1) { $h = (($g1 - $b1) / $d) + $(if ($g1 -lt $b1) { 6 } else { 0 }) }
    elseif ($max -eq $g1) { $h = (($b1 - $r1) / $d) + 2 }
    else { $h = (($r1 - $g1) / $d) + 4 }
    $h = $h / 6.0
    return @($h, $s, $l)
}

function HueToRgb($p, $q, $t) {
    if ($t -lt 0) { $t += 1 }
    if ($t -gt 1) { $t -= 1 }
    if ($t -lt 1/6) { return $p + ($q - $p) * 6 * $t }
    if ($t -lt 1/2) { return $q }
    if ($t -lt 2/3) { return $p + ($q - $p) * (2/3 - $t) * 6 }
    return $p
}

function HslToRgb($h, $s, $l) {
    if ($s -eq 0) {
        $v = [Math]::Round($l * 255)
        return @($v, $v, $v)
    }
    $q = if ($l -lt 0.5) { $l * (1 + $s) } else { $l + $s - $l * $s }
    $p = 2 * $l - $q
    $r = [Math]::Round((HueToRgb $p $q ($h + 1/3)) * 255)
    $g = [Math]::Round((HueToRgb $p $q $h) * 255)
    $b = [Math]::Round((HueToRgb $p $q ($h - 1/3)) * 255)
    $r = [Math]::Max(0, [Math]::Min(255, $r))
    $g = [Math]::Max(0, [Math]::Min(255, $g))
    $b = [Math]::Max(0, [Math]::Min(255, $b))
    return @($r, $g, $b)
}

function RgbToHex($r, $g, $b) {
    return "#{0:X2}{1:X2}{2:X2}" -f [int]$r, [int]$g, [int]$b
}

# Adjust lightness of a hex color
function AdjustLightness($hex, $amount) {
    $rgb = HexToRgb $hex
    $hsl = RgbToHsl $rgb[0] $rgb[1] $rgb[2]
    $newL = [Math]::Max(0, [Math]::Min(1, $hsl[2] + $amount))
    $newRgb = HslToRgb $hsl[0] $hsl[1] $newL
    return RgbToHex $newRgb[0] $newRgb[1] $newRgb[2]
}

# Adjust saturation of a hex color
function AdjustSaturation($hex, $amount) {
    $rgb = HexToRgb $hex
    $hsl = RgbToHsl $rgb[0] $rgb[1] $rgb[2]
    $newS = [Math]::Max(0, [Math]::Min(1, $hsl[1] + $amount))
    $newRgb = HslToRgb $hsl[0] $newS $hsl[2]
    return RgbToHex $newRgb[0] $newRgb[1] $newRgb[2]
}

# Set lightness to a specific value
function SetLightness($hex, $targetL) {
    $rgb = HexToRgb $hex
    $hsl = RgbToHsl $rgb[0] $rgb[1] $rgb[2]
    $newRgb = HslToRgb $hsl[0] $hsl[1] $targetL
    return RgbToHex $newRgb[0] $newRgb[1] $newRgb[2]
}

# Mix two hex colors
function MixColors($hex1, $hex2, $weight) {
    $rgb1 = HexToRgb $hex1
    $rgb2 = HexToRgb $hex2
    $r = [Math]::Round($rgb1[0] * $weight + $rgb2[0] * (1 - $weight))
    $g = [Math]::Round($rgb1[1] * $weight + $rgb2[1] * (1 - $weight))
    $b = [Math]::Round($rgb1[2] * $weight + $rgb2[2] * (1 - $weight))
    return RgbToHex $r $g $b
}

# Add alpha prefix to hex
function WithAlpha($hex, $alpha) {
    $hex = $hex.TrimStart('#')
    $a = [Math]::Round($alpha * 255)
    return ('#' + '{0:X2}' -f [int]$a) + $hex
}

# Get lightness of a color
function GetLightness($hex) {
    $rgb = HexToRgb $hex
    $hsl = RgbToHsl $rgb[0] $rgb[1] $rgb[2]
    return $hsl[2]
}

# Derive all theme colors
$isDark = $Base -eq "Dark"

if ($isDark) {
    # DARK THEME: Primary = background tone, Surface = controls/cards/dialogs
    $background = SetLightness $PrimaryHex 0.12
    $windowBg = SetLightness $PrimaryHex 0.11
    $darkBg = SetLightness $PrimaryHex 0.07
    $darkestBg = SetLightness $PrimaryHex 0.05
    $controlBg = SetLightness $SurfaceHex 0.17
    $controlHover = SetLightness $SurfaceHex 0.22
    $controlPressed = SetLightness $SurfaceHex 0.14
    $altRow = MixColors (SetLightness $SurfaceHex 0.14) $background 0.5
    $sidebarBg = MixColors (SetLightness $SurfaceHex 0.15) $background 0.6
    $border = SetLightness $SurfaceHex 0.25
    $controlBorder = SetLightness $SurfaceHex 0.25
    $divider = SetLightness $SurfaceHex 0.27
    $sidebarBorder = SetLightness $SurfaceHex 0.27
    $splitterDots = SetLightness $SurfaceHex 0.40
    $foreground = "#E8EAED"
    $textSecondary = "#B0B0B0"
    $controlFg = "#E8EAED"
    $disabledColor = "#6A6A6A"
    $disabledText = "#888888"
    $gridHeaderBg = SetLightness $SecondaryHex 0.12
    $gridHeaderFg = AdjustLightness $SecondaryHex 0.35
    $toolbarBg = SetLightness $SecondaryHex 0.15
    $toolbarFg = "#E8EAED"
    $toolbarHoverBg = SetLightness $SecondaryHex 0.22
    $toolbarHoverFg = "#FFFFFF"
    $statusBarBg = SetLightness $PrimaryHex 0.11
    $statusBarFg = "#B0B0B0"
    $notOwnedBg = MixColors (SetLightness $SecondaryHex 0.05) "#000000" 0.7
    $notOwnedFg = SetLightness $SecondaryHex 0.42
    $overlayBg = WithAlpha "#000000" 0.87
    $overlayText = "White"
    $overlayTextSecondary = "#CCCCCC"
    $progressTrack = SetLightness $SurfaceHex 0.20
    $shadowColor = "#000000"
    $shadowOpacity = "0.85"
    $textShadowColor = "#000000"
    $textShadowOpacity = "0.85"
    $summaryLabel = "#B0B0B0"
    $sidebarBtnBorder = SetLightness $SurfaceHex 0.33
    $btnHoverFg = $foreground
    $filterIconColor = "#808080"
    $actionBtnFg = "#E8EAED"
    $actionFilterFg = "#E8EAED"
    $warningBoxBg = "#3D2D00"
    $warningBoxBorder = "#FFB400"
    # Status colors — hardcoded to match Dark theme exactly
    $statusGreenBgBtn = "#8027AE60"
    $statusGreenFgBtn = "#E8EAED"
    $statusRedBgBtn = "#80B63434"
    $statusRedFgBtn = "#E8EAED"
    $statusInProgressBgBtn = "#80FFB400"
    $statusInProgressFgBtn = "#E8EAED"
    $statusYellowBg = "#5b4b26"
    $statusYellowFg = "#E8EAED"
    $statusGoldBg = "#4d4a00"
    $controlBgGreen = "#012a00"
    $controlBgRed = "#2a0000"
    # StatusRedBg gradient for dark themes
    $statusRedBgGradStart = $background
    $statusRedBgGradEnd = "#5b2626"
    $useRedBgGradient = $true
    $warningHighlightBg = WithAlpha "#FF6B6B" 0.27
} else {
    # LIGHT THEME: Primary = background tint, Surface = controls/cards/dialogs
    $background = SetLightness $PrimaryHex 0.96
    $windowBg = SetLightness $PrimaryHex 0.98
    $darkBg = SetLightness $PrimaryHex 0.88
    $darkestBg = SetLightness $PrimaryHex 0.15
    $controlBg = SetLightness $SurfaceHex 0.98
    $controlHover = SetLightness $SurfaceHex 0.92
    $controlPressed = SetLightness $SurfaceHex 0.86
    $altRow = MixColors (SetLightness $SurfaceHex 0.96) $background 0.5
    $sidebarBg = MixColors (SetLightness $SurfaceHex 0.94) $background 0.6
    $border = SetLightness $SurfaceHex 0.82
    $controlBorder = SetLightness $SurfaceHex 0.82
    $divider = SetLightness $SurfaceHex 0.82
    $sidebarBorder = SetLightness $SurfaceHex 0.82
    $splitterDots = SetLightness $SurfaceHex 0.72
    $foreground = SetLightness $PrimaryHex 0.15
    $textSecondary = SetLightness $PrimaryHex 0.45
    $controlFg = SetLightness $PrimaryHex 0.15
    $disabledColor = SetLightness $PrimaryHex 0.72
    $disabledText = SetLightness $PrimaryHex 0.72
    $gridHeaderBg = SetLightness $SecondaryHex 0.22
    $gridHeaderFg = AdjustLightness $SecondaryHex 0.45
    $toolbarBg = SetLightness $PrimaryHex 0.92
    $toolbarFg = "#E8EAED"
    $toolbarHoverBg = SetLightness $SecondaryHex 0.30
    $toolbarHoverFg = "#FFFFFF"
    $statusBarBg = SetLightness $PrimaryHex 0.93
    $statusBarFg = SetLightness $PrimaryHex 0.35
    $notOwnedBg = SetLightness $SecondaryHex 0.95
    $notOwnedFg = "#888888"
    $overlayBg = WithAlpha "#FFFFFF" 0.67
    $overlayText = SetLightness $PrimaryHex 0.15
    $overlayTextSecondary = SetLightness $PrimaryHex 0.45
    $progressTrack = SetLightness $SurfaceHex 0.82
    $shadowColor = "#000000"
    $shadowOpacity = "0.15"
    $textShadowColor = "#CCCCCC"
    $textShadowOpacity = "0.3"
    $summaryLabel = SetLightness $PrimaryHex 0.15
    $sidebarBtnBorder = SetLightness $SurfaceHex 0.72
    $btnHoverFg = $AccentHex
    $filterIconColor = SetLightness $SecondaryHex 0.55
    $actionBtnFg = AdjustLightness $SecondaryHex 0.45
    $actionFilterFg = SetLightness $PrimaryHex 0.15
    $warningBoxBg = "#3D2D00"
    $warningBoxBorder = "#FFB400"
    # Status colors — hardcoded to match Light theme exactly
    $statusGreenBgBtn = "#062010"
    $statusGreenFgBtn = "#FF3DD975"
    $statusRedBgBtn = "#200808"
    $statusRedFgBtn = "#FFDC5050"
    $statusInProgressBgBtn = "#201800"
    $statusInProgressFgBtn = "#FFFFC830"
    $statusYellowBg = "#5b4b26"
    $statusYellowFg = "#E8EAED"
    $statusGoldBg = "#fff8c4"
    $controlBgGreen = "#1B3D1B"
    $controlBgRed = "#3D1B1B"
    # StatusRedBg solid for light themes
    $statusRedBgGradStart = ""
    $statusRedBgGradEnd = ""
    $useRedBgGradient = $false
    $statusRedBgSolid = "#5b2626"
    $warningHighlightBg = WithAlpha "#FF6B6B" 0.27
}

# Accent-derived colors (same for both bases)
$accent = $AccentHex
$accentHover = AdjustLightness $AccentHex 0.10
$primaryBg = $AccentHex
$primaryBgHover = AdjustLightness $AccentHex 0.10
$activeFilterBorder = $AccentHex
$toggleChecked = $AccentHex
$progressBarAccent = $AccentHex
$statusInProgress = if ($isDark) { "#FFFFB400" } else { "#b63434" }
$btnPrimaryBg = $AccentHex
$btnPrimaryBorder = $AccentHex
$btnPrimaryHover = AdjustLightness $AccentHex 0.10

# Sidebar button hover — customizable per theme
$sidebarBtnHoverBorder = $AccentHex
$sidebarBtnHoverBg = if ($isDark) { SetLightness $SurfaceHex 0.22 } else { SetLightness $SurfaceHex 0.90 }

# Scan button and summary stat foregrounds — customizable per theme
$scanButtonFg = $AccentHex
$summaryBudgetFg = $AccentHex
$summaryEarnedFg = "#FF27AE60"
$summaryPercentFg = if ($isDark) { "#FFFFB400" } else { "#b63434" }

# Semantic colors — fixed across all themes
$statusGreen = "#FF27AE60"
$statusRed = "#b63434"
$statusYellow = "#5b4b26"
$statusNotStarted = "#FF95A5A6"
$errorText = "#E74C3C"
$warningText = if ($isDark) { "#FFC107" } else { "#D4A017" }
$warningHighlight = "#FF6B6B"
$btnSuccessBg = "#2D7D46"
$btnSuccessBorder = "#2D7D46"
$btnSuccessHover = "#359954"
$btnDangerBg = "#B33A3A"
$btnDangerBorder = "#B33A3A"
$btnDangerHover = "#CC4444"
$analysisRed = "#B33A3A"
$analysisOrange = "#CC7000"
$analysisYellow = "#B8A000"
$analysisGreen = "#2D7D46"
$filterIconActive = "#FF4444"

# Build the StatusRedBg entry
if ($useRedBgGradient) {
    $statusRedBgXaml = @"
    <LinearGradientBrush x:Key="StatusRedBg" StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="$statusRedBgGradStart" Offset="0.0"/>
        <GradientStop Color="$statusRedBgGradEnd" Offset="1.0"/>
    </LinearGradientBrush>
"@
} else {
    $statusRedBgXaml = "    <SolidColorBrush x:Key=`"StatusRedBg`" Color=`"$statusRedBgSolid`"/>"
}

# Generate XAML
$xaml = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- === FONT SETTINGS === -->
    <FontFamily x:Key="FontFamilyPrimary">Segoe UI</FontFamily>
    <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="FontSizeNormal">14</sys:Double>

    <!-- === COLOR PALETTE === -->
    <!-- Generated from: Primary=$PrimaryHex, Accent=$AccentHex, Secondary=$SecondaryHex, Surface=$SurfaceHex, Base=$Base -->
    <SolidColorBrush x:Key="BackgroundColor" Color="$background"/>
    <SolidColorBrush x:Key="DarkBackgroundColor" Color="$darkBg"/>
    <SolidColorBrush x:Key="DarkestBackgroundColor" Color="$darkestBg"/>
    <SolidColorBrush x:Key="ForegroundColor" Color="$foreground"/>
    <SolidColorBrush x:Key="TextColorSecondary" Color="$textSecondary"/>
    <SolidColorBrush x:Key="AccentColor" Color="$accent"/>
    <SolidColorBrush x:Key="BorderColor" Color="$border"/>
    <SolidColorBrush x:Key="DisabledColor" Color="$disabledColor"/>

    <!-- === WINDOW ELEMENTS === -->
    <SolidColorBrush x:Key="WindowBackground" Color="$windowBg"/>
    <SolidColorBrush x:Key="ControlBackground" Color="$controlBg"/>
    <SolidColorBrush x:Key="ControlBackgroundGreen" Color="$controlBgGreen"/>
    <SolidColorBrush x:Key="ControlBackgroundRed" Color="$controlBgRed"/>
    <SolidColorBrush x:Key="ControlForeground" Color="$controlFg"/>
    <SolidColorBrush x:Key="ControlBorder" Color="$controlBorder"/>
    <SolidColorBrush x:Key="ControlHoverBackground" Color="$controlHover"/>
    <SolidColorBrush x:Key="ControlPressedBackground" Color="$controlPressed"/>

    <!-- === CONTENT/DIALOG KEYS (used by FindReplaceDialog, etc.) === -->
    <SolidColorBrush x:Key="ContentBackground" Color="$controlBg"/>
    <SolidColorBrush x:Key="ContentForeground" Color="$foreground"/>
    <SolidColorBrush x:Key="PrimaryBackground" Color="$primaryBg"/>
    <SolidColorBrush x:Key="PrimaryForeground" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="ProgressBarTrackColor" Color="$progressTrack"/>
    <SolidColorBrush x:Key="GridAlternatingRowBackground" Color="$altRow"/>

    <!-- === SPLIT TOKEN KEYS (for independent per-region tuning) === -->
    <SolidColorBrush x:Key="ProgressBarAccent" Color="$progressBarAccent"/>
    <SolidColorBrush x:Key="ToggleCheckedBackground" Color="$toggleChecked"/>
    <SolidColorBrush x:Key="DialogBackground" Color="$controlBg"/>
    <SolidColorBrush x:Key="GridCellBackground" Color="$controlBg"/>
    <SolidColorBrush x:Key="DialogForeground" Color="$foreground"/>
    <SolidColorBrush x:Key="GridCellForeground" Color="$foreground"/>

    <!-- === DATAGRID COLORS === -->
    <SolidColorBrush x:Key="GridHeaderBackground" Color="$gridHeaderBg"/>

    <!-- === STATUS COLORS === -->
    <SolidColorBrush x:Key="StatusGreen" Color="$statusGreen"/>
    <SolidColorBrush x:Key="StatusGreenBgBtn" Color="$statusGreenBgBtn"/>
    <SolidColorBrush x:Key="StatusGreenFgBtn" Color="$statusGreenFgBtn"/>
    <SolidColorBrush x:Key="StatusYellow" Color="$statusYellow"/>
    <SolidColorBrush x:Key="StatusYellowBg" Color="$statusYellowBg"/>
    <SolidColorBrush x:Key="StatusYellowFg" Color="$statusYellowFg"/>
    <SolidColorBrush x:Key="StatusRed" Color="$statusRed"/>
$statusRedBgXaml
    <SolidColorBrush x:Key="StatusRedBgBtn" Color="$statusRedBgBtn"/>
    <SolidColorBrush x:Key="StatusRedFgBtn" Color="$statusRedFgBtn"/>
    <SolidColorBrush x:Key="StatusGoldBg" Color="$statusGoldBg"/>
    <SolidColorBrush x:Key="StatusInProgress" Color="$statusInProgress"/>
    <SolidColorBrush x:Key="StatusInProgressBgBtn" Color="$statusInProgressBgBtn"/>
    <SolidColorBrush x:Key="StatusInProgressFgBtn" Color="$statusInProgressFgBtn"/>
    <SolidColorBrush x:Key="StatusNotStarted" Color="$statusNotStarted"/>

    <!-- === ACTIVE FILTER BORDER === -->
    <SolidColorBrush x:Key="ActiveFilterBorderColor" Color="$activeFilterBorder"/>

    <!-- === TOOLBAR COLORS === -->
    <SolidColorBrush x:Key="ToolbarBackground" Color="$toolbarBg"/>

    <!-- === STATUSBAR COLORS === -->
    <SolidColorBrush x:Key="StatusBarBackground" Color="$statusBarBg"/>
    <SolidColorBrush x:Key="StatusBarForeground" Color="$statusBarFg"/>

    <!-- === NON-OWNED RECORDS COLORS === -->
    <SolidColorBrush x:Key="NotOwnedRowBackground" Color="$notOwnedBg"/>
    <SolidColorBrush x:Key="NotOwnedRowForeground" Color="$notOwnedFg"/>

    <!-- === ACTION BUTTON COLORS === -->
    <!-- Green buttons: Add, Submit, Save, Restore -->
    <SolidColorBrush x:Key="ButtonSuccessBackground" Color="$btnSuccessBg"/>
    <SolidColorBrush x:Key="ButtonSuccessBorder" Color="$btnSuccessBorder"/>
    <SolidColorBrush x:Key="ButtonSuccessHover" Color="$btnSuccessHover"/>

    <!-- Red buttons: Delete, Remove, Destructive actions -->
    <SolidColorBrush x:Key="ButtonDangerBackground" Color="$btnDangerBg"/>
    <SolidColorBrush x:Key="ButtonDangerBorder" Color="$btnDangerBorder"/>
    <SolidColorBrush x:Key="ButtonDangerHover" Color="$btnDangerHover"/>

    <!-- Accent buttons: Primary actions -->
    <SolidColorBrush x:Key="ButtonPrimaryBackground" Color="$btnPrimaryBg"/>
    <SolidColorBrush x:Key="ButtonPrimaryBorder" Color="$btnPrimaryBorder"/>
    <SolidColorBrush x:Key="ButtonPrimaryHover" Color="$btnPrimaryHover"/>

    <!-- === OVERLAY COLORS === -->
    <SolidColorBrush x:Key="OverlayBackground" Color="$overlayBg"/>
    <SolidColorBrush x:Key="OverlayText" Color="$overlayText"/>
    <SolidColorBrush x:Key="OverlayTextSecondary" Color="$overlayTextSecondary"/>

    <!-- === ERROR/WARNING COLORS === -->
    <SolidColorBrush x:Key="ErrorText" Color="$errorText"/>
    <SolidColorBrush x:Key="WarningText" Color="$warningText"/>
    <SolidColorBrush x:Key="WarningHighlight" Color="$warningHighlight"/>
    <SolidColorBrush x:Key="WarningBoxBg" Color="$warningBoxBg"/>
    <SolidColorBrush x:Key="WarningBoxBorder" Color="$warningBoxBorder"/>

    <!-- === ANALYSIS MODULE COLORS (% Complete ranges) === -->
    <SolidColorBrush x:Key="AnalysisRedBg" Color="#FF$($analysisRed.TrimStart('#'))"/>
    <SolidColorBrush x:Key="AnalysisOrangeBg" Color="#FF$($analysisOrange.TrimStart('#'))"/>
    <SolidColorBrush x:Key="AnalysisYellowBg" Color="#FF$($analysisYellow.TrimStart('#'))"/>
    <SolidColorBrush x:Key="AnalysisGreenBg" Color="#FF$($analysisGreen.TrimStart('#'))"/>
    <SolidColorBrush x:Key="WarningHighlightBackground" Color="$warningHighlightBg"/>

    <!-- === UI ELEMENT COLORS === -->
    <SolidColorBrush x:Key="SidebarBackground" Color="$sidebarBg"/>
    <SolidColorBrush x:Key="SidebarBorder" Color="$sidebarBorder"/>
    <SolidColorBrush x:Key="DividerColor" Color="$divider"/>
    <SolidColorBrush x:Key="SplitterDots" Color="$splitterDots"/>
    <SolidColorBrush x:Key="DisabledText" Color="$disabledText"/>

    <!-- === TOOLBAR BUTTON COLORS === -->
    <SolidColorBrush x:Key="ToolbarForeground" Color="$toolbarFg"/>
    <SolidColorBrush x:Key="ToolbarHoverBackground" Color="$toolbarHoverBg"/>
    <SolidColorBrush x:Key="ToolbarHoverForeground" Color="$toolbarHoverFg"/>

    <!-- === GRID HEADER COLORS === -->
    <SolidColorBrush x:Key="GridHeaderForeground" Color="$gridHeaderFg"/>
    <Color x:Key="FilterIconColor">$filterIconColor</Color>
    <Color x:Key="FilterIconActiveColor">$filterIconActive</Color>

    <!-- === ACTION BUTTON COLORS === -->
    <SolidColorBrush x:Key="ActionButtonForeground" Color="$actionBtnFg"/>
    <SolidColorBrush x:Key="ActionFilterForeground" Color="$actionFilterFg"/>

    <!-- === ELEVATION/SHADOW === -->
    <SolidColorBrush x:Key="SidebarButtonBorder" Color="$sidebarBtnBorder"/>
    <SolidColorBrush x:Key="ButtonHoverForeground" Color="$btnHoverFg"/>
    <Color x:Key="ButtonShadowColor">$shadowColor</Color>
    <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="ButtonShadowOpacity">$shadowOpacity</sys:Double>
    <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="TextShadowDepth">3</sys:Double>
    <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="TextShadowBlurRadius">1</sys:Double>
    <Color x:Key="TextShadowColor">$textShadowColor</Color>
    <SolidColorBrush x:Key="SummaryLabelColor" Color="$summaryLabel"/>
    <SolidColorBrush x:Key="SidebarButtonHoverBorder" Color="$sidebarBtnHoverBorder"/>
    <SolidColorBrush x:Key="SidebarButtonHoverBackground" Color="$sidebarBtnHoverBg"/>
    <SolidColorBrush x:Key="ScanButtonForeground" Color="$scanButtonFg"/>
    <SolidColorBrush x:Key="SummaryBudgetForeground" Color="$summaryBudgetFg"/>
    <SolidColorBrush x:Key="SummaryEarnedForeground" Color="$summaryEarnedFg"/>
    <SolidColorBrush x:Key="SummaryPercentForeground" Color="$summaryPercentFg"/>
    <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="TextShadowOpacity">$textShadowOpacity</sys:Double>

    <!-- ========================================= -->
    <!-- === DEFAULT BUTTON STYLE === -->
    <!-- ========================================= -->
    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource ControlBackground}"/>
        <Setter Property="Foreground" Value="{StaticResource ForegroundColor}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderColor}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,5"/>
        <Setter Property="FontSize" Value="{StaticResource FontSizeNormal}"/>
        <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}"/>

        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        SnapsToDevicePixels="True">
                        <ContentPresenter x:Name="contentPresenter"
                                    Margin="{TemplateBinding Padding}"
                                    HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                    VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                    Focusable="False"
                                    RecognizesAccessKey="True"
                                    SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource ControlHoverBackground}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource ControlBackground}"/>
                            <Setter Property="Foreground" Value="{StaticResource DisabledColor}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ========================================= -->
    <!-- === TOOLBAR BUTTON STYLE === -->
    <!-- ========================================= -->
    <Style x:Key="ToolbarButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Foreground" Value="{StaticResource ToolbarForeground}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="8,0"/>
        <Setter Property="FontSize" Value="{StaticResource FontSizeNormal}"/>
        <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}"/>

        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        SnapsToDevicePixels="True">
                        <ContentPresenter x:Name="contentPresenter"
                                    Margin="{TemplateBinding Padding}"
                                    HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                    VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                    Focusable="False"
                                    RecognizesAccessKey="True"
                                    SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource ToolbarHoverBackground}"/>
                            <Setter Property="Foreground" Value="{StaticResource ToolbarHoverForeground}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Foreground" Value="{StaticResource DisabledColor}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
"@

$outputPath = Join-Path $PSScriptRoot "..\Themes\${ThemeName}Theme.xaml"
# Normalize to CRLF line endings for Visual Studio compatibility
$xaml = $xaml -replace "`r?`n", "`r`n"
[System.IO.File]::WriteAllText($outputPath, $xaml, [System.Text.UTF8Encoding]::new($false))
Write-Host "Theme file generated: Themes/${ThemeName}Theme.xaml"
Write-Host ""
Write-Host "=== REMAINING STEPS ==="
Write-Host "1. Register in ThemeManager.cs:"
$sfBase = if ($isDark) { "FluentDark" } else { "FluentLight" }
Write-Host "   - Add to ThemeMap: { `"$ThemeName`", `"$sfBase`" }"
Write-Host "   - Add `"$ThemeName`" to AvailableThemes array"
Write-Host "2. Add radio button in ThemeManagerDialog.xaml"
Write-Host "3. Update Help/manual.html with the new theme"
Write-Host "4. Build and test all views"
