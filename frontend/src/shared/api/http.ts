import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios'

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
    const axiosError = error as AxiosError<{ error?: string; title?: string; detail?: string }>
    return axiosError.response?.data?.error || axiosError.response?.data?.detail ||
      axiosError.response?.data?.title || (axiosError.response?.status === 401
        ? 'Phiên đăng nhập đã hết hạn.'
        : axiosError.response?.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.'
          : 'Không thể hoàn thành yêu cầu.')
  }
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.'
}

export function isUnauthorized(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401
}
