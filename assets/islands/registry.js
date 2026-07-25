import NowPlayingIsland from "./NowPlayingIsland.vue";
import TransportIsland from "./TransportIsland.vue";
import QueueIsland from "./QueueIsland.vue";
import SearchIsland from "./SearchIsland.vue";
import AudioIsland from "./AudioIsland.vue";

/** @type {Record<string, import("vue").Component>} */
export const islandRegistry = {
  "now-playing": NowPlayingIsland,
  transport: TransportIsland,
  queue: QueueIsland,
  search: SearchIsland,
  audio: AudioIsland,
};
