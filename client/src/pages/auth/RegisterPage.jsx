import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ fullName: '', email: '', password: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      await register(form.fullName, form.email, form.password)
      navigate('/')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="JOIN THE COOPERATIVE"
      title="Create your account"
      subtitle="Register, then create a Stokvel or accept an invitation."
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        <AuthField label="Full name" icon="user">
          <input value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} autoComplete="name" required />
        </AuthField>
        <AuthField label="Email" icon="mail">
          <input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} autoComplete="email" required />
        </AuthField>
        <PasswordField value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} autoComplete="new-password" />
        <button className="btn auth-submit" disabled={busy}>{busy ? 'Creating account…' : 'Register'}</button>
        <p className="muted auth-foot">Already registered? <Link to="/login">Sign in</Link></p>
      </form>
    </AuthShell>
  )
}
