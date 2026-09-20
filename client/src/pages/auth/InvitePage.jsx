import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { api } from '@/lib/api'
import { AuthField, AuthShell, PasswordField } from '@/components/auth'

export function InvitePage() {
  const { user, acceptInvite, acceptExistingInvite } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const token = params.get('token') || ''
  const [preview, setPreview] = useState(null)
  const [form, setForm] = useState({ fullName: '', password: '', confirmPassword: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const alreadySignedIn = Boolean(user && preview && user.email?.toLowerCase() === preview.email?.toLowerCase())

  useEffect(() => {
    if (!token) {
      setError('This invitation link is missing a token. Ask your administrator to send the invite again.')
      return
    }
    api.get(`/api/auth/invite?token=${encodeURIComponent(token)}`)
      .then(setPreview)
      .catch((err) => setError(err.message))
  }, [token])

  function goToFirstPayment(result) {
    const groupId = result.firstPayment?.groupId
    navigate(groupId ? `/pay?groupId=${groupId}` : '/')
  }

  async function submit(event) {
    event.preventDefault()
    setError('')
    if (!alreadySignedIn && form.password !== form.confirmPassword) {
      setError('The two passwords do not match.')
      return
    }
    setBusy(true)
    try {
      if (alreadySignedIn) {
        goToFirstPayment(await acceptExistingInvite(preview.invitationId))
      } else {
        goToFirstPayment(await acceptInvite(token, form.fullName, form.password))
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthShell
      eyebrow="INVITATION"
      title={alreadySignedIn ? 'Accept your invitation' : 'Create your password'}
      subtitle={preview
        ? alreadySignedIn
          ? `You are signed in as ${preview.email}. Join ${preview.groupName}, then make your first payment.`
          : `You were invited to join ${preview.groupName}. Create a password for ${preview.email} to get your login details.`
        : 'Open the link from your invitation email to create a password and join the Stokvel.'}
    >
      <form className="auth-form" onSubmit={submit}>
        {error && <div className="alert" role="alert">{error}</div>}
        {preview && (
          <AuthField label="Email" icon="mail">
            <input value={preview.email} readOnly />
          </AuthField>
        )}
        {!alreadySignedIn && (
          <>
            <AuthField label="Full name" icon="user">
              <input value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} autoComplete="name" required />
            </AuthField>
            <PasswordField label="Create password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} autoComplete="new-password" />
            <p className="auth-hint">Use at least 10 characters, with upper case, lower case, a number and a symbol.</p>
            <PasswordField label="Confirm password" value={form.confirmPassword} onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })} autoComplete="new-password" />
          </>
        )}
        <button className="btn auth-submit" disabled={busy || !token}>
          {busy ? 'Joining…' : alreadySignedIn ? 'Accept invitation and continue' : 'Create password and join'}
        </button>
      </form>
    </AuthShell>
  )
}
