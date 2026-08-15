import { useEffect, useState } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { money, prettyDate, statusLabel } from '../format'

export function ContributionsPage() {
  const { groupId, membership } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  const [form, setForm] = useState({
    amount: 1000, kind: 'MonthlyInstalment', paymentDate: new Date().toISOString().slice(0, 10),
    paymentReference: '', paymentMethod: 'Eft', memberId: membership?.memberId || '',
  })
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))
  const load = () => groupId && api.get(`/api/groups/${groupId}/contributions`).then(setRows).catch((e) => setError(e.message))
  useEffect(() => { load() }, [groupId])

  async function record(e) {
    e.preventDefault()
    try {
      await api.post(`/api/groups/${groupId}/contributions`, {
        ...form,
        memberId: form.memberId || null,
        paymentDate: new Date(form.paymentDate).toISOString(),
      })
      load()
    } catch (err) { setError(err.message) }
  }

  async function confirm(id) {
    try { await api.post(`/api/groups/${groupId}/contributions/${id}/confirm`, {}); load() }
    catch (err) { setError(err.message) }
  }

  if (!groupId) return <p>Select a Stokvel first.</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Contributions</h1>
          <p>Record initial deposits, monthly instalments and additional payments. Only confirmed transactions change the savings pool.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="split">
        <div className="table-card" style={{ overflowX: 'auto' }}>
          <table>
            <thead><tr><th>Date</th><th>Member</th><th>Kind</th><th>Amount</th><th>Status</th><th>Ref</th><th></th></tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td>{prettyDate(r.paymentDate)}</td>
                  <td>{r.memberName || 'Group'}</td>
                  <td>{statusLabel(r.kind)}</td>
                  <td>{money(r.amount)}</td>
                  <td><span className={`badge ${r.status === 'Confirmed' ? 'ok' : ''}`}>{r.status}</span></td>
                  <td>{r.transactionNumber || r.paymentReference || '—'}</td>
                  <td>{r.status === 'Pending' && <button className="btn" onClick={() => confirm(r.id)}>Confirm</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <form className="card form" onSubmit={record}>
          <h3>Record a payment</h3>
          <label>Kind
            <select value={form.kind} onChange={(e) => set('kind', e.target.value)}>
              <option>InitialDeposit</option><option>MonthlyInstalment</option><option>AdditionalDeposit</option>
            </select>
          </label>
          <label>Amount<input type="number" value={form.amount} onChange={(e) => set('amount', Number(e.target.value))} /></label>
          <label>Date<input type="date" value={form.paymentDate} onChange={(e) => set('paymentDate', e.target.value)} /></label>
          <label>Reference<input value={form.paymentReference} onChange={(e) => set('paymentReference', e.target.value)} /></label>
          <label>Method
            <select value={form.paymentMethod} onChange={(e) => set('paymentMethod', e.target.value)}>
              <option>Eft</option><option>Cash</option><option>Card</option><option>Other</option>
            </select>
          </label>
          <button className="btn">Save payment</button>
        </form>
      </div>
    </section>
  )
}

export function InstalmentsPage() {
  const { groupId } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  const now = new Date()
  const load = () => groupId && api.get(`/api/groups/${groupId}/instalments`).then(setRows).catch((e) => setError(e.message))
  useEffect(() => { load() }, [groupId])

  async function generate() {
    try {
      await api.post(`/api/groups/${groupId}/contributions/generate?year=${now.getFullYear()}&month=${now.getMonth() + 1}`, {})
      load()
    } catch (e) { setError(e.message) }
  }

  if (!groupId) return <p>Select a Stokvel first.</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Monthly instalments</h1>
          <p>Automatic obligations with due date, outstanding amount and payment status.</p>
        </div>
        <button className="btn" onClick={generate}>Generate this month</button>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="table-card" style={{ overflowX: 'auto' }}>
        <table>
          <thead><tr><th>Period</th><th>Member</th><th>Due</th><th>Amount due</th><th>Paid</th><th>Outstanding</th><th>Status</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td>{r.month}/{r.year} {r.isGroupLevel ? '(Group)' : ''}</td>
                <td>{r.memberName || '—'}</td>
                <td>{prettyDate(r.dueDate)}</td>
                <td>{money(r.amountDue)}</td>
                <td>{money(r.amountPaid)}</td>
                <td>{money(r.outstanding)}</td>
                <td><span className={`badge ${r.status === 'Overdue' ? 'bad' : r.status === 'Paid' ? 'ok' : 'warn'}`}>{statusLabel(r.status)}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

export function SavingsPage() {
  const { groupId } = useAuth()
  const [ledger, setLedger] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => {
    if (!groupId) return
    api.get(`/api/groups/${groupId}/ledger`).then(setLedger).catch((e) => setError(e.message))
  }, [groupId])
  if (!groupId) return <p>Select a Stokvel first.</p>
  if (!ledger) return error ? <div className="alert">{error}</div> : <p>Loading ledger…</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Group savings</h1>
          <p>Balances are calculated from the ledger. They cannot be edited by hand.</p>
        </div>
      </div>
      <div className="reports-metrics metrics-4">
        <div className="stat-card"><div className="stat-body"><div className="label">Opening balance</div><div className="value">{money(ledger.openingBalance)}</div></div></div>
        <div className="stat-card"><div className="stat-body"><div className="label">Money received</div><div className="value">{money(ledger.moneyReceived)}</div></div></div>
        <div className="stat-card"><div className="stat-body"><div className="label">Money paid out</div><div className="value">{money(ledger.moneyPaidOut)}</div></div></div>
        <div className="stat-card accent"><div className="stat-body"><div className="label">Closing balance</div><div className="value">{money(ledger.closingBalance)}</div></div></div>
      </div>
    </section>
  )
}

export function TransactionsPage() {
  const { groupId } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  useEffect(() => {
    if (!groupId) return
    api.get(`/api/groups/${groupId}/ledger/transactions`).then(setRows).catch((e) => setError(e.message))
  }, [groupId])
  if (!groupId) return <p>Select a Stokvel first.</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Transactions</h1>
          <p>Every financial event is kept. Reversals create a new correction entry instead of deleting history.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="table-card" style={{ overflowX: 'auto' }}>
        <table>
          <thead><tr><th>Number</th><th>Date</th><th>Type</th><th>Direction</th><th>Amount</th><th>Status</th><th>Reference</th></tr></thead>
          <tbody>
            {rows.map((t) => (
              <tr key={t.id}>
                <td>{t.transactionNumber}</td>
                <td>{prettyDate(t.transactionDate)}</td>
                <td>{statusLabel(t.type)}</td>
                <td>{t.direction}</td>
                <td>{money(t.amount)}</td>
                <td>{t.status}</td>
                <td>{t.paymentReference || t.description || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
