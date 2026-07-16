import { eur } from './supabase.js'

// 80mm-style receipt, printable via browser (Ctrl+P → PDF or printer)
export default function ReceiptView({ tenant, receipt, items, onClose }) {
  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="receipt-print">
          <div className="r-head">
            <strong>{tenant.name}</strong>
            {tenant.address && <div>{tenant.address}{tenant.city ? `, ${tenant.city}` : ''}</div>}
            {tenant.fiscal_number && <div>Nr. Fiskal: {tenant.fiscal_number}</div>}
            {tenant.vat_number && <div>Nr. TVSH: {tenant.vat_number}</div>}
          </div>
          <div className="r-meta">
            <div>Fatura: <strong>{receipt.receipt_number ?? '—'}</strong></div>
            <div>Data: {new Date(receipt.date).toLocaleString('sq-AL')}</div>
            <div>Arkëtari: {receipt.cashier_name} ({receipt.cashier_number})</div>
            {receipt.buyer_name && receipt.buyer_name !== 'Qytetar' && <div>Blerësi: {receipt.buyer_name}</div>}
            {receipt.buyer_business_name && <div>Biznesi: {receipt.buyer_business_name}</div>}
            {receipt.buyer_nui && <div>NUI: {receipt.buyer_nui}</div>}
          </div>
          <table className="r-items">
            <thead>
              <tr><th>Artikulli</th><th>Sasia</th><th>Çmimi</th><th>Vlera</th></tr>
            </thead>
            <tbody>
              {items.map((it) => (
                <tr key={it.id}>
                  <td>{it.article_name}</td>
                  <td>{Number(it.quantity)}</td>
                  <td>{eur(it.price)}</td>
                  <td>{eur(it.total_value)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="r-totals">
            <div><span>TVSH</span><span>{eur(receipt.tax_amount)}</span></div>
            <div className="r-grand"><span>TOTALI</span><span>{eur(receipt.total_amount)}</span></div>
            <div><span>Paguar</span><span>{eur(receipt.paid_amount)}</span></div>
            {Number(receipt.paid_amount) > Number(receipt.total_amount) && (
              <div><span>Kusuri</span><span>{eur(receipt.paid_amount - receipt.total_amount)}</span></div>
            )}
            <div><span>Pagesa</span><span>{receipt.payment_method}</span></div>
          </div>
          <div className="r-foot">Faleminderit për blerjen!</div>
        </div>
        <div className="modal-actions no-print">
          <button onClick={() => window.print()}>Printo / PDF</button>
          <button onClick={onClose}>Mbyll</button>
        </div>
      </div>
    </div>
  )
}
