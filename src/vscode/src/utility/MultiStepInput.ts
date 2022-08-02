/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

// From: https://github.com/microsoft/vscode-extension-samples/tree/master/quickinput-sample

import { Disposable, QuickInput, QuickInputButton, QuickInputButtons, QuickPick, QuickPickItem, window } from 'vscode';

// -------------------------------------------------------
// Helper code that wraps the API for the multi-step case.
// -------------------------------------------------------

class InputFlowAction {
    public static back = new InputFlowAction();
    public static cancel = new InputFlowAction();
    public static resume = new InputFlowAction();

    private constructor() { }
}

export interface IActionQuickPickItem extends QuickPickItem {
    action?: () => void;
}

type InputStep = (input: MultiStepInput) => Thenable<InputStep | void>;

export interface IQuickPickParameters<T extends QuickPickItem> {
    title: string;
    step: number;
    totalSteps: number;
    items: T[];
    activeItem?: T;
    placeholder: string;
    buttons?: QuickInputButton[];
    shouldResume?: () => Thenable<boolean>;
}

interface IInputBoxParameters {
    title: string;
    step: number;
    totalSteps: number;
    value: string;
    prompt: string;
    validate: (value: string) => Promise<string | undefined>;
    buttons?: QuickInputButton[];
    shouldResume?: () => Thenable<boolean>;
}

export class MultiStepInput {

    private current?: QuickInput;
    private steps: InputStep[] = [];
    private placeHolderQuickPick: QuickPick<QuickPickItem>;

    public static async runAsync<T>(start: InputStep): Promise<void> {
        const input = new MultiStepInput();
        return input.stepThrough(start);
    }

    public showPlaceHolderQuickPick(parameters: IQuickPickParameters<QuickPickItem>): void {
        if (this.placeHolderQuickPick == null) {
            this.placeHolderQuickPick = window.createQuickPick<QuickPickItem>();
        }

        this.placeHolderQuickPick.busy = true;
        this.placeHolderQuickPick.title = parameters.title;
        this.placeHolderQuickPick.step = parameters.step;
        this.placeHolderQuickPick.totalSteps = parameters.totalSteps;
        this.placeHolderQuickPick.placeholder = parameters.placeholder;
        this.placeHolderQuickPick.ignoreFocusOut = true;
        this.placeHolderQuickPick.enabled = false;
        this.placeHolderQuickPick.items = parameters.items;
        this.placeHolderQuickPick.show();
    }

    public hidePlaceHolderQuickPick(): void {
        if (this.placeHolderQuickPick !== null) {
            this.placeHolderQuickPick.hide();
            this.placeHolderQuickPick.dispose();
            this.placeHolderQuickPick = null;
        }
    }

    // tslint:disable-next-line typedef
    public async showQuickPickAsync<T extends QuickPickItem, P extends IQuickPickParameters<T>>({ title, step, totalSteps, items, activeItem, placeholder, buttons, shouldResume }: P) {
        const disposables: Disposable[] = [];
        try {
            return await new Promise<T | (P extends { buttons: (infer I)[] } ? I : never)>((resolve, reject): void => {
                const input = window.createQuickPick<T>();
                input.title = title;
                input.step = step;
                input.totalSteps = totalSteps;
                input.placeholder = placeholder;
                input.ignoreFocusOut = true;
                input.items = items;
                if (activeItem != null) {
                    input.activeItems = [ activeItem ];
                }
                input.buttons = [
                    ...(this.steps.length > 1 ? [ QuickInputButtons.Back ] : []),
                    ...(buttons != null ? buttons : [])
                ];
                disposables.push(
                    input.onDidTriggerButton(item => {
                        if (item === QuickInputButtons.Back) {
                            reject(InputFlowAction.back);
                        }
                        else {
                            resolve(item as any);
                        }
                    }),
                    input.onDidChangeSelection(items => {
                        const actionItem: IActionQuickPickItem = items[0] as IActionQuickPickItem;
                        if (actionItem != null && actionItem.action != null) {
                            actionItem.action();
                            reject(InputFlowAction.resume);
                        }
                        else {
                            resolve(items[0]);
                        }
                    }),
                    input.onDidHide(() => {
                        (async (): Promise<void> => {
                            reject(shouldResume != null && await shouldResume() ? InputFlowAction.resume : InputFlowAction.cancel);
                        })()
                            .catch(reject);
                    })
                );
                if (this.current != null) {
                    this.current.dispose();
                }
                this.current = input;
                this.hidePlaceHolderQuickPick();
                this.current.show();
            });
        }
        finally {
            disposables.forEach(d => d.dispose());
        }
    }

    // tslint:disable-next-line typedef
    public async showInputBox<P extends IInputBoxParameters>({ title, step, totalSteps, value, prompt, validate, buttons, shouldResume }: P) {
        const disposables: Disposable[] = [];
        try {
            return await new Promise<string | (P extends { buttons: (infer I)[] } ? I : never)>((resolve, reject): any => {
                const input = window.createInputBox();
                input.title = title;
                input.step = step;
                input.totalSteps = totalSteps;
                input.value = value != null ? value : ``;
                input.prompt = prompt;
                input.buttons = [
                    ...(this.steps.length > 1 ? [ QuickInputButtons.Back ] : []),
                    ...(buttons != null ? buttons : [])
                ];
                let validating = validate(``);
                disposables.push(
                    input.onDidTriggerButton(item => {
                        if (item === QuickInputButtons.Back) {
                            reject(InputFlowAction.back);
                        }
                        else {
                            resolve(item as any);
                        }
                    }),
                    input.onDidAccept(async () => {
                        const value = input.value;
                        input.enabled = false;
                        input.busy = true;
                        const validationError = await validate(value);
                        if (validationError == null || validationError.length < 1) {
                            resolve(value);
                        }
                        input.enabled = true;
                        input.busy = false;
                    }),
                    input.onDidChangeValue(async text => {
                        const current = validate(text);
                        validating = current;
                        const validationMessage = await current;
                        if (current === validating) {
                            input.validationMessage = validationMessage;
                        }
                    }),
                    input.onDidHide(() => {
                        (async (): Promise<void> => {
                            reject(shouldResume != null && await shouldResume() ? InputFlowAction.resume : InputFlowAction.cancel);
                        })()
                            .catch(reject);
                    })
                );
                if (this.current != null) {
                    this.current.dispose();
                }
                this.current = input;
                this.current.show();
            });
        }
        finally {
            disposables.forEach(d => d.dispose());
        }
    }

    private async stepThrough<T>(start: InputStep): Promise<void> {
        let step: InputStep | void = start;
        // tslint:disable-next-line strict-boolean-expressions
        while (step) {
            this.steps.push(step);
            if (this.current != null) {
                this.current.enabled = false;
                this.current.busy = true;
            }
            try {
                step = await step(this);
            }
            catch (err) {
                if (err === InputFlowAction.back) {
                    this.steps.pop();
                    step = this.steps.pop();
                }
                else if (err === InputFlowAction.resume) {
                    step = this.steps.pop();
                }
                else if (err === InputFlowAction.cancel) {
                    step = undefined;
                }
                else {
                    this.hidePlaceHolderQuickPick();
                    throw err;
                }
            }
        }
        if (this.current != null) {
            this.current.dispose();
        }
    }
}