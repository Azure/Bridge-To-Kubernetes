// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as crypto from 'crypto';

export class StringUtility {
    // Performs a case-insensitive comparison of two strings.
    public static compareNoCase(a: string, b: string): boolean {
        if (a == null || b == null) {
            return a === b;
        }

        return a.toUpperCase() === b.toUpperCase();
    }

    // Generates a routing header, in a format similar to "username-1234".
    public static async generateRoutingHeaderAsync(username: string): Promise<string> {
        if (username == null) {
            username = ``;
        }

        // Remove any spaces from the username.
        username = username.replace(/\s/g, ``);

        // Make sure we only use lowercase characters.
        username = username.toLowerCase();

        // Make sure that the username exists and only contains ASCII characters.
        if (username.length === 0 || /[^\u0000-\u007f]/.test(username)) {
            username = crypto.createHash(`md5`).update(username).digest(`hex`);
        }

        // Make sure we only take the first 8 characters of the username.
        username = username.slice(0, 8);

        // tslint:disable-next-line: prefer-array-literal
        const randomSuffix: string = [ ...Array(4) ].map(() => Math.floor(Math.random() * 16).toString(16)).join(``);

        return `${username}-${randomSuffix}`;
    }
}