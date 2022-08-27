#!/bin/bash
set -e

function installKubectl {
    apk update
    apk upgrade

    apk add -u kubectl
}

if [ -z "$@" ]; then
    installKubectl
else
    "$@"
fi