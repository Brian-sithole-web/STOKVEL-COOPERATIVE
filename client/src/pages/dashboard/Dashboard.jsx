import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { useAuth } from '@/auth/AuthProvider'
import { isOfficer, money, prettyDate, statusLabel } from '@/lib/format'
import { Icon } from '@/components/ui/Icon'

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
        <Link className="btn" to="/stokvels/new"><Icon name="plus" size={18} /> Create Stokvel</Link>
      </div>
    </section>
  )
}

function StatCard({ label, value, icon, variant }) {
  return (
    <div className={`stat-card ${variant || ''}`.trim()}>
      <div className="stat-icon"><Icon name={icon} size={14} /></div>
      <div className="stat-body">
        <div className="label">{label}</div>
        <div className="value">{value}</div>
      </div>
    </div>
  )
}

function PlatformDash() {
  const [data, setData] = useState(null)
  const [activity, setActivity] = useState([])
  const [error, setError] = useState('')
  const [period, setPeriod] = useState('month')

  useEffect(() => {
    api.get('/api/dashboard/platform').then(setData).catch((e) => setError(e.message))
    api.get('/api/audit').then((rows) => setActivity(Array.isArray(rows) ? rows.slice(0, 6) : [])).catch(() => setActivity([]))
  }, [])

  if (error) return <div className="alert">{error}</div>
  if (!data) return <p>Loading dashboard…</p>

  const cards = [
    ['Total Stokvel groups', data.totalStokvelGroups, 'users'],
    ['Active groups', data.activeStokvelGroups, 'check'],
    ['Pending groups', data.pendingGroups, 'clock'],
    ['Total members', data.totalMembers, 'user'],
    ['Initial deposits', money(data.totalInitialDeposits), 'deposit'],
    ['Monthly instalments', money(data.totalMonthlyInstalments), 'calendar'],
    ['Cooperative savings', money(data.totalCooperativeSavings), 'piggy', 'accent'],
    ['Group loans', money(data.totalGroupLoans), 'landmark'],
    ['Member loans', money(data.totalMemberLoans), 'wallet'],
    ['Outstanding loans', money(data.outstandingLoans), 'hourglass'],
    ['Overdue loans', money(data.overdueLoans), 'alert', 'warn'],
    ['Total repayments', money(data.totalRepayments), 'repay'],
  ]

  return (
    <section className="dash">
      <div className="page-head">
        <div>
          <h1>Platform overview</h1>
          <p>All registered Stokvels, savings and borrowing across the cooperative.</p>
        </div>
        <Link className="btn" to="/stokvels"><Icon name="users" size={16} /> All Stokvel groups</Link>
      </div>

      <div className="dash-metrics">
        {cards.map(([label, value, icon, variant]) => (
          <StatCard key={label} label={label} value={value} icon={icon} variant={variant} />
        ))}
      </div>

      <div className="health-card">
        <div className="health-copy">
          <div className="health-top">
            <h2>Financial health overview</h2>
            <label className="health-period">
              <select value={period} onChange={(e) => setPeriod(e.target.value)}>
                <option value="month">This month</option>
                <option value="quarter">This quarter</option>
                <option value="year">This year</option>
              </select>
            </label>
          </div>
          <div className="health-stats">
            <HealthStat label="Total Savings" value={money(data.totalCooperativeSavings)} tone="mint" />
            <HealthStat label="Total Loans" value={money(Number(data.totalGroupLoans) + Number(data.totalMemberLoans))} tone="gold" />
            <HealthStat label="Total Repayments" value={money(data.totalRepayments)} tone="blue" />
          </div>
        </div>
        <HealthChart period={period} />
      </div>

      <div className="dash-bottom">
        <div className="panel">
          <div className="panel-head">
            <h2>Recent activity</h2>
            <Link to="/audit" className="panel-link">View all</Link>
          </div>
          {activity.length === 0 ? (
            <div className="empty-state">
              <EmptySearchArt />
              <strong>No recent activity</strong>
              <p>New deposits, loans and approvals will show up here.</p>
            </div>
          ) : (
            <table>
              <thead>
                <tr><th>Activity</th><th>Group</th><th>Amount</th><th>Date</th></tr>
              </thead>
              <tbody>
                {activity.map((row) => (
                  <tr key={row.id}>
                    <td>{row.description || row.action}</td>
                    <td>{row.actor || '—'}</td>
                    <td>—</td>
                    <td>{prettyDate(row.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="panel">
          <div className="panel-head"><h2>Quick actions</h2></div>
          <div className="quick-grid">
            <Link className="quick-tile" to="/stokvels/new">
              <span className="stat-icon"><Icon name="plus" size={18} /></span>
              Create Stokvel group
            </Link>
            <Link className="quick-tile" to="/contributions">
              <span className="stat-icon"><Icon name="deposit" size={18} /></span>
              Record deposit
            </Link>
            <Link className="quick-tile" to="/loans">
              <span className="stat-icon"><Icon name="check" size={18} /></span>
              Approve loan
            </Link>
            <Link className="quick-tile" to="/reports">
              <span className="stat-icon"><Icon name="reports" size={18} /></span>
              View reports
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}

function HealthStat({ label, value, tone }) {
  return (
    <div className="health-stat">
      <div className="health-label"><span className={`dot ${tone}`} /> {label}</div>
      <div className="health-value">{value}</div>
      <div className="health-delta">+0.0%</div>
    </div>
  )
}

function HealthChart({ period }) {
  const series = period === 'year'
    ? [18, 22, 20, 28, 34, 32, 40, 48, 44, 52, 58, 62]
    : period === 'quarter'
      ? [24, 28, 26, 36, 42, 38, 50, 56]
      : [22, 30, 26, 38, 34, 48, 44, 58, 52, 66, 62, 74]
  const w = 420
  const h = 112
  const pad = 16
  const max = Math.max(...series)
  const coords = series.map((v, i) => {
    const x = pad + (i * (w - pad * 2)) / (series.length - 1)
    const y = h - pad - (v / max) * (h - pad * 2)
    return [x, y]
  })
  const line = coords.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x} ${y}`).join(' ')
  const area = `${line} L${coords.at(-1)[0]} ${h - pad} L${coords[0][0]} ${h - pad} Z`
  return (
    <div className="health-chart">
      <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" aria-hidden="true">
        {[0.25, 0.5, 0.75].map((p) => (
          <line key={p} x1={pad} x2={w - pad} y1={pad + p * (h - pad * 2)} y2={pad + p * (h - pad * 2)} stroke="rgba(255,255,255,.12)" />
        ))}
        <path d={area} fill="rgba(61,207,142,.16)" />
        <path d={line} fill="none" stroke="#3dcf8e" strokeWidth="2.6" strokeLinejoin="round" />
        {coords.map(([x, y], i) => (
          <circle key={i} cx={x} cy={y} r="3.4" fill="#063925" stroke="#3dcf8e" strokeWidth="2" />
        ))}
      </svg>
      <div className="chart-caption">30-day trend</div>
    </div>
  )
}

function EmptySearchArt() {
  return (
    <svg className="empty-art" viewBox="0 0 120 88" fill="none" aria-hidden="true">
      <rect x="22" y="14" width="52" height="62" rx="8" stroke="#c5cdc8" strokeWidth="2" fill="#f7faf8" />
      <path d="M34 32h28M34 42h22M34 52h16" stroke="#d4dbd6" strokeWidth="2" strokeLinecap="round" />
      <circle cx="78" cy="56" r="16" fill="#eef6f1" stroke="#3dcf8e" strokeWidth="2.5" />
      <path d="M89 68l12 12" stroke="#063925" strokeWidth="3" strokeLinecap="round" />
    </svg>
  )
}

function GroupDash({ groupId }) {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => { api.get(`/api/dashboard/group/${groupId}`).then(setData).catch((e) => setError(e.message)) }, [groupId])
  if (error) return <div className="alert">{error}</div>
  if (!data) return <p>Loading dashboard…</p>
  const cards = [
    ['Group balance', money(data.groupBalance), 'piggy', 'accent'],
    ['Initial deposit', money(data.initialDeposit), 'deposit'],
    ['Monthly instalment', money(data.monthlyInstalment), 'calendar'],
    ['Total contributions', money(data.totalContributions), 'wallet'],
    ['Outstanding instalments', money(data.outstandingInstalments), 'hourglass'],
    ['Available funds', money(data.availableFunds), 'check'],
    ['Outstanding loans', money(data.outstandingLoans), 'loans'],
    ['Members', data.memberCount, 'users'],
  ]
  return (
    <section className="dash">
      <div className="page-head">
        <div>
          <h1>{data.groupName}</h1>
          <p>Status: {statusLabel(data.status)} · Next instalment {prettyDate(data.nextInstalmentDue)}</p>
        </div>
        <div className="actions">
          <Link className="btn" to="/members"><Icon name="user" size={18} /> Add member</Link>
          <Link className="btn-secondary" to="/contributions">Record deposit</Link>
          <Link className="btn-secondary" to="/group-loans">Request group loan</Link>
        </div>
      </div>
      <div className="dash-metrics">
        {cards.map(([label, value, icon, variant]) => (
          <StatCard key={label} label={label} value={value} icon={icon} variant={variant} />
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
    ['My total contributions', money(data.myTotalContributions), 'piggy', 'accent'],
    ['My monthly instalment', money(data.myMonthlyInstalment), 'calendar'],
    ['Outstanding instalments', money(data.myOutstandingInstalments), 'hourglass'],
    ['My loan', money(data.myLoan), 'loans'],
    ['Outstanding loan', money(data.myOutstandingLoan), 'alert', Number(data.myOutstandingLoan) > 0 ? 'warn' : ''],
  ]
  return (
    <section className="dash">
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
      <div className="dash-metrics">
        {cards.map(([label, value, icon, variant]) => (
          <StatCard key={label} label={label} value={value} icon={icon} variant={variant} />
        ))}
      </div>
    </section>
  )
}
