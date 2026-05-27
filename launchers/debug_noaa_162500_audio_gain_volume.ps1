$ErrorActionPreference = "Continue"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Audio = Join-Path $Root "bin\pluto_audio_monitor.exe"
$Sess = Join-Path $Root "sessions"
New-Item -Force -ItemType Directory -Path $Sess | Out-Null

$Log = Join-Path $Sess "noaa_162500_audio_gain_volume_debug_log.txt"
$Csv = Join-Path $Sess "audio_gain_volume_debug_log.csv"

"Pluto Windows Scanner GUI v2 - NOAA 162.500 Audio Gain/Volume Debug" | Set-Content -Path $Log
"Project: $Root" | Add-Content -Path $Log
"Audio EXE: $Audio" | Add-Content -Path $Log
"Date: $(Get-Date)" | Add-Content -Path $Log
"" | Add-Content -Path $Log

if (-not (Test-Path $Audio)) {
    "ERROR: Audio EXE not found: $Audio" | Add-Content -Path $Log
    Write-Host "ERROR: Audio EXE not found: $Audio"
    exit 2
}

Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $Sess "0*_*.wav")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $Sess "1*_*.wav")
Remove-Item -Force -ErrorAction SilentlyContinue $Csv

function Run-Test {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string[]]$Args
    )

    $wav = Join-Path $Sess "$Name.wav"
    "" | Add-Content -Path $Log
    "[$Name]" | Add-Content -Path $Log
    "Command: `"$Audio`" $($Args -join ' ') --wav `"$wav`" --csv `"$Csv`"" | Add-Content -Path $Log

    & $Audio @Args --wav $wav --csv $Csv 2>&1 | Tee-Object -FilePath $Log -Append
    $exit = $LASTEXITCODE

    "ExitCode: $exit" | Add-Content -Path $Log
    if (Test-Path $wav) {
        $f = Get-Item $wav
        "WAV: $($f.Name) $($f.Length) bytes" | Add-Content -Path $Log
    } else {
        "WAV: missing" | Add-Content -Path $Log
    }
}

# Baseline: known local NOAA at 162.500 MHz. Keep rate 1,000,000 Hz, not 960,000.
Run-Test "01_preset_vol_010" @("--uri","ip:192.168.2.1","--mode","nfm","--preset","noaa5","--rate","1000000","--seconds","12","--squelch-off","--volume","0.10")
Run-Test "02_preset_vol_003" @("--uri","ip:192.168.2.1","--mode","nfm","--preset","noaa5","--rate","1000000","--seconds","12","--squelch-off","--volume","0.03")
Run-Test "03_freq_vol_010" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--seconds","12","--squelch-off","--volume","0.10")
Run-Test "04_freq_vol_003" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--seconds","12","--squelch-off","--volume","0.03")

# Gain/AGC tests.
Run-Test "05_manual_gain20_vol010" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--seconds","12","--squelch-off","--gain-mode","manual","--gain-db","20","--volume","0.10")
Run-Test "06_manual_gain10_vol010" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--seconds","12","--squelch-off","--gain-mode","manual","--gain-db","10","--volume","0.10")
Run-Test "07_manual_gain5_vol010" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--seconds","12","--squelch-off","--gain-mode","manual","--gain-db","5","--volume","0.10")

# Narrower audio/RF filter tests.
Run-Test "08_bw25k_lp3k_vol005" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--bw","25000","--audio-lowpass-hz","3000","--seconds","12","--squelch-off","--volume","0.05")
Run-Test "09_bw200k_lp3k_vol005_gain10" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162500000","--rate","1000000","--bw","200000","--audio-lowpass-hz","3000","--seconds","12","--squelch-off","--gain-mode","manual","--gain-db","10","--volume","0.05")

# Small offset tests to catch ppm/tuning/demod offset problems.
Run-Test "10_offset_minus2500_vol005" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162497500","--rate","1000000","--seconds","12","--squelch-off","--volume","0.05")
Run-Test "11_offset_plus2500_vol005" @("--uri","ip:192.168.2.1","--mode","nfm","--freq","162502500","--rate","1000000","--seconds","12","--squelch-off","--volume","0.05")

"" | Add-Content -Path $Log
"Created WAV files:" | Add-Content -Path $Log
Get-ChildItem -Path $Sess -Filter "*_vol*.wav" -ErrorAction SilentlyContinue | ForEach-Object {
    "  $($_.Name) $($_.Length) bytes" | Add-Content -Path $Log
}
Get-ChildItem -Path $Sess -Filter "*_gain*.wav" -ErrorAction SilentlyContinue | ForEach-Object {
    "  $($_.Name) $($_.Length) bytes" | Add-Content -Path $Log
}
Get-ChildItem -Path $Sess -Filter "*_offset*.wav" -ErrorAction SilentlyContinue | ForEach-Object {
    "  $($_.Name) $($_.Length) bytes" | Add-Content -Path $Log
}

Write-Host "Done."
Write-Host "Log: $Log"
Write-Host "CSV: $Csv"
Write-Host "WAV files are in: $Sess"
