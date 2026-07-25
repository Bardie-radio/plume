<script setup>
import { ref } from "vue";
import { bffPost } from "../lib/bff.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
});

const busy = ref("");
const error = ref("");

async function run(action, label) {
  if (!props.strunaId || busy.value) {
    return;
  }

  busy.value = label;
  error.value = "";
  try {
    await action();
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? `Could not ${label}.`;
  } finally {
    busy.value = "";
  }
}

function resume() {
  return run(
    () => bffPost(`/bff/streams/${props.strunaId}/play`),
    "resume",
  );
}

function pause() {
  return run(
    () => bffPost(`/bff/streams/${props.strunaId}/pause`),
    "pause",
  );
}

function skip() {
  return run(
    () => bffPost(`/bff/streams/${props.strunaId}/skip`),
    "skip",
  );
}
</script>

<template>
  <section
    class="plume-island space-y-3 border border-neutral-200 px-3 py-2 text-sm"
    aria-label="Transport"
  >
    <div class="flex items-baseline justify-between gap-2">
      <p class="font-medium tracking-tight">Transport</p>
      <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    </div>

    <div class="flex flex-wrap gap-2">
      <button
        type="button"
        class="border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-white disabled:opacity-50"
        :disabled="Boolean(busy)"
        @click="resume"
      >
        {{ busy === "resume" ? "…" : "Resume" }}
      </button>
      <button
        type="button"
        class="border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
        :disabled="Boolean(busy)"
        @click="pause"
      >
        {{ busy === "pause" ? "…" : "Pause" }}
      </button>
      <button
        type="button"
        class="border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
        :disabled="Boolean(busy)"
        @click="skip"
      >
        {{ busy === "skip" ? "…" : "Skip" }}
      </button>
    </div>

    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>
  </section>
</template>
