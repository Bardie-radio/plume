<script setup>
defineProps({
  src: { type: String, default: "" },
  alt: { type: String, default: "" },
  /** @type {"sm" | "lg"} */
  size: { type: String, default: "sm" },
});
</script>

<template>
  <div
    class="shrink-0 overflow-hidden bg-neutral-200 text-neutral-500"
    :class="size === 'lg' ? 'aspect-square w-full max-w-md' : 'size-14'"
    aria-hidden="true"
  >
    <img
      v-if="src"
      :src="src"
      :alt="alt"
      class="block h-full w-full object-cover"
      loading="lazy"
      referrerpolicy="no-referrer"
    />
    <div
      v-else
      class="flex h-full w-full items-center justify-center font-mono text-xs"
    >
      —
    </div>
  </div>
</template>
