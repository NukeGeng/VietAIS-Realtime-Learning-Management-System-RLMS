<template>
  <div class="student-essay-workspace">
    <div class="toolbar page-toolbar student-essay-toolbar">
      <button type="button" class="button secondary" @click="backToClass">Về tiểu luận lớp</button>
      <div class="student-essay-assignment">
        <strong class="cell-primary">{{ assignment?.title || 'Bài tiểu luận của bạn' }}</strong>
        <span v-if="assignment" class="cell-subtle">{{ assignment.description || 'Hoàn thành và nộp báo cáo theo hướng dẫn của giảng viên.' }}</span>
      </div>
      <div v-if="assignment" class="student-essay-deadline">
        <span>Hạn nộp</span>
        <strong>{{ assignment.deadline ? formatDateTime(assignment.deadline) : 'Chưa đặt' }}</strong>
      </div>
    </div>

    <div v-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button type="button" class="mini-button" @click="load">Thử lại</button>
    </div>
    <div v-if="loading" class="loading-state">Đang tải bài tiểu luận…</div>

    <template v-else-if="assignment">
      <div v-if="!group" class="panel empty-state">
        <strong>Bạn chưa thuộc nhóm cho bài này</strong>
        <span>Liên hệ giảng viên để được thêm vào nhóm trước khi nộp báo cáo.</span>
      </div>
      <template v-else>
        <div class="student-essay-grid">
          <main class="student-essay-main">
            <section class="panel">
              <div class="panel-heading">
                <div><h2>{{ group.name }}</h2><span>{{ group.members?.length || group.memberIds?.length || 0 }} thành viên</span></div>
                <StatusPill :value="group.status" :labels="enumLabels.essayGroupStatus" />
              </div>
              <div class="student-essay-members">
                <span v-for="member in group.members || []" :key="member.studentId" class="student-essay-member">
                  {{ member.displayName || member.studentId.slice(0, 8) }}
                </span>
              </div>
            </section>

            <section class="panel student-essay-topic">
              <div class="panel-heading"><div><h2>Đề tài</h2><span>Trạng thái phê duyệt</span></div><StatusPill v-if="topic" :value="topic.status" :labels="enumLabels.essayTopicStatus" /><span v-else class="cell-subtle">Chưa đăng ký</span></div>
              <strong v-if="topic?.canonicalTitle">{{ topic.canonicalTitle }}</strong>
              <strong v-else-if="topic?.proposedTitle">{{ topic.proposedTitle }}</strong>
              <span v-else class="cell-subtle">Nhóm chưa đăng ký đề tài.</span>
              <span v-if="topic?.canonicalTitle" class="cell-subtle">Đề tài đã được giảng viên duyệt và khóa.</span>
            </section>

            <section class="panel">
              <div class="panel-heading"><div><h2>Nộp báo cáo</h2><span>PDF hoặc DOCX theo chính sách bài</span></div><span v-if="uploading" class="cell-subtle">Đang tải…</span></div>
              <input class="form-input" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" :disabled="uploading || !canUpload" @change="upload" />
              <span v-if="!canUpload" class="cell-subtle">Bài đã đóng hoặc đề tài chưa được khóa nên chưa thể nộp.</span>
              <span v-if="uploadMessage" class="form-hint">{{ uploadMessage }}</span>
            </section>

            <section class="panel">
              <div class="panel-heading"><h2>Lịch sử nộp bài</h2><span>{{ submissions.length }} phiên bản</span></div>
              <div v-if="submissions.length" class="list-compact">
                <div v-for="submission in submissions" :key="submission.id" class="list-compact-item">
                  <div><strong>Phiên bản {{ submission.version }}{{ submission.fileName ? ` · ${submission.fileName}` : '' }}</strong><span>{{ submission.uploadedAt ? formatDateTime(submission.uploadedAt) : '—' }}</span></div>
                  <div class="card-actions"><StatusPill :value="submission.status" :labels="enumLabels.essaySubmissionStatus" /><span v-if="submission.errors?.length" class="cell-subtle">{{ submission.errors.map(translateErrorMessage).join(' · ') }}</span><span v-else-if="submission.validationMessage" class="cell-subtle">{{ translateErrorMessage(submission.validationMessage) }}</span></div>
                </div>
              </div>
              <div v-else class="empty-state"><strong>Chưa có báo cáo</strong><span>Nộp phiên bản đầu tiên khi đề tài đã được khóa.</span></div>
            </section>
          </main>

          <aside class="student-essay-side">
            <section v-if="result" class="panel">
              <div class="panel-heading"><h2>Kết quả</h2><StatusPill value="Published" /></div>
              <strong class="student-essay-score">{{ result.score }}/{{ result.maxScore }}</strong>
              <p v-if="result.feedback" class="cell-subtle">{{ result.feedback }}</p>
            </section>
            <section class="panel student-essay-policy">
              <h2>Thông tin bài</h2>
              <span>Tối đa {{ assignment.maxScore }} điểm</span>
              <span>Tối đa {{ assignment.maxGroupSize }} thành viên</span>
              <span>{{ assignment.allowResubmission ? 'Được nộp lại' : 'Không cho nộp lại' }}</span>
            </section>
          </aside>
        </div>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import { essayApi } from '../../../shared/api/client'
import { errorMessage, translateErrorMessage } from '../../../shared/api/http'
import { enumLabels, formatDateTime, type EssayAssignment, type EssayGroup, type EssaySubmission, type EssayTopic } from '../../../shared/types'

type GradeResult = { score: number; maxScore: number; feedback?: string }
const route = useRoute()
const router = useRouter()
const assignment = ref<EssayAssignment | null>(null)
const group = ref<EssayGroup | null>(null)
const topic = ref<EssayTopic | null>(null)
const submissions = ref<EssaySubmission[]>([])
const result = ref<GradeResult | null>(null)
const loading = ref(true)
const uploading = ref(false)
const error = ref('')
const uploadMessage = ref('')

const canUpload = computed(() => Boolean(assignment.value && group.value && topic.value && ['Approved', 'Locked', 3, 5].includes(topic.value.status as never) && !['Closed', 2].includes(assignment.value.status as never)))

function backToClass() {
  router.push(`/classes/${String(route.params.classId)}/essays`)
}

async function load() {
  loading.value = true
  error.value = ''
  group.value = null
  topic.value = null
  submissions.value = []
  result.value = null
  try {
    const loaded = await essayApi.assignment(String(route.params.essayAssignmentId))
    if (loaded.learningClassId !== String(route.params.classId)) throw new Error('Bài tiểu luận không thuộc lớp học này.')
    assignment.value = loaded
    const groups = await essayApi.groups(loaded.id)
    group.value = groups[0] || null
    if (!group.value) return
    const [topicResult, submissionsResult, resultResult] = await Promise.allSettled([
      essayApi.topic(group.value.id),
      essayApi.submissions(group.value.id),
      essayApi.result(loaded.id, group.value.id)
    ])
    if (topicResult.status === 'fulfilled') topic.value = topicResult.value
    if (submissionsResult.status === 'fulfilled') submissions.value = submissionsResult.value
    if (resultResult.status === 'fulfilled') result.value = resultResult.value as GradeResult
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tải được bài tiểu luận.'
  } finally {
    loading.value = false
  }
}

async function upload(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file || !assignment.value || !group.value) return
  uploading.value = true
  uploadMessage.value = ''
  error.value = ''
  try {
    const uploaded = await essayApi.uploadSubmission(assignment.value.id, group.value.id, file)
    submissions.value = [uploaded, ...submissions.value.filter(item => item.id !== uploaded.id)]
    uploadMessage.value = 'Đã tải báo cáo và nhận kết quả kiểm tra.'
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không thể nộp báo cáo.'
  } finally {
    uploading.value = false
    ;(event.target as HTMLInputElement).value = ''
  }
}

onMounted(load)
</script>
