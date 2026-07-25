import { createApp } from "vue";
import { readIslandProps } from "../lib/islandProps.js";
import { islandRegistry } from "./registry.js";

const MOUNT_ATTR = "data-plume-island";

/**
 * Mount Vue CSR widgets onto Razor host elements marked with
 * `data-plume-island="<name>"` (plus optional `data-struna-id`,
 * `data-struna-slug`, `data-variant`, `data-stream-url`).
 *
 * @param {ParentNode} [root=document]
 * @returns {import("vue").App[]}
 */
export function mountIslands(root = document) {
  const apps = [];
  const nodes = root.querySelectorAll(`[${MOUNT_ATTR}]`);

  for (const el of nodes) {
    if (!(el instanceof HTMLElement)) {
      continue;
    }

    if (el.dataset.plumeMounted === "1") {
      continue;
    }

    const name = el.getAttribute(MOUNT_ATTR)?.trim();
    const component = name ? islandRegistry[name] : undefined;
    if (!component) {
      console.warn(`[plume] unknown island "${name ?? ""}"`);
      continue;
    }

    const app = createApp(component, readIslandProps(el));
    app.mount(el);
    el.dataset.plumeMounted = "1";
    apps.push(app);
  }

  return apps;
}
