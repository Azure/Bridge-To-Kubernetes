#!/bin/bash
set -e

# Default values for kubectl version, install location, and architecture
DEFAULT_KUBECTL_VERSION="v1.27.3"
DEFAULT_INSTALL_LOCATION="/app/kubectl/linux"
DEFAULT_ARCH="amd64"

# Function to install kubectl
# Arguments: kubectl version, install location, architecture
function install_kubectl {
    # Set default values if arguments are not provided
    kubectl_version="${1:-$DEFAULT_KUBECTL_VERSION}"
    install_location="${2:-$DEFAULT_INSTALL_LOCATION}"
    arch="${3:-$DEFAULT_ARCH}"

    echo "Setting up kubectl $kubectl_version in $install_location with arch $arch"    
    curl -LO "https://dl.k8s.io/release/$kubectl_version/bin/linux/$arch/kubectl"
    chmod +x kubectl
    mkdir -p "$install_location"
    mv kubectl "$install_location"
}

kubectl_version=$1
install_location=$2
arch=$3
install_kubectl "$kubectl_version" "$install_location" "$arch"