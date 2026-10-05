import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'

function pageState(overrides = {}) {
  const page = readFileSync(new URL('../src/features/reports/pages/AttendancePage.vue', import.meta.url), 'utf8')
  const script = page.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1]
    .replace(/^import .*$/gm, '')
  const compiled = ts.transpile(script, { target: ts.ScriptTarget.ES2022 })
  const context = {
    ref: value => ({ value }), computed: get => ({ get value() { return get() } }),
    watch() {}, onMounted() {}, useRoute: () => ({}), useNavigationStore: () => ({}),
    ...overrides
  }
  vm.createContext(context)
  return vm.runInContext(`${compiled}\n;({ classes, selectedClassId, schedules, sessions, selectedSessionId, enrollments, attendance, search, filteredRows, meetingOptions, sessionDateKey, sessionStart, sessionEnd, sessionStatusLabel, loadAttendance, lateLabel, exportExcel })`, context)
}

test('scheduled morning and afternoon remain selectable without a live session', () => {
  const page = pageState()
  page.schedules.value = [
    { id: 'morning', startsAt: '2026-09-09T07:00:00', endsAt: '2026-09-09T11:30:00', status: 0 },
    { id: 'afternoon', startsAt: '2026-09-09T13:00:00', endsAt: '2026-09-09T17:30:00', status: 2 }
  ]
  page.sessions.value = [{ id: 'unlinked', status: 0 }]
  const meetings = page.meetingOptions.value
  assert.equal(meetings.length, 3)
  assert.equal(page.sessionDateKey(meetings[0]), '2026-09-09')
  assert.equal(page.sessionStart(meetings[1]), '2026-09-09T13:00:00')
  assert.equal(page.sessionStatusLabel(meetings[1]), 'Tạm ngưng')
  assert.equal(page.sessionDateKey(meetings[2]), 'unknown')
})

test('linked report uses schedule date even when live starts on another day', () => {
  const page = pageState()
  page.schedules.value = [{ id: 'morning', startsAt: '2026-09-09T07:00:00', endsAt: '2026-09-09T11:30:00' }]
  page.sessions.value = [{ id: 'live', classScheduleId: 'morning', startedAt: '2026-09-13T13:00:00' }]
  assert.equal(page.meetingOptions.value.length, 1)
  assert.equal(page.meetingOptions.value[0].id, 'live')
  assert.equal(page.sessionDateKey(page.meetingOptions.value[0]), '2026-09-09')
})

test('schedule without live session does not request attendance API', async () => {
  await pageState().loadAttendance('schedule:morning')
})

function attendancePage() {
  const page = pageState()
  page.sessions.value = [{ id: 'live', startedAt: '2026-10-02T07:00:00Z', lateAfterMinutes: 15 }]
  page.selectedSessionId.value = 'live'
  return page
}

test('lateness below one minute never rounds a Late record to on time', () => {
  const page = attendancePage()
  assert.equal(page.lateLabel({ status: 1, firstJoinedAt: '2026-10-02T07:15:01Z', lateCutoffAt: '2026-10-02T07:15:00Z' }), 'Dưới 1 phút')
  assert.equal(page.lateLabel({ status: 0, firstJoinedAt: '2026-10-02T07:15:00Z', lateCutoffAt: '2026-10-02T07:15:00Z' }), 'Đúng giờ')
})

test('historical lateness uses frozen cutoff instead of a changed schedule/runtime', () => {
  const page = attendancePage()
  assert.equal(page.lateLabel({ status: 'Late', firstJoinedAt: '2026-10-02T07:04:00Z', lateCutoffAt: '2026-10-02T07:00:00Z' }), '4 phút')
})

test('legacy or manually adjusted Late records cannot claim on time', () => {
  const page = attendancePage()
  assert.equal(page.lateLabel({ status: 'Late', firstJoinedAt: '2026-10-02T07:00:00Z' }), 'Chưa xác định thời lượng')
  assert.equal(page.lateLabel({ status: 'Late', firstJoinedAt: '2026-10-02T08:00:00Z' }), 'Chưa xác định thời lượng')
  assert.equal(page.lateLabel({ status: 1, adjustedBy: 'teacher', firstJoinedAt: '2026-10-02T07:00:00Z' }), 'Giảng viên ghi nhận đi trễ')
})

test('attendance shows and searches roster names before a participant has joined', () => {
  const page = attendancePage()
  page.enrollments.value = [{ studentId: 'member-id', status: 0, studentDisplayName: 'Nguyễn Văn Minh' }]
  page.search.value = 'văn minh'
  assert.equal(page.filteredRows.value.length, 1)
  assert.equal(page.filteredRows.value[0].studentDisplayName, 'Nguyễn Văn Minh')
  assert.equal(page.filteredRows.value[0].lateLabel, '—')
  page.search.value = 'Không tồn tại'
  assert.equal(page.filteredRows.value.length, 0)
})

test('Excel export uses student names and preserves table columns and absent leave state', async () => {
  let exportedBlob
  const link = { click() { this.clicked = true } }
  const page = pageState({
    Blob, URL: { createObjectURL(blob) { exportedBlob = blob; return 'blob:attendance' }, revokeObjectURL() {} },
    document: { createElement: () => link }, formatDateTime: value => value
  })
  page.classes.value = [{ id: 'class', code: 'DEMO', name: 'Lớp kiểm thử' }]
  page.selectedClassId.value = 'class'
  page.sessions.value = [{ id: 'live', startedAt: '2026-10-02T07:00:00Z', lateAfterMinutes: 15 }]
  page.selectedSessionId.value = 'live'
  page.enrollments.value = [{ studentId: 'internal-member-id', status: 0, studentDisplayName: 'Minh & An' }]
  page.exportExcel()
  const html = await exportedBlob.text()
  assert.equal(link.clicked, true)
  assert.match(link.download, /^rlms-diem-danh-DEMO-.*\.xls$/)
  assert.equal((html.match(/<th>/g) || []).length, 8)
  const cells = [...html.matchAll(/<td>(.*?)<\/td>/g)].map(match => match[1])
  assert.equal(cells.length, 8)
  assert.equal(cells[0], 'Minh &amp; An')
  assert.equal(cells[6], '—')
  assert.ok(!html.includes('internal-member-id'))
})
