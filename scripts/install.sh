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
#    curl -fsSL https://raw.githubusercontent.com/Tatsinnit/Bridge-To-Kubernetes/feature/b2k-installer/scripts/install.sh | sh
set -e
set -o pipefail
set -f

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
        B2KOS="linux"
        elif [[ "$OSTYPE" == "darwin"* ]]; then
        DISTRIB_ID="$OSTYPE"
        B2KOS="osx"
        elif [[ "$OSTYPE" == "cygwin" ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "msys" ]]; then
        DISTRIB_ID="$OSTYPE"
        elif [[ "$OSTYPE" == "win32" ]]; then
        DISTRIB_ID="$OSTYPE"
        B2KOS="win"
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
    elif [[ "$DISTRIB_ID" == "win32" ]]; then
    PACKAGER=choco
else
    PACKAGER=zypper
    SYSTEMD_PATH=/usr/lib/systemd/system
fi
if [ "$PACKAGER" == "apt" ]; then
    export DEBIAN_FRONTEND=noninteractive
fi

# Check JQ Processor and download if not present
check_jq_processor_present(){
    log INFO "Checking locally installed JQ Processor version"
    jqversion=$(jq --version)
    log INFO "Locally installed JQ Processor version is $jqversion"
    if [ -z "${jqversion}" ]; then
        $PACKAGER install jq
    fi
}

# Download bridge stable version, this can be done via following command curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url')
download_bridge_stable_version(){
    log INFO "Starting B2K Download"
    CURLPROCESS=
    if [[ $OSTYPE == "linux"* ]]; then
        curl --create-dirs -# -o $HOME/tmp/bridgetokubernetes/lpk-linux.zip -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url')
        elif [[ $OSTYPE == "osx"* ]]; then
        curl -o $HOME/tmp/bridgetokubernetes -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.osx.url')
        elif [[ $OSTYPE == "win"* ]] || [[ $OSTYPE == "msys"* ]]; then
        curl -o $HOME/tmp/bridgetokubernetes -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.win.url')
    else
        log WARNING "$DISTRIB_ID not supported for $OSTYPE"
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
    unzip lpk*.zip
    if [[ ":$PATH:" == *":$HOME/.local/bin:"* ]]; then
        if [ ! -d "$HOME/.local/bin" ]; then
            mkdir -p "$HOME/.local/bin"
        fi
        mv $HOME/tmp/bridgetokubernetes/ "$HOME/.local/bin/bridgetokubernetes/"
    else
        echo "installation target directory is write protected, run as root to override"
        sudo mv $HOME/tmp/bridgetokubernetes /usr/local/bin/bridgetokubernetes
    fi
}

install() {
    if [[ "$OSTYPE" == "linux"* ]] || [[ "$OSTYPE" == "darwin"* ]]; then
        log INFO "bridge to kubernetes is supported for your platform - $OSTYPE"
    else
        log INFO "bridge to kubernetes isn't supported for your platform - $OSTYPE"
        file_issue_prompt
        exit 1
    fi
    check_jq_processor_present
    download_bridge_stable_version
    copy_b2k_files
    echo "Bridge to kubernetes installed."
}


install