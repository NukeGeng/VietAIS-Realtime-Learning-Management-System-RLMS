<template><span class="status-pill" :class="tone">{{ label }}</span></template>

<script setup lang="ts">
import { computed } from 'vue'
import { enumLabel, type EnumLabelMap } from '../types'
const props = defineProps<{ value?: string | number | null; labels?: EnumLabelMap; tone?: string }>()
const label = computed(() => enumLabel(props.value, props.labels))
const tone = computed(() => {
  if (props.tone) return props.tone
  const value = label.value.toLowerCase()
  if (['đã phát hành', 'đã công bố', 'đang hoạt động', 'đang mở', 'đang học', 'có mặt', 'đã hoàn thành'].includes(value)) return 'success'
  if (['bản nháp', 'đang chuẩn bị', 'đã lên lịch', 'đã đổi lịch', 'chờ duyệt', 'đang chờ'].includes(value)) return 'warning'
  if (['đã lưu trữ', 'đã hủy', 'đã rời lớp', 'vắng', 'đã đóng', 'đã vô hiệu hóa', 'đã được thay thế'].includes(value)) return 'muted'
  if (['không hoạt động', 'đang cần hỗ trợ', 'đang chờ hỗ trợ', 'đi trễ', 'không đạt kiểm tra', 'đã từ chối'].includes(value)) return 'danger'
  return 'info'
})
</script>
