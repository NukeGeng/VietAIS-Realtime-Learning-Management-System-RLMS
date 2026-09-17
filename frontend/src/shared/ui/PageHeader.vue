<template>
  <div v-if="showIntro || hasActions" class="page-header" :class="{ 'page-header-actions-only': !showIntro }">
    <div v-if="showIntro"><span class="eyebrow">{{ eyebrow || 'KHÔNG GIAN RLMS' }}</span><h1>{{ title }}</h1><p v-if="description">{{ description }}</p></div>
    <div v-if="hasActions" class="page-actions"><slot name="actions" /></div>
  </div>
</template>
<script setup lang="ts">
import { computed, useSlots } from 'vue'
import { useRoute } from 'vue-router'

const props = defineProps<{ title: string; description?: string; eyebrow?: string; compact?: boolean }>()
const route = useRoute()
const slots = useSlots()
const showIntro = computed(() => route.name === 'dashboard' && !props.compact)
const hasActions = computed(() => Boolean(slots.actions))
</script>
