<template>
  <div>
    <PageHeader :eyebrow="roleLabel" :title="greeting" :description="description">
      <template #actions><RouterLink class="button primary" :to="primaryRoute">{{ primaryLabel }}</RouterLink></template>
    </PageHeader>
    <div v-if="error" class="error-banner"><span>{{ error }}</span><button class="mini-button" @click="load">Thử lại</button></div>
    <div class="metrics-grid">
      <MetricCard icon="▦" label="Lớp đang theo dõi" :value="classes.length" :hint="auth.role === 'student' ? 'Các lớp bạn đã tham gia' : 'Các lớp được phân công'" />
      <MetricCard icon="▱" label="Khóa học" :value="courses.length" hint="Nội dung trong tổ chức" />
      <MetricCard icon="◷" label="Lịch sắp tới" :value="upcomingSchedules.length" hint="Trong 30 ngày gần nhất" />
      <MetricCard icon="♢" label="Thông báo chưa đọc" :value="unreadNotifications" hint="Cần xem xét" />
    </div>
    <div class="dashboard-grid">
      <section class="panel">
        <div class="panel-heading"><h2>{{ auth.role === 'student' ? 'Buổi học tiếp theo' : 'Lớp học cần chú ý' }}</h2><span>{{ nextSchedule ? formatDateTime(nextSchedule.startsAt) : 'Chưa có lịch' }}</span></div>
        <div v-if="nextSchedule" class="next-session">
          <div><span class="eyebrow">NEXT SESSION</span><h3>{{ className(nextSchedule.learningClassId) }}</h3><p>{{ formatDateTime(nextSchedule.startsAt) }} · {{ formatDateTime(nextSchedule.endsAt) }}</p></div>
          <RouterLink class="button primary" :to="`/classes/${nextSchedule.learningClassId}`">Mở lớp học</RouterLink>
        </div>
        <div v-else class="empty-state"><strong>Chưa có buổi học sắp tới</strong><span>Lịch học sẽ hiển thị khi tổ chức phân công buổi học.</span></div>
      </section>
      <section class="panel">
        <div class="panel-heading"><h2>Việc cần làm</h2><RouterLink to="/activities" class="mini-button">Xem tất cả</RouterLink></div>
        <div v-if="activities.length" class="todo-list">
          <div v-for="activity in activities.slice(0, 5)" :key="activity.id" class="todo-item">
            <div class="todo-copy"><strong>{{ activity.title }}</strong><span>{{ enumLabel(activity.kind) }} · {{ activity.dueAt ? formatDate(activity.dueAt) : 'Không có hạn' }}</span></div>
            <RouterLink class="mini-button" :to="`/activities/${activity.id}`">Mở</RouterLink>
          </div>
        </div>
        <div v-else class="empty-state"><strong>Không có bài cần làm</strong><span>Các hoạt động được phát hành sẽ xuất hiện ở đây.</span></div>
      </section>
    </div>
    <section class="panel" style="margin-top:16px">
      <div class="panel-heading"><h2>{{ auth.role === 'student' ? 'Tiến độ gần đây' : 'Hoạt động gần đây' }}</h2><RouterLink :to="auth.role === 'student' ? '/progress' : '/reports'" class="mini-button">Mở báo cáo</RouterLink></div>
      <div v-if="classes.length" class="list-compact">
        <div v-for="item in classes.slice(0, 6)" :key="item.id" class="list-compact-item"><div><strong>{{ item.code }} · {{ item.name }}</strong><span>{{ formatDate(item.startDate) }} – {{ formatDate(item.endDate) }}</span></div><StatusPill :value="item.status" /></div>
      </div>
      <div v-else class="empty-state"><strong>Chưa có lớp học</strong><span>Danh sách sẽ được cập nhật theo tổ chức.</span></div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import PageHeader from '../../../shared/ui/PageHeader.vue'
import MetricCard from '../../../shared/ui/MetricCard.vue'
import StatusPill from '../../../shared/ui/StatusPill.vue'
import { useAuthStore } from '../../../shared/stores/auth'
import { academicsApi, assessmentApi, communicationApi } from '../../../shared/api/client'
import { enumLabel, formatDate, formatDateTime, type AssessmentActivity, type LearningClass, type Notification, type Schedule, type Course } from '../../../shared/types'

const auth = useAuthStore()
const classes = ref<LearningClass[]>([])
const courses = ref<Course[]>([])
const schedules = ref<Schedule[]>([])
const activities = ref<AssessmentActivity[]>([])
const notifications = ref<Notification[]>([])
const error = ref('')
const loading = ref(false)
const roleLabel = computed(() => auth.role === 'student' ? 'STUDENT WORKSPACE' : auth.role === 'teacher' ? 'TEACHER WORKSPACE' : 'ADMIN WORKSPACE')
const greeting = computed(() => `Xin chào, ${auth.account?.displayName || 'bạn'}`)
const description = computed(() => auth.role === 'student' ? 'Tập trung vào buổi học tiếp theo, bài cần nộp và tiến độ của bạn.' : 'Nắm nhanh các lớp, lịch và tín hiệu vận hành trong tổ chức.')
const primaryRoute = computed(() => auth.role === 'student' ? '/activities' : auth.role === 'admin' ? '/admin/users' : '/classes')
const primaryLabel = computed(() => auth.role === 'student' ? 'Mở bài tập' : auth.role === 'admin' ? 'Quản lý người dùng' : 'Mở lớp giảng dạy')
const upcomingSchedules = computed(() => schedules.value.filter(item => new Date(item.startsAt).getTime() >= Date.now()).sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime()))
const nextSchedule = computed(() => upcomingSchedules.value[0])
const unreadNotifications = computed(() => notifications.value.filter(item => !item.readAt).length)
const classMap = computed(() => new Map(classes.value.map(item => [item.id, item])))

function className(id: string) { const item = classMap.value.get(id); return item ? `${item.code} · ${item.name}` : 'Lớp học' }

async function load() {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    academicsApi.classes(),
    academicsApi.courses(),
    academicsApi.mySchedules(),
    communicationApi.notifications()
  ])
  const [classResult, courseResult, scheduleResult, notificationResult] = results
  if (classResult.status === 'fulfilled') classes.value = classResult.value
  if (courseResult.status === 'fulfilled') courses.value = courseResult.value
  if (scheduleResult.status === 'fulfilled') schedules.value = scheduleResult.value
  if (notificationResult.status === 'fulfilled') notifications.value = notificationResult.value
  if (classResult.status === 'rejected' && scheduleResult.status === 'rejected') error.value = 'Không tải được dữ liệu dashboard. Kiểm tra kết nối và quyền truy cập.'
  const activityResults = await Promise.allSettled(classes.value.slice(0, 8).map(item => assessmentApi.activities(item.id)))
  activities.value = activityResults.flatMap(result => result.status === 'fulfilled' ? result.value : []).filter(item => ['Published', 'InProgress', 'Open', 1, 2].includes(item.status as never))
  loading.value = false
}

onMounted(load)
</script>
