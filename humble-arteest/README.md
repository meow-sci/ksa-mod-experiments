# Humble Arteest

Standalone KSA mod providing visual customization features. Toggle with F11.

## Features

- **Vehicle Paint** — Per-part RGB tinting via runtime shader patching and PerInstanceData padding hijack
- **Kitten Color** — Character model tinting via GPU material buffer AlbedoColor writes
- **Engine Emissive** — Per-engine glow control via Temperature field override

## Architecture

See [humble-arteest.lib/README.md](../humble-arteest.lib/README.md) for comprehensive technical documentation including shader modification details, struct layouts, rendering pipeline analysis, and maintenance guidance.

## Unscience Integration

All features are also available as submods in the unscience supermod via `humble-arteest.lib`.