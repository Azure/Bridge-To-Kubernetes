// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Mock } from 'typemoq';
import * as vscode from 'vscode';

import { Logger } from '../logger/Logger';
import { AccountContextManager } from '../models/context/AccountContextManager';

export const accountContextManagerMock = Mock.ofType<AccountContextManager>();
export const loggerMock = Mock.ofType<Logger>();
export const defaultWorkspaceFolder: vscode.WorkspaceFolder = { index: 0, name: `mywebapi`, uri: vscode.Uri.parse(`file://C:/projects/mywebapi`) };