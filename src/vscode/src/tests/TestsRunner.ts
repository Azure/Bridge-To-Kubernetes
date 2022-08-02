// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as glob from 'glob-promise';
import * as Mocha from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';

export async function run(): Promise<void> {
    const trxFileOutput: string = path.resolve(__dirname, `vscode-unit-tests.trx`);

    // Create the mocha test.
    const mocha = new Mocha({
        ui: `tdd`,
        reporter: `mocha-trx-reporter`,
        reporterOptions: {
            output: trxFileOutput
        },
        timeout: 2000
    });
    mocha.useColors(true);

    const testsRoot: string = path.resolve(__dirname, `..`);

    const files: string[] = await glob(`**/**-Test.js`, { cwd: testsRoot });

    // Add files to the mocha test.
    files.forEach(file => mocha.addFile(path.resolve(testsRoot, file)));

    vscode.window.showInformationMessage(`Tests run started`);
    // Run the mocha test.
    // tslint:disable: no-console
    mocha.run((failures: number) => {
        console.log(`Test results: ${trxFileOutput}`);
        if (failures > 0) {
            throw new Error(`${failures} tests failed.`);
        }

        console.log(`All tests passed successfully.`);
    }).on(`pass`, (test: Mocha.Test) => {
        console.log(`[Passed] ${test.parent.title}: ${test.title}`);
    }).on(`fail`, (test: Mocha.Test, error: any) => {
        console.error(`[Failed] ${test.parent.title}: ${test.title}\n${error}`);
    });
}