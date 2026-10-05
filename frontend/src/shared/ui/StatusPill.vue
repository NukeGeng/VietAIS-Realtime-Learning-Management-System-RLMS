<template><span class="status-pill" :class="tone">{{ label }}</span></template>

<script setup lang="ts">
import { computed } from 'vue'
import { enumLabel } from '../types'
const props = defineProps<{ value?: string | number; labels?: string[]; tone?: string }>()
const label = computed(() => {
  if (typeof props.value === 'number') return props.labels?.[props.value] || String(props.value)
  return enumLabel(props.value)
})
const tone = computed(() => {
  if (props.tone) return props.tone
  const value = String(props.value || '').toLowerCase()
  if (value.includes('publish') || value.includes('active') || value.includes('present') || value.includes('complete') || value.includes('success')) return 'success'
  if (value.includes('draft') || value.includes('prepar') || value.includes('schedul') || value.includes('pending')) return 'warning'
  if (value.includes('archiv') || value.includes('cancel') || value.includes('drop') || value.includes('absent') || value.includes('closed')) return 'muted'
  if (value.includes('idle') || value.includes('help') || value.includes('late')) return 'danger'
  return 'info'
})
</script>
