import { useState } from 'react'
import { supabase } from './supabase.js'

export default function Login({ error: outerError }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setError('')
    const { error } = await supabase.auth.signInWithPassword({ email, password })
    if (error) setError('Email ose fjalëkalim i gabuar.')
    setBusy(false)
  }

  return (
    <div className="center-page">
      <form className="login-card" onSubmit={submit}>
        <h1>KosovaPOS</h1>
        <p className="subtitle">Pika e shitjes në web</p>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
        </label>
        <label>
          Fjalëkalimi
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {(error || outerError) && <div className="error">{error || outerError}</div>}
        <button type="submit" disabled={busy}>{busy ? 'Duke hyrë…' : 'Hyr'}</button>
      </form>
    </div>
  )
}
