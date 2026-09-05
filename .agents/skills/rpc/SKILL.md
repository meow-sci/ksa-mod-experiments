---
name: rpc
description: retirement status of the former Unladen Swallow RPC mechanism
---

# Unladen Swallow RPC is retired

The server, client, endpoint projects and GenHTTP distribution dependencies were removed during the Unscience workspace redesign. The current host opens no RPC listener. Do not create new endpoints or resurrect the service as part of ordinary feature work.

Use [AGENTS.md](../../../AGENTS.md), [workspace architecture](../../../docs/WORKSPACE.md) and [RPC retirement scope](../../../scope/rpc.md) for the current design. Programmatic feature methods use the same owning runtime registries as UI actions. `unscience/Mod.cs` drains GameThread independently of HTTP; Parts Now owns its loader/purge timing.

Historical server implementation is available in Git history. The generic GenHTTP skill remains a library reference for explicitly requested separate HTTP work, not an active Unscience integration.
