<script setup>
import { computed, onMounted, ref } from "vue";
import { bffDelete, bffGet } from "../lib/bff.js";

const props = defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
});

const loading = ref(true);
const error = ref("");
const isOwner = ref(false);
const guestCode = ref("");
const listenToken = ref("");
const controlAccess = ref("");
const revealed = ref(false);
const copied = ref(false);
const busy = ref("");

const hasGuestCode = computed(
  () => isOwner.value && Boolean(guestCode.value),
);

async function load() {
  if (!props.strunaId) {
    loading.value = false;
    return;
  }

  loading.value = true;
  error.value = "";
  try {
    const [me, stream] = await Promise.all([
      bffGet("/bff/auth/me"),
      bffGet(`/bff/streams/${props.strunaId}`),
    ]);

    const meId = typeof me?.user_id === "string" ? me.user_id : me?.user_id?.toString?.() ?? "";
    const ownerId =
      typeof stream?.owner_user_id === "string"
        ? stream.owner_user_id
        : stream?.owner_user_id?.toString?.() ?? "";

    isOwner.value =
      Boolean(meId)
      && Boolean(ownerId)
      && meId.toLowerCase() === ownerId.toLowerCase();

    controlAccess.value = stream?.control_access ?? "";
    guestCode.value =
      isOwner.value && typeof stream?.guest_code === "string" ? stream.guest_code : "";
    listenToken.value =
      isOwner.value && typeof stream?.listen_token === "string" ? stream.listen_token : "";
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not load Struna settings.";
  } finally {
    loading.value = false;
  }
}

function reveal() {
  revealed.value = true;
  copied.value = false;
}

function hide() {
  revealed.value = false;
  copied.value = false;
}

async function copyCode() {
  if (!guestCode.value) {
    return;
  }

  try {
    await navigator.clipboard.writeText(guestCode.value);
    copied.value = true;
  } catch {
    // Fallback: select via prompt for older browsers / insecure contexts.
    window.prompt("Copy guest code", guestCode.value);
  }
}

async function deleteStruna() {
  if (!props.strunaId || busy.value || !isOwner.value) {
    return;
  }

  const label = props.strunaSlug || "this Struna";
  if (
    !window.confirm(
      `Delete “${label}”? This stops playback, frees the slug, and removes guests. This cannot be undone.`,
    )
  ) {
    return;
  }

  busy.value = "delete";
  error.value = "";
  try {
    await bffDelete(`/bff/streams/${props.strunaId}`);
    window.location.assign("/");
  } catch (e) {
    if (e?.status === 401) {
      return;
    }

    error.value = e?.message ?? "Could not delete Struna.";
    busy.value = "";
  }
}

onMounted(load);
</script>

<template>
  <section
    class="plume-island space-y-3 border border-neutral-200 px-3 py-2 text-sm"
    aria-label="Struna settings"
  >
    <div class="flex items-baseline justify-between gap-2">
      <p class="font-medium tracking-tight">Struna</p>
      <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    </div>

    <p v-if="loading" class="text-neutral-500">Loading…</p>

    <template v-else>
      <div v-if="hasGuestCode" class="space-y-2">
        <p class="text-neutral-600">Guest code</p>
        <div
          class="rounded border border-neutral-200 bg-neutral-100/80 px-3 py-2"
        >
          <button
            v-if="!revealed"
            type="button"
            class="flex w-full items-center justify-between gap-3 text-left"
            @click="reveal"
          >
            <span
              class="select-none font-mono tracking-[0.35em] text-neutral-400 blur-[3px]"
              aria-hidden="true"
            >••••••</span>
            <span class="shrink-0 text-xs underline underline-offset-2">Show</span>
          </button>
          <div v-else class="flex flex-wrap items-center justify-between gap-2">
            <code class="select-all font-mono text-base tracking-wide">{{ guestCode }}</code>
            <div class="flex gap-3 text-xs">
              <button
                type="button"
                class="underline underline-offset-2"
                @click="copyCode"
              >
                {{ copied ? "Copied" : "Copy" }}
              </button>
              <button
                type="button"
                class="underline underline-offset-2 text-neutral-600"
                @click="hide"
              >
                Hide
              </button>
            </div>
          </div>
        </div>
        <p class="text-xs text-neutral-500">
          Share
          <span class="font-mono">/control/{{ strunaSlug }}</span>
          — guests enter this code on sign-in.
        </p>
      </div>

      <div
        v-if="isOwner && listenToken"
        class="space-y-2"
      >
        <p class="text-neutral-600">Listen token</p>
        <details class="rounded border border-neutral-200 bg-neutral-100/80 px-3 py-2">
          <summary class="cursor-pointer text-xs underline underline-offset-2">
            Show listen token
          </summary>
          <code class="mt-2 block select-all break-all font-mono text-xs">{{ listenToken }}</code>
        </details>
      </div>

      <div
        v-else-if="isOwner && controlAccess === 'protected' && !hasGuestCode"
        class="text-xs text-neutral-500"
      >
        No guest code on this Struna.
      </div>

      <div v-if="isOwner" class="border-t border-neutral-200 pt-3">
        <button
          type="button"
          class="border border-red-800/80 px-3 py-1.5 text-red-800 disabled:opacity-50"
          :disabled="Boolean(busy)"
          @click="deleteStruna"
        >
          {{ busy === "delete" ? "Deleting…" : "Delete Struna" }}
        </button>
      </div>
    </template>

    <p v-if="error" class="text-red-700" role="alert">{{ error }}</p>
  </section>
</template>
