<script setup>
defineProps({
  strunaId: { type: String, default: "" },
  strunaSlug: { type: String, default: "" },
});
</script>

<template>
  <section
    class="plume-island space-y-1 border border-dashed border-neutral-300 px-3 py-2 text-sm"
    aria-label="Transport"
  >
    <p class="font-medium tracking-tight">Transport</p>
    <p class="font-mono text-xs text-neutral-500">{{ strunaSlug || "—" }}</p>
    <p class="text-neutral-500">Scaffold — play / pause / skip via BFF.</p>
  </section>
</template>
