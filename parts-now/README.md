# parts-now

Standalone StarMap wrapper for the **parts-now** runtime Part / SubPart loader.

All logic lives in [`parts-now.lib`](../parts-now.lib/README.md) — this project only owns the
StarMap lifecycle, the Harmony instance (HotkeyGuard only) and the floating window.

- **Hotkey:** `F10` toggles the standalone window.
- **Entry assembly:** `MeowSci.PartsNow` (see `mod.toml`).
- Also bundled into the [unscience](../unscience) supermod as the `Parts Now` submod.

See [`parts-now.lib/README.md`](../parts-now.lib/README.md) for features, usage and limitations.
