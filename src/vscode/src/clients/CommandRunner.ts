// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { ChildProcess, CommonOptions, exec, ExecOptions, spawn, SpawnOptions } from 'child_process';

import { EventSource, IReadOnlyEventSource } from '../utility/Event';

// Runs commands in new process and returns results.
export class CommandRunner {
    private readonly _outputEmitted: EventSource<string>;
    private readonly _options: CommonOptions;

    public constructor(commandEnvironmentVariables: NodeJS.ProcessEnv) {
        this._outputEmitted = new EventSource<string>();

        this._options = {
            env: commandEnvironmentVariables
        };
    }

    public get outputEmitted(): IReadOnlyEventSource<string> {
        return this._outputEmitted;
    }

    // Runs a command with args and returns the output/error result.
    // Because of limitations of child_process.spawn on Windows, this method can only be used
    // if "command" corresponds to an executable. If not, use "runThroughExecAsync" instead.
    // https://nodejs.org/api/child_process.html#child_process_spawning_bat_and_cmd_files_on_windows
    public async runAsync(
        command: string,
        args: string[],
        currentWorkingDirectory: string = null,
        customEnvironmentVariables: NodeJS.ProcessEnv = null,
        detached: boolean = true,
        quiet: boolean = false
    ): Promise<string> {
        const commandWithArgs = `${command} ${args.join(` `)}`;
        const spawnOptions: SpawnOptions = { detached: detached };
        Object.assign(spawnOptions, this._options);
        if (currentWorkingDirectory != null) {
            spawnOptions.cwd = currentWorkingDirectory;
        }
        if (spawnOptions.env != null && customEnvironmentVariables != null) {
            Object.assign(spawnOptions.env, customEnvironmentVariables);
        }

        const process: ChildProcess = spawn(command, args, spawnOptions);
        return this.handleProcessOutputAsync(process, commandWithArgs, quiet);
    }

    // "runAsync", implemented using child_process.spawn, should be the default choice for running commands.
    // However, in specific cases, running commands through child_process.exec might be needed.
    public async runThroughExecAsync(
        command: string,
        args: string[],
        currentWorkingDirectory: string = null,
        customEnvironmentVariables: NodeJS.ProcessEnv = null
    ): Promise<string> {
        const commandWithArgs = `${command} ${args.join(` `)}`;
        const execOptions: ExecOptions = this._options;
        if (currentWorkingDirectory != null) {
            execOptions.cwd = currentWorkingDirectory;
        }
        if (customEnvironmentVariables != null) {
            Object.assign(execOptions.env, customEnvironmentVariables);
        }

        const process: ChildProcess = exec(commandWithArgs, execOptions);
        return this.handleProcessOutputAsync(process, commandWithArgs);
    }

    private async handleProcessOutputAsync(process: ChildProcess, commandWithArgs: string, quiet: boolean = false): Promise<string> {
        return new Promise<string>((resolve, reject): void => {
            let outputData = ``;
            process.stdout.on(`data`, (data: any) => {
                outputData += data.toString();
                if (!quiet) {
                    this._outputEmitted.trigger(data.toString());
                }
            });

            let errorData = ``;
            process.stderr.on(`data`, (data: any) => {
                errorData += data.toString();
                this._outputEmitted.trigger(data.toString());
            });

            process.on(`error`, (error: Error) => {
                const errorMessage = `Failed to execute: ${commandWithArgs}. Error: ${error.message}`;
                errorData += errorMessage;
                this._outputEmitted.trigger(errorMessage);
                reject(new Error(errorData));
            });

            process.on(`exit`, (code: number) => {
                if (code !== 0) {
                    reject(new Error(errorData));
                    return;
                }

                resolve(outputData);
            });
        });
    }
}