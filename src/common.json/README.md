# Common.Json
Handles all Json serialization using Newtonsoft.Json. Referenced by `common` (and therefore most all projects).

## Why does this library exist?
Visual Studio specifies v9 of Newtonsoft.Json. However, ASP.NET Core now requires v11. Even though there were breaking changes between these versions, it turns out they are (for the most part) compatible. **As long as the compiled version metadata matches at runtime.**

So, to enable this, we have moved anything that uses Newtonsoft.Json code into isolated libraries that specify **v9**.  This allows them to compile against a _compatible_ version, to prevent `MissingMethodException`s at runtime.