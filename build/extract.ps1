param (
    [string]$sourceDir = ".",
    [string]$filter = "*.zip",
    [string]$outDir = "..\extract"
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Unzip
{
    param([string]$zipfile, [string]$outpath)

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

$packages = Get-ChildItem -Path "$sourceDir" -Name -File -Filter "$filter" -Recurse

foreach ($package in $packages) {
    Write-Host "Extracting $package..."
    $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension("$package")
    $packageFullPath = [System.IO.Path]::Combine("$sourceDir", "$package")
    Unzip $packageFullPath "$outDir\$fileNameWithoutExtension"
    Write-Host "$package extracted."
}