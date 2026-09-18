import pkvelaLogo from '@/assets/brand/pkvela-logo.png'

export function BrandLogo({ variant = 'lockup' }) {
  return (
    <div className={`brand-mark brand-mark-${variant}`}>
      <img src={pkvelaLogo} alt="pkvela Cooperative" />
    </div>
  )
}
