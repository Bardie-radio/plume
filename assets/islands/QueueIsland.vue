<script setup>
import { onMounted, onUnmounted, ref } from "vue";
import { bffDelete, bffGet } from "../lib/bff.js";
import { startPoll } from "../lib/poll.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
});

/** @type {import("vue").Ref<Array<{ id: string, position: number, title: string, artist: string, module: string }>>} */
const entries = ref([]);
const error = ref("");
const removing = ref("");
let stopPoll = null;

async function refresh() {
  if (!props.strunaId) {
    error.value = "Missing Struna id.";
    return;
  }

  try {
    const data = await bffGet(`/bff/streams/${props.strunaId}/queue`);
    entries.value = (data?.entries ?? []).map((e) => ({
      id: e.id,
      position: e.position,
      title: e.title || "Untitled",
      artist: e.artist || "",
      module: e.module || "",
    }));
    error.value = "";
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not load queue.";
  }
}

async function removeEntry(entryId) {
  if (!props.strunaId || removing.value) {
    return;
  }

  removing.value = entryId;
  error.value = "";
  try {
    await bffDelete(`/bff/streams/${props.strunaId}/queue/${entryId}`);
    await refresh();
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not remove queue entry.";
  } finally {
    removing.value = "";
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
    class="plume-island space-y-3 border border-neutral-200 px-3 py-2 text-sm"
    aria-label="Queue"
  >
    <div class="flex items-baseline justify-between gap-2">
      <p class="font-medium tracking-tight">Queue</p>
      <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    </div>

    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>

    <p v-if="entries.length === 0" class="text-neutral-500">Queue is empty.</p>

    <ol v-else class="divide-y divide-neutral-200 border border-neutral-200">
      <li
        v-for="entry in entries"
        :key="entry.id"
        class="flex items-start justify-between gap-3 px-3 py-2"
      >
        <div class="min-w-0">
          <p class="truncate font-medium">{{ entry.title }}</p>
          <p class="truncate font-mono text-xs text-neutral-500">
            <span v-if="entry.artist">{{ entry.artist }} · </span>
            {{ entry.module || "—" }}
          </p>
        </div>
        <button
          type="button"
          class="shrink-0 text-xs underline underline-offset-2 disabled:opacity-50"
          :disabled="removing === entry.id"
          @click="removeEntry(entry.id)"
        >
          {{ removing === entry.id ? "…" : "Remove" }}
        </button>
      </li>
    </ol>
  </section>
</template>
