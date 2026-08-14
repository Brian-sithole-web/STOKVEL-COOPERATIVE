import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'
import { money, prettyDate, statusLabel } from '../format'

export function GroupsPage() {
  const { user } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  const load = () => api.get('/api/groups').then(setRows).catch((e) => setError(e.message))
  useEffect(() => { load() }, [])

  async function act(id, path) {
    try { await api.post(`/api/groups/${id}/${path}`, { reason: 'Administrative action' }); load() }
    catch (e) { setError(e.message) }
  }

  return (
    <section>
      <div className="page-head">
        <div>
          <h1>All Stokvel groups</h1>
          <p>Independent groups registered on the cooperative platform.</p>
        </div>
        <Link className="btn" to="/stokvels/new">Create Stokvel</Link>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="card" style={{ overflowX: 'auto' }}>
        <table>
          <thead>
            <tr>
              <th>Group</th><th>Administrator</th><th>Members</th><th>Initial deposit</th>
              <th>Monthly</th><th>Savings</th><th>Outstanding loan</th><th>Loan status</th><th>Status</th><th>Created</th><th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((g) => (
              <tr key={g.id}>
                <td><Link to={`/stokvels/${g.id}`}>{g.name}</Link></td>
                <td>{g.administratorName}</td>
                <td>{g.memberCount}</td>
                <td>{money(g.initialDeposit)}</td>
                <td>{money(g.monthlyInstalment)}</td>
                <td>{money(g.totalSavings)}</td>
                <td>{money(g.outstandingLoan)}</td>
                <td>{g.loanStatus}</td>
                <td><span className="badge">{statusLabel(g.status)}</span></td>
                <td>{prettyDate(g.dateCreated)}</td>
                <td className="actions">
                  {user.isPlatformAdmin && g.status === 'PendingApproval' && <button className="btn" onClick={() => act(g.id, 'approve')}>Approve</button>}
                  {user.isPlatformAdmin && g.status === 'Active' && <button className="btn-secondary" onClick={() => act(g.id, 'suspend')}>Suspend</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

export function CreateGroupPage() {
  const navigate = useNavigate()
  const [error, setError] = useState('')
  const [form, setForm] = useState({
    name: '', description: '', initialDepositAmount: 5000, monthlyInstalmentAmount: 1000,
    instalmentDueDay: 25, gracePeriodDays: 5, latePenaltyAmount: 50, allowPartialPayments: true,
    allowAdditionalPayments: true, memberLoansEnabled: true, maxGroupBorrowingPercentOfSavings: 80,
    memberLoanMultiplier: 2, allowedLendingSources: 'Both', approvalWorkflow: 'StokvelAdminThenPlatformAdmin',
    defaultInterestRatePercent: 10,
  })
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  async function submit(e) {
    e.preventDefault()
    setError('')
    try {
      const group = await api.post('/api/groups', form)
      navigate(`/stokvels/${group.id}`)
    } catch (err) { setError(err.message) }
  }

  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Create a Stokvel</h1>
          <p>You become the Group Administrator. The group can activate once it has 5 members, an initial deposit, and platform approval.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <form className="card form" onSubmit={submit}>
        <label>Group name<input value={form.name} onChange={(e) => set('name', e.target.value)} required /></label>
        <label>Description<textarea value={form.description} onChange={(e) => set('description', e.target.value)} /></label>
        <div className="row">
          <label>Initial deposit<input type="number" value={form.initialDepositAmount} onChange={(e) => set('initialDepositAmount', Number(e.target.value))} /></label>
          <label>Monthly instalment<input type="number" value={form.monthlyInstalmentAmount} onChange={(e) => set('monthlyInstalmentAmount', Number(e.target.value))} /></label>
          <label>Due day<input type="number" min="1" max="28" value={form.instalmentDueDay} onChange={(e) => set('instalmentDueDay', Number(e.target.value))} /></label>
          <label>Grace days<input type="number" value={form.gracePeriodDays} onChange={(e) => set('gracePeriodDays', Number(e.target.value))} /></label>
          <label>Late penalty<input type="number" value={form.latePenaltyAmount} onChange={(e) => set('latePenaltyAmount', Number(e.target.value))} /></label>
        </div>
        <div className="row">
          <label>Max borrowing % of savings<input type="number" value={form.maxGroupBorrowingPercentOfSavings} onChange={(e) => set('maxGroupBorrowingPercentOfSavings', Number(e.target.value))} /></label>
          <label>Member loan multiplier<input type="number" step="0.1" value={form.memberLoanMultiplier} onChange={(e) => set('memberLoanMultiplier', Number(e.target.value))} /></label>
          <label>Default interest %<input type="number" value={form.defaultInterestRatePercent} onChange={(e) => set('defaultInterestRatePercent', Number(e.target.value))} /></label>
          <label>Lending source
            <select value={form.allowedLendingSources} onChange={(e) => set('allowedLendingSources', e.target.value)}>
              <option>GroupSavingsPool</option><option>CooperativeLendingPool</option><option>Both</option>
            </select>
          </label>
        </div>
        <label className="row">
          <span><input type="checkbox" checked={form.allowPartialPayments} onChange={(e) => set('allowPartialPayments', e.target.checked)} /> Partial payments</span>
          <span><input type="checkbox" checked={form.allowAdditionalPayments} onChange={(e) => set('allowAdditionalPayments', e.target.checked)} /> Additional payments</span>
          <span><input type="checkbox" checked={form.memberLoansEnabled} onChange={(e) => set('memberLoansEnabled', e.target.checked)} /> Member loans</span>
        </label>
        <button className="btn">Create Stokvel</button>
      </form>
    </section>
  )
}

export function GroupDetailPage() {
  const { id } = useParams()
  const { user, selectGroup } = useAuth()
  const [group, setGroup] = useState(null)
  const [members, setMembers] = useState([])
  const [txs, setTxs] = useState([])
  const [error, setError] = useState('')

  useEffect(() => {
    selectGroup(id)
    Promise.all([
      api.get(`/api/groups/${id}`),
      api.get(`/api/groups/${id}/members`),
      api.get(`/api/groups/${id}/ledger/transactions`),
    ]).then(([g, m, t]) => { setGroup(g); setMembers(m); setTxs(t) }).catch((e) => setError(e.message))
  }, [id])

  async function approve() {
    try { await api.post(`/api/groups/${id}/approve`, {}); setGroup(await api.get(`/api/groups/${id}`)) }
    catch (e) { setError(e.message) }
  }

  if (!group) return error ? <div className="alert">{error}</div> : <p>Loading…</p>
  const f = group.financials
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>{group.name}</h1>
          <p>{group.code} · {statusLabel(group.status)} · Admin {group.administratorName} · {group.memberCount} members</p>
        </div>
        {user.isPlatformAdmin && group.status === 'PendingApproval' && <button className="btn" onClick={approve}>Approve & activate</button>}
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="grid cards">
        <div className="card accent"><div className="label">Current savings</div><div className="value">{money(f.currentSavings)}</div></div>
        <div className="card"><div className="label">Initial deposit</div><div className="value">{money(f.initialDeposit)}</div></div>
        <div className="card"><div className="label">Monthly instalments</div><div className="value">{money(f.totalMonthlyInstalments)}</div></div>
        <div className="card"><div className="label">Outstanding loans</div><div className="value">{money(f.outstandingLoans)}</div></div>
      </div>
      <div className="split" style={{ marginTop: 18 }}>
        <div className="card">
          <h3>Members</h3>
          <table>
            <thead><tr><th>Name</th><th>Role</th><th>Contributions</th></tr></thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id}><td>{m.fullName}</td><td>{statusLabel(m.role)}</td><td>{money(m.contributions)}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="card">
          <h3>Recent transactions</h3>
          <table>
            <thead><tr><th>Ref</th><th>Type</th><th>Amount</th></tr></thead>
            <tbody>
              {txs.slice(0, 8).map((t) => (
                <tr key={t.id}><td>{t.transactionNumber}</td><td>{statusLabel(t.type)}</td><td>{money(t.amount)}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}
