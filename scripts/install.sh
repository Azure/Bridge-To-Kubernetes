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
    else 
        echo "permission"
        chmod +x "$HOME/tmp/b2k"
    fi
    
    if [[ "$OSTYPE" == "linux"* ]]; then
        curl -o "$HOME/tmp/b2k" -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.linux.url')
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        curl -o "$HOME/tmp/b2k" -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.osx.url')
    else 
        curl -o "$HOME/tmp/b2k" -LO $(curl -L -s https://aka.ms/bridge-lks | jq -r '.win.url')
    fi
    unzip lpk-win.zip
    echo "B2k is installed ! Enjoy!"
}


install