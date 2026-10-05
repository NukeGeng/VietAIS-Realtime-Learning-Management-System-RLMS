import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'
import * as Vue from 'vue'
import { compile } from '@vue/compiler-dom'
import { renderToString } from '@vue/server-renderer'

const source = readFileSync(new URL('../src/features/assignments/components/StudentAttemptWorkspace.vue', import.meta.url), 'utf8')
const template = source.match(/<template>([\s\S]*?)<\/template>/)[1]
const render = new Function('Vue', ts.transpile(compile(template, { mode: 'function', prefixIdentifiers: true, expressionPlugins: ['typescript'] }).code))(Vue)

function workspace(mode, submitted = false, { type = 'SingleChoice', answers = {} } = {}) {
  const script = source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm, '')
  const calls = []
  const props = Vue.reactive({ mode, submitted, saving: false, saveMessage: '', backTo: undefined, activity: { questions: [{ id: 'q1', order: 0, type, prompt: 'Chọn đáp án', options: [{ key: 'C', text: 'Lựa chọn C' }, { key: 'A', text: 'Lựa chọn A' }, { key: 'B', text: 'Lựa chọn B' }] }, { id: 'q2', order: 1, type: 'SingleChoice', options: [] }] }, attempt: { currentQuestionId: 'q1', answers, studentId: 'student', answeredCount: Object.keys(answers).length, status: 'InProgress', lastActivityAt: '2026-10-02T01:00:00Z' } })
  const context = { defineProps: () => props, withDefaults: p => p, defineEmits: () => (...args) => calls.push(args), ref: Vue.ref, computed: Vue.computed, watch: Vue.watch }
  vm.createContext(context)
  const scope = Vue.effectScope()
  const state = scope.run(() => vm.runInContext(`${ts.transpile(script, { target: ts.ScriptTarget.ES2022 })}\n;({ selectQuestion, selectAnswer, selectOption, isOptionSelected, isLastSelectedOption, isMultipleChoice, selectedOptionKeys, displayedAnswer, viewedQuestion, viewedIndex, viewedQuestionId, following, syncViewedQuestion, toggleFollowing, questions, totalQuestions, progress, studentLabel, formatLastActivity, statusLabels })`, context))
  state.syncViewedQuestion()
  return { ...state, calls, props, dispose: () => scope.stop() }
}

async function renderedWorkspace(state) {
  const app = Vue.createSSRApp({ render, setup: () => ({ ...state.props, ...state }) })
  app.component('StatusPill', { render: () => Vue.h('span') })
  app.component('RouterLink', { render: () => Vue.h('a') })
  return renderToString(app)
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

for (const type of ['MultipleChoice', 1, '1']) {
  test(`multiple answers (${type}) add and remove canonical option keys`, t => {
    const state = workspace('interactive', false, { type })
    t.after(state.dispose)
    state.selectOption('C', true)
    state.selectOption('A', true)
    assert.equal(state.displayedAnswer.value, 'A,C')
    assert.equal(state.calls.at(-1)[2], 'A,C')
    assert.equal(state.isOptionSelected('A'), true)
    assert.equal(state.isOptionSelected('C'), true)
    state.selectOption('C', false)
    assert.equal(state.displayedAnswer.value, 'A')
    assert.equal(state.calls.at(-1)[2], 'A')
    assert.equal(state.isOptionSelected('C'), false)
    state.selectOption('missing', true)
    assert.equal(state.displayedAnswer.value, 'A')
  })
}

test('multiple choice retains the final selected answer required by the API', t => {
  const state = workspace('interactive', false, { type: 'MultipleChoice', answers: { q1: 'A' } })
  t.after(state.dispose)
  assert.equal(state.isLastSelectedOption('A'), true)
  assert.equal(state.isLastSelectedOption('C'), false)
  state.selectOption('A', false)
  assert.equal(state.displayedAnswer.value, 'A')
  assert.equal(state.calls.length, 0)
})

test('multiple choice snapshot and in-place updates restore all selected answers', async t => {
  const state = workspace('interactive', false, { type: 1, answers: { q1: 'c, A' } })
  t.after(state.dispose)
  assert.equal(state.isOptionSelected('A'), true)
  assert.equal(state.isOptionSelected('C'), true)
  state.props.attempt.answers.q1 = 'B,C'
  await Vue.nextTick()
  assert.equal(state.displayedAnswer.value, 'B,C')
  assert.equal(state.isOptionSelected('A'), false)
  assert.equal(state.isOptionSelected('B'), true)
  state.props.attempt.answers = { q1: 'A,B' }
  await Vue.nextTick()
  assert.equal(state.displayedAnswer.value, 'A,B')
  assert.equal(state.calls.length, 0)
})

test('observer renders and receives multiple selected answers without emitting writes', t => {
  const state = workspace('observer', false, { type: 'MultipleChoice', answers: { q1: 'A,C' } })
  t.after(state.dispose)
  assert.equal(state.isOptionSelected('A'), true)
  assert.equal(state.isOptionSelected('C'), true)
  state.selectOption('B', true)
  state.selectAnswer('B')
  assert.equal(state.props.attempt.answers.q1, 'A,C')
  state.props.attempt.answers.q1 = 'B,C'
  assert.equal(state.isOptionSelected('A'), false)
  assert.equal(state.isOptionSelected('B'), true)
  assert.equal(state.calls.length, 0)
})

test('saving and submitted workspaces cannot change an answer', t => {
  const state = workspace('interactive', false, { type: 1, answers: { q1: 'A,C' } })
  t.after(state.dispose)
  state.props.saving = true
  state.selectOption('B', true)
  state.selectAnswer('B')
  state.props.saving = false
  state.props.submitted = true
  state.selectOption('C', false)
  state.selectAnswer('B')
  assert.equal(state.displayedAnswer.value, 'A,C')
  assert.equal(state.calls.length, 0)
})

for (const type of ['SingleChoice', 0, 'TrueFalse', 2]) {
  test(`single answer (${type}) retains radio replacement behavior`, t => {
    const state = workspace('interactive', false, { type, answers: { q1: 'A' } })
    t.after(state.dispose)
    state.selectOption('C', true)
    assert.equal(state.isMultipleChoice.value, false)
    assert.equal(state.displayedAnswer.value, 'C')
    assert.equal(state.isOptionSelected('A'), false)
    assert.equal(state.calls.at(-1)[2], 'C')
  })
}

test('canonical template renders checkboxes for multiple choice and radios for single choice', async t => {
  const multiple = workspace('interactive', false, { type: 1, answers: { q1: 'A,C' } })
  const single = workspace('interactive', false, { type: 0, answers: { q1: 'A' } })
  t.after(multiple.dispose)
  t.after(single.dispose)
  const multipleHtml = await renderedWorkspace(multiple)
  assert.equal((multipleHtml.match(/type="checkbox"/g) || []).length, 3)
  assert.equal((multipleHtml.match(/ checked/g) || []).length, 2)
  const singleHtml = await renderedWorkspace(single)
  assert.equal((singleHtml.match(/type="radio"/g) || []).length, 3)
  assert.equal((singleHtml.match(/ checked/g) || []).length, 1)
})

test('canonical observer template displays selected checkboxes without answer inputs', async t => {
  const state = workspace('observer', false, { type: 'MultipleChoice', answers: { q1: 'A,C' } })
  t.after(state.dispose)
  const html = await renderedWorkspace(state)
  assert.equal((html.match(/class="choice selected"/g) || []).length, 2)
  assert.equal((html.match(/observer-checkbox/g) || []).length, 3)
  assert.doesNotMatch(html, /<input/)
  assert.equal((html.match(/aria-checked="true"/g) || []).length, 2)
  assert.equal((html.match(/role="checkbox"/g) || []).length, 3)
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
