import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function InvitePage() {
  const { acceptInvite } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [form, setForm] = useState({ token: params.get('token') || '', fullName: '', password: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      await acceptInvite(form.token, form.fullName, form.password)
      navigate('/')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="INVITATION"
      title="Join a Stokvel"
      subtitle="If you already have an account, use the same email. A new account will be created if needed."
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        <AuthField label="Invite token" icon="shield">
          <input value={form.token} onChange={(event) => setForm({ ...form, token: event.target.value })} required />
        </AuthField>
        <AuthField label="Full name" icon="user">
          <input value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} autoComplete="name" required />
        </AuthField>
        <PasswordField value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} autoComplete="new-password" />
        <button className="btn auth-submit" disabled={busy}>{busy ? 'Accepting invitation…' : 'Accept invitation'}</button>
      </form>
    </AuthShell>
  )
}
