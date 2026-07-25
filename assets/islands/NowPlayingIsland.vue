<script setup>
defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  /** @type {"compact" | "prominent"} */
  variant: { type: String, default: "compact" },
});
</script>

<template>
  <section
    class="plume-island space-y-1 border border-dashed border-neutral-300 px-3 py-2 text-sm"
    :data-variant="variant"
    aria-label="Now playing"
  >
    <p class="font-medium tracking-tight">Now playing</p>
    <p class="font-mono text-xs text-neutral-500">
      {{ strunaSlug || "—" }}
      <span v-if="strunaId" class="text-neutral-400"> · {{ strunaId }}</span>
    </p>
    <p class="text-neutral-500">Scaffold — wired in control / player.</p>
  </section>
</template>
