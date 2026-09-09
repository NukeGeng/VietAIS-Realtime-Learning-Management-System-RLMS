import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { identityApi } from '../api/client'
import { isUnauthorized, errorMessage } from '../api/http'
import type { Account, Role } from '../types'
import { permissions as P } from '../permissions'

export const useAuthStore = defineStore('auth', () => {
  const account = ref<Account | null>(null)
  const ready = ref(false)
  const busy = ref(false)
  const lastError = ref('')

  const permissions = computed(() => new Set(account.value?.permissions ?? []))
  const isAuthenticated = computed(() => account.value !== null)
  const role = computed<Role>(() => {
    if (permissions.value.has(P.manageOrganizations) || permissions.value.has(P.manageUsers)) return 'admin'
    if (permissions.value.has(P.manageCourses) || permissions.value.has(P.manageClasses) || permissions.value.has(P.readReports)) return 'teacher'
    return 'student'
  })

  async function init() {
    if (ready.value) return
    try {
      account.value = await identityApi.me()
    } catch (error) {
      if (!isUnauthorized(error)) lastError.value = errorMessage(error)
      account.value = null
    } finally {
      ready.value = true
    }
  }

  async function login(body: { organizationCode: string; email: string; password: string }) {
    busy.value = true
    lastError.value = ''
    try {
      account.value = await identityApi.login(body)
      ready.value = true
      return account.value
    } catch (error) {
      lastError.value = errorMessage(error)
      throw error
    } finally {
      busy.value = false
    }
  }

  async function logout() {
    try { await identityApi.logout() } finally { account.value = null }
  }

  function has(permission: string) { return permissions.value.has(permission) }
  function canAccess(required?: string[]) { return !required?.length || required.some(permission => has(permission)) }

  return { account, ready, busy, lastError, permissions, isAuthenticated, role, init, login, logout, has, canAccess }
})
