# VS Code Extension

Running the extension from the dev box
--------------------------------------
1. Open `src\vscode` in VS Code

2. In the terminal window run the following:
    - `npm install` to install dependencies
    - `npm run-script compile`

3. Press F5. This will open a new VS Code window with the extension installed. Then you can open the app you want to debug and press F5 to debug that.

Generating the VSIX binaries
----------------------------
To generate the mindaro-*.vsix file locally use `npm run-script vscode:package`. The vsix file will be generated in `src/vscode`

Testing with local CLI builds
----------------------------
To use a local build of the CLI in the VS Code Extension (instead of the automatically downloaded CLI), set the following environment variable before opening VS Code:
```
PS C:\...\Mindaro-Connect\src\vscode> $env:BRIDGE_BUILD_PATH="C:\...\Mindaro-Connect\src\bridge\bin\Debug\netcoreapp3.1\win-x64\publish"
PS C:\...\Mindaro-Connect\src\vscode> code .
```

Testing with dev/staging CLI builds
----------------------------
Set the following environment variable before opening VS Code:
```
PS C:\...\Mindaro-Connect\src\vscode> $env:BRIDGE_ENVIRONMENT="staging"
PS C:\...\Mindaro-Connect\src\vscode> code .
```

For dev environment, just use `$env:BRIDGE_ENVIRONMENT="dev"` instead.

It is not possible to test pre-prod builds through this, as we don't generate binaries usable by VS Code for pre-prod.

Test scenarios
--------------
Manual tests instructions: [../../tests/manual/BridgeToKubernetes.md](../../tests/manual/BridgeToKubernetes.md)

Run unit-tests:
1. From VS Code:
    * In the Debug panel, select `Extension Tests`
    * Click the `Start Debugging` button.
    * The test results will be displayed in the Debug Console.

2. From a command line prompt:
    * Make sure that no VS Code instance is running! The tests execution will fail until they are all closed.
    * Navigate to the `\Mindaro\src\vscode` folder.
    * Run `npm run test`
    * The test results will be displayed in the command line prompt.

Debug
-----
1. Build the code. Run (current folder):
    * `npm install`
    * `npm run-script compile`
2. F5

Run the linter
--------------
A linter is a tool that validates that the code is consistent with good practices and conventions.

See linter errors while coding:
1. Install the TSLint (ms-vscode.vscode-typescript-tslint-plugin) extension. Errors will appear in your "Problems" window whenever you open a file.

Run the linter through a script:
1. Run in current folder: `npm run lint`
2. To use the linter's autofix capabilities, run: `npm run fixlint`

Resources
---------
Documentation for building VS Code extensions: https://code.visualstudio.com/api