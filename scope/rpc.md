# RPC — retired

Unladen Swallow and its HTTP/JSON-RPC server, client, dependencies and standalone entry are removed. Unscience opens no RPC listener and exports no HTTP routes. Feature-local programmatic methods are not a network API.

`GameThread.DrainOnGameThread()` is owned by the Unscience host update so queued infrastructure work does not depend on RPC. Parts Now maintains its own pre-GUI loader/purge phase. See [architecture](00-architecture-and-abstractions.md) and [part editor](part-editor-and-robotics.md).
