import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth'
import { isOfficer } from '../format'

export default function Layout() {
  const { user, groupId, selectGroup, logout, membership } = useAuth()
  const navigate = useNavigate()
  const admin = user?.isPlatformAdmin
  const officer = admin || isOfficer(membership?.role)

  const platformLinks = [
    ['/', 'Dashboard'],
    ['/stokvels', 'Stokvels'],
    ['/loans', 'Loans'],
    ['/reports', 'Reports'],
    ['/notifications', 'Notifications'],
    ['/audit', 'Audit logs'],
    ['/settings', 'Settings'],
  ]

  const groupLinks = [
    ['/', 'Dashboard'],
    ['/stokvels', 'My Stokvels'],
    ['/members', 'Members'],
    ['/contributions', 'Contributions'],
    ['/instalments', 'Monthly instalments'],
    ['/savings', 'Group savings'],
    ['/group-loans', 'Group loans'],
    ['/member-loans', 'Member loans'],
    ['/transactions', 'Transactions'],
    ['/reports', 'Reports'],
    ['/notifications', 'Notifications'],
  ]

  const memberLinks = [
    ['/', 'Dashboard'],
    ['/stokvels', 'My Stokvels'],
    ['/contributions', 'My contributions'],
    ['/instalments', 'My instalments'],
    ['/member-loans', 'My loans'],
    ['/transactions', 'My transactions'],
    ['/notifications', 'Notifications'],
  ]

  const links = admin ? platformLinks : officer ? groupLinks : memberLinks

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <img className="brand-logo" src="/pkvela-logo.png?v=2" alt="pkvela Cooperative" />
        </div>

        {user?.memberships?.length > 0 && (
          <label className="group-select">
            Select Stokvel
            <select value={groupId} onChange={(e) => selectGroup(e.target.value)}>
              {admin && <option value="">All groups</option>}
              {user.memberships.map((m) => (
                <option key={m.groupId} value={m.groupId}>{m.groupName}</option>
              ))}
            </select>
          </label>
        )}

        <nav>
          {links.map(([to, label]) => (
            <NavLink key={to} to={to} end={to === '/'}>{label}</NavLink>
          ))}
        </nav>

        <div className="sidebar-foot">
          <div className="user-card">
            <div className="user-meta">
              <strong>{user?.fullName}</strong>
              <small>{admin ? 'Platform Administrator' : statusRole(membership?.role)}</small>
            </div>
            <button type="button" className="btn-signout" onClick={() => { logout(); navigate('/login') }}>
              Sign out
            </button>
          </div>
        </div>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}

function statusRole(role) {
  if (!role) return 'Member'
  return role.replace(/([A-Z])/g, ' $1').trim()
}
