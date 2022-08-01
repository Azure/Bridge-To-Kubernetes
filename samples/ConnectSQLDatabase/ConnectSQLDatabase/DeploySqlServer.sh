#!/bin/bash
# This script was sourced from: https://docs.microsoft.com/en-us/azure/azure-sql/database/scripts/create-and-configure-database-cli

location="East US"
randomIdentifier=bridgetest123

resource="SqlServerRG"
server="server-$randomIdentifier"
database="database-$randomIdentifier"

login="sampleLogin"
password="PLACEHOLDER"

startIP=0.0.0.0
endIP=0.0.0.0

echo "Creating $resource..."
az group create --name $resource --location "$location"

echo "Creating $server in $location..."
az sql server create --name $server --resource-group $resource --location "$location" --admin-user $login --admin-password $password

echo "Configuring firewall..."
az sql server firewall-rule create --resource-group $resource --server $server -n AllowAzureTraffic --start-ip-address $startIP --end-ip-address $endIP

echo "Updating server connection policy..."
az sql server conn-policy update --connection-type Proxy --resource-group $resource --server $server

echo "Creating $database on $server..."
az sql db create --resource-group $resource --server $server --name $database --sample-name AdventureWorksLT --edition GeneralPurpose --family Gen5 --capacity 2 --zone-redundant false 