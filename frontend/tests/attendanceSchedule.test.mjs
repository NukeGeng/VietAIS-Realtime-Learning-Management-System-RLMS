import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'

function pageState() {
  const page = readFileSync(new URL('../src/features/reports/pages/AttendancePage.vue', import.meta.url), 'utf8')
  const script = page.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1]
    .replace(/^import .*$/gm, '')
  const compiled = ts.transpile(script, { target: ts.ScriptTarget.ES2022 })
  const context = {
    ref: value => ({ value }), computed: get => ({ get value() { return get() } }),
    watch() {}, onMounted() {}, useRoute: () => ({}), useNavigationStore: () => ({})
  }
  vm.createContext(context)
  return vm.runInContext(`${compiled}\n;({ schedules, sessions, meetingOptions, sessionDateKey, sessionStart, sessionEnd, sessionStatusLabel, loadAttendance })`, context)
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
