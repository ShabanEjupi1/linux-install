import { useEffect, useMemo, useRef, useState } from 'react'
import { supabase, eur } from './supabase.js'
import ReceiptView from './ReceiptView.jsx'

const PAYMENTS = ['Para në dorë', 'Kartelë', 'Bankë']
const ORDER_TYPES = [
  { id: 'dine_in', label: 'Në lokal' },
  { id: 'take_out', label: 'Merr me vete' },
  { id: 'delivery', label: 'Dërgesë' },
]

// VAT-inclusive prices (Kosovo retail): vat share = total * r / (100 + r)
const vatShare = (total, rate) => Math.round((total * rate) / (100 + rate) * 100) / 100

export default function Sale({ profile, tenant }) {
  const isRestaurant = tenant.mode === 'restaurant'
  const [articles, setArticles] = useState([])
  const [categories, setCategories] = useState([])
  const [tables, setTables] = useState([])
  const [catFilter, setCatFilter] = useState(null)
  const [search, setSearch] = useState('')
  const [cart, setCart] = useState([]) // {article, qty, price, sizeLabel}
  const [payment, setPayment] = useState(PAYMENTS[0])
  const [paid, setPaid] = useState('')
  const [orderType, setOrderType] = useState('dine_in')
  const [tableId, setTableId] = useState('')
  const [buyerName, setBuyerName] = useState('')
  const [b2b, setB2b] = useState(false)
  const [buyerBiz, setBuyerBiz] = useState({ name: '', nui: '', fiscal: '' })
  const [pizzaPick, setPizzaPick] = useState(null)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(null) // {receipt, items}
  const [error, setError] = useState('')
  const searchRef = useRef(null)

  const load = async () => {
    const [a, c, t] = await Promise.all([
      supabase.from('articles').select('*').eq('is_active', true).order('name'),
      supabase.from('categories').select('*').eq('is_active', true).order('display_order'),
      isRestaurant
        ? supabase.from('restaurant_tables').select('*').eq('is_active', true).order('table_number')
        : Promise.resolve({ data: [] }),
    ])
    setArticles(a.data ?? [])
    setCategories(c.data ?? [])
    setTables(t.data ?? [])
  }
  useEffect(() => { load() }, [])

  const shown = useMemo(() => {
    const q = search.trim().toLowerCase()
    return articles.filter((a) =>
      (!catFilter || a.category_id === catFilter) &&
      (!q || a.name.toLowerCase().includes(q) || a.barcode === search.trim()))
  }, [articles, catFilter, search])

  const addToCart = (article, price = null, sizeLabel = null) => {
    if (article.is_pizza && price === null &&
        (article.pizza_price_small || article.pizza_price_medium || article.pizza_price_large)) {
      setPizzaPick(article)
      return
    }
    const unitPrice = price ?? (Number(article.sales_price) || Number(article.base_price) || 0)
    setCart((c) => {
      const i = c.findIndex((x) => x.article.id === article.id && x.sizeLabel === sizeLabel)
      if (i >= 0) return c.map((x, j) => (j === i ? { ...x, qty: x.qty + 1 } : x))
      return [...c, { article, qty: 1, price: unitPrice, sizeLabel }]
    })
    setPizzaPick(null)
  }

  // barcode scanners send the code + Enter — add exact match straight to cart
  const onSearchKey = (e) => {
    if (e.key !== 'Enter') return
    e.preventDefault()
    const code = search.trim()
    const hit = articles.find((a) => a.barcode === code) ?? (shown.length === 1 ? shown[0] : null)
    if (hit) { addToCart(hit); setSearch('') }
  }

  const setQty = (i, qty) =>
    setCart((c) => (qty <= 0 ? c.filter((_, j) => j !== i) : c.map((x, j) => (j === i ? { ...x, qty } : x))))

  const total = cart.reduce((s, x) => s + x.qty * x.price, 0)

  const complete = async () => {
    if (!cart.length || busy) return
    setBusy(true)
    setError('')
    try {
      const { data: receipt, error: e1 } = await supabase.from('receipts').insert({
        tenant_id: tenant.id,
        buyer_name: buyerName.trim() || 'Qytetar',
        buyer_business_name: b2b ? buyerBiz.name || null : null,
        buyer_nui: b2b ? buyerBiz.nui || null : null,
        buyer_fiscal_number: b2b ? buyerBiz.fiscal || null : null,
        cashier_id: profile.id,
        cashier_number: profile.cashier_number,
        cashier_name: profile.full_name,
        payment_method: payment,
        order_type: isRestaurant ? orderType : 'take_out',
        table_id: isRestaurant && tableId ? tableId : null,
      }).select().single()
      if (e1) throw e1

      const rows = cart.map((x) => {
        const line = Math.round(x.qty * x.price * 100) / 100
        return {
          tenant_id: tenant.id,
          receipt_id: receipt.id,
          article_id: x.article.id,
          plu: x.article.plu ?? 0,
          barcode: x.article.barcode ?? '',
          article_name: x.sizeLabel ? `${x.article.name} (${x.sizeLabel})` : x.article.name,
          quantity: x.qty,
          price: x.price,
          vat_rate: x.article.vat_rate,
          vat_value: vatShare(line, Number(x.article.vat_rate)),
          total_value: line,
        }
      })
      const { data: items, error: e2 } = await supabase.from('receipt_items').insert(rows).select()
      if (e2) throw e2

      const { data: finished, error: e3 } = await supabase.rpc('complete_receipt', {
        p_receipt: receipt.id,
        p_paid: paid !== '' ? Number(paid) : null,
      })
      if (e3) throw e3

      setDone({ receipt: finished, items })
      setCart([]); setPaid(''); setBuyerName(''); setB2b(false)
      setBuyerBiz({ name: '', nui: '', fiscal: '' }); setTableId('')
      load() // refresh stock quantities
    } catch (err) {
      setError(err.message ?? 'Gabim gjatë përfundimit të faturës.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="sale">
      <section className="catalog no-print">
        <div className="catalog-tools">
          <input
            ref={searchRef}
            placeholder="Kërko artikull ose skano barkodin…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={onSearchKey}
            autoFocus
          />
          <div className="cat-chips">
            <button className={!catFilter ? 'active' : ''} onClick={() => setCatFilter(null)}>Të gjitha</button>
            {categories.map((c) => (
              <button key={c.id} className={catFilter === c.id ? 'active' : ''} onClick={() => setCatFilter(c.id)}>
                {c.name}
              </button>
            ))}
          </div>
        </div>
        <div className="grid">
          {shown.map((a) => (
            <button key={a.id} className="tile" onClick={() => addToCart(a)}>
              <span className="tile-name">{a.name}</span>
              <span className="tile-price">
                {a.is_pizza && a.pizza_price_small ? `nga ${eur(a.pizza_price_small)}` : eur(a.sales_price || a.base_price)}
              </span>
              {!a.is_pizza && Number(a.stock_quantity) <= Number(a.minimum_stock) && (
                <span className="tile-low">stoku: {Number(a.stock_quantity)}</span>
              )}
            </button>
          ))}
          {!shown.length && <div className="empty">S'ka artikuj — shtoni te «Artikujt».</div>}
        </div>
      </section>

      <aside className="cart no-print">
        <h2>Fatura</h2>
        <div className="cart-lines">
          {cart.map((x, i) => (
            <div key={`${x.article.id}-${x.sizeLabel ?? ''}`} className="cart-line">
              <span className="cl-name">{x.article.name}{x.sizeLabel ? ` (${x.sizeLabel})` : ''}</span>
              <span className="cl-qty">
                <button onClick={() => setQty(i, x.qty - 1)}>−</button>
                {x.qty}
                <button onClick={() => setQty(i, x.qty + 1)}>+</button>
              </span>
              <span className="cl-total">{eur(x.qty * x.price)}</span>
            </div>
          ))}
          {!cart.length && <div className="empty">Fatura është e zbrazët.</div>}
        </div>

        {isRestaurant && (
          <>
            <div className="row-buttons">
              {ORDER_TYPES.map((o) => (
                <button key={o.id} className={orderType === o.id ? 'active' : ''} onClick={() => setOrderType(o.id)}>
                  {o.label}
                </button>
              ))}
            </div>
            {!!tables.length && orderType === 'dine_in' && (
              <select value={tableId} onChange={(e) => setTableId(e.target.value)}>
                <option value="">— Tavolina —</option>
                {tables.map((t) => <option key={t.id} value={t.id}>Tavolina {t.table_number}</option>)}
              </select>
            )}
          </>
        )}

        <input placeholder="Blerësi (opsionale)" value={buyerName} onChange={(e) => setBuyerName(e.target.value)} />
        {!isRestaurant && (
          <label className="b2b-toggle">
            <input type="checkbox" checked={b2b} onChange={(e) => setB2b(e.target.checked)} /> Faturë për biznes
          </label>
        )}
        {b2b && (
          <div className="b2b-fields">
            <input placeholder="Emri i biznesit" value={buyerBiz.name} onChange={(e) => setBuyerBiz({ ...buyerBiz, name: e.target.value })} />
            <input placeholder="NUI" value={buyerBiz.nui} onChange={(e) => setBuyerBiz({ ...buyerBiz, nui: e.target.value })} />
            <input placeholder="Nr. fiskal" value={buyerBiz.fiscal} onChange={(e) => setBuyerBiz({ ...buyerBiz, fiscal: e.target.value })} />
          </div>
        )}

        <div className="row-buttons">
          {PAYMENTS.map((p) => (
            <button key={p} className={payment === p ? 'active' : ''} onClick={() => setPayment(p)}>{p}</button>
          ))}
        </div>
        <input
          type="number" step="0.01" min="0"
          placeholder={`Paguar (${eur(total)})`}
          value={paid}
          onChange={(e) => setPaid(e.target.value)}
        />
        {paid !== '' && Number(paid) >= total && total > 0 && (
          <div className="change">Kusuri: <strong>{eur(Number(paid) - total)}</strong></div>
        )}

        <div className="cart-total">
          <span>TOTALI</span>
          <strong>{eur(total)}</strong>
        </div>
        {error && <div className="error">{error}</div>}
        <button className="btn-complete" disabled={!cart.length || busy} onClick={complete}>
          {busy ? 'Duke përfunduar…' : 'Përfundo faturën'}
        </button>
      </aside>

      {pizzaPick && (
        <div className="modal-backdrop" onClick={() => setPizzaPick(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h3>{pizzaPick.name} — zgjidh madhësinë</h3>
            <div className="row-buttons">
              {pizzaPick.pizza_price_small && (
                <button onClick={() => addToCart(pizzaPick, Number(pizzaPick.pizza_price_small), 'E vogël')}>
                  E vogël {eur(pizzaPick.pizza_price_small)}
                </button>
              )}
              {pizzaPick.pizza_price_medium && (
                <button onClick={() => addToCart(pizzaPick, Number(pizzaPick.pizza_price_medium), 'Mesme')}>
                  Mesme {eur(pizzaPick.pizza_price_medium)}
                </button>
              )}
              {pizzaPick.pizza_price_large && (
                <button onClick={() => addToCart(pizzaPick, Number(pizzaPick.pizza_price_large), 'E madhe')}>
                  E madhe {eur(pizzaPick.pizza_price_large)}
                </button>
              )}
            </div>
            <div className="modal-actions">
              <button onClick={() => setPizzaPick(null)}>Anulo</button>
            </div>
          </div>
        </div>
      )}

      {done && (
        <ReceiptView tenant={tenant} receipt={done.receipt} items={done.items} onClose={() => setDone(null)} />
      )}
    </div>
  )
}
