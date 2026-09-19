import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '@/lib/api'
import { AuthField, AuthShell } from '@/components/auth'

export function ForgotPasswordPage() {
  const [params] = useSearchParams()
  const [email, setEmail] = useState(params.get('email') || '')
  const [error, setError] = useState('')
  const [result, setResult] = useState(null)
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    setResult(null)
    setBusy(true)
    try {
      const response = await api.post('/api/auth/forgot-password', {
        email,
        clientOrigin: window.location.origin,
      })
      setResult(response)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="ACCOUNT RECOVERY"
      title="Forgot your password?"
      subtitle="Enter the email on your account. We will send a link to create a new password."
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        {result && (
          <div className="success" role="status">
            <p>{result.message}</p>
            {result.resetUrl && (
              <p>
                <a href={result.resetUrl}>Open password reset link</a>
              </p>
            )}
          </div>
        )}
        <AuthField label="Email" icon="mail">
          <input
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            autoComplete="email"
            required
          />
        </AuthField>
        <button className="btn auth-submit" disabled={busy}>{busy ? 'Sending link…' : 'Send reset link'}</button>
        <p className="muted auth-foot"><Link to="/login">Back to sign in</Link></p>
      </form>
    </AuthShell>
  )
}
