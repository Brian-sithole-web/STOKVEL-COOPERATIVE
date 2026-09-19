import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { useAuth } from '@/auth/AuthProvider'
import { money, prettyDate, statusLabel } from '@/lib/format'

export default function MembersPage() {
  const { groupId } = useAuth()
  const [members, setMembers] = useState([])
  const [email, setEmail] = useState('')
  const [invite, setInvite] = useState(null)
  const [error, setError] = useState('')

  const load = () => groupId && api.get(`/api/groups/${groupId}/members`).then(setMembers).catch((e) => setError(e.message))
  useEffect(() => { load() }, [groupId])

  async function inviteMember(e) {
    e.preventDefault()
    setError('')
    try {
      const result = await api.post(`/api/groups/${groupId}/invites`, { email, clientOrigin: window.location.origin })
      setInvite(result)
      setEmail('')
    } catch (err) { setError(err.message) }
  }

  async function addExisting(e) {
    e.preventDefault()
    setError('')
    try {
      await api.post(`/api/groups/${groupId}/members`, { email })
      setEmail('')
      load()
    } catch (err) { setError(err.message) }
  }

  if (!groupId) return <p>Select a Stokvel first.</p>

  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Members</h1>
          <p>A Stokvel needs at least 5 members before the Platform Administrator can activate it.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      {invite && (
        <div className="success">
          <p>{invite.message || `Invitation created for ${invite.email}.`}</p>
          {invite.token && (
            <p>
              <a href={`${window.location.origin}/invite?token=${encodeURIComponent(invite.token)}`}>
                Open create-password link
              </a>
            </p>
          )}
        </div>
      )}
      <div className="split">
        <div className="card">
          <table>
            <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Joined</th><th>Contributions</th><th>Loan</th></tr></thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id}>
                  <td>{m.fullName}</td><td>{m.email}</td><td>{statusLabel(m.role)}</td>
                  <td>{prettyDate(m.joinedAt)}</td><td>{money(m.contributions)}</td><td>{money(m.loanBalance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="card form">
          <h3>Invite or add a member</h3>
          <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
          <div className="actions">
            <button className="btn" onClick={inviteMember}>Invite member</button>
            <button className="btn-secondary" onClick={addExisting}>Add registered user</button>
          </div>
        </div>
      </div>
    </section>
  )
}
