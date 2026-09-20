const TOKEN = 'stokvel.token'
const API_ORIGIN = import.meta.env.VITE_API_ORIGIN || (import.meta.env.DEV ? 'http://localhost:5188' : '')

export function getToken() {
  return localStorage.getItem(TOKEN)
}

export function setToken(token) {
  if (token) localStorage.setItem(TOKEN, token)
  else localStorage.removeItem(TOKEN)
}

function errorMessage(data, status, rawText) {
  return data?.message || data?.title || (rawText ? rawText.slice(0, 180) : '') || `Request failed (${status})`
}

async function request(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) }
  const token = getToken()
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(`${API_ORIGIN}${path}`, { ...options, headers })
  if (res.status === 204) return null
  const text = await res.text()
  let data = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = null
    }
  }
  if (!res.ok) {
    const err = new Error(errorMessage(data, res.status, text))
    err.code = data?.code
    err.status = res.status
    throw err
  }
  return data
}

export const api = {
  get: (path) => request(path),
  post: (path, body) => request(path, { method: 'POST', body: JSON.stringify(body ?? {}) }),
  put: (path, body) => request(path, { method: 'PUT', body: JSON.stringify(body) }),
  del: (path) => request(path, { method: 'DELETE' }),
}
