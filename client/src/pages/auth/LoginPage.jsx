import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { api } from '@/lib/api'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.get('/api/setup/status').then((status) => { if (status.requiresSetup) navigate('/setup') }).catch(() => {})
  }, [navigate])

  async function submit(event) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      await login(form.email, form.password)
      navigate('/')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="SECURE ACCESS"
      title="Welcome back"
      subtitle="Sign in to manage savings, instalments and borrowing."
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        <AuthField label="Email" icon="mail">
          <input
            type="email"
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
            autoComplete="email"
            required
          />
        </AuthField>
        <PasswordField
          value={form.password}
          onChange={(event) => setForm({ ...form, password: event.target.value })}
        />
        <button className="btn auth-submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
        <p className="muted auth-foot">New here? <Link to="/register">Create an account</Link></p>
      </form>
    </AuthShell>
  )
}
