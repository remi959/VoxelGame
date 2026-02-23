param(
    [string]$SourcePath = "$PSScriptRoot\..\Assets\Scripts",
    [string]$OutputFile = "$PSScriptRoot\..\ScriptsForReview\AllScripts.txt",
    [string]$Filter = "*.cs"
)

# Ensure output directory exists
$outDir = Split-Path -Path $OutputFile -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# Find files
$files = Get-ChildItem -Path $SourcePath -Filter $Filter -Recurse -File -ErrorAction SilentlyContinue

if (-not $files -or $files.Count -eq 0) {
    Write-Host "No files found in: $SourcePath"
    exit 0
}

# Remove existing output if present
if (Test-Path $OutputFile) {
    Remove-Item $OutputFile -Force
}

# Header for the combined file
$header = "Combined C# scripts from: $SourcePath"
$header += "`r`nGenerated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$header += "`r`n`r`n"
Add-Content -Path $OutputFile -Value $header -Encoding utf8

# Append each file with a clear separator
foreach ($file in $files | Sort-Object FullName) {
    $sep = ("=" * 80)
    Add-Content -Path $OutputFile -Value $sep -Encoding utf8
    Add-Content -Path $OutputFile -Value ("File: " + $file.FullName) -Encoding utf8
    Add-Content -Path $OutputFile -Value $sep -Encoding utf8
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { $content = "" }
    Add-Content -Path $OutputFile -Value $content -Encoding utf8
    Add-Content -Path $OutputFile -Value "`r`n" -Encoding utf8
}

Write-Host "Wrote $($files.Count) files to: $OutputFile"