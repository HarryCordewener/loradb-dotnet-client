# License

This package contains code under two different licenses.

## MIT — managed code authored by this project

All C# source and the compiled managed assemblies (`lib/**`) are licensed under
the MIT License.

```
MIT License

Copyright (c) 2026 Harry Cordewener

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Business Source License 1.1 — bundled LoraDB native libraries

The native libraries under `runtimes/*/native/` (`lora_ffi.dll`,
`liblora_ffi.so`, `liblora_ffi.dylib`) are **not** MIT. They are compiled from
[LoraDB](https://github.com/lora-db/lora), copyright LoraDB, Inc., which is
licensed under the Business Source License 1.1 (SPDX: `BUSL-1.1`).

Key parameters as published by LoraDB, Inc.:

- Licensor: LoraDB, Inc.
- Licensed Work: LoraDB
- Change Date: 2029-04-19
- Change License: Apache License, Version 2.0
- Additional Use Grant: permits internal-business and non-production use,
  but does not permit using the Licensed Work to offer, operate, or make
  available a database-as-a-service, hosted API, managed database platform,
  or any substantially similar hosted service for third parties.

BUSL-1.1 is not an OSI-approved open source license, and its restrictions
travel with the binaries when you redistribute this package.

The complete, verbatim license text is included in this package as
`THIRD-PARTY-NOTICES.md`. Read it before use.
