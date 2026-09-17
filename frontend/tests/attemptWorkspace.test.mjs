import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'

function workspace(mode, submitted = false) {
  const source = readFileSync(new URL('../src/features/assignments/components/StudentAttemptWorkspace.vue', import.meta.url), 'utf8')
  const script = source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm, '')
  const calls = []
  const props = { mode, submitted, activity: { questions: [{ id: 'q1', order: 0 }, { id: 'q2', order: 1 }] }, attempt: { currentQuestionId: 'q1', answers: {}, studentId: 'student', answeredCount: 0 } }
  const context = { defineProps: () => props, withDefaults: p => p, defineEmits: () => (...args) => calls.push(args), ref: value => ({ value }), computed: get => ({ get value() { return get() } }), watch() {} }
  vm.createContext(context)
  const state = vm.runInContext(`${ts.transpile(script, { target: ts.ScriptTarget.ES2022 })}\n;({ selectQuestion, viewedQuestionId, syncViewedQuestion, toggleFollowing })`, context)
  state.syncViewedQuestion()
  return { ...state, calls, props }
}

test('interactive navigation waits for server confirmation, including rejected requests', () => {
  const state = workspace('interactive')
  state.selectQuestion('q2')
  assert.equal(state.viewedQuestionId.value, 'q1')
  assert.equal(state.calls[0][0], 'open-question')
  state.props.attempt.currentQuestionId = 'q2'
  state.syncViewedQuestion()
  assert.equal(state.viewedQuestionId.value, 'q2')
})
test('expired attempt inspection is local and emits no mutation', () => {
  const state = workspace('interactive', true)
  state.selectQuestion('q2')
  assert.equal(state.viewedQuestionId.value, 'q2')
  assert.equal(state.calls.length, 0)
})
test('observer inspect never navigates student; follow again restores student question', () => {
  const state = workspace('observer')
  state.selectQuestion('q2')
  assert.equal(state.props.attempt.currentQuestionId, 'q1')
  assert.equal(state.calls.length, 0)
  state.toggleFollowing()
  assert.equal(state.viewedQuestionId.value, 'q1')
})

test('student observer routes A and B share the activity application tab', () => {
  const source = readFileSync(new URL('../src/shared/stores/navigation.ts', import.meta.url), 'utf8')
  const fn = source.slice(source.indexOf('export function applicationTabRoute'), source.indexOf('const menus:')).replace('export ', '')
  const route = vm.runInNewContext(`${ts.transpile(fn)};applicationTabRoute`)
  assert.equal(route('/activities/activity/attempts/A'), '/activities/activity')
  assert.equal(route('/activities/activity/attempts/B'), '/activities/activity')
  assert.equal(route('/classes/class/content'), '/classes/class')
  assert.equal(route('/classes/class/curriculums/curriculum'), '/classes/class/curriculums/curriculum')
})
