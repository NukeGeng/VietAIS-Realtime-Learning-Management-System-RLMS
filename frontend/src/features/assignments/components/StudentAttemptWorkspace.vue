<template>
  <section :class="['student-attempt-workspace', `student-attempt-workspace--${mode}`]" :aria-label="mode === 'observer' ? 'Không gian làm bài của sinh viên đang được quan sát' : 'Không gian làm bài'">
    <div v-if="mode === 'observer'" class="observer-toolbar">
      <div class="observer-summary">
        <strong>{{ studentLabel }}</strong>
        <span>{{ attempt.answeredCount }}/{{ totalQuestions }} · {{ progress }}% · <i></i> Trực tiếp · {{ following ? 'Đang theo dõi' : 'Đang xem riêng' }}</span>
      </div>
      <div class="observer-actions">
        <RouterLink v-if="backTo" class="mini-button" :to="backTo">← Tổng quan</RouterLink>
        <button class="mini-button" type="button" @click="toggleFollowing">{{ following ? 'Dừng theo dõi' : 'Theo dõi lại' }}</button>
      </div>
    </div>

    <div class="attempt-workspace-content">
      <aside class="attempt-question-list">
        <div class="attempt-question-heading">
          <strong>Câu hỏi · {{ attempt.answeredCount }}/{{ totalQuestions }}</strong>
          <span>{{ progress }}% đã hoàn thành</span>
        </div>
        <div class="attempt-question-nav">
          <button
            v-for="(question, index) in questions"
            :key="question.id"
            class="attempt-question-number"
            :class="{ current: question.id === viewedQuestionId, answered: Boolean(attempt.answers[question.id]) }"
            type="button"
            @click="selectQuestion(question.id)"
          >
            {{ index + 1 }}
          </button>
        </div>
        <div class="progress-track" aria-hidden="true">
          <div class="progress-fill" :style="{ width: `${progress}%` }"></div>
        </div>
      </aside>

      <main v-if="viewedQuestion" class="attempt-question-panel">
        <div class="card-topline">
          <span class="eyebrow">CÂU HỎI {{ viewedIndex + 1 }}</span>
          <StatusPill :value="mode === 'interactive' && submitted ? 'Chỉ xem' : attempt.status" :labels="statusLabels" />
        </div>
        <h2>{{ viewedQuestion.prompt }}</h2>
        <p v-if="isMultipleChoice" class="choice-hint">Chọn một hoặc nhiều đáp án. Cần giữ ít nhất một đáp án đã chọn.</p>
        <div v-if="viewedQuestion.options?.length" class="choice-list">
          <label
            v-for="option in viewedQuestion.options"
            :key="option.key"
            class="choice"
            :class="{ selected: isOptionSelected(option.key) }"
          >
            <input
              v-if="mode === 'interactive'"
              :type="isMultipleChoice ? 'checkbox' : 'radio'"
              :name="viewedQuestion.id"
              :value="option.key"
              :checked="isOptionSelected(option.key)"
              :disabled="saving || submitted || isLastSelectedOption(option.key)"
              @change="selectOption(option.key, ($event.target as HTMLInputElement).checked)"
            />
            <span v-else class="observer-radio" :class="{ 'observer-checkbox': isMultipleChoice }" :role="isMultipleChoice ? 'checkbox' : 'radio'" :aria-checked="isOptionSelected(option.key)" aria-disabled="true" :aria-label="option.text"></span>
            <span>{{ option.text }}</span>
          </label>
        </div>
        <textarea
          v-else-if="mode === 'interactive'"
          v-model="interactiveAnswer"
          class="form-textarea"
          placeholder="Nhập câu trả lời của bạn…"
          :disabled="saving || submitted"
          @blur="selectAnswer(interactiveAnswer)"
        ></textarea>
        <div v-else class="observer-text-answer">{{ displayedAnswer || 'Chưa trả lời' }}</div>
        <div class="question-actions">
          <span class="save-state">{{ mode === 'interactive' ? saveMessage : `Cập nhật ${formatLastActivity}` }}</span>
          <div v-if="mode === 'interactive'" class="page-actions">
            <button class="button secondary" type="button" :disabled="saving || submitted || viewedIndex === 0" @click="selectQuestion(questions[viewedIndex - 1].id)">← Trước</button>
            <button class="button primary" type="button" :disabled="saving || submitted || viewedIndex === questions.length - 1" @click="selectQuestion(questions[viewedIndex + 1].id)">Tiếp →</button>
          </div>
          <span v-else class="observer-view-state">{{ following ? 'Đang theo dõi câu hiện tại' : 'Đang xem riêng câu này' }}</span>
        </div>
      </main>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import type { AssessmentActivity, StudentActivity, StudentDetail } from '../../../shared/types'

type WorkspaceMode = 'interactive' | 'observer'
type Attempt = StudentActivity | StudentDetail

const props = withDefaults(defineProps<{
  mode: WorkspaceMode
  activity: AssessmentActivity
  attempt: Attempt
  saving?: boolean
  submitted?: boolean
  saveMessage?: string
  backTo?: string
}>(), { saving: false, submitted: false, saveMessage: '' })

const emit = defineEmits<{
  'open-question': [questionId: string]
  'select-answer': [questionId: string, value: string]
}>()

const statusLabels = ['Đang làm bài', 'Tạm nghỉ', 'Tạm dừng', 'Đã nộp', 'Đã đóng']
const following = ref(props.mode === 'observer')
const viewedQuestionId = ref('')
const interactiveAnswer = ref('')
const questions = computed(() => {
  const ordered = props.activity.questions.slice().sort((left, right) => left.order - right.order)
  const questionOrder = 'questionOrder' in props.attempt ? props.attempt.questionOrder : []
  if (!questionOrder?.length) return ordered
  const byId = new Map(ordered.map(question => [question.id, question]))
  return questionOrder.map(questionId => byId.get(questionId)).filter(Boolean) as typeof ordered
})
const totalQuestions = computed(() => props.activity.questions.length)
const progress = computed(() => Math.round(props.attempt.answeredCount / Math.max(1, totalQuestions.value) * 100))
const studentLabel = computed(() => 'studentDisplayName' in props.attempt && props.attempt.studentDisplayName || 'Học viên chưa có tên')
const viewedIndex = computed(() => {
  const index = questions.value.findIndex(question => question.id === viewedQuestionId.value)
  return index >= 0 ? index : 0
})
const viewedQuestion = computed(() => questions.value[viewedIndex.value])
const isMultipleChoice = computed(() => viewedQuestion.value?.type === 1 || viewedQuestion.value?.type === '1' || viewedQuestion.value?.type === 'MultipleChoice')
const displayedAnswer = computed(() => props.mode === 'observer'
  ? (viewedQuestion.value ? props.attempt.answers[viewedQuestion.value.id] || '' : '')
  : interactiveAnswer.value)
const selectedOptionKeys = computed(() => {
  const selected = new Set(displayedAnswer.value.split(',').map(value => value.trim().toLowerCase()).filter(Boolean))
  return (viewedQuestion.value?.options || []).filter(option => selected.has(option.key.toLowerCase())).map(option => option.key)
})
const formatLastActivity = computed(() => {
  const value = new Date(props.attempt.lastActivityAt)
  return Number.isNaN(value.getTime()) ? 'vừa cập nhật' : value.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
})

function currentQuestionId() {
  return props.attempt.currentQuestionId || questions.value[0]?.id || ''
}

function syncViewedQuestion() {
  const current = currentQuestionId()
  if (props.mode === 'interactive' || following.value || !viewedQuestionId.value) viewedQuestionId.value = current
  interactiveAnswer.value = viewedQuestion.value ? props.attempt.answers[viewedQuestion.value.id] || '' : ''
}

function selectQuestion(questionId: string) {
  if (props.mode === 'observer') {
    viewedQuestionId.value = questionId
    following.value = questionId === currentQuestionId() && following.value
    return
  }
  if (props.saving) return
  if (props.submitted) {
    viewedQuestionId.value = questionId
    interactiveAnswer.value = props.attempt.answers[questionId] || ''
    return
  }
  emit('open-question', questionId)
}

function selectAnswer(value: string) {
  if (props.mode !== 'interactive' || !viewedQuestion.value || props.saving || props.submitted) return
  interactiveAnswer.value = value
  emit('select-answer', viewedQuestion.value.id, value)
}

function isOptionSelected(key: string) {
  return isMultipleChoice.value ? selectedOptionKeys.value.includes(key) : displayedAnswer.value === key
}

function isLastSelectedOption(key: string) {
  return isMultipleChoice.value && selectedOptionKeys.value.length === 1 && isOptionSelected(key)
}

function selectOption(key: string, checked: boolean) {
  if (props.mode !== 'interactive' || props.saving || props.submitted || !viewedQuestion.value?.options.some(option => option.key === key)) return
  if (!isMultipleChoice.value) {
    if (checked) selectAnswer(key)
    return
  }
  const selected = new Set(selectedOptionKeys.value)
  if (checked) selected.add(key)
  else selected.delete(key)
  if (!selected.size) return
  selectAnswer([...selected].sort().join(','))
}

function toggleFollowing() {
  following.value = !following.value
  if (following.value) viewedQuestionId.value = currentQuestionId()
}

watch(() => props.attempt.currentQuestionId, syncViewedQuestion, { immediate: true })
watch(() => viewedQuestion.value && props.attempt.answers[viewedQuestion.value.id], () => {
  if (props.mode === 'interactive' && viewedQuestion.value) interactiveAnswer.value = props.attempt.answers[viewedQuestion.value.id] || ''
})
watch(() => props.mode, () => {
  following.value = props.mode === 'observer'
  syncViewedQuestion()
})
</script>

<style scoped>
.student-attempt-workspace { width: 100%; overflow: hidden; background: var(--surface); }
.observer-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 14px; padding: 12px 16px; border-bottom: 1px solid var(--border); background: var(--surface); }
.observer-summary { display: grid; gap: 3px; min-width: 0; }
.observer-summary strong { overflow: hidden; color: var(--text); font-size: 14px; text-overflow: ellipsis; white-space: nowrap; }
.observer-summary span { color: var(--text-3); font-size: 11px; }
.observer-summary i { display: inline-block; width: 6px; height: 6px; margin: 0 3px; border-radius: 50%; background: var(--success); }
.observer-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 7px; }
.attempt-workspace-content { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 0; background: var(--surface); }
.attempt-question-list { align-self: start; min-height: 100%; padding: 16px; border-right: 1px solid var(--border); }
.attempt-question-heading { display: grid; gap: 3px; margin-bottom: 11px; }
.attempt-question-heading strong { font-size: 12px; }
.attempt-question-heading span { color: var(--text-3); font-size: 10px; }
.attempt-question-nav { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 7px; }
.attempt-question-number { display: grid; place-items: center; min-height: 34px; border: 1px solid var(--border); border-radius: 7px; background: transparent; color: var(--text-3); font: inherit; font-size: 11px; cursor: pointer; }
.attempt-question-number:hover { border-color: var(--brand); }
.attempt-question-number.answered { border-color: var(--brand); background: var(--brand-soft); color: var(--brand); }
.attempt-question-number.current { outline: 2px solid var(--brand); outline-offset: -2px; color: var(--text); font-weight: 700; }
.attempt-question-list .progress-track { margin-top: 12px; }
.attempt-question-panel { min-height: 500px; padding: 28px; }
.attempt-question-panel h2 { margin: 10px 0 22px; font-size: 22px; line-height: 1.4; }
.choice-list { display: grid; gap: 10px; }
.choice-hint { margin: -10px 0 14px; color: var(--text-3); font-size: 12px; }
.choice { display: flex; align-items: center; gap: 10px; min-height: 48px; padding: 12px 14px; border: 1px solid var(--border); border-radius: 8px; color: var(--text-2); }
.choice.selected { border-color: var(--brand); background: var(--brand-soft); color: var(--text); }
.choice input { accent-color: var(--brand); }
.observer-radio { width: 15px; height: 15px; flex: 0 0 15px; border: 1px solid var(--text-3); border-radius: 50%; }
.choice.selected .observer-radio { border: 4px solid var(--brand); }
.observer-checkbox { border-radius: 3px; }
.choice.selected .observer-checkbox { border: 1px solid var(--brand); background: var(--brand); }
.choice.selected .observer-checkbox::after { content: '✓'; display: block; color: white; font-size: 11px; line-height: 13px; text-align: center; }
.observer-text-answer { min-height: 100px; padding: 12px; border: 1px solid var(--border); border-radius: 8px; color: var(--text-2); }
.question-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-top: 28px; }
.observer-view-state { color: var(--text-3); font-size: 11px; }
@media (max-width: 700px) { .observer-toolbar { align-items: flex-start; flex-direction: column; } .observer-actions { justify-content: flex-start; } .attempt-workspace-content { grid-template-columns: 1fr; } .attempt-question-list { order: 2; min-height: 0; border-right: 0; border-top: 1px solid var(--border); } .attempt-question-panel { min-height: 360px; padding: 18px; } .attempt-question-panel h2 { font-size: 18px; } }
</style>
