import { computed, onMounted, onUnmounted, ref } from "vue";
import { bffGet } from "./bff.js";
import { startPoll } from "./poll.js";

/**
 * Shared now-playing poll for desk + listen islands.
 * @param {() => string} getStrunaId
 */
export function useNowPlaying(getStrunaId) {
  const playing = ref(false);
  const paused = ref(false);
  const title = ref("");
  const artist = ref("");
  const streamTitle = ref("");
  const artworkUrl = ref("");
  const error = ref("");
  let stopPoll = null;

  const headline = computed(() => {
    if (!playing.value) {
      return "Nothing playing";
    }

    if (streamTitle.value) {
      return streamTitle.value;
    }

    const parts = [title.value, artist.value].filter(Boolean);
    return parts.length > 0 ? parts.join(" — ") : "Playing";
  });

  const status = computed(() => {
    if (!playing.value) {
      return "idle";
    }

    return paused.value ? "paused" : "playing";
  });

  async function refresh() {
    const strunaId = getStrunaId();
    if (!strunaId) {
      error.value = "Missing Struna id.";
      return;
    }

    try {
      const data = await bffGet(`/bff/streams/${strunaId}/now-playing`);
      playing.value = Boolean(data?.playing);
      paused.value = Boolean(data?.paused);
      title.value = data?.title ?? "";
      artist.value = data?.artist ?? "";
      streamTitle.value = data?.stream_title ?? "";
      artworkUrl.value = data?.artwork_url ?? "";
      error.value = "";
    } catch (e) {
      if (e?.status === 401) {
        return;
      }

      error.value = e?.message ?? "Could not load now playing.";
    }
  }

  function start() {
    stopPoll?.();
    stopPoll = startPoll(refresh);
  }

  function stop() {
    stopPoll?.();
    stopPoll = null;
  }

  onMounted(start);
  onUnmounted(stop);

  return {
    playing,
    paused,
    title,
    artist,
    streamTitle,
    artworkUrl,
    error,
    headline,
    status,
    refresh,
  };
}
