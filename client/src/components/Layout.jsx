import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth'
import { isOfficer } from '../format'
import { Icon } from '../icons'

export default function Layout() {
  const { user, groupId, selectGroup, logout, membership } = useAuth()
  const navigate = useNavigate()
  const admin = user?.isPlatformAdmin
  const officer = admin || isOfficer(membership?.role)

  const platformLinks = [
    ['/', 'Dashboard', 'dashboard'],
    ['/stokvels', 'Stokvels', 'users'],
    ['/loans', 'Loans', 'loans'],
    ['/reports', 'Reports', 'reports'],
    ['/notifications', 'Notifications', 'bell'],
    ['/audit', 'Audit logs', 'clipboard'],
    ['/settings', 'Settings', 'settings'],
  ]

  const groupLinks = [
    ['/', 'Dashboard', 'dashboard'],
    ['/stokvels', 'My Stokvels', 'users'],
    ['/members', 'Members', 'user'],
    ['/contributions', 'Contributions', 'deposit'],
    ['/instalments', 'Monthly instalments', 'calendar'],
    ['/savings', 'Group savings', 'piggy'],
    ['/group-loans', 'Group loans', 'landmark'],
    ['/member-loans', 'Member loans', 'wallet'],
    ['/transactions', 'Transactions', 'loans'],
    ['/reports', 'Reports', 'reports'],
    ['/notifications', 'Notifications', 'bell'],
  ]

  const memberLinks = [
    ['/', 'Dashboard', 'dashboard'],
    ['/stokvels', 'My Stokvels', 'users'],
    ['/contributions', 'My contributions', 'deposit'],
    ['/instalments', 'My instalments', 'calendar'],
    ['/member-loans', 'My loans', 'wallet'],
    ['/transactions', 'My transactions', 'loans'],
    ['/notifications', 'Notifications', 'bell'],
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
          {links.map(([to, label, icon]) => (
            <NavLink key={to} to={to} end={to === '/'}>
              <Icon name={icon} size={18} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-foot">
          <div className="user-card">
            <div className="user-meta">
              <strong>{user?.fullName}</strong>
              <small>{admin ? 'Platform Administrator' : statusRole(membership?.role)}</small>
            </div>
            <button type="button" className="btn-signout" onClick={() => { logout(); navigate('/login') }}>
              <span>Sign out</span>
              <Icon name="logout" size={16} />
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
