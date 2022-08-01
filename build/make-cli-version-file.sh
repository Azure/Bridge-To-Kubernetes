#!/bin/bash
set -e
if [ -z "$1" ]; then
    echo 'Please specify the CLI version number'
    exit 1
fi

if [ -z "$2" ]; then
    echo 'Please specify the file name'
    exit 1
fi

if [ -z "$3" ]; then
    echo 'Please specify the file path'
    exit 1
fi

buildNumber=$1
fileName=$2
filePath=$3

echo "Creating file"
# Make sure the folder exists
if [[ ! -d $filePath ]]; then
    echo "Creating folder"
    mkdir -p $filePath || true
fi
echo $buildNumber > $filePath/$fileName