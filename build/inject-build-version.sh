#!/bin/bash
if [ -z "$1" ]; then
    echo 'Please specify the imageTag/buildId'.
    exit 1
fi

buildVersion=$1
sudo apt-get install -f rpl || true

rpl -qf '$BUILD_VERSION$' $buildVersion ../src/execsvc/appsettings.json &> /dev/null