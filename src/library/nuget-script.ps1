param(
    [string] $BuildNumber, 
    [string] $NugetRepoPath
)

$Packages = "..\common\bin\Debug\Microsoft.BridgeToKubernetes.Common.$BuildNumber.nupkg",
            "..\common.json\bin\Debug\Microsoft.BridgeToKubernetes.Common.Json.$BuildNumber.nupkg",
            ".\bin\Debug\Microsoft.BridgeToKubernetes.Client.$BuildNumber.nupkg",

foreach ($pkg in $Packages) {
    Write-Host "adding $pkg to $NugetRepoPath..."
    nuget add $pkg -source $NugetRepoPath 
    Write-Host "`n"
}