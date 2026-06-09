/**
 * starwars-api.ts
 * HTTP helpers for its-so-shiny light grid control via the Unladen Swallow RPC server.
 */

const BASE_URL = "http://localhost:7887";

/**
 * Apply a static set of pixels to a grid in replace mode.
 * Pixels not in the list are turned off (reset=true diff behaviour).
 * Passing an empty array turns off all pixels.
 */
export async function setPixels(
  vehicleId: string,
  gridName: string,
  pixels: Array<{ x: number; y: number }>
): Promise<void> {
  const res = await fetch(`${BASE_URL}/shiny/static`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ vehicleId, gridName, pixels, reset: true }),
  });
  if (!res.ok) {
    const body = await res.text().catch(() => "(no body)");
    throw new Error(`setPixels HTTP ${res.status}: ${body}`);
  }
}

/**
 * Stop any animation and turn off all pixels on the grid.
 */
export async function clearGrid(
  vehicleId: string,
  gridName: string
): Promise<void> {
  const res = await fetch(`${BASE_URL}/shiny/off`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ vehicleId, gridName }),
  });
  if (!res.ok) {
    const body = await res.text().catch(() => "(no body)");
    throw new Error(`clearGrid HTTP ${res.status}: ${body}`);
  }
}
