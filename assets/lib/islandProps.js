/**
 * Read mount props from a Razor host element (`data-*` attributes).
 * @param {HTMLElement} el
 */
export function readIslandProps(el) {
  return {
    strunaId: el.dataset.strunaId ?? "",
    strunaSlug: el.dataset.strunaSlug ?? "",
    variant: el.dataset.variant || "compact",
    streamUrl: el.dataset.streamUrl || "",
    playbackAccess: el.dataset.playbackAccess || "public",
    openPlayback: el.dataset.openPlayback === "true",
  };
}
