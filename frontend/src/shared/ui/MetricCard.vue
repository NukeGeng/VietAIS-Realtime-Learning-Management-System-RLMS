<template>
  <article class="metric-card">
    <div class="metric-card-top"><span class="metric-icon">{{ icon }}</span><span class="metric-label">{{ label }}</span></div>
    <strong class="metric-value">{{ value }}</strong>
    <span v-if="hint" class="metric-hint">{{ hint }}</span>
  </article>
</template>
<script setup lang="ts">
defineProps<{ label: string; value: string | number; hint?: string; icon?: string }>()
</script>
