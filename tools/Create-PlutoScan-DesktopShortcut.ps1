$ErrorActionPreference = "Stop"

$RepoRoot = "C:\msys64\home\jim\sdrdev\pluto_windows_scanner"
$Launcher = Join-Path $RepoRoot "launchers\start_windows_scanner_gui_v2.cmd"
$IconDir = Join-Path $RepoRoot "assets"
$LogoPng = Join-Path $IconDir "bakstaaj_logo.png"
$IconIco = Join-Path $IconDir "pluto_scan.ico"
$ShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Pluto Scan.lnk"

$LogoUrl = "https://img1.wsimg.com/blobby/go/1af6d27d-7369-4162-835b-7a28aec50265/downloads/1cele9pt5_792531.png?ver=1771269176603"

if (!(Test-Path $RepoRoot)) {
    throw "Repo root not found: $RepoRoot"
}

if (!(Test-Path $Launcher)) {
    throw "Launcher not found: $Launcher"
}

New-Item -ItemType Directory -Force -Path $IconDir | Out-Null

Write-Host "Downloading bakStaaJ logo..."
Invoke-WebRequest -Uri $LogoUrl -OutFile $LogoPng

Write-Host "Creating ICO file..."
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile($LogoPng)
try {
    $size = 64
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $scale = [Math]::Min($size / $src.Width, $size / $src.Height)
        $w = [int]($src.Width * $scale)
        $h = [int]($src.Height * $scale)
        $x = [int](($size - $w) / 2)
        $y = [int](($size - $h) / 2)

        $g.DrawImage($src, $x, $y, $w, $h)

        $hIcon = $bmp.GetHicon()
        $icon = [System.Drawing.Icon]::FromHandle($hIcon)
        $fs = New-Object System.IO.FileStream($IconIco, [System.IO.FileMode]::Create)
        try {
            $icon.Save($fs)
        }
        finally {
            $fs.Close()
            $icon.Dispose()
        }
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}
finally {
    $src.Dispose()
}

Write-Host "Creating desktop shortcut..."
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = "$env:WINDIR\System32\cmd.exe"
$shortcut.Arguments = "/c `"$Launcher`""
$shortcut.WorkingDirectory = $RepoRoot
$shortcut.WindowStyle = 1
$shortcut.Description = "Launch Pluto Scan"
$shortcut.IconLocation = $IconIco
$shortcut.Save()

Write-Host "Created shortcut:"
Write-Host "  $ShortcutPath"
Write-Host "Icon:"
Write-Host "  $IconIco"
