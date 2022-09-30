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

log INFO "Information"

# try to get os release vars
# https://www.gnu.org/software/bash/manual/html_node/Bash-Variables.html
# https://stackoverflow.com/questions/394230/how-to-detect-the-os-from-a-bash-script
if [ -e /etc/os-release ]; then
    . /etc/os-release
    DISTRIB_ID=$ID
    DISTRIB_CODENAME=$VERSION_CODENAME
    if [ -z "$DISTRIB_CODENAME" ]; then
        if [ "$DISTRIB_ID" == "debian" ] && [ "$DISTRIB_RELEASE" == "9" ]; then
            DISTRIB_CODENAME=stretch
        fi
    fi
else
    if [ -e /etc/lsb-release ]; then
        . /etc/lsb-release
    fi
fi

if [ -z "${DISTRIB_ID}" ]; then
    log INFO "Trying to identify using OSTYPE var $OSTYPE "
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
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
if [ -z "${DISTRIB_CODENAME}" ]; then
    log WARNING "Unknown DISTRIB_CODENAME."
fi
# DISTRIB_ID=${DISTRIB_ID,,}
# DISTRIB_CODENAME=${DISTRIB_CODENAME,,}

log INFO "Distro Information as $DISTRIB_ID  - $DISTRIB_CODENAME"

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
else
    PACKAGER=zypper
    SYSTEMD_PATH=/usr/lib/systemd/system
fi
if [ "$PACKAGER" == "apt" ]; then
    export DEBIAN_FRONTEND=noninteractive
fi

log INFO "Pckager Information as $PACKAGER"

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
  log INFO "$B2KOS"
  if [ $B2KOS == "linux" ]; then
      curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url')
  elif [[ $B2KOS == "osx" ]]; then
      curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.osx.url')
  elif [ $B2KOS == "win" ]; then
      curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.win.url')
  else
    log WARNING "$DISTRIB_ID not supported for $B2KOS"
  fi
  log INFO "Finished B2K Download"
}

check_jq_processor_present
download_bridge_stable_version
