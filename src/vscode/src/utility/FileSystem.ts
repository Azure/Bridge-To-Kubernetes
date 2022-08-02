// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as fs from 'fs';
import * as glob from 'glob-promise';
import * as util from 'util';

class FileSystem {
    public accessAsync = util.promisify(fs.access);
    public globAsync = glob;
    public mkdirAsync = util.promisify(fs.mkdir);
    public readDirAsync = util.promisify(fs.readdir);
    public readFileAsync = util.promisify(fs.readFile);
    public statAsync = util.promisify(fs.stat);
    public rmdirAsync = util.promisify(fs.rmdir);
    public copyFileAsync = util.promisify(fs.copyFile);

    public existsAsync = async (path: fs.PathLike): Promise<boolean> => {
        try {
            await this.statAsync(path);
            return true;
        }
        catch (error) {
            if (error.code === `ENOENT`) {
                return false;
            }
            throw error;
        }
    }
}

export const fileSystem = new FileSystem();