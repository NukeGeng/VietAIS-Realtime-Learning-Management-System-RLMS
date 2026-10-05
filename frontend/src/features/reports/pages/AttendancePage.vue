<template>
  <div class="attendance-report-page">
    <div v-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button class="mini-button" @click="retry">Thử lại</button>
    </div>

    <TeacherClassSelector v-if="!selectedClassId" :classes="classes" :loading="loading" title="Điểm danh" description="Chọn một lớp để xem từng buổi học và lịch sử tham gia." feature-count-label="buổi học" @select="selectClass" />

    <div v-else class="toolbar page-toolbar attendance-report-toolbar">
      <div class="attendance-report-context">
        <div class="class-context"><strong>{{ selectedClass?.code }} · {{ selectedClass?.name }}</strong><button class="mini-button" @click="changeClass">Đổi lớp</button></div>
        <div class="toolbar-actions">
          <span class="cell-subtle">{{ filteredRows.length }} học viên</span>
          <button class="button secondary" :disabled="!selectedSessionId || selectedSessionId.startsWith('schedule:') || !filteredRows.length" @click="exportExcel">Xuất Excel (.xls)</button>
        </div>
      </div>
      <div class="attendance-report-filters">
        <select v-model="selectedDate" class="form-select" :disabled="!sessionDates.length" aria-label="Ngày học">
          <option value="">Chọn ngày học</option>
          <option v-for="date in sessionDates" :key="date" :value="date">
            {{ dateLabel(date) }}
          </option>
        </select>
        <select v-model="selectedSessionId" class="form-select" :disabled="!daySessions.length" aria-label="Buổi học trong ngày">
          <option value="">Chọn buổi học</option>
          <option v-for="session in daySessions" :key="session.id" :value="session.id">
            {{ sessionTimeLabel(session) }}
          </option>
        </select>
        <input v-model.trim="search" class="search-input" placeholder="Tìm theo tên học viên…" aria-label="Tìm học viên" />
      </div>
    </div>

    <div v-if="loading" class="loading-state">Đang tải dữ liệu điểm danh…</div>
    <template v-else-if="selectedClassId">
      <div class="metrics-grid attendance-report-summary">
        <MetricCard icon="♙" label="Tổng học viên" :value="attendance?.totalStudents ?? reportRows.length" hint="Theo danh sách lớp đang hoạt động" />
        <MetricCard icon="✓" label="Có mặt" :value="attendance?.presentCount ?? 0" hint="Đã vào đúng ngưỡng" />
        <MetricCard icon="!" label="Đi trễ" :value="attendance?.lateCount ?? 0" hint="Sau mốc tính trễ" />
        <MetricCard icon="×" label="Vắng" :value="attendance?.absentCount ?? 0" hint="Chưa có lần vào lớp" />
      </div>

      <section class="panel attendance-report-panel">
        <div class="panel-heading">
          <div>
            <h2>{{ selectedClass?.name || selectedClass?.code || 'Lớp học' }} · Điểm danh</h2>
            <span>{{ selectedSession ? sessionLabel(selectedSession) : 'Chưa chọn phiên trực tuyến' }}</span>
            <span v-if="selectedSession?.startedAt">Phiên thực tế: {{ formatTime(selectedSession.startedAt) }}{{ selectedSession.endedAt ? ' → ' + formatTime(selectedSession.endedAt) : ' · đang diễn ra' }}. Lịch sử bên dưới ghi thời gian tham gia thực tế.</span>
          </div>
          <span v-if="selectedSession && !selectedSession.id.startsWith('schedule:')">Ngưỡng trễ {{ selectedSession.lateAfterMinutes }} phút · TB {{ averageDuration }}</span>
        </div>

        <div v-if="!selectedSessionId" class="empty-state">
          <strong>Chọn một ngày và buổi học</strong>
          <span>Lịch sử điểm danh được lưu riêng cho từng buổi để giáo viên đối chiếu.</span>
        </div>
        <div v-else-if="selectedSessionId.startsWith('schedule:')" class="empty-state">
          <strong>Buổi học chưa có phiên trực tuyến</strong>
          <span>Ngày và giờ lấy theo lịch giảng/lịch học. Dữ liệu điểm danh sẽ có khi phiên được tạo cho buổi này.</span>
        </div>
        <div v-else-if="attendanceLoading" class="loading-state">Đang tải lịch sử phiên…</div>
        <div v-else-if="filteredRows.length" class="table-wrap">
          <table class="data-table attendance-report-table">
            <thead>
              <tr>
                <th>Học viên</th>
                <th>Trạng thái</th>
                <th>Vào lần đầu</th>
                <th>Đi trễ</th>
                <th>Tổng thời gian</th>
                <th>Vào lại</th>
                <th>Rời lần cuối</th>
                <th>Lịch sử vào / rời</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in filteredRows" :key="row.studentId">
                <td>
                  <strong class="cell-primary">{{ row.studentDisplayName }}</strong>
                </td>
                <td><StatusPill :value="row.statusLabel" :tone="row.statusTone" /></td>
                <td>{{ row.firstJoinedAt ? formatDateTime(row.firstJoinedAt) : '—' }}</td>
                <td>{{ row.lateLabel }}</td>
                <td><strong>{{ row.totalDuration }}</strong></td>
                <td>{{ row.reconnectCount }}</td>
                <td>{{ row.lastLeftAt ? formatDateTime(row.lastLeftAt) : row.firstJoinedAt ? 'Đang học' : '—' }}</td>
                <td>
                  <div v-if="row.segments.length" class="attendance-report-history">
                    <span v-for="(line, index) in row.historyLines" :key="`${row.studentId}-${index}`">
                      {{ line }}
                    </span>
                  </div>
                  <span v-else class="cell-subtle">Chưa có lần vào lớp</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-else class="empty-state">
          <strong>Chưa có dữ liệu điểm danh</strong>
          <span>Phiên chưa ghi nhận người học hoặc bộ lọc không có kết quả.</span>
        </div>
      </section>
    </template>
    <div v-else class="panel empty-state">
      <strong>Chọn lớp học</strong>
      <span>Chọn lớp để xem các phiên trực tuyến và lịch sử tham gia.</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import MetricCard from '../../../shared/ui/MetricCard.vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import TeacherClassSelector from '../../../shared/ui/TeacherClassSelector.vue'
import { academicsApi, classroomApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import { useNavigationStore } from '../../../shared/stores/navigation'
import { formatDateTime, type AttendanceRecord, type AttendanceSegment, type AttendanceSummary, type LearningClass, type LiveSession, type Schedule } from '../../../shared/types'

type Enrollment = { id: string; studentId: string; studentDisplayName?: string; status: string | number; enrolledAt: string }
type ReportRow = {
  studentDisplayName: string
  studentId: string
  statusLabel: string
  statusTone: string
  firstJoinedAt?: string
  lastLeftAt?: string
  lateLabel: string
  totalDuration: string
  reconnectCount: number
  segments: AttendanceSegment[]
  historyLines: string[]
}

const classes = ref<LearningClass[]>([])
const route = useRoute()
const navigation = useNavigationStore()
const selectedClassId = ref('')
const sessions = ref<LiveSession[]>([])
const selectedDate = ref('')
const selectedSessionId = ref('')
const schedules = ref<Schedule[]>([])
const enrollments = ref<Enrollment[]>([])
const attendance = ref<AttendanceSummary | null>(null)
const search = ref('')
const loading = ref(true)
const attendanceLoading = ref(false)
const error = ref('')
let classRequestVersion = 0
let attendanceRequestVersion = 0

const selectedClass = computed(() => classes.value.find(item => item.id === selectedClassId.value))
// Include scheduled meetings even when no live session has been created yet.
// Never guess a schedule for an unlinked live session.
const meetingOptions = computed<LiveSession[]>(() => [
  ...schedules.value.flatMap(schedule => {
    const linked = sessions.value.filter(session => session.classScheduleId === schedule.id)
    return linked.length ? linked : [{
      id: `schedule:${schedule.id}`, learningClassId: schedule.learningClassId,
      classScheduleId: schedule.id, teacherId: schedule.teacherId,
      status: 'Scheduled', lateAfterMinutes: 0, mediaRoomId: ''
    }]
  }),
  ...sessions.value.filter(session => !schedules.value.some(schedule => schedule.id === session.classScheduleId))
])
const selectedSession = computed(() => meetingOptions.value.find(item => item.id === selectedSessionId.value))
const sessionDates = computed(() => [...new Set(meetingOptions.value.map(sessionDateKey))].sort((left, right) => {
  if (left === 'unknown') return 1
  if (right === 'unknown') return -1
  return right.localeCompare(left)
}))
const daySessions = computed(() => selectedDate.value
  ? meetingOptions.value.filter(session => sessionDateKey(session) === selectedDate.value).sort((left, right) => {
    const leftTime = sessionStart(left) ? new Date(sessionStart(left) as string).getTime() : Number.MAX_SAFE_INTEGER
    const rightTime = sessionStart(right) ? new Date(sessionStart(right) as string).getTime() : Number.MAX_SAFE_INTEGER
    return leftTime - rightTime
  })
  : [])
const recordByStudent = computed(() => new Map((attendance.value?.records || []).map(record => [record.studentId, record])))

const reportRows = computed<ReportRow[]>(() => {
  const ids = new Set(enrollments.value
    .filter(item => item.status === 0 || String(item.status).toLowerCase() === 'active')
    .map(item => item.studentId))
  for (const record of attendance.value?.records || []) ids.add(record.studentId)
  return [...ids].sort().map(studentId => toReportRow(studentId, recordByStudent.value.get(studentId)))
})

const filteredRows = computed(() => {
  const normalized = search.value.toLowerCase()
  return normalized ? reportRows.value.filter(row => row.studentDisplayName.toLocaleLowerCase('vi').includes(normalized) || row.studentId.toLowerCase().includes(normalized)) : reportRows.value
})

const averageDuration = computed(() => {
  const records = attendance.value?.records || []
  const durations = records.map(record => durationSeconds(record.totalDuration)).filter(value => value > 0)
  if (!durations.length) return '0 phút'
  const total = durations.reduce((sum, value) => sum + value, 0)
  return formatSeconds(Math.round(total / durations.length))
})

async function loadClasses() {
  loading.value = true
  error.value = ''
  try {
    classes.value = await academicsApi.classes()
    // Keep the first screen class-first: the teacher explicitly chooses a card.
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải được danh sách lớp học.'
  } finally {
    loading.value = false
  }
}

function selectClass(id: string) { selectedClassId.value = id }
function changeClass() { selectedClassId.value = '' }

async function retry() {
  await loadClasses()
  if (selectedClassId.value) {
    loading.value = true
    await loadClassData(selectedClassId.value)
    loading.value = false
  }
}

async function loadClassData(classId: string) {
  const version = ++classRequestVersion
  sessions.value = []
  selectedDate.value = ''
  schedules.value = []
  enrollments.value = []
  attendance.value = null
  selectedSessionId.value = ''
  if (!classId) return
  try {
    const [sessionResult, scheduleResult, enrollmentResult] = await Promise.all([
      classroomApi.sessions(classId, true),
      academicsApi.schedules(classId, true),
      academicsApi.enrollments(classId)
    ])
    if (version !== classRequestVersion) return
    sessions.value = sessionResult
    schedules.value = scheduleResult
    enrollments.value = enrollmentResult
    const firstSession = meetingOptions.value.find(session => sessionDateKey(session) !== 'unknown') || meetingOptions.value[0]
    selectedDate.value = firstSession ? sessionDateKey(firstSession) : ''
    selectedSessionId.value = firstSession?.id || ''
  } catch (cause) {
    if (version === classRequestVersion) error.value = errorMessage(cause) || 'Không tải được dữ liệu lớp học.'
  }
}

async function loadAttendance(sessionId: string) {
  const version = ++attendanceRequestVersion
  attendance.value = null
  if (!sessionId || sessionId.startsWith('schedule:')) {
    attendanceLoading.value = false
    return
  }
  attendanceLoading.value = true
  try {
    const result = await classroomApi.attendance(sessionId)
    if (version === attendanceRequestVersion) attendance.value = result
  } catch (cause) {
    if (version === attendanceRequestVersion) error.value = errorMessage(cause) || 'Không tải được lịch sử điểm danh.'
  } finally {
    if (version === attendanceRequestVersion) attendanceLoading.value = false
  }
}

function toReportRow(studentId: string, record?: AttendanceRecord): ReportRow {
  const statusLabel = record ? attendanceStatusLabel(record.status) : 'Chưa ghi nhận'
  return {
    studentId,
    studentDisplayName: record?.studentDisplayName || enrollments.value.find(item => item.studentId === studentId)?.studentDisplayName || 'Học viên chưa có tên',
    statusLabel,
    statusTone: record ? attendanceStatusTone(record.status) : 'muted',
    firstJoinedAt: record?.firstJoinedAt,
    lastLeftAt: record?.lastLeftAt,
    lateLabel: record?.firstJoinedAt ? lateLabel(record) : '—',
    totalDuration: record ? formatDuration(record.totalDuration) : '0 phút',
    reconnectCount: record?.reconnectCount || 0,
    segments: record?.segments || [],
    historyLines: record ? segmentHistory(record.segments || []) : []
  }
}

function attendanceStatusLabel(value: string | number) {
  const key = typeof value === 'number' ? ['present', 'late', 'absent', 'excused'][value] : String(value || '').toLowerCase()
  return ({ present: 'Có mặt', late: 'Đi trễ', absent: 'Vắng', excused: 'Có phép' } as Record<string, string>)[key] || String(value || 'Chưa ghi nhận')
}

function attendanceStatusTone(value: string | number) {
  const key = String(typeof value === 'number' ? ['Present', 'Late', 'Absent', 'Excused'][value] : value).toLowerCase()
  if (key.includes('late')) return 'danger'
  if (key.includes('absent')) return 'muted'
  if (key.includes('excused')) return 'warning'
  return 'success'
}

function lateLabel(record: AttendanceRecord) {
  const joinedAt = record.firstJoinedAt
  if (!joinedAt) return '—'

  // Only the persisted cutoff can establish historical lateness. A current
  // schedule may have changed since this attendance record was created.
  const cutoff = record.lateCutoffAt ? new Date(record.lateCutoffAt).getTime() : NaN
  const delay = Math.max(0, Math.ceil((new Date(joinedAt).getTime() - cutoff) / 1000))
  if (delay > 0 && delay < 60) return 'Dưới 1 phút'
  if (delay >= 60) return formatSeconds(delay)
  // Legacy/manual statuses remain authoritative; do not invent a historical
  // cutoff or label a persisted Late record as on time.
  if (record.status === 1 || String(record.status).toLowerCase() === 'late')
    return record.adjustedBy ? 'Giảng viên ghi nhận đi trễ' : 'Chưa xác định thời lượng'
  return 'Đúng giờ'
}

function sessionLabel(session: LiveSession) {
  return `${dateLabel(sessionDateKey(session))} · ${dayPartLabel(session)} · ${timeRangeLabel(session)} · ${sessionStatusLabel(session)}`
}

function sessionStatusLabel(session: LiveSession) {
  if (session.id.startsWith('schedule:')) {
    const status = scheduleFor(session)?.status
    return status === 2 || String(status).toLowerCase() === 'cancelled' ? 'Tạm ngưng' : 'Chưa tạo phiên trực tuyến'
  }
  return typeof session.status === 'number'
    ? ['Đã lên lịch', 'Đang diễn ra', 'Đã kết thúc', 'Đã hủy'][session.status] || 'Không rõ trạng thái'
    : ({ Scheduled: 'Đã lên lịch', Started: 'Đang diễn ra', Ended: 'Đã kết thúc', Cancelled: 'Đã hủy' } as Record<string, string>)[session.status] || 'Không rõ trạng thái'
}

function scheduleFor(session: LiveSession) {
  return session.classScheduleId ? schedules.value.find(item => item.id === session.classScheduleId) : undefined
}

function sessionDateTime(session: LiveSession) {
  // A report belongs to the scheduled class meeting. Runtime timestamps are
  // only a fallback for sessions that were opened without a schedule.
  return scheduleFor(session)?.startsAt || session.startedAt || session.endedAt
}

function sessionDateKey(session: LiveSession) {
  const value = sessionDateTime(session)
  if (!value) return 'unknown'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'unknown' : `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function dateLabel(value: string) {
  if (value === 'unknown') return 'Phiên chưa gắn lịch học'
  const date = new Date(`${value}T00:00:00`)
  if (Number.isNaN(date.getTime())) return 'Ngày chưa xác định'
  const weekdays = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7']
  return `${weekdays[date.getDay()]} · ${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`
}

function sessionTimeLabel(session: LiveSession) {
  return `${dayPartLabel(session)} · ${timeRangeLabel(session)} · ${sessionStatusLabel(session)}`
}

function sessionStart(session: LiveSession) {
  return scheduleFor(session)?.startsAt || session.startedAt || session.endedAt
}

function sessionEnd(session: LiveSession) {
  return scheduleFor(session)?.endsAt || session.endedAt
}

function timeRangeLabel(session: LiveSession) {
  const startsAt = sessionStart(session)
  const endsAt = sessionEnd(session)
  if (!startsAt) return 'Chưa gắn lịch học'
  const start = formatTime(startsAt)
  return endsAt ? `${start}–${formatTime(endsAt)}` : start
}

function dayPartLabel(session: LiveSession) {
  const value = sessionStart(session)
  if (!value) return 'Buổi học'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Buổi học'
  const hour = date.getHours()
  if (hour < 12) return 'Buổi sáng'
  if (hour < 18) return 'Buổi chiều'
  return 'Buổi tối'
}

function sessionDisplayLabel(session: LiveSession) {
  const className = selectedClass.value?.name || selectedClass.value?.code || 'Lớp học'
  const date = dateLabel(sessionDateKey(session))
  const classAlreadyNamesWeekday = /\b(?:thứ\s*[2-7]|chủ\s*nhật)\b/i.test(className)
  const datePart = classAlreadyNamesWeekday ? date.split(' · ').slice(-1)[0] : date
  return `${className} · ${datePart} · ${dayPartLabel(session)}`
}

function updateAttendanceTabTitle() {
  const className = selectedClass.value?.name || selectedClass.value?.code
  const title = className && selectedSession.value
    ? `Theo dõi lớp học · ${sessionDisplayLabel(selectedSession.value)}`
    : className
      ? `Theo dõi lớp học · ${className}`
      : 'Theo dõi lớp học'
  navigation.renameTab(route.fullPath, title)
}

function formatTime(value?: string) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
}

function segmentHistory(segments: AttendanceSegment[]) {
  return segments.map((segment, index) => {
    const previous = segments[index - 1]
    const gap = previous?.leftAt ? secondsBetween(previous.leftAt, segment.joinedAt) : 0
    const gapLabel = gap > 0 ? ` · gián đoạn ${formatSeconds(gap)}` : ''
    return `Lần ${index + 1}: ${formatTime(segment.joinedAt)} → ${segment.leftAt ? formatTime(segment.leftAt) : 'đang học'} · ${formatDuration(segment.duration)}${gapLabel}`
  })
}

function secondsBetween(start: string, end: string) {
  const difference = new Date(end).getTime() - new Date(start).getTime()
  return Number.isFinite(difference) ? Math.max(0, Math.round(difference / 1000)) : 0
}

function durationSeconds(value?: string) {
  if (!value) return 0
  const parts = value.split(':')
  if (parts.length !== 3) return Number(value) || 0
  const [rawHour, minute, second] = parts
  const dayAndHour = rawHour.split('.')
  const days = dayAndHour.length === 2 ? Number(dayAndHour[0]) : 0
  const hours = Number(dayAndHour.at(-1)) || 0
  return days * 86400 + hours * 3600 + (Number(minute) || 0) * 60 + (Number(second) || 0)
}

function formatDuration(value?: string) {
  const seconds = durationSeconds(value)
  return formatSeconds(seconds)
}

function formatSeconds(seconds: number) {
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  if (hours) return `${hours} giờ ${minutes} phút`
  if (minutes) return `${minutes} phút`
  return seconds > 0 && seconds < 1 ? 'Dưới 1 giây' : `${Math.floor(seconds)} giây`
}

function escapeHtml(value: unknown) {
  return String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character] || character))
}

function exportExcel() {
  const headers = ['Học viên', 'Trạng thái', 'Vào lần đầu', 'Đi trễ', 'Tổng thời gian', 'Số lần vào lại', 'Rời lần cuối', 'Lịch sử vào / rời']
  const rows = filteredRows.value.map(row => [
    row.studentDisplayName,
    row.statusLabel,
    row.firstJoinedAt ? formatDateTime(row.firstJoinedAt) : '',
    row.lateLabel,
    row.totalDuration,
    row.reconnectCount,
    row.lastLeftAt ? formatDateTime(row.lastLeftAt) : row.firstJoinedAt ? 'Đang học' : '—',
    row.historyLines.join(' | ')
  ])
  const title = selectedSession.value ? sessionDisplayLabel(selectedSession.value) : 'Theo dõi lớp học'
  const table = `<table><caption>${escapeHtml(title)}</caption><thead><tr>${headers.map(header => `<th>${escapeHtml(header)}</th>`).join('')}</tr></thead><tbody>${rows.map(row => `<tr>${row.map(cell => `<td>${escapeHtml(cell)}</td>`).join('')}</tr>`).join('')}</tbody></table>`
  const html = `<html><head><meta charset="UTF-8"></head><body>${table}</body></html>`
  const blob = new Blob([`\ufeff${html}`], { type: 'application/vnd.ms-excel;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  const sessionDate = selectedSession.value ? sessionDateKey(selectedSession.value) : new Date().toISOString().slice(0, 10)
  link.download = `rlms-diem-danh-${selectedClass.value?.code || 'lop'}-${sessionDate === 'unknown' ? 'khong-xac-dinh-ngay' : sessionDate}.xls`
  link.click()
  URL.revokeObjectURL(url)
}

watch(selectedClassId, value => { void loadClassData(value) })
watch(selectedDate, value => {
  const selectedStillVisible = daySessions.value.some(session => session.id === selectedSessionId.value)
  if (!selectedStillVisible) selectedSessionId.value = daySessions.value[0]?.id || ''
})
watch(selectedSessionId, value => { void loadAttendance(value) })
watch([selectedClass, selectedSession], updateAttendanceTabTitle)
onMounted(loadClasses)
</script>
