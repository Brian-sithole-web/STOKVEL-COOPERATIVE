export function money(value) {
  const n = Number(value || 0)
  return `R${n.toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function statusLabel(value) {
  if (!value) return '—'
  return String(value).replace(/([A-Z])/g, ' $1').trim()
}

export function prettyDate(value) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString('en-ZA', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function officerRoles() {
  return ['StokvelAdministrator', 'Treasurer', 'Chairperson', 'Secretary']
}

export function isOfficer(role) {
  return officerRoles().includes(role)
}

export function isInactiveGroup(status) {
  return Boolean(status) && status !== 'Active'
}
