import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { translateErrorMessage } from './errorMessages'
export { translateErrorMessage } from './errorMessages'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || '/'

export const http: AxiosInstance = axios.create({
  baseURL: apiBaseUrl,
  withCredentials: true,
  headers: { Accept: 'application/json' }
})

let csrfRequest: Promise<string> | null = null

async function getCsrfToken(): Promise<string> {
  if (!csrfRequest) {
    csrfRequest = http.get<{ token: string }>('/api/identity/csrf')
      .then(({ data }) => data.token)
      .finally(() => { csrfRequest = null })
  }
  return csrfRequest
}

http.interceptors.request.use(async (config) => {
  const method = (config.method || 'get').toLowerCase()
  if (['post', 'put', 'patch', 'delete'].includes(method) && !config.headers?.['X-CSRF-TOKEN']) {
    const token = await getCsrfToken()
    config.headers = config.headers ?? {}
    config.headers['X-CSRF-TOKEN'] = token
  }
  return config
})

export async function request<T>(config: AxiosRequestConfig): Promise<T> {
  const response = await http.request<T>(config)
  return response.data
}

export function errorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<{ error?: string; title?: string; detail?: string; errors?: string[] | Record<string, string[]>; submission?: { errors?: string[] } }>
    const data = axiosError.response?.data
    const validationErrors = data?.errors || data?.submission?.errors
    const messages = validationErrors && (Array.isArray(validationErrors) ? validationErrors : Object.values(validationErrors).flat())
    if (messages?.length) return messages.map(translateErrorMessage).join(' ')
    const detail = data?.error || data?.detail || data?.title
    if (detail) return translateErrorMessage(detail)
    if (!axiosError.response) return 'Không kết nối được máy chủ. Hãy kiểm tra kết nối và thử lại.'
    if (axiosError.response.status === 401) return 'Phiên đăng nhập đã hết hạn.'
    if (axiosError.response.status === 403) return 'Bạn không có quyền thực hiện thao tác này.'
    if (axiosError.response.status === 404) return 'Dữ liệu không còn tồn tại hoặc bạn không có quyền truy cập.'
    if (axiosError.response.status >= 500) return 'Máy chủ chưa thể xử lý yêu cầu. Hãy thử lại sau.'
    return 'Không thể hoàn thành yêu cầu.'
  }
  return error instanceof Error ? translateErrorMessage(error.message) : 'Đã xảy ra lỗi không xác định.'
}

export function isUnauthorized(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401
}
