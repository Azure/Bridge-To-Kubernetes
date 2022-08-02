#!/bin/bash
set -e

DEFAULT_INSTALL_LOCATION="/app/kubectl/linux"
DEFAULT_KUBECTL_VERSION="v1.21.2"

# Arguments: kubectl version, install location
function installKubectl {
    local loc_kube_version=$1
    local loc_setup_location=$2

    if [ -z "$loc_kube_version" ]; then
        loc_kube_version=$DEFAULT_KUBECTL_VERSION
    fi

    if [ -z "$loc_setup_location" ]; then
        loc_setup_location=$DEFAULT_INSTALL_LOCATION
    fi

    echo "Setting up kubectl $loc_kube_version in $loc_setup_location"
    curl -LO https://storage.googleapis.com/kubernetes-release/release/${loc_kube_version}/bin/linux/amd64/kubectl
    chmod +x kubectl
    mkdir -p $loc_setup_location
    mv kubectl $loc_setup_location
}

if [ -z "$@" ]; then
    installKubectl
else
    "$@"
fi