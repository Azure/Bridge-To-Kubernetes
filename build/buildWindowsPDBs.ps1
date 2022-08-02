param (
    [string]$symbolsRootPath = ".",
    [string]$tempDirectory = "..\"
 )

 # set up the symbols paths
$AZDS_SYMBOLS_FOLDER = "$symbolsRootPath"
$pdb2pdb = "$tempDirectory\Pdb2Pdb\Microsoft.DiaSymReader.Pdb2Pdb.1.1.0-beta2-21054-01\tools\Pdb2Pdb.exe"

# Install Pdb2Pdb tool
nuget.exe install Microsoft.DiaSymReader.Pdb2Pdb -Version 1.1.0-beta2-21054-01 -OutputDirectory "$tempDirectory\Pdb2Pdb" -Source  https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json -NoCache

$SYMBOL_FILES = Get-ChildItem -Path $AZDS_SYMBOLS_FOLDER -Name -File -Filter "*.pdb" -Recurse

# Convert by creating a copy
foreach ($fileRelativePath in $SYMBOL_FILES) {
    Write-Host "Converting file $fileRelativePath"
    
    # move portable pdbs to a temp location
    Move-Item -path "$AZDS_SYMBOLS_FOLDER\$fileRelativePath" -destination "$tempDirectory"

    # Convert the portable pdbs to windows pdbs
    $fileRelativeDir = [System.IO.Path]::GetDirectoryName("$fileRelativePath")
    $fileNameWithExtension = [System.IO.Path]::GetFileName("$fileRelativePath")
    $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension("$fileRelativePath")
    &$pdb2pdb "$AZDS_SYMBOLS_FOLDER\$fileRelativeDir\$fileNameWithoutExtension.dll" /pdb "$tempDirectory\$fileNameWithExtension" /out "$AZDS_SYMBOLS_FOLDER\$fileRelativePath"
    if (!$?) {
        Write-Host "Could not convert $fileRelativePath to windows pdb."
        Remove-Item -Path "$tempDirectory\$fileNameWithExtension" -Force
        exit 2
    }

    # Remove the copied portable pdbs
    Remove-Item -Path "$tempDirectory\$fileNameWithExtension" -Force
}