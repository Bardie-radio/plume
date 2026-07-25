<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import CoverArt from "./CoverArt.vue";
import { useNowPlaying } from "../lib/useNowPlaying.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  streamUrl: { type: String, default: "" },
  /** @type {"public" | "protected" | "private"} */
  playbackAccess: { type: String, default: "public" },
});

const { artworkUrl, error: nowPlayingError, headline, status, title } = useNowPlaying(
  () => props.strunaId,
);

const enabled = ref(false);
const listenToken = ref("");
const audioError = ref("");
const audioEl = ref(null);

const needsToken = computed(() => props.playbackAccess === "protected");
const browserUnsupported = computed(() => props.playbackAccess === "private");

const activeSrc = computed(() => {
  if (!enabled.value || !props.streamUrl || browserUnsupported.value) {
    return "";
  }

  if (needsToken.value) {
    const token = listenToken.value.trim();
    if (!token) {
      return "";
    }

    const join = props.streamUrl.includes("?") ? "&" : "?";
    return `${props.streamUrl}${join}token=${encodeURIComponent(token)}`;
  }

  return props.streamUrl;
});

const vlcHint = computed(() => {
  if (!props.streamUrl) {
    return "";
  }

  if (needsToken.value && listenToken.value.trim()) {
    const join = props.streamUrl.includes("?") ? "&" : "?";
    return `${props.streamUrl}${join}token=${encodeURIComponent(listenToken.value.trim())}`;
  }

  return props.streamUrl;
});

function start() {
  audioError.value = "";
  if (browserUnsupported.value) {
    audioError.value =
      "Private Strunas cannot use in-browser /stream without a Kithara Bearer — browser audio is unavailable.";
    return;
  }

  if (needsToken.value && !listenToken.value.trim()) {
    audioError.value = "Listen token required for protected playback.";
    return;
  }

  if (!props.streamUrl) {
    audioError.value =
      "Stream URL is not configured (set Kithara:PublicBaseUrl for split-port Compose).";
    return;
  }

  enabled.value = true;
}

function stop() {
  enabled.value = false;
  const el = audioEl.value;
  if (el) {
    el.pause();
    el.removeAttribute("src");
    el.load();
  }
}

watch(activeSrc, async (src) => {
  await nextTick();
  const el = audioEl.value;
  if (!el) {
    return;
  }

  if (!src) {
    el.pause();
    el.removeAttribute("src");
    el.load();
    return;
  }

  el.src = src;
  el.load();
  void el.play().catch(() => {
    audioError.value = "Could not start playback — check the stream URL or try VLC.";
  });
});

onMounted(() => {
  // Listen page: auto-on for public (and protected once token is filled via start).
  if (!needsToken.value && !browserUnsupported.value) {
    start();
  }
});

onUnmounted(() => {
  stop();
});
</script>

<template>
  <section
    class="plume-island space-y-4 border border-neutral-200 px-4 py-5 text-sm"
    aria-label="Listen"
  >
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

    <div v-if="needsToken" class="mx-auto w-full max-w-sm space-y-1">
      <label class="block text-xs text-neutral-600" for="plume-listen-token">Listen token</label>
      <div class="flex gap-2">
        <input
          id="plume-listen-token"
          v-model="listenToken"
          type="password"
          autocomplete="off"
          class="min-w-0 flex-1 border border-neutral-300 px-3 py-2 font-mono text-sm"
          :disabled="enabled"
          placeholder="Shown once at create"
          @keyup.enter="start"
        />
        <button
          v-if="!enabled"
          type="button"
          class="shrink-0 border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-white"
          @click="start"
        >
          Listen
        </button>
      </div>
    </div>

    <div class="mx-auto w-full max-w-md space-y-2">
      <audio
        v-show="enabled && activeSrc"
        ref="audioEl"
        class="w-full"
        controls
        preload="none"
      />

      <div v-if="enabled" class="flex justify-center">
        <button type="button" class="text-xs underline underline-offset-2" @click="stop">
          Stop browser audio
        </button>
      </div>
    </div>

    <p v-if="nowPlayingError || audioError" class="text-center text-red-700" role="alert">
      {{ audioError || nowPlayingError }}
    </p>

    <p v-if="vlcHint" class="break-all text-center font-mono text-xs text-neutral-500">
      VLC:
      <a class="underline underline-offset-2" :href="vlcHint" target="_blank" rel="noopener">{{
        vlcHint
      }}</a>
    </p>
  </section>
</template>
