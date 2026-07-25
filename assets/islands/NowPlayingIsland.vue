<script setup>
import { computed } from "vue";
import CoverArt from "./CoverArt.vue";
import { useNowPlaying } from "../lib/useNowPlaying.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  /** @type {"compact" | "prominent"} — control desk vs listen (code-selected, not a user toggle). */
  variant: { type: String, default: "compact" },
});

const { artworkUrl, error, headline, status, title } = useNowPlaying(
  () => props.strunaId,
);

const prominent = computed(() => props.variant === "prominent");
</script>

<template>
  <section
    class="plume-island border border-neutral-200 text-sm"
    :class="prominent ? 'space-y-3 px-4 py-4' : 'px-3 py-3'"
    :data-variant="variant"
    aria-label="Now playing"
    aria-live="polite"
  >
    <!-- Compact (control): text left, small cover right -->
    <div
      v-if="!prominent"
      class="flex items-center gap-3"
    >
      <div class="min-w-0 flex-1 space-y-0.5">
        <p class="text-xs font-medium tracking-tight text-neutral-500">Now playing</p>
        <p class="truncate font-medium tracking-tight">{{ headline }}</p>
        <p class="font-mono text-xs text-neutral-500">
          {{ strunaSlug || "—" }}
          <span class="text-neutral-400"> · {{ status }}</span>
        </p>
      </div>
      <CoverArt :src="artworkUrl" :alt="title || headline" size="sm" />
    </div>

    <!-- Prominent: large cover on top, text under (used if mounted alone) -->
    <div v-else class="space-y-3">
      <div class="mx-auto w-full max-w-md">
        <CoverArt :src="artworkUrl" :alt="title || headline" size="lg" />
      </div>
      <div class="space-y-1 text-center">
        <p class="text-xs font-medium uppercase tracking-tight text-neutral-500">Now playing</p>
        <p class="text-xl font-medium tracking-tight">{{ headline }}</p>
        <p class="font-mono text-xs text-neutral-500">
          {{ strunaSlug || "—" }}
          <span class="text-neutral-400"> · {{ status }}</span>
        </p>
      </div>
    </div>

    <p v-if="error" class="mt-2 text-red-700" role="alert">{{ error }}</p>
  </section>
</template>
