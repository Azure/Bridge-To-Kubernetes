// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as assert from 'assert';

import { StringUtility } from '../../utility/StringUtility';

suite(`StringUtility Test`, () => {
    test(`compareNoCase`, () => {
        assert.strictEqual(StringUtility.compareNoCase(`hello`, `hello`), true);
        assert.strictEqual(StringUtility.compareNoCase(`hello`, `hi`), false);
        assert.strictEqual(StringUtility.compareNoCase(`hello`, undefined), false);
        assert.strictEqual(StringUtility.compareNoCase(null, `hi`), false);
        assert.strictEqual(StringUtility.compareNoCase(undefined, undefined), true);
        assert.strictEqual(StringUtility.compareNoCase(null, null), true);
        assert.strictEqual(StringUtility.compareNoCase(undefined, null), false);
        assert.strictEqual(StringUtility.compareNoCase(`hello`, `HELLO`), true);
    });

    test(`generateRoutingHeaderAsync`, async () => {
        let routingHeader = await StringUtility.generateRoutingHeaderAsync(``);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(``);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`   `);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(undefined);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`Glück`);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`Gléck`);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`こんにちわ`);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`alias@alias.al`);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`alias%alias`);
        assert.strictEqual(routingHeader.length, 13);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`myverylongusername`);
        assert.strictEqual(routingHeader.length, 13);
        assert.strictEqual(routingHeader.startsWith(`myverylo-`), true);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`alias`);
        assert.strictEqual(routingHeader.length, 10);
        assert.strictEqual(routingHeader.startsWith(`alias-`), true);

        const routingHeader2 = await StringUtility.generateRoutingHeaderAsync(`alias`);
        assert.notStrictEqual(routingHeader, routingHeader2);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(` alias `);
        assert.strictEqual(routingHeader.length, 10);
        assert.strictEqual(routingHeader.startsWith(`alias-`), true);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`ALIAS`);
        assert.strictEqual(routingHeader.length, 10);
        assert.strictEqual(routingHeader.startsWith(`alias-`), true);

        routingHeader = await StringUtility.generateRoutingHeaderAsync(`My Username`);
        assert.strictEqual(routingHeader.length, 13);
        assert.strictEqual(routingHeader.startsWith(`myuserna-`), true);
    });
});