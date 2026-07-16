import { useEffect, useState } from 'react'
import { supabase, eur } from './supabase.js'

const VAT_OPTIONS = [
  { rate: 18, type: 3, label: '18%' },
  { rate: 8, type: 2, label: '8%' },
  { rate: 0, type: 1, label: '0%' },
]

const emptyForm = {
  name: '', barcode: '', unit: 'Copë', sales_price: '', purchase_price: '',
  vat_rate: 18, category_id: '', stock_quantity: '', plu: '',
  is_pizza: false, pizza_price_small: '', pizza_price_medium: '', pizza_price_large: '',
}

export default function Articles({ tenant }) {
  const isRestaurant = tenant.mode === 'restaurant'
  const [articles, setArticles] = useState([])
  const [categories, setCategories] = useState([])
  const [search, setSearch] = useState('')
  const [form, setForm] = useState(null) // null = closed; {...emptyForm, id?} = open
  const [newCat, setNewCat] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    const [a, c] = await Promise.all([
      supabase.from('articles').select('*').order('name'),
      supabase.from('categories').select('*').order('display_order'),
    ])
    setArticles(a.data ?? [])
    setCategories(c.data ?? [])
  }
  useEffect(() => { load() }, [])

  const shown = articles.filter((a) => {
    const q = search.trim().toLowerCase()
    return !q || a.name.toLowerCase().includes(q) || a.barcode.includes(search.trim())
  })

  const save = async (e) => {
    e.preventDefault()
    setError('')
    const vat = VAT_OPTIONS.find((v) => v.rate === Number(form.vat_rate)) ?? VAT_OPTIONS[0]
    const row = {
      tenant_id: tenant.id,
      name: form.name.trim(),
      barcode: form.barcode.trim(),
      unit: form.unit,
      sales_price: Number(form.sales_price) || 0,
      purchase_price: Number(form.purchase_price) || 0,
      vat_rate: vat.rate,
      vat_type: vat.type,
      category_id: form.category_id || null,
      stock_quantity: Number(form.stock_quantity) || 0,
      plu: form.plu !== '' ? Number(form.plu) : null,
      is_pizza: isRestaurant && form.is_pizza,
      pizza_price_small: form.is_pizza && form.pizza_price_small !== '' ? Number(form.pizza_price_small) : null,
      pizza_price_medium: form.is_pizza && form.pizza_price_medium !== '' ? Number(form.pizza_price_medium) : null,
      pizza_price_large: form.is_pizza && form.pizza_price_large !== '' ? Number(form.pizza_price_large) : null,
      is_customizable: isRestaurant && form.is_pizza,
    }
    const q = form.id
      ? supabase.from('articles').update(row).eq('id', form.id)
      : supabase.from('articles').insert(row)
    const { error } = await q
    if (error) { setError(error.message); return }
    setForm(null)
    load()
  }

  const remove = async (a) => {
    if (!confirm(`Të fshihet artikulli “${a.name}”?`)) return
    const { error } = await supabase.from('articles').delete().eq('id', a.id)
    if (error) setError('Nuk keni leje për fshirje të artikujve.')
    load()
  }

  const addCategory = async () => {
    const name = newCat.trim()
    if (!name) return
    const { error } = await supabase.from('categories')
      .insert({ tenant_id: tenant.id, name, display_order: categories.length })
    if (error) { setError(error.message); return }
    setNewCat('')
    load()
  }

  const edit = (a) =>
    setForm({
      id: a.id, name: a.name, barcode: a.barcode, unit: a.unit,
      sales_price: a.sales_price, purchase_price: a.purchase_price,
      vat_rate: Number(a.vat_rate), category_id: a.category_id ?? '',
      stock_quantity: a.stock_quantity, plu: a.plu ?? '',
      is_pizza: a.is_pizza,
      pizza_price_small: a.pizza_price_small ?? '',
      pizza_price_medium: a.pizza_price_medium ?? '',
      pizza_price_large: a.pizza_price_large ?? '',
    })

  return (
    <div className="articles no-print">
      <div className="articles-tools">
        <input placeholder="Kërko…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <button onClick={() => setForm({ ...emptyForm })}>+ Artikull i ri</button>
        <div className="cat-add">
          <input placeholder="Kategori e re…" value={newCat} onChange={(e) => setNewCat(e.target.value)} />
          <button onClick={addCategory}>Shto</button>
        </div>
      </div>
      {error && <div className="error">{error}</div>}

      <table className="list">
        <thead>
          <tr>
            <th>Emri</th><th>Barkodi</th><th>Kategoria</th>
            <th>Çmimi</th><th>TVSH</th><th>Stoku</th><th></th>
          </tr>
        </thead>
        <tbody>
          {shown.map((a) => (
            <tr key={a.id}>
              <td>{a.name}{a.is_pizza ? ' 🍕' : ''}</td>
              <td>{a.barcode}</td>
              <td>{categories.find((c) => c.id === a.category_id)?.name ?? a.category ?? ''}</td>
              <td>{a.is_pizza && a.pizza_price_small ? `nga ${eur(a.pizza_price_small)}` : eur(a.sales_price)}</td>
              <td>{Number(a.vat_rate)}%</td>
              <td>{Number(a.stock_quantity)}</td>
              <td className="row-actions">
                <button onClick={() => edit(a)}>Ndrysho</button>
                <button className="danger" onClick={() => remove(a)}>Fshij</button>
              </td>
            </tr>
          ))}
          {!shown.length && <tr><td colSpan="7" className="empty">Asnjë artikull.</td></tr>}
        </tbody>
      </table>

      {form && (
        <div className="modal-backdrop" onClick={() => setForm(null)}>
          <form className="modal article-form" onClick={(e) => e.stopPropagation()} onSubmit={save}>
            <h3>{form.id ? 'Ndrysho artikullin' : 'Artikull i ri'}</h3>
            <label>Emri<input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required autoFocus /></label>
            <label>Barkodi<input value={form.barcode} onChange={(e) => setForm({ ...form, barcode: e.target.value })} /></label>
            <div className="form-row">
              <label>Çmimi i shitjes<input type="number" step="0.01" min="0" value={form.sales_price} onChange={(e) => setForm({ ...form, sales_price: e.target.value })} /></label>
              <label>Çmimi i blerjes<input type="number" step="0.01" min="0" value={form.purchase_price} onChange={(e) => setForm({ ...form, purchase_price: e.target.value })} /></label>
            </div>
            <div className="form-row">
              <label>TVSH
                <select value={form.vat_rate} onChange={(e) => setForm({ ...form, vat_rate: e.target.value })}>
                  {VAT_OPTIONS.map((v) => <option key={v.rate} value={v.rate}>{v.label}</option>)}
                </select>
              </label>
              <label>Kategoria
                <select value={form.category_id} onChange={(e) => setForm({ ...form, category_id: e.target.value })}>
                  <option value="">—</option>
                  {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
            </div>
            <div className="form-row">
              <label>Stoku<input type="number" step="0.001" value={form.stock_quantity} onChange={(e) => setForm({ ...form, stock_quantity: e.target.value })} /></label>
              {!isRestaurant && <label>PLU<input type="number" value={form.plu} onChange={(e) => setForm({ ...form, plu: e.target.value })} /></label>}
              <label>Njësia
                <select value={form.unit} onChange={(e) => setForm({ ...form, unit: e.target.value })}>
                  {['Copë', 'Kg', 'Litër', 'Metër', 'Pako'].map((u) => <option key={u}>{u}</option>)}
                </select>
              </label>
            </div>
            {isRestaurant && (
              <>
                <label className="b2b-toggle">
                  <input type="checkbox" checked={form.is_pizza} onChange={(e) => setForm({ ...form, is_pizza: e.target.checked })} /> Picë (me madhësi)
                </label>
                {form.is_pizza && (
                  <div className="form-row">
                    <label>E vogël<input type="number" step="0.01" min="0" value={form.pizza_price_small} onChange={(e) => setForm({ ...form, pizza_price_small: e.target.value })} /></label>
                    <label>Mesme<input type="number" step="0.01" min="0" value={form.pizza_price_medium} onChange={(e) => setForm({ ...form, pizza_price_medium: e.target.value })} /></label>
                    <label>E madhe<input type="number" step="0.01" min="0" value={form.pizza_price_large} onChange={(e) => setForm({ ...form, pizza_price_large: e.target.value })} /></label>
                  </div>
                )}
              </>
            )}
            <div className="modal-actions">
              <button type="submit">Ruaj</button>
              <button type="button" onClick={() => setForm(null)}>Anulo</button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}
