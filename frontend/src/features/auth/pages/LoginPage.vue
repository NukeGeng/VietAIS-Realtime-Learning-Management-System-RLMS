<template>
  <main class="login-page">
    <section class="login-hero">
      <div class="hero-brand"><span class="brand-mark">R</span><span>RLMS</span></div>
      <div class="hero-copy">
        <span class="eyebrow">REALTIME LEARNING MANAGEMENT SYSTEM</span>
        <h1>Học tập rõ ràng.<br />Tiến bộ theo thời gian thực.</h1>
        <p>Không gian làm việc riêng cho từng vai trò, dữ liệu học tập bền vững và lớp học trực tuyến được theo dõi đúng lúc.</p>
      </div>
      <div class="hero-points"><span class="hero-point">Theo dõi tiến độ</span><span class="hero-point">Lớp học trực tuyến</span><span class="hero-point">Đánh giá minh bạch</span></div>
    </section>
    <section class="login-panel">
      <form class="login-card" @submit.prevent="submit">
        <span class="eyebrow">WELCOME BACK</span>
        <h2>Đăng nhập</h2>
        <p>Đăng nhập vào tổ chức của bạn để tiếp tục phiên học.</p>
        <div v-if="error" class="login-error" role="alert">{{ error }}</div>
        <div class="form-grid">
          <div class="form-field"><label for="organization">Mã tổ chức</label><input id="organization" v-model.trim="form.organizationCode" class="form-input" autocomplete="organization" required placeholder="RLMS" /></div>
          <div class="form-field"><label for="email">Email</label><input id="email" v-model.trim="form.email" class="form-input" type="email" autocomplete="email" required placeholder="ban@example.com" /></div>
          <div class="form-field"><label for="password">Mật khẩu</label><input id="password" v-model="form.password" class="form-input" type="password" autocomplete="current-password" required placeholder="••••••••" /></div>
        </div>
        <button class="button primary login-submit" :disabled="auth.busy">{{ auth.busy ? 'Đang đăng nhập…' : 'Đăng nhập' }}</button>
        <div class="auth-note">Backend luôn kiểm tra tổ chức, tài khoản và quyền truy cập. Frontend chỉ hiển thị workspace phù hợp.</div>
      </form>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../../shared/stores/auth'
import { errorMessage } from '../../../shared/api/http'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const form = reactive({ organizationCode: '', email: '', password: '' })
const error = computed(() => auth.lastError || '')

async function submit() {
  try {
    await auth.login(form)
    const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') ? route.query.redirect : '/dashboard'
    await router.replace(redirect)
  } catch (cause) {
    if (!auth.lastError) auth.lastError = errorMessage(cause)
  }
}
</script>
