<template>
  <div class="page-header">
    <div><span class="eyebrow">{{ eyebrow || 'RLMS WORKSPACE' }}</span><h1>{{ title }}</h1><p v-if="description">{{ description }}</p></div>
    <div v-if="$slots.actions" class="page-actions"><slot name="actions" /></div>
  </div>
</template>
<script setup lang="ts">
defineProps<{ title: string; description?: string; eyebrow?: string }>()
</script>
