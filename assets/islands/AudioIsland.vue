<script setup>
import { computed, onUnmounted, ref, watch } from "vue";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  /** Base stream URL without listen token (absolute or relative). */
  streamUrl: { type: String, default: "" },
  /** @type {"public" | "protected" | "private"} */
  playbackAccess: { type: String, default: "public" },
});

const enabled = ref(false);
const listenToken = ref("");
const error = ref("");
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
  error.value = "";
  if (browserUnsupported.value) {
    error.value =
      "Private Strunas cannot use in-browser /stream without a Kithara Bearer — browser audio is unavailable.";
    return;
  }

  if (needsToken.value && !listenToken.value.trim()) {
    error.value = "Listen token required for protected playback.";
    return;
  }

  if (!props.streamUrl) {
    error.value = "Stream URL is not configured (set Kithara:PublicBaseUrl for split-port Compose).";
    return;
  }

  enabled.value = true;
}

function stop() {
  enabled.value = false;
  error.value = "";
  const el = audioEl.value;
  if (el) {
    el.pause();
    el.removeAttribute("src");
    el.load();
  }
}

watch(activeSrc, (src) => {
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
    error.value = "Could not start playback — check the stream URL or try VLC.";
  });
});

onUnmounted(() => {
  stop();
});
</script>

<template>
  <section
    class="plume-island space-y-3 border border-neutral-200 px-3 py-3 text-sm"
    aria-label="Audio"
  >
    <div class="flex items-baseline justify-between gap-2">
      <p class="font-medium tracking-tight">Audio</p>
      <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }} · off by default</p>
    </div>

    <p class="text-neutral-600">
      Opt-in stream to Kithara <span class="font-mono text-xs">/stream/{{ strunaSlug || "…" }}</span>
      — not via BFF.
    </p>

    <div v-if="needsToken" class="space-y-1">
      <label class="block text-xs text-neutral-600" for="plume-listen-token">Listen token</label>
      <input
        id="plume-listen-token"
        v-model="listenToken"
        type="password"
        autocomplete="off"
        class="w-full border border-neutral-300 px-3 py-2 font-mono text-sm"
        :disabled="enabled"
        placeholder="Shown once at create"
      />
    </div>

    <div class="flex flex-wrap gap-2">
      <button
        v-if="!enabled"
        type="button"
        class="border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-white disabled:opacity-50"
        :disabled="browserUnsupported"
        @click="start"
      >
        Start listening
      </button>
      <button
        v-else
        type="button"
        class="border border-neutral-300 px-3 py-1.5"
        @click="stop"
      >
        Stop
      </button>
    </div>

    <audio
      v-show="enabled && activeSrc"
      ref="audioEl"
      class="w-full"
      controls
      preload="none"
    />

    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>

    <p v-if="vlcHint" class="break-all font-mono text-xs text-neutral-500">
      VLC / external:
      <a class="underline underline-offset-2" :href="vlcHint" target="_blank" rel="noopener">{{
        vlcHint
      }}</a>
    </p>
  </section>
</template>
