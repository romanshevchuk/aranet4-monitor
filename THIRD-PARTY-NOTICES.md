# Third-Party Notices

## Aranet4-Python protocol reference

This application references the Aranet4 BLE protocol documentation and implementation in [Aranet4-Python](https://github.com/Anrijs/Aranet4-Python), by Anrijs Jargans and contributors. The reference version reviewed for this notice is [v2.6.0](https://github.com/Anrijs/Aranet4-Python/tree/414079ccb3abc6baf95ce32a919a6be3226d6c39).

Relevant references:

- [GATT UUID and packet-format documentation](https://github.com/Anrijs/Aranet4-Python/blob/414079ccb3abc6baf95ce32a919a6be3226d6c39/docs/UUIDs.md)
- [Protocol implementation](https://github.com/Anrijs/Aranet4-Python/blob/414079ccb3abc6baf95ce32a919a6be3226d6c39/aranet4/client.py)
- [Upstream MIT license](https://github.com/Anrijs/Aranet4-Python/blob/414079ccb3abc6baf95ce32a919a6be3226d6c39/LICENSE)

The C# BLE implementation in `BleListener/Bluetooth` is a separate implementation. It uses protocol identifiers, command values, and packet layouts as interoperability facts; this repository does not bundle the upstream Python package or its source files. No verbatim upstream source was identified in the files reviewed.

The upstream project is licensed under the MIT License, Copyright (c) 2022 Anrijs Jargans. That license applies to the upstream project and any material from it that is actually redistributed; it does not license Aranet hardware, trademarks, or patents, and it is not the license for this application.