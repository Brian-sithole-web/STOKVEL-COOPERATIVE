import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'
import { isOfficer, money, prettyDate, statusLabel } from '../format'

export default function Dashboard() {
  const { user, groupId, membership } = useAuth()
  if (user.isPlatformAdmin && !groupId) return <PlatformDash />
  if (groupId && (user.isPlatformAdmin || isOfficer(membership?.role))) return <GroupDash groupId={groupId} />
  if (groupId) return <MemberDash groupId={groupId} />
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>My Stokvel</h1>
          <p>You are not in a Stokvel yet. Create one or wait for an invitation.</p>
        </div>
        <Link className="btn" to="/stokvels/new">Create Stokvel</Link>
      </div>
    </section>
  )
}

function PlatformDash() {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.get('/api/dashboard/platform').then(setData).catch((e) => setError(e.message)) }, [])
  if (error) return <div className="alert">{error}</div>
  if (!data) return <p>Loading dashboard…</p>
  const cards = [
    ['Total Stokvel groups', data.totalStokvelGroups],
    ['Active groups', data.activeStokvelGroups],
    ['Pending groups', data.pendingGroups],
    ['Total members', data.totalMembers],
    ['Initial deposits', money(data.totalInitialDeposits)],
    ['Monthly instalments', money(data.totalMonthlyInstalments)],
    ['Cooperative savings', money(data.totalCooperativeSavings)],
    ['Group loans', money(data.totalGroupLoans)],
    ['Member loans', money(data.totalMemberLoans)],
    ['Outstanding loans', money(data.outstandingLoans)],
    ['Overdue loans', money(data.overdueLoans)],
    ['Total repayments', money(data.totalRepayments)],
  ]
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>Platform overview</h1>
          <p>All registered Stokvels, savings and borrowing across the cooperative.</p>
        </div>
        <Link className="btn" to="/stokvels">All Stokvel groups</Link>
      </div>
      <div className="grid cards">
        {cards.map(([label, value], i) => (
          <div className={`card ${i === 6 ? 'accent' : ''}`} key={label}>
            <div className="label">{label}</div>
            <div className="value">{value}</div>
          </div>
        ))}
      </div>
    </section>
  )
}

function GroupDash({ groupId }) {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.get(`/api/dashboard/group/${groupId}`).then(setData).catch((e) => setError(e.message)) }, [groupId])
  if (error) return <div className="alert">{error}</div>
  if (!data) return <p>Loading dashboard…</p>
  const cards = [
    ['Group balance', money(data.groupBalance), true],
    ['Initial deposit', money(data.initialDeposit)],
    ['Monthly instalment', money(data.monthlyInstalment)],
    ['Total contributions', money(data.totalContributions)],
    ['Outstanding instalments', money(data.outstandingInstalments)],
    ['Available funds', money(data.availableFunds)],
    ['Outstanding loans', money(data.outstandingLoans)],
    ['Members', data.memberCount],
  ]
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>{data.groupName}</h1>
          <p>Status: {statusLabel(data.status)} · Next instalment {prettyDate(data.nextInstalmentDue)}</p>
        </div>
        <div className="actions">
          <Link className="btn" to="/members">Add member</Link>
          <Link className="btn-secondary" to="/contributions">Record deposit</Link>
          <Link className="btn-secondary" to="/group-loans">Request group loan</Link>
        </div>
      </div>
      <div className="grid cards">
        {cards.map(([label, value, accent]) => (
          <div className={`card ${accent ? 'accent' : ''}`} key={label}>
            <div className="label">{label}</div>
            <div className="value">{value}</div>
          </div>
        ))}
      </div>
    </section>
  )
}

function MemberDash({ groupId }) {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.get(`/api/dashboard/member/${groupId}`).then(setData).catch((e) => setError(e.message)) }, [groupId])
  if (error) return <div className="alert">{error}</div>
  if (!data) return <p>Loading dashboard…</p>
  const cards = [
    ['My total contributions', money(data.myTotalContributions), true],
    ['My monthly instalment', money(data.myMonthlyInstalment)],
    ['Outstanding instalments', money(data.myOutstandingInstalments)],
    ['My loan', money(data.myLoan)],
    ['Outstanding loan', money(data.myOutstandingLoan)],
  ]
  return (
    <section>
      <div className="page-head">
        <div>
          <h1>{data.groupName}</h1>
          <p>Next payment: {prettyDate(data.myNextPayment)}</p>
        </div>
        <div className="actions">
          <Link className="btn" to="/contributions">Make contribution</Link>
          <Link className="btn-secondary" to="/member-loans">Request loan</Link>
        </div>
      </div>
      <div className="grid cards">
        {cards.map(([label, value, accent]) => (
          <div className={`card ${accent ? 'accent' : ''}`} key={label}>
            <div className="label">{label}</div>
            <div className="value">{value}</div>
          </div>
        ))}
      </div>
    </section>
  )
}
