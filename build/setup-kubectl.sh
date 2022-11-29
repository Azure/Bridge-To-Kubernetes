#!/bin/bash
set -e

function installKubectl {
    apk update
    apk upgrade

    apk add -u kubectl --repository=http://dl-cdn.alpinelinux.org/alpine/edge/testing
}

if [ -z "$@" ]; then
    installKubectl
else
    "$@"
fi