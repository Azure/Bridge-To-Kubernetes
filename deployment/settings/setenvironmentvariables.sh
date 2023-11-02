#!/bin/bash
#If you are changing this file, make sure to change the Powershell script as well

JQ=$(which jq)
if [[ -z "$JQ" ]];
then
	sudo apt-get -y install jq
fi

settingsFile=$1
keyVault=$2
set -e

function set_BashEnvVariable() {
	echo export $1=$2
	local tmp=$2
	export $1="${tmp:q}"
}

function set_VstsEnvVariable() {
	vstsEnvValue="$2"
	echo export $1=$vstsEnvValue
	echo "##vso[task.setvariable variable=$1]$vstsEnvValue"
}

function set_BashEnvSecretVariable() {
	echo export $1=*****
	local tmp=$2
	export $1="${tmp:q}"
}

function set_VstsEnvSecretVariable() {
	vstsEnvValue="$2"
	echo export $1=*****
	echo "##vso[task.setvariable variable=$1;issecret=true]$vstsEnvValue"
}

function set_Variable() {
	validateVariableName "$1"
	if [[ -z "$AGENT_ID" ]];
	then
		set_BashEnvVariable "$@"
	else
		set_VstsEnvVariable "$@"
	fi
}

function set_SecretVariable() {
	validateVariableName "$1"
	if [[ -z "$AGENT_ID" ]];
	then
		set_BashEnvSecretVariable "$@"
	else
		set_VstsEnvSecretVariable "$@"
	fi
}

function validateVariableName() {
	if ! [[ "$1" =~ ^[a-zA-Z_][a-zA-Z0-9_]*$ ]]
    then
        echo "$1: invalid variable name."
        exit 1
    fi
}

function read_Setting() {
	local settingValue="$1"
	local parentSettingsFile=$2
	local deploymentKeyVault=$3
	key=$(cut -d '=' -f 1 <<< "$settingValue" | tr -d '[:space:]')
	value=$(cut -d '=' -f 2- <<< "$settingValue" | tr -d '[:space:]')
	if [ $key == InheritFrom ]
	then
		source $BASH_SOURCE $(dirname "${parentSettingsFile}")/$value $deploymentKeyVault
		# set settingsFile back to parentSettingsFile
		settingsFile=$parentSettingsFile
	elif [[ $value == vault:///* ]]	
	then
		vault=(${value//:\/\/\// })
		secretValue=$(az keyvault secret show --vault-name "$deploymentKeyVault" -n ${vault[1]} -o json)
		set_SecretVariable $key "$(echo $secretValue | jq -r '.value' )"
	elif [[ $value == vault://* ]]	
	then
		vault=(${value//\/\// })
		secretValue=$(az keyvault secret show --vault-name ${vault[1]} -n ${vault[2]} -o json)
		set_SecretVariable $key "$(echo $secretValue | jq -r '.value' )"
	else 
		set_Variable $key "$value"
	fi
}

while IFS='' read -r setting || [[ -n "$setting" ]]
do
	if [ "$setting" ]
	then
		read_Setting "$setting" $settingsFile $keyVault
	fi
done < "$settingsFile"