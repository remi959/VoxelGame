# Script to copy all C# scripts to a single folder for easy sharing with AI agents

param(
    [string]$SourcePath = "$PSScriptRoot\..\Assets\Scripts",
    [string]$OutputPath = "$PSScriptRoot\..\ScriptsForReview"
)

# Create output directory
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Get all .cs files
$scripts = Get-ChildItem -Path $SourcePath -Filter "*.cs" -Recurse

Write-Host "Found $($scripts.Count) script files"

# Copy all files to output folder with original names
foreach ($script in $scripts) {
    Copy-Item $script.FullName -Destination $OutputPath
}

Write-Host "Copied $($scripts.Count) files to: $((Resolve-Path $OutputPath).Path)"
Write-Host "Done!"