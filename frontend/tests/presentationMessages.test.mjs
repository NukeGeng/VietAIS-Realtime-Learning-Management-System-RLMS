import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'

function loadModule(relative, overrides = {}) {
  const source = readFileSync(new URL(relative, import.meta.url), 'utf8')
    .replace(/^import .*$/gm, '')
    .replace(/^export \{ .*\} from .*$/gm, '')
    .replace(/^const apiBaseUrl = .*$/m, "const apiBaseUrl = '/'")
  const context = { exports: {}, Error, ...overrides }
  vm.createContext(context)
  vm.runInContext(ts.transpile(source, { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS }), context)
  return context.exports
}

const { enumLabel, enumLabels } = loadModule('../src/shared/types/index.ts')
const { translateErrorMessage } = loadModule('../src/shared/api/errorMessages.ts')
const { errorMessage } = loadModule('../src/shared/api/http.ts', {
  translateErrorMessage,
  axios: {
    create: () => ({ interceptors: { request: { use() {} } } }),
    isAxiosError: error => error?.isAxiosError === true
  }
})
const apiError = (status, data) => ({ isAxiosError: true, response: { status, data }, message: `Request failed with status code ${status}` })

function pillState(value, labels) {
  const page = readFileSync(new URL('../src/shared/ui/StatusPill.vue', import.meta.url), 'utf8')
  const source = page.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm, '')
  return vm.runInNewContext(`${ts.transpile(source, { target: ts.ScriptTarget.ES2022 })};({ label, tone })`, {
    defineProps: () => ({ value, labels }), enumLabel,
    computed: get => ({ get value() { return get() } })
  })
}

test('the same REST enum ordinal has its owning domain meaning', () => {
  assert.equal(enumLabel(0, enumLabels.courseStatus), 'Bản nháp')
  assert.equal(enumLabel(0, enumLabels.classStatus), 'Đang chuẩn bị')
  assert.equal(enumLabel(0, enumLabels.essaySubmissionStatus), 'Không đạt kiểm tra')
  assert.equal(enumLabel(0, enumLabels.questionType), 'Một lựa chọn')
  assert.equal(enumLabel(1, enumLabels.questionType), 'Nhiều lựa chọn')
  assert.equal(enumLabel(3, enumLabels.activityKind), 'Bài thi')
  assert.equal(enumLabel(1, enumLabels.lessonProgressStatus), 'Đã hoàn thành')
})

test('numeric and canonical string states use the same contextual label', () => {
  for (const map of Object.values(enumLabels)) {
    Object.keys(map).forEach((name, ordinal) => {
      assert.equal(enumLabel(ordinal, map), enumLabel(name, map), name)
    })
  }
  assert.equal(enumLabel('Published', enumLabels.gradeEntryStatus), 'Đã công bố')
  assert.equal(enumLabel('Rejected', enumLabels.essaySubmissionStatus), 'Không đạt kiểm tra')
  assert.equal(enumLabel('pending_review', enumLabels.essayTopicStatus), 'Chờ duyệt')
})

test('label maps follow the backend enum order used by numeric REST responses', () => {
  const contracts = [
    ['Academics/Modules.Academics.Contracts/CourseStatus.cs', 'CourseStatus', 'courseStatus'],
    ['Academics/Modules.Academics.Contracts/AcademicContentStatus.cs', 'AcademicContentStatus', 'academicContentStatus'],
    ['Academics/Modules.Academics.Contracts/LearningClassStatus.cs', 'LearningClassStatus', 'classStatus'],
    ['Academics/Modules.Academics.Contracts/EnrollmentStatus.cs', 'EnrollmentStatus', 'enrollmentStatus'],
    ['Academics/Modules.Academics.Contracts/ScheduleStatus.cs', 'ScheduleStatus', 'scheduleStatus'],
    ['Assessment/Modules.Assessment/Domain/AssessmentDocuments.cs', 'AssessmentActivityKind', 'activityKind'],
    ['Assessment/Modules.Assessment/Domain/AssessmentDocuments.cs', 'AssessmentActivityStatus', 'activityStatus'],
    ['Assessment/Modules.Assessment/Domain/AssessmentDocuments.cs', 'QuestionType', 'questionType'],
    ['Assessment/Modules.Assessment/Domain/AssessmentDocuments.cs', 'QuestionBankStatus', 'questionBankStatus'],
    ['Assessment/Modules.Assessment/Domain/EssayDocuments.cs', 'EssayAssignmentStatus', 'essayAssignmentStatus'],
    ['Assessment/Modules.Assessment/Domain/EssayDocuments.cs', 'EssayGroupStatus', 'essayGroupStatus'],
    ['Assessment/Modules.Assessment/Domain/EssayDocuments.cs', 'EssayTopicStatus', 'essayTopicStatus'],
    ['Assessment/Modules.Assessment/Domain/EssayDocuments.cs', 'EssaySubmissionStatus', 'essaySubmissionStatus'],
    ['Assessment/Modules.Assessment/Domain/GradingDocuments.cs', 'GradeEntryStatus', 'gradeEntryStatus'],
    ['Classroom/Modules.Classroom.Contracts/StudentActivityStatus.cs', 'StudentActivityStatus', 'studentActivityStatus'],
    ['Classroom/Modules.Classroom/Domain/HelpRequestDocuments.cs', 'HelpRequestStatus', 'helpRequestStatus'],
    ['Classroom/Modules.Classroom/Domain/LiveSessionDocuments.cs', 'AttendanceStatus', 'attendanceStatus'],
    ['Classroom/Modules.Classroom/Domain/LiveSessionDocuments.cs', 'LiveSessionStatus', 'liveSessionStatus'],
    ['Learning/Modules.Learning/Domain/LearningDocuments.cs', 'LearningProgressStatus', 'learningProgressStatus'],
    ['Learning/Modules.Learning/Domain/LearningDocuments.cs', 'LessonProgressStatus', 'lessonProgressStatus'],
    ['Learning/Modules.Learning/Domain/LearningDocuments.cs', 'LearningResourceType', 'resourceType'],
    ['Learning/Modules.Learning/Domain/LearningDocuments.cs', 'LearningResourceStatus', 'resourceStatus'],
    ['Communication/Modules.Communication/Domain/CommunicationDocuments.cs', 'AnnouncementStatus', 'announcementStatus'],
    ['Identity/Modules.Identity/Domain/IdentityDocuments.cs', 'OrganizationStatus', 'organizationStatus'],
    ['Identity/Modules.Identity/Domain/IdentityDocuments.cs', 'AccountStatus', 'accountStatus']
  ]
  for (const [file, name, map] of contracts) {
    const source = readFileSync(new URL(`../../backend/src/Modules/${file}`, import.meta.url), 'utf8')
    const members = source.match(new RegExp(`public enum ${name}\\s*\\{([^}]+)\\}`))[1].split(',').map(value => value.trim()).filter(Boolean)
    assert.deepEqual(Object.keys(enumLabels[map]), members, name)
  }
})

test('numeric status pills show translated labels and their semantic tone', () => {
  const published = pillState(1, enumLabels.courseStatus)
  assert.equal(published.label.value, 'Đã phát hành')
  assert.equal(published.tone.value, 'success')
  assert.equal(pillState(0, enumLabels.courseStatus).tone.value, 'warning')
  assert.equal(pillState(0, enumLabels.essaySubmissionStatus).tone.value, 'danger')
  assert.equal(pillState('Absent').label.value, 'Vắng')
  assert.equal(pillState('Absent').tone.value, 'muted')
})

test('missing values, unknown ordinals and known difficulty/state strings are readable', () => {
  assert.equal(enumLabel(undefined, enumLabels.essayTopicStatus), '—')
  assert.equal(enumLabel(null), '—')
  assert.equal(enumLabel(99, enumLabels.courseStatus), 'Chưa xác định')
  assert.equal(enumLabel(0), 'Chưa xác định')
  assert.equal(enumLabel('Absent'), 'Vắng')
  assert.equal(enumLabel('Medium'), 'Trung bình')
  assert.equal(enumLabel('Superseded'), 'Đã được thay thế')
  assert.equal(enumLabel('Requested'), 'Đang chờ hỗ trợ')
})

test('business validation explains what to correct rather than the HTTP status', () => {
  assert.equal(errorMessage(apiError(400, { error: 'Schedule end must be after schedule start.' })), 'Thời gian kết thúc buổi học phải sau thời gian bắt đầu.')
  assert.equal(errorMessage(apiError(400, { detail: 'A single-choice question needs exactly one correct answer.' })), 'Câu hỏi một lựa chọn phải có đúng một đáp án đúng.')
  assert.equal(errorMessage(apiError(409, { error: 'A course with that code already exists.' })), 'Mã khóa học này đã tồn tại.')
})

test('rejected report errors retain thresholds and assignment section names', () => {
  const error = apiError(400, { error: 'The report failed pre-submission validation.', submission: { errors: ['The report has 50 words; at least 200 are required.', 'Required sections are missing: Introduction, Kết luận.'] } })
  assert.equal(errorMessage(error), 'Báo cáo có 50 từ; yêu cầu ít nhất 200 từ. Báo cáo thiếu các mục bắt buộc: Introduction, Kết luận.')
})

test('field validation, permission and connection failures have useful messages', () => {
  assert.equal(errorMessage(apiError(400, { errors: { password: ['Password must be between 12 and 256 characters.'] } })), 'Mật khẩu phải có từ 12 đến 256 ký tự.')
  assert.equal(errorMessage(apiError(401, undefined)), 'Phiên đăng nhập đã hết hạn.')
  assert.equal(errorMessage(apiError(403, { title: 'Forbidden' })), 'Bạn không có quyền thực hiện thao tác này.')
  assert.equal(errorMessage({ isAxiosError: true }), 'Không kết nối được máy chủ. Hãy kiểm tra kết nối và thử lại.')
})

test('unknown business details and existing Vietnamese text are preserved', () => {
  assert.equal(errorMessage(apiError(409, { error: 'Policy X requires 7 participants.' })), 'Policy X requires 7 participants.')
  assert.equal(errorMessage(new Error('Buổi học này đã được lên lịch.')), 'Buổi học này đã được lên lịch.')
})
