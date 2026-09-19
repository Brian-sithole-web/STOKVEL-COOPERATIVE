import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function ResetPasswordPage() {
  const { resetPassword } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const email = params.get('email') || ''
  const token = params.get('token') || ''
  const [form, setForm] = useState({ password: '', confirmPassword: '' })
  const [error, setError] = useState(() => (
    email && token ? '' : 'This reset link is missing an email or token. Request a new link from the sign-in page.'
  ))
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    if (form.password !== form.confirmPassword) {
      setError('The two passwords do not match.')
      return
    }
    setBusy(true)
    try {
      await resetPassword(email, token, form.password)
      navigate('/')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="ACCOUNT RECOVERY"
      title="Choose a new password"
      subtitle={email
        ? `Create a new password for ${email}, then sign in with that email.`
        : 'Open the reset link from your email to choose a new password.'}
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        {email && (
          <AuthField label="Email" icon="mail">
            <input type="email" value={email} readOnly />
          </AuthField>
        )}
        <PasswordField
          label="New password"
          value={form.password}
          onChange={(event) => setForm({ ...form, password: event.target.value })}
          autoComplete="new-password"
        />
        <p className="auth-hint">Use at least 10 characters, with upper case, lower case, a number and a symbol.</p>
        <PasswordField
          label="Confirm password"
          value={form.confirmPassword}
          onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })}
          autoComplete="new-password"
        />
        <button className="btn auth-submit" disabled={busy || !email || !token}>
          {busy ? 'Saving password…' : 'Save password and sign in'}
        </button>
        <p className="muted auth-foot"><Link to="/forgot-password">Request a new reset link</Link></p>
      </form>
    </AuthShell>
  )
}
