#!/bin/bash
set -e

DEFAULT_KUBECTL_VERSION="v1.21.2"

# Arguments: kubectl version
function installKubectl {
    local loc_kube_version=$1

    if [ -z "$loc_kube_version" ]; then
        loc_kube_version=$DEFAULT_KUBECTL_VERSION
    fi

    apk update
    apk upgrade

    apk add -u kubectl=${loc_kube_version}
}

if [ -z "$@" ]; then
    installKubectl
else
    "$@"
fi