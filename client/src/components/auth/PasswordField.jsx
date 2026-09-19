import { useState } from 'react'
import { Icon } from '@/components/ui/Icon'

export function PasswordField({ label = 'Password', value, onChange, autoComplete = 'current-password', required = true }) {
  const [visible, setVisible] = useState(false)

  return (
    <label className="auth-field">
      {label}
      <div className="auth-input-wrap">
        <span className="auth-input-icon"><Icon name="lock" size={16} /></span>
        <input
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={onChange}
          autoComplete={autoComplete}
          required={required}
        />
        <button
          type="button"
          className="auth-reveal"
          onClick={() => setVisible((current) => !current)}
          aria-label={visible ? 'Hide password' : 'Show password'}
        >
          <Icon name={visible ? 'eyeOff' : 'eye'} size={16} />
        </button>
      </div>
    </label>
  )
}
