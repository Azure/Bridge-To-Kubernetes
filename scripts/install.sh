#!/bin/bash
# Bridge to kubernetes installer
#
#  ____       _     _              _          _  __     _                          _
# | __ ) _ __(_) __| | __ _  ___  | |_ ___   | |/ /   _| |__   ___ _ __ _ __   ___| |_ ___  ___
# |  _ \| '__| |/ _` |/ _` |/ _ \ | __/ _ \  | ' / | | | '_ \ / _ \ '__| '_ \ / _ \ __/ _ \/ __|
# | |_) | |  | | (_| | (_| |  __/ | || (_) | | . \ |_| | |_) |  __/ |  | | | |  __/ ||  __/\__ \
# |____/|_|  |_|\__,_|\__, |\___|  \__\___/  |_|\_\__,_|_.__/ \___|_|  |_| |_|\___|\__\___||___/
#                    |___/
# usage:
#    curl -fsSL https://raw.githubusercontent.com/Azure/Bridge-To-Kubernetes/main/scripts/install.sh | bash
set -ef

log() {
    local level=$1
    shift
    echo "$(date -u $now) - $level - $*"
}

# dump uname immediately
uname -ar

log INFO "Information logged for bridge to kubernetes."

# Try to get os release vars
# https://www.gnu.org/software/bash/manual/html_node/Bash-Variables.html
# https://stackoverflow.com/questions/394230/how-to-detect-the-os-from-a-bash-script
if [ -e /etc/os-release ]; then
    . /etc/os-release
    DISTRIB_ID=$ID
else
    if [ -e /etc/lsb-release ]; then
        . /etc/lsb-release
    fi
fi

if [ -z "${DISTRIB_ID}" ]; then
    log INFO "Trying to identify using OSTYPE var $OSTYPE "
    if [[ "$OSTYPE" == "linux"* ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "darwin"* ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "cygwin" ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "msys" ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "win32" ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "freebsd"* ]]; then
        DISTRIB_ID="$OSTYPE"
    else
        log ERROR "Unknown DISTRIB_ID or DISTRIB_RELEASE."
        exit 1
    fi
fi

if [ -z "${DISTRIB_ID}" ]; then
    log ERROR "Unknown DISTRIB_ID or DISTRIB_RELEASE."
    exit 1
fi

log INFO "Distro Information as $DISTRIB_ID"

# set distribution specific vars
PACKAGER=
SYSTEMD_PATH=/lib/systemd/system
if [ "$DISTRIB_ID" == "ubuntu" ]; then
    PACKAGER=apt
    elif [ "$DISTRIB_ID" == "debian" ]; then
    PACKAGER=apt
    elif [[ $DISTRIB_ID == centos* ]] || [ "$DISTRIB_ID" == "rhel" ]; then
    PACKAGER=yum
    elif [[ "$DISTRIB_ID" == "darwin"* ]]; then
    PACKAGER=brew
    elif [[ "$DISTRIB_ID" == "win32" ]] || [[ "$DISTRIB_ID" == "msys" ]]; then
    PACKAGER=choco
else
    PACKAGER=zypper
    SYSTEMD_PATH=/usr/lib/systemd/system
fi
if [ "$PACKAGER" == "apt" ]; then
    export DEBIAN_FRONTEND=noninteractive
fi

# Check JQ Processor and download if not present
check_jq_processor_present() {
    check_if_exists jq
    if [[ $result != 0 ]]; then
        install_tool jq
    fi
    jqversion=$(jq --version)
    log INFO "Locally installed JQ Processor version is $jqversion"
}

check_kubectl_present() {
    check_if_exists kubectl
    if [[ $result != 0 ]]; then
        install_tool kubectl
    fi
    kubectlversion=$(kubectl version --client=true -o json | jq ".clientVersion.gitVersion")
    log INFO "Locally installed kubectl version is $kubectlversion"
}

check_dotnet_runtime_present() {
    check_if_exists dotnet
    # if dotnet doesn't exist install it
    if [[ $result != 0 ]]; then
        install_tool dotnet
        return;
    fi
    #if dotnet exists, check the version required for b2k and install it.
    dotnetruntimes=$(dotnet --list-runtimes)
    if [[ -z "${dotnetruntimes}" ]] || ! ([[ "${dotnetruntimes}" =~ '7.0'* ]] && [[ "${dotnetruntimes}" =~ 'AspNetCore'* ]]); then
        install_tool dotnet
    else 
        log INFO "dotnet version is $(dotnet --version)"
    fi
}

install_tool() {
    log INFO "installing $1.."

    case $1 in 

        kubectl)
            install_pre_requirements_kubectl
            install_with_sudo kubectl
            ;;
        dotnet)
            if [[ $OSTYPE == "darwin"* ]]; then
                arch=$(uname -m)
                if [[ "$arch" == 'arm64' ]]; then
                    install_dotnet_x64_for_arm
                else 
                    $PACKAGER tap isen-ng/dotnet-sdk-versions
                    install_with_sudo dotnet-sdk7-0-300 --cask
                fi 
            elif [[ $OSTYPE == "linux"* ]]; then
                install_with_sudo dotnet-sdk-7.0
            else 
                install_with_sudo dotnet-7.0-sdk -y
            fi
            ;;
        jq)
            install_with_sudo jq
            ;;
        *)
            log INFO "Unknown option for install $1"
            exit 1
            ;;
    esac
}

install_pre_requirements_kubectl() {
    if [[ $OSTYPE == "linux"* ]]; then
        #add google packages to install kubectl or else apt install kubectl will give error kubectl not found.
        sudo $PACKAGER update
        sudo $PACKAGER install -y ca-certificates curl
        sudo $PACKAGER install -y apt-transport-https
        curl -fsSL https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo gpg --dearmor -o /etc/apt/keyrings/kubernetes-archive-keyring.gpg
        echo "deb [signed-by=/etc/apt/keyrings/kubernetes-archive-keyring.gpg] https://apt.kubernetes.io/ kubernetes-xenial main" | sudo tee /etc/apt/sources.list.d/kubernetes.list
        sudo $PACKAGER update
    fi
}

install_dotnet_x64_for_arm() {
    log INFO "downloading and installing dotnet x64 binaries in arm machines"
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version 7.0.306 --arch x64
    if [[ ! -d /usr/local/share/dotnet ]] || [[ ! -d /usr/local/share/dotnet/x64 ]]; then
        sudo mkdir -p /usr/local/share/dotnet/x64
    fi
    sudo cp -r "$HOME/.dotnet/" /usr/local/share/dotnet/x64/
    export PATH="/usr/local/share/dotnet/x64/*":$PATH
}

install_with_sudo() {
    if [[ $OSTYPE == "linux"* ]]; then
        sudo $PACKAGER install $1 -y
    elif [[ $OSTYPE == "darwin"* ]]; then
        $PACKAGER install $2 $1
    else 
        $PACKAGER install $1 $2
    fi
}

check_if_exists() {
    log INFO "checking if $1 exists"
    if ! [[ -x "$(command -v $1)" ]]; then
        log INFO "Error: $1 is not installed." >&2
        result=1
    else 
        result=0
    fi
}

# Download bridge stable version, this can be done via following command curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url')
download_bridge_stable_version() {
    log INFO "Starting B2K Download"
    CURLPROCESS=
    if [[ $OSTYPE == "linux"* ]]; then
        curl --create-dirs -# -o $HOME/tmp/bridgetokubernetes/lpk-linux.zip -LO $(curl -L -s https://aka.ms/bridge-lks-v2 | jq -r '.linux.bridge.url')
        elif [[ $OSTYPE == "darwin"* ]]; then
        curl --create-dirs -o $HOME/tmp/bridgetokubernetes/lpk-osx.zip -LO $(curl -L -s https://aka.ms/bridge-lks-v2 | jq -r '.osx.bridge.url')
        elif [[ $OSTYPE == "win"* ]] || [[ $OSTYPE == "msys"* ]]; then
        curl --create-dirs -o $HOME/tmp/bridgetokubernetes/lpk-win.zip -LO $(curl -L -s https://aka.ms/bridge-lks-v2 | jq -r '.win.bridge.url')
    else
        log WARNING "$DISTRIB_ID not supported for $OSTYPE"
        exit 1
    fi
    chmod +x $HOME/tmp/bridgetokubernetes
    log INFO "Finished B2K download complete."
}

file_issue_prompt() {
    echo "If you wish us to support your platform, please file an issue"
    echo "https://github.com/Azure/Bridge-To-Kubernetes/issues/new/choose"
    exit 1
}

copy_b2k_files() {
    cd $HOME/tmp/bridgetokubernetes
    unzip -o $HOME/tmp/bridgetokubernetes/*.zip
    if [[ ":$PATH:" == *":$HOME/.local/bin:"* ]] || [[ $OSTYPE == "msys"* ]]; then
        if [ ! -d "$HOME/.local/bin" ]; then
            mkdir -p "$HOME/.local/bin"
        fi
        chmod -R +x "$HOME/.local/bin/"
        installdir=$HOME/.local/bin/bridgetokubernetes
        remove_tmp_dirs $installdir
        cp -r $HOME/tmp/bridgetokubernetes/ $installdir
        chmod -R +x $installdir/dsc $installdir/kubectl $installdir/EndpointManager/EndpointManager
        create_sym_link $installdir/dsc $HOME/.local/bin/dsc
    else
        log WARNING "installation target directory is write protected, run as root to override"
        installdir=/usr/local/bin/bridgetokubernetes
        remove_tmp_dirs $installdir sudo
        sudo cp -r $HOME/tmp/bridgetokubernetes/  $installdir
        sudo chmod -R +x $installdir/dsc $installdir/kubectl $installdir/EndpointManager/EndpointManager
        create_sym_link $installdir/dsc /usr/local/bin/dsc sudo
    fi
    cd ~
    remove_tmp_dirs $HOME/tmp/bridgetokubernetes
}

remove_tmp_dirs() {
    log INFO "removing directory:$1"
    if [ -d $1 ]; then
        $2 rm -rf $1
    fi
}

create_sym_link() {
    log INFO "creating or overwriting sym link for :$1"
    # ln -sf source destination - creates symlink for dsc command to run from anywhere ex: any folder or location in the file system.
    $3 ln -sf $1 $2
}

install() {
    if [[ "$OSTYPE" == "linux"* ]] || [[ "$OSTYPE" == "darwin"* ]] || [[ "$OSTYPE" == "msys"* ]]; then
        log INFO "bridge to kubernetes is supported for your platform - $OSTYPE"
    else
        log INFO "bridge to kubernetes isn't supported for your platform - $OSTYPE"
        file_issue_prompt
        exit 1
    fi
    check_jq_processor_present
    check_kubectl_present
    check_dotnet_runtime_present
    download_bridge_stable_version
    copy_b2k_files
    echo "Bridge to kubernetes installed in $installdir"
}


install
