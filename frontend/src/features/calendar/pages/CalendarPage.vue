<template>
  <div>
    <div v-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button class="mini-button" @click="load">Thử lại</button>
    </div>

    <section class="panel calendar-panel">
      <div class="calendar-toolbar">
        <div class="calendar-filters" role="tablist" aria-label="Loại lịch">
          <button
            v-for="option in filterOptions"
            :key="option.value"
            class="calendar-filter"
            type="button"
            :class="{ active: viewFilter === option.value }"
            role="tab"
            :aria-selected="viewFilter === option.value"
            @click="viewFilter = option.value"
          >
            <span class="calendar-filter-dot" aria-hidden="true" />
            {{ option.label }}
          </button>
        </div>

        <div class="calendar-actions">
          <label class="calendar-date-field">
            <span class="sr-only">Chọn ngày trong tuần</span>
            <input v-model="dateInputValue" class="form-input" type="date" aria-label="Chọn ngày trong tuần" />
          </label>
          <button class="button secondary" type="button" @click="goToday">Hiện tại</button>
          <button class="button secondary" type="button" @click="printCalendar">In lịch</button>
          <button class="button secondary calendar-nav-button" type="button" aria-label="Tuần trước" @click="shiftWeek(-1)">
            ‹
          </button>
          <button class="button secondary calendar-nav-button" type="button" aria-label="Tuần sau" @click="shiftWeek(1)">
            ›
          </button>
        </div>
      </div>

      <div class="calendar-heading-row">
        <div>
          <h2>Lịch theo tuần</h2>
          <span>{{ weekLabel }}</span>
        </div>
        <span>{{ weekSchedules.length }} lịch trong tuần</span>
      </div>

      <div class="calendar-table-wrap">
        <table class="calendar-table">
          <caption class="sr-only">Lịch theo tuần từ {{ weekLabel }}</caption>
          <thead>
            <tr>
              <th scope="col" class="calendar-period-column">Ca học</th>
              <th v-for="day in weekDays" :key="day.key" scope="col" :class="{ today: day.isToday }">
                <span>{{ day.label }}</span>
                <small>{{ day.dateLabel }}</small>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="period in periods" :key="period.value">
              <th scope="row" class="calendar-period-cell">
                <span>{{ period.label }}</span>
                <small>{{ period.time }}</small>
              </th>
              <td
                v-for="day in weekDays"
                :key="`${period.value}-${day.key}`"
                class="calendar-slot"
                :class="{ today: day.isToday }"
              >
                <RouterLink
                  v-for="item in schedulesFor(day.key, period.value)"
                  :key="item.id"
                  class="calendar-event"
                  :class="eventTone(item)"
                  :to="`/classes/${item.learningClassId}`"
                >
                  <span class="calendar-event-time">{{ formatTime(item.startsAt) }} – {{ formatTime(item.endsAt) }}</span>
                  <strong>{{ className(item.learningClassId) }}</strong>
                  <small>{{ eventLabel(item) }}</small>
                </RouterLink>
                <span v-if="!schedulesFor(day.key, period.value).length" class="calendar-slot-empty" aria-hidden="true" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="loading" class="loading-state calendar-loading">Đang tải lịch…</div>
      <div v-else-if="!weekSchedules.length" class="empty-state calendar-empty">
        <strong>{{ viewFilter === 'exam' ? 'Chưa có lịch thi trong tuần này' : 'Chưa có lịch trong tuần này' }}</strong>
        <span>Chọn tuần khác hoặc đổi bộ lọc để xem các lịch đã được sắp xếp.</span>
      </div>

      <div class="calendar-legend" aria-label="Chú thích lịch">
        <span><i class="calendar-legend-swatch lesson" />Buổi học</span>
        <span><i class="calendar-legend-swatch online" />Trực tuyến</span>
        <span><i class="calendar-legend-swatch exam" />Lịch thi</span>
        <span><i class="calendar-legend-swatch suspended" />Tạm ngưng</span>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '../../../shared/stores/auth'
import { academicsApi } from '../../../shared/api/client'
import { type LearningClass, type Schedule } from '../../../shared/types'

type CalendarFilter = 'all' | 'lesson' | 'exam'
type CalendarPeriod = 'morning' | 'afternoon' | 'evening'
type CalendarSchedule = Schedule & {
  kind?: string | number
  type?: string | number
  sessionType?: string | number
  deliveryMode?: string | number
}

const auth = useAuthStore()
const schedules = ref<CalendarSchedule[]>([])
const classes = ref<LearningClass[]>([])
const loading = ref(true)
const error = ref('')
const viewFilter = ref<CalendarFilter>('all')
const weekAnchor = ref(new Date())

const filterOptions: { value: CalendarFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'lesson', label: 'Lịch học' },
  { value: 'exam', label: 'Lịch thi' },
]
const periods: { value: CalendarPeriod; label: string; time: string }[] = [
  { value: 'morning', label: 'Sáng', time: '06:00 – 12:00' },
  { value: 'afternoon', label: 'Chiều', time: '12:00 – 18:00' },
  { value: 'evening', label: 'Tối', time: '18:00 – 24:00' },
]

function startOfWeek(value: Date) {
  const date = new Date(value)
  const day = date.getDay()
  const offset = day === 0 ? -6 : 1 - day
  date.setHours(0, 0, 0, 0)
  date.setDate(date.getDate() + offset)
  return date
}

function formatDateKey(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function parseDateInput(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

const dateInputValue = computed({
  get: () => formatDateKey(weekAnchor.value),
  set: (value: string) => {
    const next = parseDateInput(value)
    if (next) weekAnchor.value = next
  },
})

const weekStart = computed(() => startOfWeek(weekAnchor.value))
const weekDays = computed(() => {
  const dayLabels = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật']
  return dayLabels.map((label, index) => {
    const date = new Date(weekStart.value)
    date.setDate(date.getDate() + index)
    return {
      key: formatDateKey(date),
      label,
      dateLabel: date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }),
      isToday: formatDateKey(date) === formatDateKey(new Date()),
    }
  })
})

const weekLabel = computed(() => {
  const first = weekDays.value[0]
  const last = weekDays.value[6]
  return `${first?.dateLabel} – ${last?.dateLabel}`
})

function scheduleKind(item: CalendarSchedule): Exclude<CalendarFilter, 'all'> | 'online' | 'suspended' {
  const status = String(item.status ?? '').toLowerCase()
  const statusCode = Number(item.status)
  if (statusCode === 2 || status.includes('cancel') || status.includes('suspend') || status.includes('pause') || status.includes('stop')) return 'suspended'
  const raw = [item.kind, item.type, item.sessionType, item.deliveryMode].map(value => String(value ?? '').toLowerCase()).join(' ')
  if (raw.includes('exam') || raw.includes('thi')) return 'exam'
  if (raw.includes('online') || raw.includes('truc') || raw.includes('virtual')) return 'online'
  return 'lesson'
}

const weekSchedules = computed(() => schedules.value.filter(item => {
  const dayKey = formatDateKey(new Date(item.startsAt))
  const inWeek = weekDays.value.some(day => day.key === dayKey)
  const kind = scheduleKind(item)
  const matchesFilter = viewFilter.value === 'all' || (viewFilter.value === 'exam' ? kind === 'exam' : kind !== 'exam')
  return inWeek && matchesFilter
}).sort((a, b) => +new Date(a.startsAt) - +new Date(b.startsAt)))

const names = computed(() => new Map(classes.value.map(item => [item.id, `${item.code} · ${item.name}`])))

function className(id: string) {
  return names.value.get(id) || 'Lớp học'
}

function periodFor(value: string): CalendarPeriod {
  const hour = new Date(value).getHours()
  if (hour < 12) return 'morning'
  if (hour < 18) return 'afternoon'
  return 'evening'
}

function schedulesFor(dayKey: string, period: CalendarPeriod) {
  return weekSchedules.value.filter(item => formatDateKey(new Date(item.startsAt)) === dayKey && periodFor(item.startsAt) === period)
}

function formatTime(value: string) {
  const date = new Date(value)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

function eventTone(item: CalendarSchedule) {
  return `calendar-event-${scheduleKind(item)}`
}

function eventLabel(item: CalendarSchedule) {
  const kind = scheduleKind(item)
  if (kind === 'exam') return 'Lịch thi'
  if (kind === 'online') return 'Trực tuyến'
  if (kind === 'suspended') return 'Tạm ngưng'
  return auth.role === 'teacher' ? 'Buổi giảng' : 'Buổi học'
}

function shiftWeek(amount: number) {
  const next = new Date(weekAnchor.value)
  next.setDate(next.getDate() + amount * 7)
  weekAnchor.value = next
}

function goToday() {
  weekAnchor.value = new Date()
}

function printCalendar() {
  window.print()
}

async function load() {
  loading.value = true
  error.value = ''
  const result = await Promise.allSettled([academicsApi.mySchedules(), academicsApi.classes()])
  if (result[0].status === 'fulfilled') schedules.value = result[0].value
  if (result[1].status === 'fulfilled') classes.value = result[1].value
  if (result[0].status === 'rejected') error.value = 'Không tải được lịch học.'
  loading.value = false
}

onMounted(load)
</script>
