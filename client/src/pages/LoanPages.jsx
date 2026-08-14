import { useEffect, useState } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { money, prettyDate, statusLabel } from '../format'

export function GroupLoansPage() {
  const { groupId } = useAuth()
  const [rows, setRows] = useState([])
  const [schedule, setSchedule] = useState([])
  const [eligibility, setEligibility] = useState(null)
  const [error, setError] = useState('')
  const [form, setForm] = useState({ principal: 50000, interestRatePercent: 10, repaymentMonths: 10, purpose: 'Working capital', source: 'GroupSavingsPool' })
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))
  const load = () => groupId && api.get(`/api/groups/${groupId}/group-loans`).then(setRows).catch((e) => setError(e.message))
  useEffect(() => { load() }, [groupId])

  async function check() {
    try {
      setEligibility(await api.get(`/api/groups/${groupId}/group-loans/eligibility?amount=${form.principal}&source=${form.source}`))
    } catch (e) { setError(e.message) }
  }

  async function create(e) {
    e.preventDefault()
    try { await api.post(`/api/groups/${groupId}/group-loans`, form); load() }
    catch (err) { setError(err.message) }
  }

  async function action(id, path, body = {}) {
    try { await api.post(`/api/groups/${groupId}/group-loans/${id}/${path}`, body); load() }
    catch (err) { setError(err.message) }
  }

  async function showSchedule(id) {
    setSchedule(await api.get(`/api/groups/${groupId}/group-loans/${id}/schedule`))
  }

  if (!groupId) return <p>Select a Stokvel first.</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Group loans</h1>
          <p>Borrowing for the Stokvel as a whole. Approval is required before money can be paid out.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="split">
        <div className="card" style={{ overflowX: 'auto' }}>
          <table>
            <thead><tr><th>Loan</th><th>Principal</th><th>Total</th><th>Paid</th><th>Outstanding</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {rows.map((l) => (
                <tr key={l.id}>
                  <td>{l.loanNumber}<div className="muted">{l.purpose}</div></td>
                  <td>{money(l.principal)}</td>
                  <td>{money(l.totalRepayable)}</td>
                  <td>{money(l.amountPaid)}</td>
                  <td>{money(l.outstanding)}</td>
                  <td>{statusLabel(l.status)}</td>
                  <td className="actions">
                    {l.status === 'Draft' && <button className="btn" onClick={() => action(l.id, 'submit')}>Submit</button>}
                    {(l.status === 'Submitted' || l.status === 'UnderReview') && (
                      <>
                        <button className="btn" onClick={() => action(l.id, 'decide', { approve: true, comment: 'Approved' })}>Approve</button>
                        <button className="btn-danger" onClick={() => action(l.id, 'decide', { approve: false, comment: 'Rejected' })}>Reject</button>
                      </>
                    )}
                    {l.status === 'Approved' && <button className="btn" onClick={() => action(l.id, 'disburse')}>Disburse</button>}
                    {(l.status === 'Active' || l.status === 'PartiallyRepaid') && (
                      <button className="btn-secondary" onClick={() => action(l.id, 'repayments', { amount: l.nextInstalment || l.monthlyRepayment, paymentDate: new Date().toISOString(), paymentMethod: 'Eft', paymentReference: l.loanNumber })}>Record repayment</button>
                    )}
                    <button className="btn-secondary" onClick={() => showSchedule(l.id)}>Schedule</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {schedule.length > 0 && (
            <table>
              <thead><tr><th>#</th><th>Due</th><th>Due amount</th><th>Paid</th><th>Status</th></tr></thead>
              <tbody>
                {schedule.map((s) => (
                  <tr key={s.id}><td>{s.instalmentNumber}</td><td>{prettyDate(s.dueDate)}</td><td>{money(s.amountDue)}</td><td>{money(s.amountPaid)}</td><td>{s.status}</td></tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <form className="card form" onSubmit={create}>
          <h3>Borrowing application</h3>
          <label>Amount<input type="number" value={form.principal} onChange={(e) => set('principal', Number(e.target.value))} /></label>
          <label>Interest %<input type="number" value={form.interestRatePercent} onChange={(e) => set('interestRatePercent', Number(e.target.value))} /></label>
          <label>Months<input type="number" value={form.repaymentMonths} onChange={(e) => set('repaymentMonths', Number(e.target.value))} /></label>
          <label>Purpose<input value={form.purpose} onChange={(e) => set('purpose', e.target.value)} /></label>
          <label>Source
            <select value={form.source} onChange={(e) => set('source', e.target.value)}>
              <option>GroupSavingsPool</option><option>CooperativeLendingPool</option>
            </select>
          </label>
          <button type="button" className="btn-secondary" onClick={check}>Check eligibility</button>
          {eligibility && (
            <div className={eligibility.eligible ? 'success' : 'alert'}>
              {eligibility.eligible ? `Eligible. Maximum ${money(eligibility.maxAmount)}.` : eligibility.reasons.join(' ')}
            </div>
          )}
          <button className="btn">Save draft</button>
        </form>
      </div>
    </section>
  )
}

export function MemberLoansPage() {
  const { groupId } = useAuth()
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  const [form, setForm] = useState({ principal: 5000, interestRatePercent: 10, repaymentMonths: 6, purpose: 'Personal' })
  const load = () => groupId && api.get(`/api/groups/${groupId}/member-loans`).then(setRows).catch((e) => setError(e.message))
  useEffect(() => { load() }, [groupId])

  async function create(e) {
    e.preventDefault()
    try { await api.post(`/api/groups/${groupId}/member-loans`, form); load() }
    catch (err) { setError(err.message) }
  }

  async function action(id, path, body = {}) {
    try { await api.post(`/api/groups/${groupId}/member-loans/${id}/${path}`, body); load() }
    catch (err) { setError(err.message) }
  }

  if (!groupId) return <p>Select a Stokvel first.</p>
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Member loans</h1>
          <p>Members borrow from the Stokvel according to group rules. You cannot approve your own loan.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="split">
        <div className="card" style={{ overflowX: 'auto' }}>
          <table>
            <thead><tr><th>Loan</th><th>Member</th><th>Principal</th><th>Outstanding</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {rows.map((l) => (
                <tr key={l.id}>
                  <td>{l.loanNumber}</td>
                  <td>{l.memberName}</td>
                  <td>{money(l.principal)}</td>
                  <td>{money(l.outstanding)}</td>
                  <td>{statusLabel(l.status)}</td>
                  <td className="actions">
                    {(l.status === 'Submitted' || l.status === 'UnderReview') && (
                      <>
                        <button className="btn" onClick={() => action(l.id, 'decide', { approve: true, comment: 'Approved' })}>Approve</button>
                        <button className="btn-danger" onClick={() => action(l.id, 'decide', { approve: false, comment: 'Rejected' })}>Reject</button>
                      </>
                    )}
                    {l.status === 'Approved' && <button className="btn" onClick={() => action(l.id, 'disburse')}>Disburse</button>}
                    {(l.status === 'Active' || l.status === 'PartiallyRepaid') && (
                      <button className="btn-secondary" onClick={() => action(l.id, 'repayments', { amount: l.nextInstalment || l.monthlyRepayment, paymentDate: new Date().toISOString(), paymentMethod: 'Eft', paymentReference: l.loanNumber })}>Repay</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <form className="card form" onSubmit={create}>
          <h3>Request a loan</h3>
          <label>Amount<input type="number" value={form.principal} onChange={(e) => setForm({ ...form, principal: Number(e.target.value) })} /></label>
          <label>Interest %<input type="number" value={form.interestRatePercent} onChange={(e) => setForm({ ...form, interestRatePercent: Number(e.target.value) })} /></label>
          <label>Months<input type="number" value={form.repaymentMonths} onChange={(e) => setForm({ ...form, repaymentMonths: Number(e.target.value) })} /></label>
          <label>Purpose<input value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })} /></label>
          <button className="btn">Submit request</button>
        </form>
      </div>
    </section>
  )
}

export function AllLoansPage() {
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')
  useEffect(() => { api.get('/api/loans/group').then(setRows).catch((e) => setError(e.message)) }, [])
  return (
    <section>
      <div className="page-head"><div><h1>Loans</h1><p>Group borrowing across the cooperative.</p></div></div>
      {error && <div className="alert">{error}</div>}
      <div className="card">
        <table>
          <thead><tr><th>Loan</th><th>Group</th><th>Principal</th><th>Outstanding</th><th>Status</th></tr></thead>
          <tbody>
            {rows.map((l) => (
              <tr key={l.id}><td>{l.loanNumber}</td><td>{l.groupName}</td><td>{money(l.principal)}</td><td>{money(l.outstanding)}</td><td>{statusLabel(l.status)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
