import { useEffect, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api } from '@/lib/api'
import { useAuth } from '@/auth/AuthProvider'
import { money, statusLabel } from '@/lib/format'

export function FirstPaymentPage() {
  const { groupId, selectGroup } = useAuth()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const selectedGroupId = params.get('groupId') || groupId
  const [firstPayment, setFirstPayment] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!selectedGroupId) return
    selectGroup(selectedGroupId)
    api.get(`/api/groups/${selectedGroupId}/payments/first`)
      .then(setFirstPayment)
      .catch((err) => setError(err.message))
  }, [selectedGroupId])

  async function startCardPayment() {
    setError('')
    setBusy(true)
    try {
      const payment = await api.post(`/api/groups/${selectedGroupId}/payments/card`, {
        kind: firstPayment.suggestedKind,
        amount: firstPayment.suggestedAmount,
      })
      navigate(`/pay/${payment.paymentId}`)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  if (!selectedGroupId) return <p>Select a Stokvel first.</p>

  return (
    <section className="pay-page">
      <div className="page-head">
        <div>
          <h1>First payment</h1>
          <p>Pay with a card. Your bank app must approve 3-D Secure before the Stokvel ledger is updated.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      {firstPayment && (
        <div className="card pay-summary">
          <p className="eyebrow">FIRST CONTRIBUTION</p>
          <h2>{firstPayment.groupName}</h2>
          <p className="muted">
            {firstPayment.initialDepositOutstanding
              ? 'This is your initial deposit. It is recorded only after the bank authenticates the card.'
              : 'This is your next monthly instalment.'}
          </p>
          <dl>
            <div><dt>Payment type</dt><dd>{statusLabel(firstPayment.suggestedKind)}</dd></div>
            <div><dt>Amount due</dt><dd>{money(firstPayment.suggestedAmount)}</dd></div>
          </dl>
          <button className="btn" disabled={busy || firstPayment.suggestedAmount <= 0} onClick={startCardPayment}>
            {busy ? 'Opening secure checkout…' : 'Pay with card'}
          </button>
        </div>
      )}
    </section>
  )
}

export function CardCheckoutPage() {
  const { paymentId } = useParams()
  const navigate = useNavigate()
  const [payment, setPayment] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [card, setCard] = useState({ holderName: '', number: '', expiry: '', cvc: '' })

  useEffect(() => {
    if (!paymentId) return
    api.get(`/api/payments/${paymentId}`).then(setPayment).catch((err) => setError(err.message))
  }, [paymentId])

  async function submit(event) {
    event.preventDefault()
    setError('')
    if (!luhnValid(card.number)) {
      setError('Enter a valid card number. It is checked here only and is never sent to pkvela.')
      return
    }
    if (!/^\d{2}\/\d{2}$/.test(card.expiry.trim())) {
      setError('Enter expiry as MM/YY.')
      return
    }
    if (card.cvc.replace(/\D/g, '').length < 3) {
      setError('Enter the CVC from the back of the card.')
      return
    }
    setBusy(true)
    try {
      await api.post(`/api/payments/${paymentId}/challenge`)
      setCard({ holderName: '', number: '', expiry: '', cvc: '' })
      navigate(`/pay/${paymentId}/bank`)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  if (!payment && !error) return <p>Loading secure checkout…</p>

  return (
    <section className="pay-page">
      <div className="page-head">
        <div>
          <h1>Card checkout</h1>
          <p>Card details stay in this browser. pkvela never stores the number, expiry or CVC.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      {payment && (
        <form className="card form pay-card-form" onSubmit={submit} autoComplete="off">
          <p className="muted">{payment.groupName} · {money(payment.amount)}</p>
          <label>Name on card<input value={card.holderName} onChange={(event) => setCard({ ...card, holderName: event.target.value })} required /></label>
          <label>Card number<input inputMode="numeric" value={card.number} onChange={(event) => setCard({ ...card, number: event.target.value })} placeholder="ACCT-000015" required /></label>
          <div className="pay-card-row">
            <label>Expiry (MM/YY)<input value={card.expiry} onChange={(event) => setCard({ ...card, expiry: event.target.value })} placeholder="12/28" required /></label>
            <label>CVC<input inputMode="numeric" value={card.cvc} onChange={(event) => setCard({ ...card, cvc: event.target.value })} required /></label>
          </div>
          <button className="btn" disabled={busy}>{busy ? 'Contacting your bank…' : 'Continue to bank approval'}</button>
        </form>
      )}
    </section>
  )
}

export function BankChallengePage() {
  const { paymentId } = useParams()
  const navigate = useNavigate()
  const [payment, setPayment] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!paymentId) return
    api.get(`/api/payments/${paymentId}`).then(setPayment).catch((err) => setError(err.message))
  }, [paymentId])

  async function decide(approved) {
    setError('')
    setBusy(true)
    try {
      const result = await api.post(`/api/payments/${paymentId}/complete`, { approved })
      if (result.status === 'Succeeded') navigate('/contributions')
      else setError('The bank did not authenticate this payment. No money was posted to the Stokvel.')
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="pay-page">
      <div className="page-head">
        <div>
          <h1>Bank authentication</h1>
          <p>3-D Secure: your issuing bank must approve this payment in the banking app.</p>
        </div>
      </div>
      {error && <div className="alert">{error}</div>}
      <div className="bank-phone" aria-live="polite">
        <div className="bank-phone-bezel">
          <p className="bank-app-kicker">{payment?.issuerName || 'Issuing bank'}</p>
          <h2>Approve payment?</h2>
          <p>pkvela Cooperative is requesting 3-D Secure authentication.</p>
          {payment && <p className="bank-amount">{money(payment.amount)}</p>}
          <p className="muted">Merchant: {payment?.groupName || 'Stokvel'}</p>
          <div className="bank-actions">
            <button className="btn" disabled={busy} onClick={() => decide(true)}>{busy ? 'Waiting for bank…' : 'Approve in bank app'}</button>
            <button className="btn-secondary" disabled={busy} onClick={() => decide(false)}>Decline</button>
          </div>
        </div>
      </div>
    </section>
  )
}

function luhnValid(value) {
  const digits = (value || '').replace(/\D/g, '')
  if (digits.length < 13 || digits.length > 19) return false
  let sum = 0
  let alternate = false
  for (let index = digits.length - 1; index >= 0; index -= 1) {
    let digit = Number(digits[index])
    if (alternate) {
      digit *= 2
      if (digit > 9) digit -= 9
    }
    sum += digit
    alternate = !alternate
  }
  return sum % 10 === 0
}
