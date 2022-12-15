#!/bin/ash
set -e

apk update
apk upgrade

echo "Add bash"
apk add -u bash

echo "Add procps"
apk add -u procps

echo "Add curl"
apk add -u curl

echo "Add bind-tools (dnsutils)"
apk add -u bind-tools

for pkg in "$@"
do
    echo "Add $pkg"
    apk add -u $pkg
done