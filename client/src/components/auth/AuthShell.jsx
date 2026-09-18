import { BrandLogo } from '@/components/brand/BrandLogo'
import { Icon } from '@/components/ui/Icon'

export function AuthShell({ eyebrow, title, subtitle, children }) {
  return (
    <div className="auth-shell">
      <aside className="auth-stage" aria-hidden="true">
        <div className="auth-stage-glow" />
        <div className="auth-stage-grid" />
        <div className="auth-stage-inner">
          <p className="auth-stage-kicker">PRIVATE WEALTH · LEDGER TECHNOLOGY</p>
          <h2>Collective capital with private-bank discipline.</h2>
          <p className="auth-stage-lead">
            A secure operating system for Stokvel savings, instalments and credit — built on an immutable ledger, not a spreadsheet.
          </p>
          <AuthSparkline />
          <div className="auth-metrics">
            <AuthMetric label="Session security" value="Encrypted" />
            <AuthMetric label="Ledger" value="Immutable" />
            <AuthMetric label="Ownership" value="Member-held" />
          </div>
        </div>
        <div className="auth-stage-foot">
          <Icon name="shield" size={16} />
          <span>Bank-grade access · Calculated balances · Role-based control</span>
        </div>
      </aside>
      <main className="auth-panel">
        <div className="auth-panel-inner">
          <BrandLogo variant="lockup" />
          {eyebrow && <div className="eyebrow">{eyebrow}</div>}
          <h1>{title}</h1>
          {subtitle && <p className="auth-lead">{subtitle}</p>}
          {children}
        </div>
      </main>
    </div>
  )
}

function AuthMetric({ label, value }) {
  return (
    <div className="auth-metric">
      <small>{label}</small>
      <strong>{value}</strong>
    </div>
  )
}

function AuthSparkline() {
  return (
    <svg className="auth-sparkline" viewBox="0 0 420 120" preserveAspectRatio="none">
      <defs>
        <linearGradient id="auth-area" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#c4a35a" stopOpacity="0.35" />
          <stop offset="100%" stopColor="#c4a35a" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d="M0 88 L35 80 L70 84 L105 62 L140 68 L175 42 L210 48 L245 28 L280 36 L315 18 L350 24 L385 10 L420 14 V120 H0 Z" fill="url(#auth-area)" />
      <path d="M0 88 L35 80 L70 84 L105 62 L140 68 L175 42 L210 48 L245 28 L280 36 L315 18 L350 24 L385 10 L420 14" fill="none" stroke="#e6d3a3" strokeWidth="2.2" />
    </svg>
  )
}
