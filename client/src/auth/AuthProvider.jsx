import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { api, getToken, setToken } from '@/lib/api'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [ready, setReady] = useState(false)
  const [groupId, setGroupId] = useState(() => localStorage.getItem('stokvel.group') || '')

  function applyAuth(auth) {
    setToken(auth.token)
    setUser(auth)
    if (auth.memberships?.length && !groupId) {
      const id = auth.memberships[0].groupId
      setGroupId(id)
      localStorage.setItem('stokvel.group', id)
    }
  }

  useEffect(() => {
    async function boot() {
      if (!getToken()) {
        setReady(true)
        return
      }
      try {
        const me = await api.get('/api/auth/me')
        applyAuth(me)
      } catch {
        setToken(null)
        setUser(null)
      } finally {
        setReady(true)
      }
    }
    boot()
  }, [])

  const value = useMemo(() => ({
    user,
    ready,
    groupId,
    membership: user?.memberships?.find((m) => m.groupId === groupId),
    selectGroup(id) {
      setGroupId(id)
      localStorage.setItem('stokvel.group', id)
    },
    async login(email, password) {
      const auth = await api.post('/api/auth/login', { email, password })
      applyAuth(auth)
      return auth
    },
    async register(fullName, email, password) {
      const auth = await api.post('/api/auth/register', { fullName, email, password })
      applyAuth(auth)
      return auth
    },
    async setup(fullName, email, password) {
      const auth = await api.post('/api/setup/initialize', { fullName, email, password })
      applyAuth(auth)
      return auth
    },
    async acceptInvite(token, fullName, password) {
      const auth = await api.post('/api/auth/accept-invite', { token, fullName, password })
      applyAuth(auth)
      return auth
    },
    logout() {
      setToken(null)
      setUser(null)
    },
    async refresh() {
      const me = await api.get('/api/auth/me')
      setUser(me)
      return me
    },
  }), [user, ready, groupId])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  return useContext(AuthContext)
}
