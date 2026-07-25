<script setup>
defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
  streamUrl: { type: String, default: "" },
});
</script>

<template>
  <section
    class="plume-island space-y-1 border border-dashed border-neutral-300 px-3 py-2 text-sm"
    aria-label="Audio"
  >
    <p class="font-medium tracking-tight">Audio</p>
    <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    <p class="text-neutral-500">
      Scaffold — opt-in stream
      <span v-if="streamUrl" class="font-mono text-xs">({{ streamUrl }})</span>; off by default.
    </p>
  </section>
</template>
