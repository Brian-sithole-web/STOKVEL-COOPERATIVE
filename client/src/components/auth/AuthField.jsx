import { Icon } from '@/components/ui/Icon'

export function AuthField({ label, icon, children }) {
  return (
    <label className="auth-field">
      {label}
      <div className="auth-input-wrap">
        {icon && <span className="auth-input-icon"><Icon name={icon} size={16} /></span>}
        {children}
      </div>
    </label>
  )
}
