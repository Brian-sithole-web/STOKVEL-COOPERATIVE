import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { BrandLogo } from '@/components/brand/BrandLogo'
import { api } from '@/lib/api'

export function Gate({ children }) {
  const { user, ready } = useAuth()
  const navigate = useNavigate()
  const [setup, setSetup] = useState(null)

  useEffect(() => {
    api.get('/api/setup/status').then((status) => setSetup(status)).catch(() => setSetup({ requiresSetup: false }))
  }, [])

  useEffect(() => {
    if (!ready || setup == null) return
    if (setup.requiresSetup) navigate('/setup')
    else if (!user) navigate('/login')
  }, [ready, setup, user, navigate])

  if (!ready || !user) {
    return (
      <div className="auth-loading">
        <BrandLogo variant="lockup" />
        <p>Opening the ledger…</p>
      </div>
    )
  }
  return children
}
