import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function SetupPage() {
  const { setup } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ fullName: '', email: '', password: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  function set(key, value) { setForm((current) => ({ ...current, [key]: value })) }

  async function submit(event) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      await setup(form.fullName, form.email, form.password)
      navigate('/')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="FIRST-RUN SETUP"
      title="Create the Platform Administrator"
      subtitle="No default password is stored in production. Choose a strong password of at least 10 characters with upper, lower, digit and symbol."
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        <AuthField label="Full name" icon="user">
          <input value={form.fullName} onChange={(event) => set('fullName', event.target.value)} autoComplete="name" required />
        </AuthField>
        <AuthField label="Email" icon="mail">
          <input type="email" value={form.email} onChange={(event) => set('email', event.target.value)} autoComplete="email" required />
        </AuthField>
        <PasswordField value={form.password} onChange={(event) => set('password', event.target.value)} autoComplete="new-password" />
        <button className="btn auth-submit" disabled={busy}>{busy ? 'Creating administrator…' : 'Complete setup'}</button>
      </form>
    </AuthShell>
  )
}
