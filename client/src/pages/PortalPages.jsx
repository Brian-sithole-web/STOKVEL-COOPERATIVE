import { useEffect, useState } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { money, prettyDate, statusLabel } from '../format'

export function ReportsPage() {
  const { groupId, user } = useAuth()
  const [statement, setStatement] = useState(null)
  const [members, setMembers] = useState([])
  const [loans, setLoans] = useState([])
  const [error, setError] = useState('')
  const from = new Date(new Date().getFullYear(), 0, 1).toISOString()
  const to = new Date().toISOString()

  useEffect(() => {
    async function load() {
      try {
        if (groupId) {
          setStatement(await api.get(`/api/reports/groups/${groupId}/statement?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`))
          setMembers(await api.get(`/api/reports/groups/${groupId}/members`))
          setLoans(await api.get(`/api/reports/loans?groupId=${groupId}`))
        } else if (user.isPlatformAdmin) {
          setLoans(await api.get('/api/reports/loans'))
        }
      } catch (e) { setError(e.message) }
    }
    load()
  }, [groupId])

  return (
    <section className="reports">
      <div className="page-head">
        <div>
          <h1>Reports</h1>
          <p>Financial statements generated from the ledger and loan registers.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      {statement && (
        <div className="reports-metrics">
          <div className="stat-card"><div className="stat-body"><div className="label">Opening</div><div className="value">{money(statement.openingBalance)}</div></div></div>
          <div className="stat-card"><div className="stat-body"><div className="label">Initial deposits</div><div className="value">{money(statement.initialDeposits)}</div></div></div>
          <div className="stat-card"><div className="stat-body"><div className="label">Monthly instalments</div><div className="value">{money(statement.monthlyInstalments)}</div></div></div>
          <div className="stat-card"><div className="stat-body"><div className="label">Loan disbursements</div><div className="value">{money(statement.loanDisbursements)}</div></div></div>
          <div className="stat-card"><div className="stat-body"><div className="label">Repayments</div><div className="value">{money(statement.loanRepayments)}</div></div></div>
          <div className="stat-card accent"><div className="stat-body"><div className="label">Closing</div><div className="value">{money(statement.closingBalance)}</div></div></div>
        </div>
      )}
      {members.length > 0 && (
        <div className="table-card">
          <h3>Member statements</h3>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Member</th><th>Contributions</th><th>Loans</th><th>Repayments</th><th>Outstanding</th></tr></thead>
              <tbody>
                {members.map((m) => (
                  <tr key={m.memberId}><td>{m.memberName}</td><td>{money(m.contributions)}</td><td>{money(m.loans)}</td><td>{money(m.repayments)}</td><td>{money(m.outstandingBalance)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
      <div className="table-card">
        <h3>Loan report</h3>
        <div className="table-wrap">
          <table>
            <thead><tr><th>Loan</th><th>Borrower</th><th>Kind</th><th>Principal</th><th>Interest</th><th>Paid</th><th>Outstanding</th><th>Status</th></tr></thead>
            <tbody>
              {loans.map((l) => (
                <tr key={l.loanNumber}><td>{l.loanNumber}</td><td>{l.borrower}</td><td>{statusLabel(l.kind)}</td><td>{money(l.principal)}</td><td>{money(l.interest)}</td><td>{money(l.paid)}</td><td>{money(l.outstanding)}</td><td>{statusLabel(l.status)}</td></tr>
              ))}
              {loans.length === 0 && (
                <tr><td colSpan={8} className="muted">No loans in this period.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}

export function NotificationsPage() {
  const [rows, setRows] = useState([])
  const load = () => api.get('/api/notifications').then(setRows)
  useEffect(() => { load() }, [])
  return (
    <section>
      <div className="page-head"><div><h1>Notifications</h1><p>Due dates, approvals and membership updates.</p></div></div>
      <div className="card">
        {rows.map((n) => (
          <div key={n.id} style={{ padding: '12px 0', borderBottom: '1px solid var(--line)', display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <div>
              <strong>{n.title}</strong>
              <div className="muted">{n.message}</div>
              <small>{prettyDate(n.createdAt)}</small>
            </div>
            {!n.isRead && <button className="btn-secondary" onClick={async () => { await api.post(`/api/notifications/${n.id}/read`, {}); load() }}>Mark read</button>}
          </div>
        ))}
        {rows.length === 0 && <p className="muted">No notifications yet.</p>}
      </div>
    </section>
  )
}

export function AuditPage() {
  const { groupId } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  useEffect(() => {
    const q = groupId ? `?groupId=${groupId}` : ''
    api.get(`/api/audit${q}`).then(setRows).catch((e) => setError(e.message))
  }, [groupId])
  return (
    <section>
      <div className="page-head"><div><h1>Audit logs</h1><p>Important actions across the cooperative.</p></div></div>
      {error && <div className="alert">{error}</div>}
      <div className="card">
        <table>
          <thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Description</th></tr></thead>
          <tbody>
            {rows.map((a) => (
              <tr key={a.id}><td>{prettyDate(a.createdAt)}</td><td>{a.actor || '—'}</td><td>{a.action}</td><td>{a.description}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

export function SettingsPage() {
  const [form, setForm] = useState(null)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState('')
  useEffect(() => { api.get('/api/settings').then(setForm).catch((e) => setError(e.message)) }, [])
  if (!form) return error ? <div className="alert">{error}</div> : <p>Loading…</p>
  async function save(e) {
    e.preventDefault()
    try { await api.put('/api/settings', form); setSaved(true) }
    catch (err) { setError(err.message) }
  }
  return (
    <section>
      <div className="page-head"><div><h1>Platform settings</h1><p>Default borrowing rules for all Stokvels.</p></div></div>
      {error && <div className="alert">{error}</div>}
      {saved && <div className="success">Settings saved.</div>}
      <form className="card form" onSubmit={save}>
        <label>Default max borrowing % of savings
          <input type="number" value={form.defaultMaxGroupBorrowingPercent} onChange={(e) => setForm({ ...form, defaultMaxGroupBorrowingPercent: Number(e.target.value) })} />
        </label>
        <label>Default minimum members
          <input type="number" value={form.defaultMinimumMembers} onChange={(e) => setForm({ ...form, defaultMinimumMembers: Number(e.target.value) })} />
        </label>
        <label>Default lending sources
          <select value={form.defaultLendingSources} onChange={(e) => setForm({ ...form, defaultLendingSources: e.target.value })}>
            <option>GroupSavingsPool</option><option>CooperativeLendingPool</option><option>Both</option>
          </select>
        </label>
        <button className="btn">Save settings</button>
      </form>
    </section>
  )
}
