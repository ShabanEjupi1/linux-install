import { useEffect, useState } from 'react'
import { supabase } from './supabase.js'
import Login from './Login.jsx'
import Sale from './Sale.jsx'
import Receipts from './Receipts.jsx'
import Articles from './Articles.jsx'

const VIEWS = [
  { id: 'sale', label: 'Shitje' },
  { id: 'receipts', label: 'Faturat' },
  { id: 'articles', label: 'Artikujt' },
]

export default function App() {
  const [session, setSession] = useState(undefined) // undefined = loading
  const [profile, setProfile] = useState(null)
  const [tenant, setTenant] = useState(null)
  const [view, setView] = useState('sale')
  const [error, setError] = useState('')

  useEffect(() => {
    supabase.auth.getSession().then(({ data }) => setSession(data.session ?? null))
    const { data: sub } = supabase.auth.onAuthStateChange((_e, s) => setSession(s))
    return () => sub.subscription.unsubscribe()
  }, [])

  useEffect(() => {
    if (!session) { setProfile(null); setTenant(null); return }
    ;(async () => {
      const { data: prof, error: e1 } = await supabase
        .from('profiles').select('*').eq('id', session.user.id).single()
      if (e1 || !prof?.tenant_id) {
        setError('Ky përdorues nuk është i lidhur me asnjë biznes. Kontaktoni administratorin.')
        await supabase.auth.signOut()
        return
      }
      const { data: ten } = await supabase
        .from('tenants').select('*').eq('id', prof.tenant_id).single()
      setProfile(prof)
      setTenant(ten)
      setError('')
    })()
  }, [session])

  if (session === undefined) return <div className="center-page">Duke u ngarkuar…</div>
  if (!session) return <Login error={error} />
  if (!profile || !tenant) return <div className="center-page">Duke u ngarkuar profilin…</div>

  return (
    <div className="app">
      <header className="topbar no-print">
        <div className="brand">
          <strong>{tenant.name}</strong>
          <span className="mode-tag">{tenant.mode === 'restaurant' ? 'Restorant' : 'Dyqan'}</span>
        </div>
        <nav>
          {VIEWS.map((v) => (
            <button key={v.id} className={view === v.id ? 'active' : ''} onClick={() => setView(v.id)}>
              {v.label}
            </button>
          ))}
        </nav>
        <div className="user">
          <span>{profile.full_name || session.user.email}</span>
          <button onClick={() => supabase.auth.signOut()}>Dil</button>
        </div>
      </header>
      <main>
        {view === 'sale' && <Sale profile={profile} tenant={tenant} />}
        {view === 'receipts' && <Receipts tenant={tenant} />}
        {view === 'articles' && <Articles tenant={tenant} />}
      </main>
    </div>
  )
}
