<template>
  <div class="essays-page">
    <div v-if="error" class="error-banner">
      <span>{{ error }}</span>
      <button class="mini-button" @click="retryLoad">Thử lại</button>
    </div>

    <TeacherClassSelector
      v-if="!selectedClassId"
      :classes="classes"
      :metadata="classMetadata"
      :loading="loadingClasses"
      title="Tiểu luận"
      description="Chọn một lớp học để quản lý bài tiểu luận, nhóm nghiên cứu và bài nộp."
      @select="selectClass"
    />

    <template v-else>
      <section class="toolbar page-toolbar essay-class-context" aria-labelledby="essay-class-title">
        <div class="essay-class-context-copy">
          <strong id="essay-class-title" class="cell-primary">{{ selectedClass?.code }} · {{ selectedClass?.name }}</strong>
          <span>{{ selectedCourseName }} · {{ selectedStudentCount }} · {{ assignments.length }} bài tiểu luận</span>
        </div>
        <div class="essay-class-context-actions">
          <button class="button secondary" @click="changeClass">Đổi lớp</button>
          <button v-if="canManage" class="button primary" @click="showForm = !showForm">
            {{ showForm ? 'Đóng biểu mẫu' : 'Tạo bài tiểu luận' }}
          </button>
        </div>
      </section>

      <section v-if="showForm && canManage" class="panel essay-form">
        <div class="essay-form-heading">
          <div>
            <h2>Tạo bài tiểu luận</h2>
            <span>{{ selectedClass?.code }} · {{ selectedClass?.name }}</span>
          </div>
          <button class="mini-button" type="button" @click="showForm = false">Đóng</button>
        </div>
        <form class="grid-two" @submit.prevent="createAssignment">
          <div class="form-field">
            <label for="essay-title">Tiêu đề</label>
            <input id="essay-title" v-model.trim="form.title" class="form-input" required placeholder="Đề tài tiểu luận cuối kỳ" />
          </div>
          <div class="form-field">
            <label for="essay-deadline">Hạn nộp</label>
            <input id="essay-deadline" v-model="form.deadline" class="form-input" type="datetime-local" />
          </div>
          <div class="form-field">
            <label>Điểm tối đa / cỡ nhóm</label>
            <div class="form-split">
              <input v-model.number="form.maxScore" class="form-input" type="number" min="1" required aria-label="Điểm tối đa" />
              <input v-model.number="form.maxGroupSize" class="form-input" type="number" min="1" required aria-label="Cỡ nhóm tối đa" />
            </div>
          </div>
          <div class="form-field">
            <label for="essay-description">Mô tả</label>
            <textarea id="essay-description" v-model.trim="form.description" class="form-textarea" rows="3" placeholder="Mục tiêu và yêu cầu của bài tiểu luận" />
          </div>
          <div class="page-actions essay-form-actions">
            <button class="button primary" :disabled="busy">Tạo bài tiểu luận</button>
          </div>
        </form>
      </section>

      <div v-if="loadingAssignments" class="loading-state">Đang tải bài tiểu luận…</div>
      <template v-else>
        <div v-if="assignments.length" class="cards-grid">
          <article v-for="assignment in assignments" :key="assignment.id" class="activity-card essay-assignment-card">
            <div class="card-topline">
              <span class="code-label">{{ assignment.maxScore }} ĐIỂM</span>
              <StatusPill :value="assignment.status" :labels="enumLabels.essayAssignmentStatus" />
            </div>
            <h3>{{ assignment.title }}</h3>
            <p>{{ assignment.description || 'Bài tiểu luận theo nhóm.' }}</p>
            <div class="card-footer">
              <span class="card-meta">{{ assignment.deadline ? `Hạn ${formatDate(assignment.deadline)}` : 'Chưa đặt hạn' }}</span>
              <div class="card-actions">
                <button class="mini-button" @click="selectAssignment(assignment)">
                  {{ selectedAssignment?.id === assignment.id ? 'Đang mở' : 'Mở chi tiết' }}
                </button>
                <button v-if="canManage && isAssignmentPublished(assignment.status)" class="mini-button" @click="closeAssignment(assignment)">Đóng</button>
              </div>
            </div>
          </article>
        </div>
        <div v-else class="panel empty-state">
          <strong>Chưa có bài tiểu luận</strong>
          <span v-if="canManage">Tạo bài tiểu luận đầu tiên cho lớp này.</span>
          <span v-else>Giảng viên chưa tạo bài tiểu luận cho lớp này.</span>
        </div>

        <div v-if="selectedAssignment" class="grid-two essay-detail-grid">
          <section class="panel">
            <div class="panel-heading essay-groups-heading">
              <div>
                <h2>{{ selectedAssignment.title }}</h2>
                <span>{{ groups.length }} nhóm</span>
              </div>
              <div class="card-actions essay-groups-heading-actions">
                <button v-if="canManage" class="mini-button" @click="publishAssignment">Phát hành</button>
                <button v-if="auth.role === 'student' || canManage" class="mini-button" @click="createGroup">Tạo nhóm</button>
              </div>
            </div>
            <div v-if="groups.length" class="list-compact">
              <button
                v-for="group in visibleGroups"
                :key="group.id"
                class="essay-group-row"
                :class="{ active: selectedGroup?.id === group.id }"
                type="button"
                :aria-pressed="selectedGroup?.id === group.id"
                @click="selectGroup(group)"
              >
                <span class="essay-group-copy">
                  <strong>{{ group.name || `Nhóm ${group.id.slice(0, 6)}` }}</strong>
                  <span class="essay-group-summary">{{ memberCount(group) }} thành viên · {{ topicLabel(group) }}</span>
                </span>
                <span class="essay-group-open-state" :class="{ active: selectedGroup?.id === group.id }">
                  {{ selectedGroup?.id === group.id ? 'Đang mở' : 'Mở nhóm' }}
                </span>
              </button>
              <span v-for="slot in emptyGroupSlots" :key="`empty-${slot}`" class="essay-group-slot" aria-hidden="true" />
              <nav class="essay-group-pagination" aria-label="Phân trang nhóm">
                <span>Nhóm {{ groupPageStart }}–{{ groupPageEnd }} trong {{ groups.length }}</span>
                <div class="essay-group-pagination-controls">
                  <button class="mini-button" type="button" aria-label="Trang trước" :disabled="groupPage <= 1" @click="groupPage--">‹</button>
                  <span>Trang {{ groupPage }} / {{ groupPageCount }}</span>
                  <button class="mini-button" type="button" aria-label="Trang sau" :disabled="groupPage >= groupPageCount" @click="groupPage++">›</button>
                </div>
              </nav>
            </div>
            <div v-else class="empty-state">
              <strong>Chưa có nhóm</strong>
              <span>Nhóm được tạo theo quy trình bài tiểu luận.</span>
            </div>
          </section>

          <section class="panel">
            <div v-if="selectedGroup">
              <div class="panel-heading">
                <h2>{{ selectedGroup.name }}</h2>
                <StatusPill :value="selectedGroup.status" :labels="enumLabels.essayGroupStatus" />
              </div>
              <div class="stack">
                <div class="list-compact-item">
                  <div>
                    <strong>Đề tài</strong>
                    <span>{{ topic?.canonicalTitle || topic?.proposedTitle || 'Chưa đăng ký đề tài' }}</span>
                  </div>
                  <StatusPill :value="topic?.status ?? 'Chưa có'" :labels="enumLabels.essayTopicStatus" />
                </div>
                <div class="list-compact-item">
                  <div>
                    <strong>Thành viên</strong>
                    <span>{{ selectedGroup.members?.map(member => member.displayName || member.studentId.slice(0, 8)).join(' · ') || selectedGroup.memberIds?.map(id => id.slice(0, 8)).join(' · ') || 'Chưa có thành viên' }}</span>
                  </div>
                  <form class="inline-form" @submit.prevent="addGroupMember">
                    <input v-model.trim="memberId" class="form-input" required placeholder="Mã học viên" />
                    <button class="mini-button" :disabled="busy">Thêm</button>
                  </form>
                </div>
                <div v-if="topic && canManage" class="page-actions">
                  <button class="mini-button" @click="reviewTopic('approve')">Duyệt</button>
                  <button class="mini-button" @click="reviewTopic('changes')">Yêu cầu chỉnh sửa</button>
                  <button class="mini-button" @click="reviewTopic('reject')">Từ chối</button>
                  <button v-if="String(topic.status).toLowerCase().includes('approv')" class="mini-button" @click="reviewTopic('lock')">Khóa đề tài</button>
                </div>
                <form v-if="!topic && auth.role === 'student'" class="inline-form" @submit.prevent="registerTopic">
                  <div class="form-field">
                    <label>Đề xuất đề tài</label>
                    <input v-model.trim="topicTitle" class="form-input" required placeholder="Đề tài chuẩn sẽ được giảng viên duyệt" />
                  </div>
                  <button class="button primary">Đăng ký</button>
                </form>
                <form v-if="topic && auth.role === 'student' && !isTopicLocked(topic.status)" class="inline-form" @submit.prevent="requestTopicChange">
                  <div class="form-field">
                    <label>Đề xuất đổi đề tài</label>
                    <input v-model.trim="topicChangeTitle" class="form-input" required placeholder="Tên đề tài mới" />
                  </div>
                  <button class="button secondary">Xin đổi đề tài</button>
                </form>
                <div class="panel-heading essay-submission-heading">
                  <h2>Bài nộp</h2>
                  <span>{{ submissions.length }}</span>
                </div>
                <div v-if="submissions.length" class="list-compact">
                  <div v-for="submission in submissions" :key="submission.id" class="list-compact-item">
                    <div>
                      <strong>Phiên bản {{ submission.version }}</strong>
                      <span>{{ submission.fileName || 'Tệp đã tải lên' }} · {{ formatDateTime(submission.submittedAt || submission.uploadedAt) }}</span>
                      <span v-if="submission.errors?.length" class="cell-subtle">{{ submission.errors.map(translateErrorMessage).join(' · ') }}</span>
                    </div>
                    <div class="card-actions">
                      <StatusPill :value="submission.validationStatus || submission.status" :labels="enumLabels.essaySubmissionStatus" />
                      <button class="mini-button" @click="openSubmissionFile(submission)">Mở tệp</button>
                      <button v-if="canManage" class="mini-button" @click="selectedSubmission = submission">Chấm</button>
                    </div>
                  </div>
                </div>
                <div v-else class="empty-state">
                  <strong>Chưa có bài nộp</strong>
                  <span>Tệp PDF/DOCX sẽ được kiểm tra trước khi tạo bài nộp cuối.</span>
                </div>
                <label v-if="auth.role === 'student'" class="button secondary upload-button">Chọn tệp<input type="file" hidden accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" @change="upload" /></label>
                <span v-if="uploadState" class="cell-subtle">{{ uploadState }}</span>
                <form v-if="selectedSubmission && canManage" class="panel grading-form" @submit.prevent="gradeSelected">
                  <div class="panel-heading">
                    <h2>Chấm bài · v{{ selectedSubmission.version }}</h2>
                    <button type="button" class="mini-button" @click="selectedSubmission = null">Đóng</button>
                  </div>
                  <div class="grid-two">
                    <div class="form-field">
                      <label>Điểm</label>
                      <input v-model.number="gradeForm.score" class="form-input" type="number" min="0" :max="selectedAssignment.maxScore" required />
                    </div>
                    <div class="form-field">
                      <label>Nhận xét</label>
                      <input v-model.trim="gradeForm.feedback" class="form-input" placeholder="Nhận xét của giảng viên" />
                    </div>
                  </div>
                  <div class="page-actions grading-form-actions">
                    <button class="button primary" :disabled="grading">Lưu điểm</button>
                    <button v-if="selectedSubmission.gradeEntryId" type="button" class="button secondary" @click="publishSelected">Công bố kết quả</button>
                  </div>
                </form>
              </div>
            </div>
            <div v-else class="empty-state">
              <strong>Chọn một nhóm</strong>
              <span>Chi tiết đề tài và bài nộp chỉ tải cho nhóm đang mở.</span>
            </div>
          </section>
        </div>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { errorMessage, translateErrorMessage } from '../../../shared/api/http'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import TeacherClassSelector from '../../../shared/ui/TeacherClassSelector.vue'
import { useAuthStore } from '../../../shared/stores/auth'
import { academicsApi, essayApi } from '../../../shared/api/client'
import { enumLabels, formatDate, formatDateTime, type EssayAssignment, type EssayGroup, type EssaySubmission, type EssayTopic, type LearningClass } from '../../../shared/types'

interface EssayClassMetadata {
  courseName?: string
  studentCount?: number
  featureCount?: number
}

const auth = useAuthStore()
const classes = ref<LearningClass[]>([])
const classMetadata = ref<Record<string, EssayClassMetadata>>({})
const selectedClassId = ref('')
const assignments = ref<EssayAssignment[]>([])
const selectedAssignment = ref<EssayAssignment | null>(null)
const groups = ref<EssayGroup[]>([])
const groupPage = ref(1)
const selectedGroup = ref<EssayGroup | null>(null)
const topic = ref<EssayTopic | null>(null)
const submissions = ref<EssaySubmission[]>([])
const selectedSubmission = ref<EssaySubmission | null>(null)
const topicTitle = ref('')
const topicChangeTitle = ref('')
const memberId = ref('')
const loadingClasses = ref(true)
const loadingAssignments = ref(false)
const busy = ref(false)
const grading = ref(false)
const error = ref('')
const showForm = ref(false)
const uploadState = ref('')
const assignmentRequest = ref(0)
const groupRequest = ref(0)
const groupDetailRequest = ref(0)
const form = reactive({ title: '', description: '', deadline: '', maxScore: 100, maxGroupSize: 4 })
const gradeForm = reactive({ score: 0, feedback: '' })
const groupsPerPage = 5
const canManage = computed(() => auth.has('assessment.activities.manage'))
const selectedClass = computed(() => classes.value.find(item => item.id === selectedClassId.value))
const selectedClassMeta = computed(() => classMetadata.value[selectedClassId.value])
const selectedCourseName = computed(() => selectedClassMeta.value?.courseName || 'Môn học chưa xác định')
const selectedStudentCount = computed(() => selectedClassMeta.value?.studentCount === undefined ? 'Sĩ số đang cập nhật' : `${selectedClassMeta.value.studentCount} sinh viên`)
const groupPageCount = computed(() => Math.max(1, Math.ceil(groups.value.length / groupsPerPage)))
const visibleGroups = computed(() => groups.value.slice((groupPage.value - 1) * groupsPerPage, groupPage.value * groupsPerPage))
const emptyGroupSlots = computed(() => Math.max(0, groupsPerPage - visibleGroups.value.length))
const groupPageStart = computed(() => (groupPage.value - 1) * groupsPerPage + 1)
const groupPageEnd = computed(() => Math.min(groupPage.value * groupsPerPage, groups.value.length))

function memberCount(group: EssayGroup) { return group.members?.length ?? group.memberIds?.length ?? 0 }
function topicLabel(group: EssayGroup) { return group.topicId ? 'Đã đăng ký đề tài' : 'Chưa có đề tài' }
function isAssignmentPublished(status: string | number) { return status === 1 || String(status).toLowerCase() === 'published' }
function isTopicLocked(status: string | number) { return status === 5 || String(status).toLowerCase() === 'locked' }

async function loadClasses() {
  loadingClasses.value = true
  error.value = ''
  const results = await Promise.allSettled([academicsApi.classes(), academicsApi.courses()])
  if (results[0].status === 'fulfilled') classes.value = results[0].value
  if (results[1].status === 'fulfilled') {
    const courseNames = new Map(results[1].value.map(course => [course.id, `${course.code} · ${course.title}`]))
    classMetadata.value = Object.fromEntries(classes.value.map(item => [item.id, { courseName: courseNames.get(item.courseId) }]))
  }
  if (results[0].status === 'rejected') error.value = 'Không tải được lớp học.'
  loadingClasses.value = false
  if (results[0].status === 'fulfilled') await loadClassMetadata(results[0].value)
}

async function loadClassMetadata(availableClasses: LearningClass[]) {
  const entries = await Promise.all(availableClasses.map(async item => {
    const [enrollmentResult, assignmentResult] = await Promise.allSettled([academicsApi.enrollments(item.id), essayApi.assignments(item.id)])
    const current = classMetadata.value[item.id] || {}
    return [item.id, {
      ...current,
      studentCount: enrollmentResult.status === 'fulfilled' ? enrollmentResult.value.length : undefined,
      featureCount: assignmentResult.status === 'fulfilled' ? assignmentResult.value.length : undefined
    }] as const
  }))
  classMetadata.value = Object.fromEntries(entries)
}

function selectClass(classId: string) {
  selectedClassId.value = classId
  showForm.value = false
  error.value = ''
}

function changeClass() {
  selectedClassId.value = ''
  assignments.value = []
  selectedAssignment.value = null
  groupRequest.value++
  groupDetailRequest.value++
  selectedGroup.value = null
  groupPage.value = 1
  groups.value = []
  topic.value = null
  submissions.value = []
  selectedSubmission.value = null
  showForm.value = false
}

async function loadAssignments(classId: string) {
  const requestId = ++assignmentRequest.value
  loadingAssignments.value = true
  error.value = ''
  assignments.value = []
  selectedAssignment.value = null
  groupRequest.value++
  groupDetailRequest.value++
  groupPage.value = 1
  groups.value = []
  try {
    const result = await essayApi.assignments(classId)
    if (requestId === assignmentRequest.value && selectedClassId.value === classId) assignments.value = result
  } catch (cause) {
    if (requestId === assignmentRequest.value) error.value = errorMessage(cause) || 'Không tải được bài tiểu luận.'
  } finally {
    if (requestId === assignmentRequest.value) loadingAssignments.value = false
  }
}

function retryLoad() {
  return selectedClassId.value ? loadAssignments(selectedClassId.value) : loadClasses()
}

async function createAssignment() {
  const classId = selectedClassId.value
  if (!classId) return
  busy.value = true
  try {
    const assignment = await essayApi.createAssignment(classId, { title: form.title, description: form.description, deadline: form.deadline || undefined, maxScore: form.maxScore, maxGroupSize: form.maxGroupSize, allowResubmission: true })
    assignments.value.unshift(assignment)
    classMetadata.value = { ...classMetadata.value, [classId]: { ...classMetadata.value[classId], featureCount: (classMetadata.value[classId]?.featureCount ?? 0) + 1 } }
    showForm.value = false
    form.title = ''
    form.description = ''
    form.deadline = ''
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không tạo được bài tiểu luận.'
  } finally {
    busy.value = false
  }
}

async function selectAssignment(item: EssayAssignment) {
  const requestId = ++groupRequest.value
  groupDetailRequest.value++
  selectedAssignment.value = item
  groupPage.value = 1
  groups.value = []
  selectedGroup.value = null
  topic.value = null
  submissions.value = []
  try {
    const result = await essayApi.groups(item.id)
    if (requestId === groupRequest.value && selectedAssignment.value?.id === item.id) groups.value = result
  } catch (cause) {
    if (requestId === groupRequest.value) error.value = errorMessage(cause) || 'Không tải được nhóm.'
  }
}

async function publishAssignment() {
  if (!selectedAssignment.value) return
  try {
    const updated = await essayApi.publishAssignment(selectedAssignment.value.id)
    selectedAssignment.value = updated
    const index = assignments.value.findIndex(item => item.id === updated.id)
    if (index >= 0) assignments.value[index] = updated
  } catch (cause) { error.value = errorMessage(cause) || 'Không phát hành được bài tiểu luận.' }
}

async function closeAssignment(assignment: EssayAssignment) {
  if (!window.confirm('Đóng bài tiểu luận này?')) return
  try {
    const updated = await essayApi.closeAssignment(assignment.id)
    Object.assign(assignment, updated)
    if (selectedAssignment.value?.id === assignment.id) selectedAssignment.value = updated
  } catch (cause) { error.value = errorMessage(cause) || 'Không đóng được bài tiểu luận.' }
}

async function createGroup() {
  if (!selectedAssignment.value) return
  const assignmentId = selectedAssignment.value.id
  try {
    const created = await essayApi.createGroup(assignmentId, `Nhóm ${groups.value.length + 1}`)
    if (selectedAssignment.value?.id !== assignmentId) return
    groups.value.push(created)
    groupPage.value = groupPageCount.value
  } catch (cause) { error.value = errorMessage(cause) || 'Không tạo được nhóm.' }
}

async function selectGroup(group: EssayGroup) {
  const requestId = ++groupDetailRequest.value
  selectedGroup.value = group
  topic.value = null
  submissions.value = []
  const result = await Promise.allSettled([essayApi.topic(group.id), essayApi.submissions(group.id)])
  if (requestId !== groupDetailRequest.value || selectedGroup.value?.id !== group.id) return
  if (result[0].status === 'fulfilled') topic.value = result[0].value
  if (result[1].status === 'fulfilled') submissions.value = result[1].value
}

async function addGroupMember() {
  if (!selectedGroup.value || !memberId.value) return
  busy.value = true
  try {
    const updated = await essayApi.addMember(selectedGroup.value.id, memberId.value)
    selectedGroup.value = updated
    const index = groups.value.findIndex(item => item.id === updated.id)
    if (index >= 0) groups.value[index] = updated
    memberId.value = ''
  } catch (cause) { error.value = errorMessage(cause) || 'Không thêm được thành viên.' } finally { busy.value = false }
}

async function registerTopic() {
  if (!selectedGroup.value) return
  try { topic.value = await essayApi.registerTopic(selectedGroup.value.id, topicTitle.value); topicTitle.value = '' } catch (cause) { error.value = errorMessage(cause) || 'Không đăng ký được đề tài.' }
}

async function requestTopicChange() {
  if (!topic.value || !topicChangeTitle.value) return
  try { topic.value = await essayApi.requestTopicChange(topic.value.essayGroupId, topicChangeTitle.value); topicChangeTitle.value = '' } catch (cause) { error.value = errorMessage(cause) || 'Không gửi được yêu cầu đổi đề tài.' }
}

async function reviewTopic(action: 'approve' | 'changes' | 'reject' | 'lock') {
  if (!topic.value) return
  try {
    topic.value = action === 'approve' ? await essayApi.approveTopic(topic.value.id) : action === 'changes' ? await essayApi.requestTopicChanges(topic.value.id) : action === 'reject' ? await essayApi.rejectTopic(topic.value.id) : await essayApi.lockTopic(topic.value.id)
  } catch (cause) { error.value = errorMessage(cause) || 'Không cập nhật được đề tài.' }
}

async function gradeSelected() {
  if (!selectedSubmission.value) return
  grading.value = true
  try {
    const result = await essayApi.gradeSubmission(selectedSubmission.value.id, gradeForm.score, gradeForm.feedback) as { id: string }
    selectedSubmission.value.gradeEntryId = result.id
    selectedSubmission.value.score = gradeForm.score
    selectedSubmission.value.feedback = gradeForm.feedback
  } catch (cause) { error.value = errorMessage(cause) || 'Không lưu được điểm.' } finally { grading.value = false }
}

async function publishSelected() {
  if (!selectedSubmission.value?.gradeEntryId) return
  try { await essayApi.publishGrade(selectedSubmission.value.gradeEntryId); selectedSubmission.value.status = 'Graded' } catch (cause) { error.value = errorMessage(cause) || 'Không công bố được điểm.' }
}

async function openSubmissionFile(submission: EssaySubmission) {
  const popup = window.open('about:blank', '_blank')
  try {
    const file = await essayApi.submissionFile(submission.id)
    if (popup) popup.location.href = file.accessUrl
    else window.location.href = file.accessUrl
  } catch (cause) {
    popup?.close()
    error.value = errorMessage(cause) || 'Không mở được tệp.'
  }
}

async function upload(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file || !selectedAssignment.value || !selectedGroup.value) return
    uploadState.value = 'Đang tải và kiểm tra tệp…'
  try {
    const result = await essayApi.uploadSubmission(selectedAssignment.value.id, selectedGroup.value.id, file)
    submissions.value.unshift(result)
    uploadState.value = result.validationMessage ? translateErrorMessage(result.validationMessage) : 'Tệp đã được tiếp nhận.'
  } catch (cause) {
    uploadState.value = errorMessage(cause) || 'Tải tệp lên thất bại.'
    error.value = uploadState.value
  }
}

watch(selectedClassId, value => { if (value) void loadAssignments(value) })
onMounted(loadClasses)
</script>
