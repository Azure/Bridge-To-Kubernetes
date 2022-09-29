#!/bin/bash
######                                   #######           #    #                                                               
#     # #####  # #####   ####  ######       #     ####     #   #  #    # #####  ###### #####  #    # ###### ##### ######  ####  
#     # #    # # #    # #    # #            #    #    #    #  #   #    # #    # #      #    # ##   # #        #   #      #      
######  #    # # #    # #      #####        #    #    #    ###    #    # #####  #####  #    # # #  # #####    #   #####   ####  
#     # #####  # #    # #  ### #            #    #    #    #  #   #    # #    # #      #####  #  # # #        #   #           # 
#     # #   #  # #    # #    # #            #    #    #    #   #  #    # #    # #      #   #  #   ## #        #   #      #    # 
######  #    # # #####   ####  ######       #     ####     #    #  ####  #####  ###### #    # #    # ######   #   ######  ####  

set -e

install() {
    if [ ! -d "$HOME/tmp/b2k" ]; then 
        mkdir -p "$HOME/tmp/b2k"
        chmod +x "$HOME/tmp/b2k"
        cd "$HOME/tmp/b2k"
    else 
        rm -rf "$HOME/tmp/b2k"
        rm -rf $HOME/tmp
        mkdir -p "$HOME/tmp/b2k"
        chmod +x "$HOME/tmp/b2k"
        cd "$HOME/tmp/b2k"
    fi
    
    if [[ "$OSTYPE" == "linux"* ]]; then
        curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url') 
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.osx.url')
    else 
        curl -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.win.url')
    fi
    unzip lpk*.zip
    chmod +x $HOME/tmp/b2k/*
    echo "B2k is installed in the $HOME/tmp/b2k! Enjoy!"
}


install