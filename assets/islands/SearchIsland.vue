<script setup>
import { ref } from "vue";
import { bffGet, bffPost } from "../lib/bff.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
});

const query = ref("");
/** @type {import("vue").Ref<Array<{ id: string, title: string, artist: string, module: string, track_ref: string }>>} */
const results = ref([]);
const error = ref("");
const busy = ref("");

async function search() {
  const q = query.value.trim();
  if (!q || busy.value) {
    return;
  }

  busy.value = "search";
  error.value = "";
  try {
    const data = await bffGet(`/bff/search/quick?q=${encodeURIComponent(q)}`);
    results.value = (data?.results ?? []).map((r) => ({
      id: r.id,
      title: r.title || "Untitled",
      artist: r.artist || "",
      module: r.module || "",
      track_ref: r.track_ref || "",
    }));
    if (results.value.length === 0) {
      error.value = "No results.";
    }
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    results.value = [];
    error.value = e?.message ?? "Search failed.";
  } finally {
    busy.value = "";
  }
}

async function playResult(resultId) {
  if (!props.strunaId || busy.value) {
    return;
  }

  busy.value = `play:${resultId}`;
  error.value = "";
  try {
    await bffPost(`/bff/streams/${props.strunaId}/play`, {
      search_result_id: resultId,
    });
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not play.";
  } finally {
    busy.value = "";
  }
}

async function queueResult(resultId) {
  if (!props.strunaId || busy.value) {
    return;
  }

  busy.value = `queue:${resultId}`;
  error.value = "";
  try {
    await bffPost(`/bff/streams/${props.strunaId}/queue`, {
      search_result_id: resultId,
    });
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not queue.";
  } finally {
    busy.value = "";
  }
}

async function quickPlay() {
  const q = query.value.trim();
  if (!props.strunaId || !q || busy.value) {
    return;
  }

  busy.value = "quickplay";
  error.value = "";
  try {
    await bffPost(`/bff/streams/${props.strunaId}/quickplay`, { q });
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Quick play failed.";
  } finally {
    busy.value = "";
  }
}

async function quickQueue() {
  const q = query.value.trim();
  if (!props.strunaId || !q || busy.value) {
    return;
  }

  busy.value = "quickqueue";
  error.value = "";
  try {
    await bffPost(`/bff/streams/${props.strunaId}/quickqueue`, { q });
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Quick queue failed.";
  } finally {
    busy.value = "";
  }
}
</script>

<template>
  <section
    class="plume-island space-y-3 border border-neutral-200 px-3 py-2 text-sm"
    aria-label="Search"
  >
    <div class="flex items-baseline justify-between gap-2">
      <p class="font-medium tracking-tight">Search</p>
      <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    </div>

    <form class="space-y-2" @submit.prevent="search">
      <label class="block text-xs text-neutral-600" for="plume-search-q">Query</label>
      <input
        id="plume-search-q"
        v-model="query"
        type="search"
        class="w-full border border-neutral-300 px-3 py-2 text-sm"
        autocomplete="off"
        placeholder="Title or Magpie URL / id"
      />
      <div class="flex flex-wrap gap-2">
        <button
          type="submit"
          class="border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-white disabled:opacity-50"
          :disabled="Boolean(busy) || !query.trim()"
        >
          {{ busy === "search" ? "…" : "Search" }}
        </button>
        <button
          type="button"
          class="border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
          :disabled="Boolean(busy) || !query.trim()"
          @click="quickPlay"
        >
          {{ busy === "quickplay" ? "…" : "Quick play" }}
        </button>
        <button
          type="button"
          class="border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
          :disabled="Boolean(busy) || !query.trim()"
          @click="quickQueue"
        >
          {{ busy === "quickqueue" ? "…" : "Quick queue" }}
        </button>
      </div>
    </form>

    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>

    <ul v-if="results.length > 0" class="divide-y divide-neutral-200 border border-neutral-200">
      <li
        v-for="hit in results"
        :key="hit.id"
        class="flex flex-col gap-2 px-3 py-2 sm:flex-row sm:items-center sm:justify-between"
      >
        <div class="min-w-0">
          <p class="truncate font-medium">{{ hit.title }}</p>
          <p class="truncate font-mono text-xs text-neutral-500">
            <span v-if="hit.artist">{{ hit.artist }} · </span>
            {{ hit.module || "—" }}
          </p>
        </div>
        <div class="flex shrink-0 gap-3 text-xs">
          <button
            type="button"
            class="underline underline-offset-2 disabled:opacity-50"
            :disabled="Boolean(busy)"
            @click="playResult(hit.id)"
          >
            {{ busy === `play:${hit.id}` ? "…" : "Play" }}
          </button>
          <button
            type="button"
            class="underline underline-offset-2 disabled:opacity-50"
            :disabled="Boolean(busy)"
            @click="queueResult(hit.id)"
          >
            {{ busy === `queue:${hit.id}` ? "…" : "Queue" }}
          </button>
        </div>
      </li>
    </ul>
  </section>
</template>
