<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { bffGet } from "../lib/bff.js";
import { startPoll } from "../lib/poll.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  /** @type {"compact" | "prominent"} */
  variant: { type: String, default: "compact" },
});

const playing = ref(false);
const paused = ref(false);
const title = ref("");
const artist = ref("");
const streamTitle = ref("");
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
  if (!props.strunaId) {
    error.value = "Missing Struna id.";
    return;
  }

  try {
    const data = await bffGet(`/bff/streams/${props.strunaId}/now-playing`);
    playing.value = Boolean(data?.playing);
    paused.value = Boolean(data?.paused);
    title.value = data?.title ?? "";
    artist.value = data?.artist ?? "";
    streamTitle.value = data?.stream_title ?? "";
    error.value = "";
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not load now playing.";
  }
}

onMounted(() => {
  stopPoll = startPoll(refresh);
});

onUnmounted(() => {
  stopPoll?.();
});
</script>

<template>
  <section
    class="plume-island space-y-1 border border-neutral-200 px-3 py-2 text-sm"
    :class="variant === 'prominent' ? 'space-y-2 px-4 py-4' : ''"
    :data-variant="variant"
    aria-label="Now playing"
    aria-live="polite"
  >
    <p
      class="font-medium tracking-tight text-neutral-500"
      :class="variant === 'prominent' ? 'text-xs uppercase' : 'text-xs'"
    >
      Now playing
    </p>
    <p
      class="font-medium tracking-tight"
      :class="variant === 'prominent' ? 'text-xl' : 'text-sm'"
    >
      {{ headline }}
    </p>
    <p class="font-mono text-xs text-neutral-500">
      {{ strunaSlug || "—" }}
      <span class="text-neutral-400"> · {{ status }}</span>
    </p>
    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>
  </section>
</template>
