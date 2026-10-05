<template>
  <section class="teacher-class-selector" :aria-label="title">
    <label class="teacher-class-search">
      <span>Tìm lớp học</span>
      <input
        v-model="query"
        class="search-input"
        type="search"
        aria-label="Tìm theo tên hoặc mã lớp"
        placeholder="Tìm theo tên hoặc mã lớp…"
      />
    </label>

    <div v-if="loading" class="loading-state">Đang tải lớp học…</div>
    <div v-else-if="filteredClasses.length" class="teacher-class-grid">
      <button
        v-for="item in filteredClasses"
        :key="item.id"
        type="button"
        class="teacher-class-card"
        :class="{ selected: item.id === selectedClassId }"
        @click="emit('select', item.id)"
      >
        <span class="teacher-class-card-topline">
          <span class="code-label">{{ item.code }}</span>
          <StatusPill :value="item.status" :labels="classStatusLabels" />
        </span>
        <strong class="teacher-class-card-name">{{ item.name }}</strong>
        <span v-if="metadata[item.id]?.courseName || item.courseName" class="teacher-class-card-course">{{ metadata[item.id]?.courseName || item.courseName }}</span>
        <span class="teacher-class-card-stats">
          <span v-if="metadata[item.id]?.studentCount !== undefined || item.studentCount !== undefined"><strong>{{ metadata[item.id]?.studentCount ?? item.studentCount }}</strong> sinh viên</span>
          <span v-if="metadata[item.id]?.featureCount !== undefined"><strong>{{ metadata[item.id]?.featureCount }}</strong> {{ featureCountLabel }}</span>
        </span>
        <span class="teacher-class-card-action">Mở lớp <span aria-hidden="true">→</span></span>
      </button>
    </div>
    <div v-else class="teacher-class-selector-empty">
      <strong>Không tìm thấy lớp học</strong>
      <span>Thử tìm bằng mã lớp hoặc tên lớp khác.</span>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import StatusPill from './StatusPill.vue'
import type { LearningClass } from '../types'

interface ClassMetadata {
  courseName?: string
  studentCount?: number
  featureCount?: number
}

const props = withDefaults(defineProps<{
  classes: LearningClass[]
  metadata?: Record<string, ClassMetadata>
  selectedClassId?: string
  loading?: boolean
  title?: string
  description?: string
  featureCountLabel?: string
}>(), {
  metadata: () => ({}),
  selectedClassId: '',
  loading: false,
  title: 'Tiểu luận',
  description: 'Chọn một lớp học để quản lý các bài tiểu luận và nhóm nghiên cứu.',
  featureCountLabel: 'bài tiểu luận'
})

const emit = defineEmits<{ select: [classId: string] }>()
const query = ref('')
const classStatusLabels = ['Đang chuẩn bị', 'Đang mở', 'Tạm dừng', 'Đã hoàn thành', 'Đã lưu trữ']
const metadata = computed(() => props.metadata)
const filteredClasses = computed(() => {
  const normalized = query.value.trim().toLocaleLowerCase()
  if (!normalized) return props.classes
  return props.classes.filter(item => `${item.code} ${item.name} ${metadata.value[item.id]?.courseName || ''}`.toLocaleLowerCase().includes(normalized))
})

function formatCount(value?: number) {
  return value === undefined ? '—' : value
}
</script>
