#If you are changing this file, make sure to change the bash script as well

Param(
    [ValidateNotNullOrEmpty()]
    [string]$settingsFile,
    [ValidateNotNullOrEmpty()]
    [string]$keyVault
)

# Current settings file path
$settingsFilePath = Split-Path -Path $settingsFile

$ErrorActionPreference = "Stop"

function Set-EnvironmentVariable {
    Param(
        [string]$environmentVariableKey,
        [string]$environmentVariableValue
    )

    $environmentVariableKey = $environmentVariableKey.Trim()
    $environmentVariableValue = $environmentVariableValue.Trim()

    Set-Item -LiteralPath Env:$environmentVariableKey -Value $environmentVariableValue
    Write-Host $environmentVariableKey = (Get-Childitem -LiteralPath Env:$environmentVariableKey).Value
    if (Test-Path Env:AGENT_ID) {
        Write-Host "##vso[task.setvariable variable=$environmentVariableKey]$environmentVariableValue"
    }
}

function Set-EnvironmentVariableSecret {
    Param(
        [string]$environmentVariableKey,
        [string]$environmentVariableValue
    )

    $environmentVariableKey = $environmentVariableKey.Trim()
    $environmentVariableValue = $environmentVariableValue.Trim()

    Set-Item -LiteralPath Env:$environmentVariableKey -Value $environmentVariableValue
    Write-Host $environmentVariableKey = "*****"
    if (Test-Path Env:AGENT_ID) {
        Write-Host "##vso[task.setvariable variable=$environmentVariableKey;issecret=true]$environmentVariableValue"
    }
}

# Read settings file
foreach ($line in Get-Content $settingsFile) {
    
    # Get the property name and value
    $keyValuePair = ConvertFrom-StringData($line)
    
    # If property name is InheritFrom, read data from parent settings file
    if ($keyValuePair.Keys[0] -eq "InheritFrom") {
        
        # Get parent settings file path
        $parentSettingsFile = (Join-Path -Path $settingsFilePath -ChildPath $keyValuePair.Values[0])        
        
        # Call the same script with parent settings file
        & $MyInvocation.MyCommand.Path -settingsFile $parentSettingsFile -keyVault $keyVault

    } elseif ($keyValuePair.Values[0].StartsWith("vault:///")) {

        # Get the key vault key
        if (([string]$keyValuePair.Values[0]) -match 'vault:///(\w+)') {
            $keyVaultKey = $matches[1]

            # Call az CLI to get the key value
            $key = [string]$keyValuePair.Keys[0]
            $secretValue = (Get-AzureKeyVaultSecret -VaultName $keyVault -name $keyVaultKey).SecretValueText

            Set-EnvironmentVariableSecret $key $secretValue
        }

    } elseif ($keyValuePair.Values[0].StartsWith("vault://")) {

        # This key requires a different keyvault, extract both keyvault name and key
        if (([string]$keyValuePair.Values[0]) -match 'vault://(\w+)//(\w+)') {
            $customKeyVault = $matches[1]
            $keyVaultKey = $matches[2]

            # Call az CLI to get the key value
            $key = [string]$keyValuePair.Keys[0]
            $secretValue = (Get-AzureKeyVaultSecret -VaultName $customKeyVault -name $keyVaultKey).SecretValueText

            Set-EnvironmentVariableSecret $key $secretValue
        }

    } else {

        $key = [string]$keyValuePair.Keys[0]
        $value = [string]$keyValuePair.Values[0]

        # Set environment variable
        Set-EnvironmentVariable $key $value
    }
}

Set-EnvironmentVariable "BuildNumber" $Env:BUILD_BUILDNUMBER
