<template>
  <div class="live-page">
    <header class="live-topbar">
      <div class="live-title">
        <span class="brand-mark">R</span>
        <div>
          <strong>{{ classInfo?.code || 'Lớp học trực tuyến' }}</strong>
          <span>{{ liveSessionLabel() }}</span>
        </div>
      </div>
      <div class="topbar-actions">
        <span class="live-connection"><i></i>{{ connectionState }}</span>
        <button type="button" class="live-topbar-button" :class="{ active: showParticipants }" @click="toggleParticipantsPanel">
          <UsersIcon class="live-button-icon" aria-hidden="true" /> Người tham gia <b>{{ participants.length }}</b>
        </button>
        <button type="button" class="live-topbar-button" :class="{ active: showChat }" @click="toggleChatPanel">
          <ChatBubbleLeftRightIcon class="live-button-icon" aria-hidden="true" /> Trò chuyện <b v-if="messages.length">{{ messages.length }}</b>
        </button>
        <button v-if="isTeacher && isSessionStarted" type="button" class="button secondary" @click="end"><StopCircleIcon class="live-button-icon" aria-hidden="true" /> Kết thúc phiên</button>
        <button class="button secondary" @click="leaveToClass">Về lớp học</button>
      </div>
    </header>

    <div v-if="error" class="error-banner" style="margin:14px;background:#2b1d23;border-color:#6c2f39;color:#ffb4b4">
      <span>{{ error }}</span>
      <button class="mini-button" @click="load">Thử lại</button>
    </div>

    <main class="live-body">
      <section class="media-stage">
        <TransitionGroup name="live-tile" tag="div" ref="tileContainer" class="media-canvas participant-grid" :class="{ 'has-presentation': hasPresentation, 'is-single-participant': participants.length === 1 }" aria-label="Người tham gia trong phiên học">
          <article v-for="(tile, index) in pageTiles" :key="tile.userId + (tile.kind === 'screen' ? '::screen' : '::user')" :style="tileStyle(index)" class="participant-tile" :class="{ 'participant-tile--presentation': tile.kind === 'screen', 'participant-tile--presenter-camera': isPresenterCamera(tile), 'participant-tile--local': tile.isLocal }">
            <video v-if="tile.stream" :ref="element => bindParticipantVideo(tile.key, tile.stream, element)" muted autoplay playsinline></video>

            <div v-if="!tile.stream" class="participant-avatar">{{ participantInitials(tile.userId) }}</div>
            <span class="participant-mic" :class="{ muted: tile.muted }" aria-hidden="true"><MicrophoneIcon /></span>
            <span class="participant-nameplate"><i></i>{{ tile.label }}<template v-if="tile.kind === 'screen'"> · Đang trình bày</template></span>
            <button v-if="tile.kind === 'screen' && tile.stream" type="button" class="presentation-fullscreen-button" :title="presentationFullscreenKey === tile.key ? 'Thu nhỏ stream' : 'Phóng to stream'" @click.stop="togglePresentationFullscreen(tile.key)">
              <ArrowsPointingInIcon v-if="presentationFullscreenKey === tile.key" aria-hidden="true" />
              <ArrowsPointingOutIcon v-else aria-hidden="true" />
              <span class="sr-only">{{ presentationFullscreenKey === tile.key ? 'Thu nhỏ stream' : 'Phóng to stream' }}</span>
            </button>
          </article>
          <div v-if="!pageTiles.length" key="placeholder" class="media-placeholder">
            <div class="media-avatar">{{ initials }}</div>
            <strong>{{ isSessionStarted ? 'Bạn đang ở trong lớp' : isSessionEnded ? 'Phiên đã kết thúc' : 'Phiên chưa bắt đầu' }}</strong>
            <span>{{ isSessionStarted ? 'Bật camera hoặc mic để tham gia cùng lớp.' : isSessionEnded ? 'Giáo viên đã kết thúc phiên học này.' : 'Giáo viên cần bắt đầu phiên trước khi tham gia.' }}</span>
          </div>
        </TransitionGroup>

        <div v-if="participantPageCount > 1" class="media-pagination" aria-label="Phân trang người tham gia">
          <button type="button" class="media-pagination-button" :disabled="participantPage === 0" aria-label="Trang trước" @click="previousParticipantPage">
            <ChevronLeftIcon aria-hidden="true" />
          </button>
          <span>Trang {{ participantPage + 1 }} / {{ participantPageCount }}</span>
          <button type="button" class="media-pagination-button" :disabled="participantPage >= participantPageCount - 1" aria-label="Trang sau" @click="nextParticipantPage">
            <ChevronRightIcon aria-hidden="true" />
          </button>
        </div>

        <audio v-for="(stream, key) in remoteStreams" :key="key" class="participant-audio" :ref="element => bindParticipantAudio(String(key), stream, element)" autoplay></audio>
        <div v-if="canUseMedia" class="media-controls" role="group" aria-label="Điều khiển thiết bị">
          <button type="button" class="media-control" :class="{ active: isMicrophoneEnabled }" :disabled="!canUseMedia || mediaBusy || teacherMutedByTeacher" :aria-pressed="isMicrophoneEnabled" :title="teacherMutedByTeacher ? 'Mic bị giảng viên tắt' : isMicrophoneEnabled ? 'Tắt mic' : 'Bật mic'" @click="toggleMicrophone">
            <span class="media-control-icon" aria-hidden="true"><MicrophoneIcon /></span>
            <span>{{ isMicrophoneEnabled ? 'Tắt mic' : 'Bật mic' }}</span>
          </button>
          <button type="button" class="media-control" :class="{ active: isCameraEnabled }" :disabled="!canUseMedia || mediaBusy" :aria-pressed="isCameraEnabled" :title="isCameraEnabled ? 'Tắt camera' : 'Bật camera'" @click="toggleCamera">
            <span class="media-control-icon" aria-hidden="true"><VideoCameraIcon /></span>
            <span>{{ isCameraEnabled ? 'Tắt cam' : 'Bật cam' }}</span>
          </button>
          <button type="button" class="media-control" :class="{ active: isScreenSharing }" :disabled="!canUseMedia || mediaBusy" :aria-pressed="isScreenSharing" :title="isScreenSharing ? 'Dừng chia sẻ màn hình' : 'Chia sẻ màn hình'" @click="toggleScreenShare">
            <span class="media-control-icon" aria-hidden="true"><ComputerDesktopIcon /></span>
            <span>{{ isScreenSharing ? 'Dừng trình bày' : 'Trình bày ngay' }}</span>
          </button>
          <button v-if="!isTeacher" type="button" class="media-control" :class="{ active: handRaised }" :disabled="!canUseMedia" :aria-pressed="handRaised" :title="handRaised ? 'Hạ tay' : 'Giơ tay'" @click="toggleHand">
            <span class="media-control-icon" aria-hidden="true"><HandRaisedIcon /></span>
            <span>{{ handRaised ? 'Hạ tay' : 'Giơ tay' }}</span>
          </button>
        </div>
        <small v-if="mediaMessage" class="media-status" role="status">{{ mediaMessage }}<button v-if="mediaRetryAction" type="button" class="media-retry" @click="retryMedia">Thử lại</button></small>
      </section>

      <aside v-if="showParticipants || showChat" class="live-drawer" aria-label="Thông tin phiên học">
        <section v-if="showParticipants" class="live-panel participants-panel">
          <div class="live-panel-header">
            <div><strong>Người tham gia</strong><span>{{ participants.length }} đang trong phiên</span></div>
            <button type="button" class="panel-close" aria-label="Đóng danh sách người tham gia" @click="showParticipants = false"><XMarkIcon aria-hidden="true" /></button>
          </div>
          <div class="live-panel-body">
            <div v-if="participants.length" class="list-compact">
              <div v-for="participant in participants" :key="participant" class="overview-row participant-overview-row">
                <div class="overview-student">
                  <i class="student-dot"></i>
                  <div><strong>{{ participantLabel(participant) }}</strong><span>{{ participant === localUserId ? 'Thiết bị của bạn' : 'Đã tham gia' }}</span></div>
                </div>
                <div class="participant-actions">
                  <span class="overview-cell">Trực tuyến</span>
                  <template v-if="isTeacher && participant !== localUserId">
                    <button class="mini-button participant-action participant-action--mute" @click="moderateParticipant(participant, isParticipantMuted(participant) ? 'unmute' : 'mute')">{{ isParticipantMuted(participant) ? 'Mở mic' : 'Tắt mic' }}</button>
                    <button class="mini-button participant-action participant-action--kick" @click="moderateParticipant(participant, 'kick')">Mời ra</button>
                    <button class="mini-button participant-action participant-action--block" @click="moderateParticipant(participant, 'block')">Chặn</button>
                  </template>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">
              <strong>Chưa có người tham gia</strong>
              <span>Danh sách sẽ cập nhật ngay khi có người vào lớp.</span>
            </div>
            <div v-if="isTeacher && blockedModerations.length" class="moderation-blocked-list">
              <div class="panel-heading"><h3>Đã chặn trong phiên</h3><span>{{ blockedModerations.length }}</span></div>
              <div v-for="moderation in blockedModerations" :key="moderation.studentId" class="blocked-student-row">
                <strong>{{ participantLabel(moderation.studentId) }}</strong>
                <button class="mini-button participant-action participant-action--unblock" @click="moderateParticipant(moderation.studentId, 'unblock')">Bỏ chặn</button>
              </div>
            </div>
            <div v-if="isTeacher && overview" style="margin-top:14px">
              <RouterLink class="button ghost" :to="'/activities/' + overview.activityId + '?sessionId=' + session?.id">Mở tổng quan hoạt động</RouterLink>
            </div>
            <div v-if="isTeacher && raisedHands.length" class="raised-hands">
              <div class="panel-heading"><h3>Đang giơ tay</h3><span>{{ raisedHands.length }}</span></div>
              <div v-for="hand in raisedHands" :key="hand.studentId" class="raised-hand-row">
                <strong>{{ hand.studentId.slice(0, 8) }}</strong>
                <span>{{ formatTime(hand.raisedAt) }}</span>
                <button class="mini-button" @click="clearHand(hand.studentId)">Đã gọi</button>
              </div>
            </div>
          </div>
        </section>

        <section v-if="showParticipants && isTeacher && attendance" class="live-panel attendance-panel">
          <div class="live-panel-header">
            <strong>Điểm danh</strong>
            <span>{{ attendance.attendancePercentage }}% · Trễ sau {{ session?.lateAfterMinutes ?? 15 }} phút</span>
          </div>
          <div class="attendance-metrics">
            <span><strong>{{ attendance.presentCount }}</strong> Có mặt</span>
            <span><strong>{{ attendance.lateCount }}</strong> Muộn</span>
            <span><strong>{{ attendance.absentCount }}</strong> Vắng</span>
            <span><strong>{{ attendance.excusedCount }}</strong> Có phép</span>
          </div>
          <div class="live-panel-body">
            <div v-if="attendanceRows.length" class="list-compact">
              <div v-for="student in attendanceRows" :key="student.studentId" class="attendance-row">
                <div class="overview-student"><i class="student-dot"></i><strong>{{ student.studentId.slice(0, 8) }}</strong></div>
                <select v-model="attendanceDrafts[student.studentId].status" class="form-select attendance-select">
                  <option value="Present">Có mặt</option>
                  <option value="Late">Muộn</option>
                  <option value="Absent">Vắng</option>
                  <option value="Excused">Có phép</option>
                </select>
                <input v-model.trim="attendanceDrafts[student.studentId].reason" class="form-input attendance-reason" placeholder="Lý do" />
                <button class="mini-button" :disabled="attendanceBusy === student.studentId" @click="adjustAttendance(student.studentId)">
                  {{ attendanceBusy === student.studentId ? '…' : 'Lưu' }}
                </button>
                <div v-if="student.record" class="attendance-history">
                  <span>Học {{ formatDuration(student.record.totalDuration) }} · {{ student.record.reconnectCount || 0 }} lần vào lại</span>
                  <details v-if="student.record.segments?.length">
                    <summary>Lịch sử vào/rời</summary>
                    <div v-for="(segment, index) in student.record.segments" :key="`${student.studentId}-${index}`">
                      Lần {{ index + 1 }}: {{ formatTime(segment.joinedAt) }} → {{ segment.leftAt ? formatTime(segment.leftAt) : 'đang học' }} · {{ formatDuration(segment.duration) }}
                      <template v-if="index > 0 && student.record.segments[index - 1]?.leftAt"> · Gián đoạn {{ formatDurationBetween(student.record.segments[index - 1].leftAt, segment.joinedAt) }}</template>
                    </div>
                  </details>
                </div>
              </div>
            </div>
            <div v-else class="empty-state"><strong>Chưa có danh sách lớp</strong><span>Điểm danh sẽ xuất hiện khi lớp có học viên.</span></div>
          </div>
        </section>

        <section v-if="showChat" class="live-panel live-chat">
          <div class="live-panel-header"><div><strong>Trò chuyện</strong><span>{{ messages.length }} tin nhắn</span></div><button type="button" class="panel-close" aria-label="Đóng trò chuyện" @click="showChat = false"><XMarkIcon aria-hidden="true" /></button></div>
          <div class="chat-messages">
            <div v-for="message in messages" :key="message.id" class="chat-message">
              <strong>{{ message.senderId === auth.account?.id ? 'Bạn' : participantLabel(message.senderId) }}</strong>
              <span>{{ message.body }}</span>
            </div>
            <div v-if="!messages.length" class="empty-state"><strong>Chưa có tin nhắn</strong><span>Trao đổi nhanh trong phiên học.</span></div>
          </div>
          <form class="chat-form" @submit.prevent="send">
            <input v-model.trim="draft" placeholder="Viết tin nhắn…" :disabled="!canChat" />
            <button class="button primary" :disabled="!draft || !canChat">Gửi</button>
          </form>
        </section>
      </aside>
    </main>

    <footer v-if="isTeacher && (isSessionScheduled || isSessionEnded)" class="live-footer">
      <button v-if="isTeacher && isSessionScheduled" class="button primary" @click="start">Bắt đầu phiên</button>
      <span v-if="isTeacher && isSessionEnded" class="cell-subtle">Phiên đã kết thúc</span>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { createMediaLanes, receiveMediaLanes } from '../media/mediaLanes'
import { layoutLiveTiles, pageUserTiles } from '../media/tileLayout'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { ArrowsPointingInIcon, ArrowsPointingOutIcon, ChatBubbleLeftRightIcon, ChevronLeftIcon, ChevronRightIcon, ComputerDesktopIcon, HandRaisedIcon, MicrophoneIcon, StopCircleIcon, UsersIcon, VideoCameraIcon, XMarkIcon } from '@heroicons/vue/24/outline'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../../shared/stores/auth'
import { useNavigationStore } from '../../../shared/stores/navigation'
import { academicsApi, assessmentApi, classroomApi } from '../../../shared/api/client'
import { errorMessage } from '../../../shared/api/http'
import { type AttendanceSummary, type ClassOverview, type LearningClass, type LiveJoinResponse, type LiveSession, type LiveSessionModeration } from '../../../shared/types'

type Enrollment = { id: string; studentId: string; status: string | number; enrolledAt: string }
type MediaOfferSignal = { fromUserId: string; toUserId: string; sdp: string }
type MediaAnswerSignal = { fromUserId: string; toUserId: string; sdp: string }
type MediaIceCandidateSignal = { fromUserId: string; toUserId: string; candidate: string; sdpMid?: string | null; sdpMLineIndex?: number | null }
type PresentationReservationResponse = { acquired: boolean; presenterId?: string | null; presenterIsTeacher?: boolean }
type PresentationSignal = { presenterId: string; presenterIsTeacher: boolean; startedAt: string }
type PresentationReplacementSignal = { previousPresenterId: string; presenterId: string; presenterIsTeacher: boolean; at: string }
type PresentationStoppedSignal = { presenterId: string; at: string }
type LiveSessionEndedSignal = { sessionId: string; endedAt: string }
type LiveSessionStartedSignal = { sessionId: string; startedAt: string }
type LiveSessionStateSignal = { sessionId: string; status: string | number; startedAt?: string; endedAt?: string }
type RemoteMediaKind = 'screen' | 'camera'
type ParticipantTile = { key: string; userId: string; kind: RemoteMediaKind | 'avatar'; stream?: MediaStream; isLocal: boolean; label: string; muted: boolean }

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const navigation = useNavigationStore()
const session = ref<LiveSession | null>(null)
const joined = ref<LiveJoinResponse | null>(null)
const classInfo = ref<LearningClass | null>(null)
const participants = ref<string[]>([])
const participantNames = ref<Record<string, string>>({})
const messages = ref<{ id: string; senderId: string; body: string; sentAt: string }[]>([])
const draft = ref('')
const handRaised = ref(false)
const error = ref('')
const connection = ref<HubConnection | null>(null)
const connectionState = ref('Đang kết nối')
const overview = ref<ClassOverview | null>(null)
const attendance = ref<AttendanceSummary | null>(null)
const enrollments = ref<Enrollment[]>([])
const attendanceDrafts = ref<Record<string, { status: string; reason: string }>>({})
const attendanceBusy = ref('')
const moderations = ref<LiveSessionModeration[]>([])
const teacherMutedByTeacher = ref(false)
const leaving = ref(false)
const showParticipants = ref(false)
const showChat = ref(false)
const presentationFullscreenKey = ref('')
const presentationOwnerId = ref('')
const participantPage = ref(0)
const localMedia = ref<MediaStream | null>(null)
const cameraTrack = ref<MediaStreamTrack | null>(null)
const microphoneTrack = ref<MediaStreamTrack | null>(null)
const screenTrack = ref<MediaStreamTrack | null>(null)
const mediaBusy = ref(false)
const mediaMessage = ref('')
const mediaRetryAction = ref<'microphone' | 'camera' | 'screen' | ''>('')
const remoteStreams = ref<Record<string, MediaStream>>({})
const participantVideoElements = new Map<string, HTMLVideoElement>()
const participantAudioElements = new Map<string, HTMLAudioElement>()
const peerConnections = new Map<string, RTCPeerConnection>()
type PeerMediaTransceivers = { audio: RTCRtpTransceiver; screen: RTCRtpTransceiver; camera: RTCRtpTransceiver }
const peerMediaTransceivers = new Map<RTCPeerConnection, PeerMediaTransceivers>()
const pendingMediaOffers: MediaOfferSignal[] = []
const pendingMediaIceSignals: MediaIceCandidateSignal[] = []
const remoteTrackMuted = ref<Record<string, boolean>>({})
const pendingIceCandidates = new Map<string, RTCIceCandidateInit[]>()

const isTeacher = computed(() => auth.has('classroom.session.manage'))
const localUserId = computed(() => auth.account?.id || '')
const isSessionScheduled = computed(() => {
  const status = session.value?.status
  return status === 0 || String(status).toLowerCase() === 'scheduled'
})
const isSessionStarted = computed(() => {
  const status = session.value?.status
  return status === 1 || String(status).toLowerCase() === 'started'
})
const isSessionEnded = computed(() => {
  const status = session.value?.status
  return status === 2 || String(status).toLowerCase() === 'ended'
})
const isCameraEnabled = computed(() => Boolean(cameraTrack.value))
const isMicrophoneEnabled = computed(() => Boolean(microphoneTrack.value))
const isScreenSharing = computed(() => Boolean(screenTrack.value))
const canChat = computed(() => Boolean(joined.value))
const canUseMedia = computed(() => isSessionStarted.value && Boolean(joined.value))
const joiningSession = ref(false)
const attendanceRows = computed(() => enrollments.value.map(item => ({
  studentId: item.studentId,
  record: attendance.value?.records.find(value => value.studentId === item.studentId)
})))
const blockedModerations = computed(() => moderations.value.filter(item => item.isBlocked))
const participantTiles = computed<ParticipantTile[]>(() => {
  const tiles: ParticipantTile[] = []
  for (const userId of participants.value) {
    const isLocal = userId === localUserId.value
    const cameraStream = isLocal
      ? cameraTrack.value ? new MediaStream([cameraTrack.value, ...(microphoneTrack.value ? [microphoneTrack.value] : [])]) : undefined
      : remoteStreams.value[`${userId}::camera`]
    const screenStream = isLocal
      ? screenTrack.value ? new MediaStream([screenTrack.value, ...(microphoneTrack.value ? [microphoneTrack.value] : [])]) : undefined
      : remoteStreams.value[`${userId}::screen`]
    const hasCamera = hasRenderableVideo(cameraStream, isLocal ? undefined : `${userId}::camera`)
    const hasScreen = presentationOwnerId.value === userId && hasRenderableVideo(screenStream, isLocal ? undefined : `${userId}::screen`)
    const muted = isLocal
      ? !microphoneTrack.value
      : !cameraStream?.getAudioTracks().some(track => track.readyState === 'live' && track.enabled && !track.muted)
    if (hasScreen) tiles.push({ key: `${userId}::screen`, userId, kind: 'screen', stream: screenStream, isLocal, label: participantLabel(userId), muted })
    if (hasCamera) tiles.push({ key: `${userId}::camera`, userId, kind: 'camera', stream: cameraStream, isLocal, label: participantLabel(userId), muted })
    // Keep the presenter visible in the participant rail even when their
    // camera is off, just like Meet's side filmstrip.
    if (!hasCamera) tiles.push({ key: `${userId}::avatar`, userId, kind: 'avatar', isLocal, label: participantLabel(userId), muted })
  }
  return tiles
})
const hasPresentation = computed(() => participantTiles.value.some(tile => tile.kind === 'screen'))
const presentationScreenTile = computed(() => participantTiles.value.find(tile => tile.kind === 'screen'))
const tileContainer = ref<{ $el: HTMLElement } | null>(null)
const stageSize = ref({ width: 1000, height: 600 })
let stageObserver: ResizeObserver | undefined
const userTiles = computed(() => participantTiles.value.filter(tile => tile.kind !== 'screen'))
const pageCapacity = computed(() => hasPresentation.value ? (stageSize.value.width < 640 ? 3 : 6) : (stageSize.value.width < 640 ? 6 : 12))
const userPage = computed(() => pageUserTiles(userTiles.value, presentationScreenTile.value?.userId, participantPage.value, pageCapacity.value))
const participantPageCount = computed(() => userPage.value.pageCount)
const pageTiles = computed(() => presentationScreenTile.value
  ? [presentationScreenTile.value, ...userPage.value.users] : userPage.value.users)
const presentationRatio = computed(() => {
  const settings = presentationScreenTile.value?.stream?.getVideoTracks()[0]?.getSettings()
  return settings?.width && settings?.height ? settings.width / settings.height : 16 / 9
})
const tileRects = computed(() => layoutLiveTiles(pageTiles.value.length, hasPresentation.value,
  stageSize.value.width, stageSize.value.height, presentationRatio.value))
function tileStyle(index: number) {
  const rect = tileRects.value[index]
  return rect ? { left: `${rect.left}px`, top: `${rect.top}px`, width: `${rect.width}px`, height: `${rect.height}px` } : {}
}
const initials = computed(() => (auth.account?.displayName || 'RL').split(/\s+/).slice(-2).map(item => item[0]).join('').toUpperCase())
const raisedHands = ref<{ id: string; liveSessionId: string; studentId: string; status: string | number; raisedAt: string; loweredAt?: string; clearedBy?: string }[]>([])

function liveSessionLabel() {
  const className = classInfo.value?.name || 'Buổi học trực tuyến'
  const value = session.value?.startedAt || session.value?.endedAt
  if (!value) return className
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return className
  const weekdays = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7']
  const datePart = `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`
  const dateLabel = /\b(?:thứ\s*[2-7]|chủ\s*nhật)\b/i.test(className) ? datePart : `${weekdays[date.getDay()]} · ${datePart}`
  const period = date.getHours() < 12 ? 'Buổi sáng' : date.getHours() < 18 ? 'Buổi chiều' : 'Buổi tối'
  return `${className} · ${dateLabel} · ${period}`
}

watch([participantPageCount, hasPresentation], () => {
  participantPage.value = Math.min(participantPage.value, participantPageCount.value - 1)
})
watch(hasPresentation, () => {
  participantPage.value = 0
})

function formatTime(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function participantLabel(userId: string) {
  return userId === localUserId.value ? (auth.account?.displayName || 'Bạn') + ' (bạn)' : participantNames.value[userId] || `Học viên ${userId.slice(0, 8)}`
}

function participantInitials(userId: string) {
  const label = participantLabel(userId).replace(/\s+\(bạn\)$/, '')
  return label.split(/\s+/).slice(-2).map(item => item[0]).join('').toUpperCase()
}

function hasRenderableVideo(stream?: MediaStream, key?: string) {
  return Boolean(stream?.getVideoTracks().some(track => track.readyState === 'live' &&
    (key ? remoteTrackMuted.value[key] !== true : true)))
}

function isPresenterCamera(tile: ParticipantTile) {
  return tile.kind === 'camera' && participantTiles.value.some(item => item.kind === 'screen' && item.userId === tile.userId)
}

function previousParticipantPage() {
  participantPage.value = Math.max(0, participantPage.value - 1)
}

function nextParticipantPage() {
  participantPage.value = Math.min(participantPageCount.value - 1, participantPage.value + 1)
}

function toggleParticipantsPanel() {
  showParticipants.value = !showParticipants.value
  if (showParticipants.value) showChat.value = false
}

function toggleChatPanel() {
  showChat.value = !showChat.value
  if (showChat.value) showParticipants.value = false
}

function togglePresentationFullscreen(key: string) {
  const video = participantVideoElements.get(key)
  if (!video) return
  if (document.fullscreenElement === video) {
    void document.exitFullscreen?.()
  } else {
    void video.requestFullscreen?.()
  }
}

function handleFullscreenChange() {
  const element = document.fullscreenElement
  const match = element && [...participantVideoElements.entries()].find(([, video]) => video === element)
  presentationFullscreenKey.value = match?.[0] || ''
}

function rtcIceServers(): RTCIceServer[] {
  const configured = String(import.meta.env.VITE_RTC_ICE_SERVERS || '')
    .split(',')
    .map(value => value.trim())
    .filter(Boolean)
  return configured.length ? [{ urls: configured }] : []
}

function getLocalMedia() {
  if (!localMedia.value) localMedia.value = new MediaStream()
  return localMedia.value
}

async function syncMediaPreview() {
  await nextTick()
  for (const tile of participantTiles.value) {
    const video = participantVideoElements.get(tile.key)
    if (video && tile.stream) {
      video.srcObject = tile.stream
      video.play().catch(() => undefined)
    }
  }
}

function mediaDevicesAvailable() {
  if (typeof navigator === 'undefined' || !navigator.mediaDevices) {
    mediaMessage.value = 'Thiết bị media chưa khả dụng trong trình duyệt này. Hãy mở bằng HTTPS hoặc localhost rồi thử lại.'
    return false
  }
  return true
}

function mediaAccessError(cause: unknown, device: string) {
  const name = typeof cause === 'object' && cause !== null && 'name' in cause ? String(cause.name) : ''
  if (device === 'màn hình' && name === 'AbortError') {
    mediaMessage.value = 'Bạn đã hủy chia sẻ màn hình.'
    mediaRetryAction.value = ''
  } else if (name === 'NotAllowedError' || name === 'SecurityError') {
    mediaMessage.value = 'Quyền truy cập ' + device + ' đang bị chặn. Hãy cho phép trong cài đặt trình duyệt rồi thử lại.'
  } else if (name === 'NotFoundError') {
    mediaMessage.value = 'Không tìm thấy ' + device + ' trên thiết bị.'
  } else if (name === 'NotReadableError') {
    mediaMessage.value = device[0].toUpperCase() + device.slice(1) + ' đang được ứng dụng khác sử dụng.'
  } else if (name === 'NotSupportedError' || name === 'OverconstrainedError') {
    mediaMessage.value = device[0].toUpperCase() + device.slice(1) + ' không được môi trường hiện tại hỗ trợ.'
  } else {
    mediaMessage.value = 'Không thể bật ' + device + '. Vui lòng thử lại.'
  }
}

function removeCameraTrack(updatePeers = true) {
  const track = cameraTrack.value
  if (!track) return
  track.onended = null
  track.stop()
  localMedia.value?.removeTrack(track)
  cameraTrack.value = null
  void syncMediaPreview()
  if (updatePeers) void syncOutgoingTracks()
}

function removeMicrophoneTrack(updatePeers = true) {
  const track = microphoneTrack.value
  if (!track) return
  track.onended = null
  track.stop()
  localMedia.value?.removeTrack(track)
  microphoneTrack.value = null
  if (updatePeers) void syncOutgoingTracks()
}

function stopScreenShare(updatePeers = true, notifyServer = true) {
  const track = screenTrack.value
  if (track) {
    track.onended = null
    track.stop()
    localMedia.value?.removeTrack(track)
    screenTrack.value = null
    void syncMediaPreview()
    mediaMessage.value = 'Đã dừng chia sẻ màn hình.'
    mediaRetryAction.value = ''
    if (updatePeers) void syncOutgoingTracks()
  }
  if (notifyServer) void releasePresentation()
}

function stopMedia() {
  removeCameraTrack(false)
  removeMicrophoneTrack(false)
  stopScreenShare(false)
  localMedia.value?.getTracks().forEach(track => track.stop())
  localMedia.value = null
  participantVideoElements.forEach(video => { video.srcObject = null })
  participantVideoElements.clear()
  participantAudioElements.forEach(audio => { audio.srcObject = null })
  participantAudioElements.clear()
  presentationOwnerId.value = ''
  mediaMessage.value = ''
  mediaRetryAction.value = ''
}

async function toggleMicrophone() {
  if (!canUseMedia.value || mediaBusy.value) return
  if (teacherMutedByTeacher.value && !isTeacher.value) {
    mediaMessage.value = 'Mic đang bị giảng viên tắt trong phiên này.'
    return
  }
  mediaMessage.value = ''
  if (microphoneTrack.value) {
    removeMicrophoneTrack()
    mediaMessage.value = 'Mic đã tắt.'
    mediaRetryAction.value = ''
    return
  }
  if (!mediaDevicesAvailable()) return
  mediaBusy.value = true
  mediaRetryAction.value = 'microphone'
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    const track = stream.getAudioTracks()[0]
    if (!track) { stream.getTracks().forEach(value => value.stop()); throw new Error('No microphone track') }
    getLocalMedia().addTrack(track)
    microphoneTrack.value = track
    track.onended = () => {
      if (microphoneTrack.value === track) {
        microphoneTrack.value = null
        localMedia.value?.removeTrack(track)
        mediaMessage.value = 'Mic đã tắt.'
        void syncOutgoingTracks()
      }
    }
    await syncOutgoingTracks()
    mediaMessage.value = 'Mic đang bật.'
    mediaRetryAction.value = ''
  } catch (cause) {
    mediaAccessError(cause, 'microphone')
  } finally {
    mediaBusy.value = false
  }
}

async function toggleCamera() {
  if (!canUseMedia.value || mediaBusy.value) return
  mediaMessage.value = ''
  if (cameraTrack.value) {
    removeCameraTrack()
    mediaMessage.value = 'Camera đã tắt.'
    mediaRetryAction.value = ''
    return
  }
  if (!mediaDevicesAvailable()) return
  mediaBusy.value = true
  mediaRetryAction.value = 'camera'
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true })
    const track = stream.getVideoTracks()[0]
    if (!track) { stream.getTracks().forEach(value => value.stop()); throw new Error('No camera track') }
    getLocalMedia().addTrack(track)
    cameraTrack.value = track
    track.onended = () => {
      if (cameraTrack.value === track) {
        cameraTrack.value = null
        localMedia.value?.removeTrack(track)
        syncMediaPreview()
        mediaMessage.value = 'Camera đã tắt.'
        void syncOutgoingTracks()
      }
    }
    await syncMediaPreview()
    await syncOutgoingTracks()
    mediaMessage.value = 'Camera đang bật.'
    mediaRetryAction.value = ''
  } catch (cause) {
    mediaAccessError(cause, 'camera')
  } finally {
    mediaBusy.value = false
  }
}

async function waitForHubConnected(timeoutMs = 3000) {
  const startedAt = Date.now()
  while (Date.now() - startedAt < timeoutMs) {
    if (connection.value?.state === HubConnectionState.Connected) return true
    await new Promise(resolve => window.setTimeout(resolve, 100))
  }
  return connection.value?.state === HubConnectionState.Connected
}

async function requestPresentation(takeover = false): Promise<PresentationReservationResponse | null> {
  const hub = connection.value
  if (!hub || hub.state !== HubConnectionState.Connected) {
    mediaMessage.value = 'Realtime chưa sẵn sàng. Vui lòng đợi kết nối rồi thử lại.'
    return null
  }
  try {
    const result = await hub.invoke<PresentationReservationResponse>('RequestPresentation', takeover)
    presentationOwnerId.value = result.acquired ? localUserId.value : (result.presenterId || '')
    return result
  } catch (cause) {
    console.warn('RequestPresentation failed', cause)
    mediaMessage.value = 'Không thể cấp quyền trình bày. Vui lòng thử lại.'
    return null
  }
}

async function releasePresentation() {
  const hub = connection.value
  if (hub?.state === HubConnectionState.Connected) {
    await hub.invoke('ReleasePresentation').catch(() => undefined)
  }
  if (presentationOwnerId.value === localUserId.value) {
    presentationOwnerId.value = ''
  }
}

async function toggleScreenShare() {
  if (!canUseMedia.value || mediaBusy.value) return
  mediaMessage.value = ''
  if (screenTrack.value) {
    stopScreenShare()
    return
  }
  if (!mediaDevicesAvailable() || !navigator.mediaDevices.getDisplayMedia) {
    mediaMessage.value = 'Trình duyệt không hỗ trợ chia sẻ màn hình.'
    return
  }
  mediaBusy.value = true
  mediaRetryAction.value = 'screen'
  let leaseHeld = false
  let presentationStarted = false
  let stream: MediaStream | undefined
  try {
    // getDisplayMedia must run directly from the user's click while the
    // browser's transient user activation is still present. Do not await a
    // SignalR request before opening the browser picker.
    stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false })
    const track = stream.getVideoTracks()[0]
    if (!track) { stream.getTracks().forEach(value => value.stop()); throw new Error('No screen track') }

    if (!await waitForHubConnected()) {
      stream.getTracks().forEach(value => value.stop())
      mediaMessage.value = 'Realtime chưa sẵn sàng. Vui lòng đợi kết nối rồi thử lại.'
      return
    }

    let claim = await requestPresentation(false)
    if (!claim) {
      stream.getTracks().forEach(value => value.stop())
      return
    }
    if (!claim.acquired) {
      if (!isTeacher.value || !claim.presenterId) {
        stream.getTracks().forEach(value => value.stop())
        mediaMessage.value = 'Đang có người khác trình bày. Vui lòng chờ họ dừng chia sẻ.'
        return
      }
      if (claim.presenterIsTeacher) {
        stream.getTracks().forEach(value => value.stop())
        mediaMessage.value = 'Một giảng viên khác đang trình bày trong phiên này.'
        return
      }
      const confirmed = window.confirm('Sinh viên đang trình bày. Bạn có muốn dừng phần trình bày đó và trình bày không?')
      if (!confirmed) {
        stream.getTracks().forEach(value => value.stop())
        mediaMessage.value = 'Đã giữ phần trình bày hiện tại.'
        return
      }
      claim = await requestPresentation(true)
      if (!claim?.acquired) {
        stream.getTracks().forEach(value => value.stop())
        if (!mediaMessage.value) mediaMessage.value = 'Không thể giành quyền trình bày. Vui lòng thử lại.'
        return
      }
    }
    leaseHeld = true
    getLocalMedia().addTrack(track)
    screenTrack.value = track
    presentationStarted = true
    track.onended = () => {
      if (screenTrack.value === track) stopScreenShare()
    }
    await syncMediaPreview()
    await syncOutgoingTracks()
    mediaMessage.value = 'Đang chia sẻ màn hình.'
    mediaRetryAction.value = ''
  } catch (cause) {
    mediaAccessError(cause, 'màn hình')
  } finally {
    if (leaseHeld && !presentationStarted) await releasePresentation()
    mediaBusy.value = false
  }
}

function retryMedia() {
  const action = mediaRetryAction.value
  if (action === 'microphone') void toggleMicrophone()
  else if (action === 'camera') void toggleCamera()
  else if (action === 'screen') void toggleScreenShare()
}

async function syncOutgoingTracksForPeer(peer: RTCPeerConnection) {
  const transceivers = peerMediaTransceivers.get(peer)
  if (!transceivers) return
  await transceivers.audio.sender.replaceTrack(microphoneTrack.value)
  await transceivers.screen.sender.replaceTrack(screenTrack.value)
  await transceivers.camera.sender.replaceTrack(cameraTrack.value)
}

async function syncOutgoingTracks() {
  await Promise.all([...peerConnections.values()].map(peer => syncOutgoingTracksForPeer(peer).catch(() => undefined)))
}

function removeRemoteStream(userId: string) {
  const nextStreams = { ...remoteStreams.value }
  for (const key of Object.keys(nextStreams)) {
    if (key.startsWith(`${userId}::`)) {
      participantVideoElements.get(key)?.srcObject && (participantVideoElements.get(key)!.srcObject = null)
      participantVideoElements.delete(key)
      participantAudioElements.get(key)?.srcObject && (participantAudioElements.get(key)!.srcObject = null)
      participantAudioElements.delete(key)
      const nextMuted = { ...remoteTrackMuted.value }
      delete nextMuted[key]
      remoteTrackMuted.value = nextMuted
      delete nextStreams[key]
    }
  }
  remoteStreams.value = nextStreams
}

function bindParticipantVideo(key: string, stream: MediaStream | undefined, element: unknown) {
  if (typeof HTMLVideoElement !== 'undefined' && element instanceof HTMLVideoElement) {
    participantVideoElements.set(key, element)
    if (stream) {
      element.srcObject = stream
      element.play().catch(() => undefined)
    }
  } else {
    participantVideoElements.delete(key)
  }
}

function bindParticipantAudio(key: string, stream: MediaStream | undefined, element: unknown) {
  if (typeof HTMLAudioElement !== 'undefined' && element instanceof HTMLAudioElement) {
    participantAudioElements.set(key, element)
    if (stream) {
      element.srcObject = stream
      element.play().catch(() => undefined)
    }
  } else {
    participantAudioElements.delete(key)
  }
}

function handleRemoteTrack(userId: string, event: RTCTrackEvent) {
  const peer = peerConnections.get(userId)
  const videos = peer?.getTransceivers().filter(item => item.receiver.track.kind === 'video') || []
  const kind: RemoteMediaKind = event.track.kind === 'video' && videos[0] === event.transceiver ? 'screen' : 'camera'
  const key = `${userId}::${kind}`
  let stream = remoteStreams.value[key]
  if (!stream) {
    stream = new MediaStream()
  }
  if (!stream.getTracks().some(track => track.id === event.track.id)) stream.addTrack(event.track)
  remoteStreams.value = { ...remoteStreams.value, [key]: stream }
  if (event.track.kind === 'video') remoteTrackMuted.value = { ...remoteTrackMuted.value, [key]: event.track.muted }
  event.track.onmute = () => {
    if (event.track.kind === 'video') remoteTrackMuted.value = { ...remoteTrackMuted.value, [key]: true }
    remoteStreams.value = { ...remoteStreams.value }
  }
  event.track.onunmute = () => {
    if (event.track.kind === 'video') remoteTrackMuted.value = { ...remoteTrackMuted.value, [key]: false }
    remoteStreams.value = { ...remoteStreams.value }
  }
  event.track.onended = () => {
    stream?.removeTrack(event.track)
    if (stream && stream.getTracks().length === 0) {
      const nextStreams = { ...remoteStreams.value }
      delete nextStreams[key]
      remoteStreams.value = nextStreams
      const nextMuted = { ...remoteTrackMuted.value }
      delete nextMuted[key]
      remoteTrackMuted.value = nextMuted
      participantVideoElements.get(key)?.srcObject && (participantVideoElements.get(key)!.srcObject = null)
      participantVideoElements.delete(key)
      participantAudioElements.get(key)?.srcObject && (participantAudioElements.get(key)!.srcObject = null)
      participantAudioElements.delete(key)
    }
  }
  const video = participantVideoElements.get(key)
  if (video) {
    video.srcObject = stream
    video.play().catch(() => undefined)
  }
  const audio = participantAudioElements.get(key)
  if (audio) {
    audio.srcObject = stream
    audio.play().catch(() => undefined)
  }
}

function removePeer(userId: string) {
  const peer = peerConnections.get(userId)
  if (peer) {
    peerMediaTransceivers.delete(peer)
    peer.close()
  }
  peerConnections.delete(userId)
  pendingIceCandidates.delete(userId)
  removeRemoteStream(userId)
}

function stopPeerConnections() {
  for (const userId of peerConnections.keys()) removePeer(userId)
  peerConnections.clear()
  peerMediaTransceivers.clear()
  pendingIceCandidates.clear()
  remoteStreams.value = {}
  remoteTrackMuted.value = {}
  participantVideoElements.clear()
  participantAudioElements.clear()
}

function getOrCreatePeer(userId: string) {
  if (!userId || userId === localUserId.value) return null
  const existing = peerConnections.get(userId)
  if (existing) return existing
  if (typeof RTCPeerConnection === 'undefined') {
    mediaMessage.value = 'Trình duyệt không hỗ trợ kết nối media WebRTC.'
    return null
  }

  const peer = new RTCPeerConnection({ iceServers: rtcIceServers() })
  peer.onicecandidate = event => {
    const candidate = event.candidate
    if (!candidate || !connection.value) return
    connection.value.invoke('SendMediaIceCandidate', userId, {
      candidate: candidate.candidate,
      sdpMid: candidate.sdpMid,
      sdpMLineIndex: candidate.sdpMLineIndex
    }).catch(() => undefined)
  }
  peer.ontrack = event => handleRemoteTrack(userId, event)
  peer.onconnectionstatechange = () => {
    if (peerConnections.get(userId) === peer && (peer.connectionState === 'failed' || peer.connectionState === 'closed')) removePeer(userId)
  }
  peerConnections.set(userId, peer)
  return peer
}

function shouldInitiate(userId: string) {
  return Boolean(localUserId.value) && localUserId.value.localeCompare(userId) < 0
}

async function createOffer(userId: string, peer: RTCPeerConnection) {
  if (!connection.value || peer.signalingState !== 'stable') return
  try {
    const offer = await peer.createOffer()
    await peer.setLocalDescription(offer)
    const sdp = peer.localDescription?.sdp
    if (sdp) await connection.value.invoke('SendMediaOffer', userId, sdp)
  } catch {
    removePeer(userId)
  }
}

async function ensurePeer(userId: string, offerer: boolean) {
  const peer = getOrCreatePeer(userId)
  if (!peer) return
  try {
    if (offerer && !peerMediaTransceivers.has(peer)) {
      peerMediaTransceivers.set(peer, createMediaLanes(peer))
    }
    await syncOutgoingTracksForPeer(peer)
    if (offerer) await createOffer(userId, peer)
  } catch {
    removePeer(userId)
  }
}

async function flushIceCandidates(userId: string, peer: RTCPeerConnection) {
  const candidates = pendingIceCandidates.get(userId) || []
  pendingIceCandidates.delete(userId)
  for (const candidate of candidates) await peer.addIceCandidate(candidate).catch(() => undefined)
}

async function handleMediaOffer(signal: MediaOfferSignal) {
  if (signal.toUserId !== localUserId.value || !signal.fromUserId || !signal.sdp) return
  // SignalR can deliver the first offer while its start promise is still
  // settling. Defer the answer until the hub is fully connected so the
  // initial negotiation cannot be silently dropped.
  if (connection.value?.state !== HubConnectionState.Connected) {
    if (!pendingMediaOffers.some(item => item.fromUserId === signal.fromUserId && item.sdp === signal.sdp)) {
      pendingMediaOffers.push(signal)
    }
    return
  }
  const existing = peerConnections.get(signal.fromUserId)
  if (existing?.signalingState === 'have-local-offer' && shouldInitiate(signal.fromUserId)) return
  // A fresh offer also represents a reloaded browser: discard its previous transport.
  const queuedCandidates = pendingIceCandidates.get(signal.fromUserId)
  removePeer(signal.fromUserId)
  if (queuedCandidates) pendingIceCandidates.set(signal.fromUserId, queuedCandidates)
  const peer = getOrCreatePeer(signal.fromUserId)
  if (!peer) return
  try {
    await peer.setRemoteDescription({ type: 'offer', sdp: signal.sdp })
    peerMediaTransceivers.set(peer, receiveMediaLanes(peer))
    await syncOutgoingTracksForPeer(peer)
    await flushIceCandidates(signal.fromUserId, peer)
    const answer = await peer.createAnswer()
    await peer.setLocalDescription(answer)
    if (connection.value && peer.localDescription?.sdp) await connection.value.invoke('SendMediaAnswer', signal.fromUserId, peer.localDescription.sdp)
  } catch {
    removePeer(signal.fromUserId)
  }
}

async function handleMediaAnswer(signal: MediaAnswerSignal) {
  if (signal.toUserId !== localUserId.value || !signal.fromUserId || !signal.sdp) return
  const peer = peerConnections.get(signal.fromUserId)
  if (!peer || peer.signalingState !== 'have-local-offer') return
  try {
    await peer.setRemoteDescription({ type: 'answer', sdp: signal.sdp })
    await flushIceCandidates(signal.fromUserId, peer)
  } catch {
    removePeer(signal.fromUserId)
  }
}

async function handleMediaIceCandidate(signal: MediaIceCandidateSignal) {
  if (signal.toUserId !== localUserId.value || !signal.fromUserId || !signal.candidate) return
  if (connection.value?.state !== HubConnectionState.Connected) {
    if (!pendingMediaIceSignals.some(item => item.fromUserId === signal.fromUserId && item.candidate === signal.candidate)) {
      pendingMediaIceSignals.push(signal)
    }
    return
  }
  const peer = getOrCreatePeer(signal.fromUserId)
  if (!peer) return
  const candidate: RTCIceCandidateInit = {
    candidate: signal.candidate,
    sdpMid: signal.sdpMid ?? null,
    sdpMLineIndex: signal.sdpMLineIndex ?? null
  }
  if (!peer.remoteDescription) {
    pendingIceCandidates.set(signal.fromUserId, [...(pendingIceCandidates.get(signal.fromUserId) || []), candidate])
    return
  }
  await peer.addIceCandidate(candidate).catch(() => undefined)
}

async function handleSessionEnded(signal: LiveSessionEndedSignal) {
  if (!session.value || signal?.sessionId !== session.value.id || leaving.value) return
  session.value = { ...session.value, status: 2, endedAt: signal.endedAt }
  mediaMessage.value = 'Phiên học đã kết thúc.'
  // Stop the local hub, media and presence, then leave the live route for
  // every browser that was connected to this session.
  await leave()
}

function addParticipant(userId: string, initiate = false) {
  if (!userId || userId === localUserId.value) return
  if (participants.value.includes(userId)) return
  participants.value.push(userId)
  if (connection.value?.state === HubConnectionState.Connected) {
    void ensurePeer(userId, initiate || shouldInitiate(userId))
  }
}

function attendanceStatus(status: string | number | undefined) {
  return status === 1 || String(status).toLowerCase() === 'late' ? 'Late' : status === 2 || String(status).toLowerCase() === 'absent' ? 'Absent' : status === 3 || String(status).toLowerCase() === 'excused' ? 'Excused' : 'Present'
}

function formatDuration(value: string | undefined) {
  if (!value) return '0 phút'
  const match = value.match(/^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.\d+)?$/)
  if (!match) return value
  const totalMinutes = (Number(match[1] || 0) * 24 * 60) + (Number(match[2]) * 60) + Number(match[3]) + (Number(match[4]) >= 30 ? 1 : 0)
  if (totalMinutes < 60) return `${totalMinutes} phút`
  return `${Math.floor(totalMinutes / 60)} giờ ${totalMinutes % 60} phút`
}

function formatDurationBetween(start?: string, end?: string) {
  if (!start || !end) return '0 phút'
  const milliseconds = new Date(end).getTime() - new Date(start).getTime()
  if (!Number.isFinite(milliseconds) || milliseconds <= 0) return '0 phút'
  const totalMinutes = Math.round(milliseconds / 60000)
  if (totalMinutes < 60) return `${totalMinutes} phút`
  return `${Math.floor(totalMinutes / 60)} giờ ${totalMinutes % 60} phút`
}

function isParticipantMuted(studentId: string) {
  return moderations.value.some(item => item.studentId === studentId && item.isMuted)
}

function applyModerationState(signal: { targetUserId: string; action: string }) {
  if (signal.targetUserId === localUserId.value) {
    if (signal.action === 'mute') {
      teacherMutedByTeacher.value = true
      if (microphoneTrack.value) removeMicrophoneTrack()
      mediaMessage.value = 'Mic đã được tắt bởi giảng viên.'
    } else if (signal.action === 'unmute') {
      teacherMutedByTeacher.value = false
      mediaMessage.value = 'Giảng viên đã cho phép bật mic.'
    }
  }

  const current = moderations.value.find(item => item.studentId === signal.targetUserId)
  if (current) {
    current.isMuted = signal.action === 'mute' ? true : signal.action === 'unmute' ? false : current.isMuted
    current.isBlocked = signal.action === 'block' ? true : signal.action === 'unblock' ? false : current.isBlocked
    moderations.value = [...moderations.value]
  } else if (signal.action === 'mute' || signal.action === 'block') {
    moderations.value = [...moderations.value, {
      liveSessionId: session.value?.id || '', studentId: signal.targetUserId,
      isBlocked: signal.action === 'block', isMuted: signal.action === 'mute', updatedAt: new Date().toISOString()
    }]
  }
}

async function moderateParticipant(studentId: string, action: 'mute' | 'unmute' | 'kick' | 'block' | 'unblock') {
  if (!session.value) return
  try {
    const updated = await classroomApi.moderate(session.value.id, studentId, action)
    if (action === 'kick' || action === 'block') {
      participants.value = participants.value.filter(id => id !== studentId)
      removePeer(studentId)
    } else {
      const current = moderations.value.find(item => item.studentId === studentId)
      if (current) Object.assign(current, updated)
      else moderations.value = [...moderations.value, updated]
      moderations.value = [...moderations.value]
    }
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không cập nhật được quyền điều khiển người tham gia.'
  }
}

function syncAttendanceDrafts() {
  const records = attendance.value?.records || []
  attendanceDrafts.value = Object.fromEntries(enrollments.value.map(item => {
    const record = records.find(value => value.studentId === item.studentId)
    return [item.studentId, { status: attendanceStatus(record?.status), reason: record?.adjustmentReason || '' }]
  }))
}

async function adjustAttendance(studentId: string) {
  if (!session.value) return
  const draftValue = attendanceDrafts.value[studentId]
  if (!draftValue?.reason.trim()) {
    error.value = 'Điều chỉnh điểm danh cần có lý do.'
    return
  }
  attendanceBusy.value = studentId
  try {
    await classroomApi.adjustAttendance(session.value.id, studentId, { status: draftValue.status, reason: draftValue.reason.trim() })
    attendance.value = await classroomApi.attendance(session.value.id)
    syncAttendanceDrafts()
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không điều chỉnh được điểm danh.'
  } finally {
    attendanceBusy.value = ''
  }
}

async function load() {
  error.value = ''
  mediaMessage.value = ''
  joined.value = null
  stopPeerConnections()
  stopMedia()
  pendingMediaOffers.length = 0
  pendingMediaIceSignals.length = 0
  participants.value = []
  participantNames.value = {}
  moderations.value = []
  presentationOwnerId.value = ''
  teacherMutedByTeacher.value = false
  try {
    classInfo.value = (await academicsApi.classDetail(String(route.params.classId))).class
    session.value = await classroomApi.session(String(route.params.sessionId))
    document.title = `${liveSessionLabel()} · RLMS`
    navigation.renameTab(route.fullPath, liveSessionLabel())
    if (isSessionEnded.value) {
      connectionState.value = 'Đã kết thúc'
      return
    }
    await connect()
    if (!isSessionStarted.value) {
      connectionState.value = 'Chưa bắt đầu'
      return
    }
    await joinStartedSession()
    if (isTeacher.value && typeof route.query.activityId === 'string') overview.value = await assessmentApi.overview(session.value.learningClassId, route.query.activityId)
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không thể tham gia lớp học trực tuyến.'
    connectionState.value = 'Không kết nối'
  }
}

async function joinStartedSession() {
  if (!session.value || !isSessionStarted.value || joined.value || joiningSession.value) return
  joiningSession.value = true
  try {
    joined.value = await classroomApi.joinSession(session.value.id)
    messages.value = await classroomApi.messages(session.value.id)
    if (isTeacher.value) {
      const [attendanceResult, enrollmentResult, handsResult, moderationResult] = await Promise.allSettled([
        classroomApi.attendance(session.value.id),
        academicsApi.enrollments(session.value.learningClassId),
        classroomApi.raiseHands(session.value.id),
        classroomApi.moderation(session.value.id)
      ])
      if (attendanceResult.status === 'fulfilled') attendance.value = attendanceResult.value
      if (enrollmentResult.status === 'fulfilled') enrollments.value = enrollmentResult.value
      if (handsResult.status === 'fulfilled') raisedHands.value = handsResult.value
      if (moderationResult.status === 'fulfilled') moderations.value = moderationResult.value
      syncAttendanceDrafts()
    }
    if (connection.value?.state === HubConnectionState.Connected) {
      await connection.value.invoke('RefreshPresentation').catch(() => undefined)
      await connection.value.invoke('RequestMediaPeers').catch(() => undefined)
    }
  } finally {
    joiningSession.value = false
  }
}

async function start() {
  if (!session.value) return
  try {
    session.value = await classroomApi.startSession(session.value.id)
    await load()
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không bắt đầu được phiên.'
  }
}

async function end() {
  if (!session.value) return
  try {
    session.value = await classroomApi.endSession(session.value.id)
    await connection.value?.stop()
    stopPeerConnections()
    stopMedia()
    connectionState.value = 'Đã kết thúc'
    if (!leaving.value) await leave()
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không kết thúc được phiên.'
  }
}

async function leaveToClass() {
  // Leaving the live view is separate from ending the session. Only the
  // explicit "Kết thúc phiên" action changes the durable session state and
  // disconnects every participant.
  await leave()
}

async function clearHand(studentId: string) {
  if (!session.value) return
  try {
    await classroomApi.clearHand(session.value.id, studentId)
    raisedHands.value = raisedHands.value.filter(hand => hand.studentId !== studentId)
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không xử lý được yêu cầu giơ tay.'
  }
}

async function connect() {
  if (!session.value) return
  await connection.value?.stop().catch(() => undefined)
  const hub = new HubConnectionBuilder()
    .withUrl(`/hubs/live/${session.value.id}`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  hub.on('participant-snapshot', (userIds: string[]) => {
    for (const userId of userIds || []) addParticipant(userId, true)
  })
  hub.on('participant-presence-snapshot', (items: { userId: string; displayName: string }[]) => {
    for (const item of items || []) {
      if (item?.userId && item.displayName) participantNames.value[item.userId] = item.displayName
      if (item?.userId) addParticipant(item.userId)
    }
  })
  hub.on('participant-joined', (delta: { userId: string }) => {
    addParticipant(delta.userId)
  })
  hub.on('participant-presence-joined', (item: { userId: string; displayName: string }) => {
    if (item?.userId && item.displayName) participantNames.value[item.userId] = item.displayName
    if (item?.userId) addParticipant(item.userId)
  })
  hub.on('participant-left', (delta: { userId: string }) => {
    participants.value = participants.value.filter(id => id !== delta.userId)
    delete participantNames.value[delta.userId]
    removePeer(delta.userId)
    if (presentationOwnerId.value === delta.userId) {
      presentationOwnerId.value = ''
    }
  })
  hub.on('participant-moderation-state', (signal: { targetUserId: string; action: string }) => {
    applyModerationState(signal)
  })
  hub.on('participant-kicked', (signal: { targetUserId: string; action: string }) => {
    participants.value = participants.value.filter(id => id !== signal.targetUserId)
    removePeer(signal.targetUserId)
    applyModerationState(signal)
    if (signal.targetUserId === localUserId.value) {
      mediaMessage.value = signal.action === 'block' ? 'Bạn đã bị chặn khỏi phiên này.' : 'Bạn đã bị đưa ra khỏi phiên này.'
      void leave()
    }
  })
  hub.on('presentation-started', (signal: PresentationSignal) => {
    if (!signal?.presenterId) return
    presentationOwnerId.value = signal.presenterId
  })
  hub.on('presentation-replaced', (signal: PresentationReplacementSignal) => {
    if (!signal?.presenterId) return
    presentationOwnerId.value = signal.presenterId
    if (signal.previousPresenterId === localUserId.value && screenTrack.value) {
      stopScreenShare(true, false)
      mediaMessage.value = 'Giảng viên đã tiếp quản phần trình bày.'
    }
  })
  hub.on('presentation-stopped', (signal: PresentationStoppedSignal) => {
    if (!signal?.presenterId || presentationOwnerId.value === signal.presenterId) {
      presentationOwnerId.value = ''
    }
    if (signal?.presenterId === localUserId.value && screenTrack.value) {
      stopScreenShare(true, false)
    }
  })
  hub.on('session-ended', (signal: LiveSessionEndedSignal) => { void handleSessionEnded(signal) })
  hub.on('session-started', (signal: LiveSessionStartedSignal) => {
    if (!session.value || signal?.sessionId !== session.value.id || isSessionStarted.value) return
    session.value = { ...session.value, status: 1, startedAt: signal.startedAt }
    connectionState.value = 'Đã kết nối'
    void joinStartedSession()
  })
  hub.on('session-state', (signal: LiveSessionStateSignal) => {
    if (!session.value || signal?.sessionId !== session.value.id) return
    const nextStatus = signal.status
    session.value = { ...session.value, status: nextStatus, startedAt: signal.startedAt, endedAt: signal.endedAt }
    if (String(nextStatus).toLowerCase() === 'ended' || nextStatus === 2) {
      connectionState.value = 'Đã kết thúc'
    } else if (String(nextStatus).toLowerCase() === 'scheduled' || nextStatus === 0) {
      connectionState.value = 'Chưa bắt đầu'
    }
  })
  hub.on('media-offer', (signal: MediaOfferSignal) => { void handleMediaOffer(signal) })
  hub.on('media-answer', (signal: MediaAnswerSignal) => { void handleMediaAnswer(signal) })
  hub.on('media-ice-candidate', (signal: MediaIceCandidateSignal) => { void handleMediaIceCandidate(signal) })
  hub.on('hand-raised', (hand: { studentId: string; id: string; liveSessionId: string; status: string | number; raisedAt: string }) => {
    if (!isTeacher.value || raisedHands.value.some(item => item.id === hand.id)) return
    raisedHands.value.push(hand)
  })
  hub.on('hand-lowered', (hand: { studentId: string }) => {
    raisedHands.value = raisedHands.value.filter(item => item.studentId !== hand.studentId)
  })
  hub.on('chat-message', (message: { id: string; senderId: string; body: string; sentAt: string }) => {
    if (!messages.value.some(item => item.id === message.id)) messages.value.push(message)
  })
  hub.onreconnecting(() => {
    pendingMediaOffers.length = 0
    pendingMediaIceSignals.length = 0
    connectionState.value = 'Đang kết nối lại'
  })
  hub.onreconnected(() => {
    stopPeerConnections()
    participants.value = auth.account?.id ? [auth.account.id] : []
    participantNames.value = {}
    connectionState.value = 'Đã kết nối'
    if (session.value && isSessionStarted.value) {
      if (!isTeacher.value) classroomApi.reconnectSession(session.value.id).catch(() => undefined)
      void joinStartedSession()
    }
    hub.invoke('RefreshPresence').catch(() => undefined)
    if (isSessionStarted.value) {
      hub.invoke('RefreshPresentation').catch(() => undefined)
      hub.invoke('RequestMediaPeers').catch(() => undefined)
    }
  })
  hub.onclose(() => {
    connectionState.value = 'Đã ngắt kết nối'
    pendingMediaOffers.length = 0
    pendingMediaIceSignals.length = 0
    stopPeerConnections()
    presentationOwnerId.value = ''
  })
  try {
    await hub.start()
    connection.value = hub
    connectionState.value = 'Đã kết nối'
    if (isSessionStarted.value && auth.account?.id && !participants.value.includes(auth.account.id)) participants.value.push(auth.account.id)
    const queuedOffers = pendingMediaOffers.splice(0)
    for (const signal of queuedOffers) await handleMediaOffer(signal)
    const queuedIceSignals = pendingMediaIceSignals.splice(0)
    for (const signal of queuedIceSignals) void handleMediaIceCandidate(signal)
    // Presence events may arrive before the SignalR start promise resolves.
    // Reconcile all discovered peers now that signaling is definitely ready.
    for (const userId of participants.value) {
      if (userId !== localUserId.value && !peerConnections.has(userId)) void ensurePeer(userId, true)
    }
    if (isSessionStarted.value) {
      await hub.invoke('RefreshPresentation')
      await hub.invoke('RequestMediaPeers')
    } else {
      // The start signal can race the initial HTTP snapshot. Reconcile once
      // after the hub is connected so a missed event cannot leave a stale
      // scheduled view.
      const refreshed = await classroomApi.session(session.value.id).catch(() => null)
      if (refreshed) {
        session.value = refreshed
        if (isSessionStarted.value) await joinStartedSession()
      }
    }
  } catch (cause) {
    if (connection.value === hub) connection.value = null
    error.value = errorMessage(cause) || 'Không kết nối được realtime.'
  }
}

async function send() {
  if (!session.value || !draft.value) return
  const body = draft.value
  draft.value = ''
  try {
    const message = await classroomApi.sendMessage(session.value.id, body) as { id: string; senderId: string; body: string; sentAt: string }
    if (!messages.value.some(item => item.id === message.id)) messages.value.push(message)
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không gửi được tin nhắn.'
  }
}

async function toggleHand() {
  if (!session.value) return
  try {
    if (handRaised.value) {
      await classroomApi.lowerHand(session.value.id)
      handRaised.value = false
    } else {
      await classroomApi.raiseHand(session.value.id)
      handRaised.value = true
    }
  } catch (cause) {
    error.value = errorMessage(cause) || 'Không cập nhật được trạng thái giơ tay.'
  }
}

async function leave() {
  if (leaving.value) return
  leaving.value = true
  if (joined.value && session.value) await classroomApi.leaveSession(session.value.id).catch(() => undefined)
  await connection.value?.stop().catch(() => undefined)
  stopPeerConnections()
  stopMedia()
  await router.push(`/classes/${route.params.classId}`)
}

onMounted(() => {
  stageObserver = new ResizeObserver(entries => {
    const rect = entries[0]?.contentRect
    if (rect) stageSize.value = { width: rect.width, height: rect.height }
  })
  if (tileContainer.value?.$el) stageObserver.observe(tileContainer.value.$el)
  document.addEventListener('fullscreenchange', handleFullscreenChange)
  void load()
})
onBeforeUnmount(async () => {
  stageObserver?.disconnect()
  if (!leaving.value && joined.value && session.value) await classroomApi.leaveSession(session.value.id).catch(() => undefined)
  await connection.value?.stop().catch(() => undefined)
  stopPeerConnections()
  stopMedia()
  document.removeEventListener('fullscreenchange', handleFullscreenChange)
})
</script>
