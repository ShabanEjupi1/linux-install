import { useEffect, useState } from 'react'
import { supabase, eur } from './supabase.js'
import ReceiptView from './ReceiptView.jsx'

const todayISO = () => new Date().toISOString().slice(0, 10)

export default function Receipts({ tenant }) {
  const [date, setDate] = useState(todayISO())
  const [receipts, setReceipts] = useState([])
  const [open, setOpen] = useState(null) // {receipt, items}

  useEffect(() => {
    ;(async () => {
      const from = `${date}T00:00:00`
      const to = `${date}T23:59:59`
      const { data } = await supabase
        .from('receipts').select('*')
        .eq('status', 'completed')
        .gte('date', from).lte('date', to)
        .order('date', { ascending: false })
      setReceipts(data ?? [])
    })()
  }, [date])

  const show = async (r) => {
    const { data: items } = await supabase.from('receipt_items').select('*').eq('receipt_id', r.id)
    setOpen({ receipt: r, items: items ?? [] })
  }

  const dayTotal = receipts.reduce((s, r) => s + Number(r.total_amount), 0)

  return (
    <div className="receipts no-print">
      <div className="receipts-tools">
        <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        <div className="day-total">Totali i ditës: <strong>{eur(dayTotal)}</strong> ({receipts.length} fatura)</div>
      </div>
      <table className="list">
        <thead>
          <tr><th>Nr. i faturës</th><th>Ora</th><th>Blerësi</th><th>Arkëtari</th><th>Pagesa</th><th>Totali</th></tr>
        </thead>
        <tbody>
          {receipts.map((r) => (
            <tr key={r.id} onClick={() => show(r)}>
              <td>{r.receipt_number}</td>
              <td>{new Date(r.date).toLocaleTimeString('sq-AL', { hour: '2-digit', minute: '2-digit' })}</td>
              <td>{r.buyer_business_name || r.buyer_name}</td>
              <td>{r.cashier_name}</td>
              <td>{r.payment_method}</td>
              <td>{eur(r.total_amount)}</td>
            </tr>
          ))}
          {!receipts.length && <tr><td colSpan="6" className="empty">Asnjë faturë për këtë datë.</td></tr>}
        </tbody>
      </table>
      {open && <ReceiptView tenant={tenant} receipt={open.receipt} items={open.items} onClose={() => setOpen(null)} />}
    </div>
  )
}
