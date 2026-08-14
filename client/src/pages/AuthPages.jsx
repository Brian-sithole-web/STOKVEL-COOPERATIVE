import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'

export function SetupPage() {
  const { setup } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ fullName: '', email: '', password: '' })
  const [error, setError] = useState('')

  function set(k, v) { setForm((f) => ({ ...f, [k]: v })) }

  async function submit(e) {
    e.preventDefault()
    setError('')
    try {
      await setup(form.fullName, form.email, form.password)
      navigate('/')
    } catch (err) { setError(err.message) }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card form" onSubmit={submit}>
        <div className="eyebrow">FIRST-RUN SETUP</div>
        <h1>Create the Platform Administrator</h1>
        <p>No default password is stored in the system. Choose a strong password of at least 10 characters with upper, lower, digit and symbol.</p>
        {error && <div className="alert">{error}</div>}
        <label>Full name<input value={form.fullName} onChange={(e) => set('fullName', e.target.value)} required /></label>
        <label>Email<input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} required /></label>
        <label>Password<input type="password" value={form.password} onChange={(e) => set('password', e.target.value)} required /></label>
        <button className="btn">Complete setup</button>
      </form>
    </div>
  )
}

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState('')

  useEffect(() => {
    api.get('/api/setup/status').then((s) => { if (s.requiresSetup) navigate('/setup') }).catch(() => {})
  }, [navigate])

  async function submit(e) {
    e.preventDefault()
    setError('')
    try {
      await login(form.email, form.password)
      navigate('/')
    } catch (err) { setError(err.message) }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card form" onSubmit={submit}>
        <div className="auth-brand">
          <img className="auth-logo" src="/pkvela-logo.png?v=2" alt="pkvela Cooperative" />
          <h1>Welcome back</h1>
          <p>Sign in to manage savings, instalments and borrowing.</p>
        </div>
        {error && <div className="alert">{error}</div>}
        <label>Email<input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></label>
        <label>Password<input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required /></label>
        <button className="btn">Sign in</button>
        <p className="muted auth-foot">New here? <Link to="/register">Create an account</Link></p>
      </form>
    </div>
  )
}

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ fullName: '', email: '', password: '' })
  const [error, setError] = useState('')

  async function submit(e) {
    e.preventDefault()
    setError('')
    try {
      await register(form.fullName, form.email, form.password)
      navigate('/')
    } catch (err) { setError(err.message) }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card form" onSubmit={submit}>
        <div className="eyebrow">JOIN THE COOPERATIVE</div>
        <h1>Create your account</h1>
        <p>Register, then create a Stokvel or accept an invitation.</p>
        {error && <div className="alert">{error}</div>}
        <label>Full name<input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required /></label>
        <label>Email<input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></label>
        <label>Password<input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required /></label>
        <button className="btn">Register</button>
        <p className="muted">Already registered? <Link to="/login">Sign in</Link></p>
      </form>
    </div>
  )
}

export function InvitePage() {
  const { acceptInvite } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [form, setForm] = useState({ token: params.get('token') || '', fullName: '', password: '' })
  const [error, setError] = useState('')

  async function submit(e) {
    e.preventDefault()
    setError('')
    try {
      await acceptInvite(form.token, form.fullName, form.password)
      navigate('/')
    } catch (err) { setError(err.message) }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card form" onSubmit={submit}>
        <div className="eyebrow">INVITATION</div>
        <h1>Join a Stokvel</h1>
        <p>If you already have an account, use the same email. A new account will be created if needed.</p>
        {error && <div className="alert">{error}</div>}
        <label>Invite token<input value={form.token} onChange={(e) => setForm({ ...form, token: e.target.value })} required /></label>
        <label>Full name<input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required /></label>
        <label>Password<input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required /></label>
        <button className="btn">Accept invitation</button>
      </form>
    </div>
  )
}

export function Gate({ children }) {
  const { user, ready } = useAuth()
  const navigate = useNavigate()
  const [setup, setSetup] = useState(null)

  useEffect(() => {
    api.get('/api/setup/status').then((s) => setSetup(s)).catch(() => setSetup({ requiresSetup: false }))
  }, [])

  useEffect(() => {
    if (!ready || setup == null) return
    if (setup.requiresSetup) navigate('/setup')
    else if (!user) navigate('/login')
  }, [ready, setup, user, navigate])

  if (!ready || !user) return <div className="content">Loading…</div>
  return children
}
