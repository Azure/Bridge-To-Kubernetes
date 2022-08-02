// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as path from 'path';
import { runTests } from 'vscode-test';

async function main(): Promise<void> {
    try {
        // The folder containing the Extension Manifest package.json.
        const extensionDevelopmentPath: string = path.resolve(__dirname, `../../`);

        // The path to the extension test script.
        const extensionTestsPath: string = path.resolve(__dirname, `./TestsRunner`);

        // Download VS Code, unzip it and run the tests.
        // Using old version of vscode instead of 1.47 to run the tests, as 1.47 version has issues.
        await runTests({ version: `1.46.1`, extensionDevelopmentPath, extensionTestsPath });
    }
    catch (error) {
        // tslint:disable-next-line: no-console
        console.error(`Failed to run tests`);
        process.exit(1);
    }
}

main();