import { useEffect, useState } from 'react'
import { useAuth } from '../context/AuthContext'
import { api } from '../lib/api'
import './Hero.css'

function Hero({ onRequireLogin }) {
  const { isAuthenticated } = useAuth()
  const [location, setLocation] = useState('')
  const [date, setDate] = useState('')
  const [status, setStatus] = useState('idle') // idle | loading | done | error
  const [error, setError] = useState(null)
  const [results, setResults] = useState([])
  const [reservingId, setReservingId] = useState(null)
  const [reservation, setReservation] = useState({ licensePlate: '', hours: 2 })
  const [reservationMessage, setReservationMessage] = useState(null)

  const runSearch = async () => {
    setStatus('loading')
    setError(null)
    try {
      const res = await api.getParkingLots({ sortBy: 'available', order: 'desc', pageSize: 20 })
      const all = res.data || []
      const filtered = location.trim()
        ? all.filter((lot) =>
            `${lot.name} ${lot.location}`.toLowerCase().includes(location.trim().toLowerCase())
          )
        : all
      setResults(filtered)
      setStatus('done')
    } catch (err) {
      setError(err.message)
      setStatus('error')
    }
  }

  const handleSubmit = (e) => {
    e.preventDefault()
    runSearch()
  }

  // Laat de CTA-knop verderop op de pagina dezelfde zoekactie triggeren
  useEffect(() => {
    const handler = () => runSearch()
    window.addEventListener('mobypark:load-locations', handler)
    return () => window.removeEventListener('mobypark:load-locations', handler)
  }, [location])

  const startReservation = (lotId) => {
    if (!isAuthenticated) {
      onRequireLogin()
      return
    }
    setReservationMessage(null)
    setReservingId(lotId)
  }

  const submitReservation = async (e, lot) => {
    e.preventDefault()
    setReservationMessage(null)
    try {
      const now = new Date()
      const end = new Date(now.getTime() + reservation.hours * 60 * 60 * 1000)
      const fmt = (d) =>
        `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(
          d.getDate()
        ).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(
          d.getMinutes()
        ).padStart(2, '0')}:00`

      await api.createReservation({
        licensePlate: reservation.licensePlate.toUpperCase(),
        startDate: fmt(now),
        endDate: fmt(end),
        parkingLotId: lot.id,
      })
      setReservationMessage({ type: 'success', text: `Plek gereserveerd bij ${lot.name}!` })
      setReservingId(null)
      setReservation({ licensePlate: '', hours: 2 })
    } catch (err) {
      setReservationMessage({ type: 'error', text: err.message })
    }
  }

  return (
    <section id="top" className="hero">
      <div className="section-inner hero__inner">
        <div className="hero__copy">
          <span className="hero__eyebrow">Parkeren, geregeld</span>
          <h1 className="hero__title">
            Zoek een plek.
            <br />
            Rijd naar binnen.
            <br />
            <span className="hero__title-accent">Geen gedoe.</span>
          </h1>
          <p className="hero__lead">
            MobyPark reserveert je parkeerplek voordat je vertrekt. Slagboom
            open, direct betaald, geen rondjes rijden op zoek naar een plekje.
          </p>

          <form className="hero__search" onSubmit={handleSubmit}>
            <div className="hero__search-field">
              <label htmlFor="location">Locatie</label>
              <input
                id="location"
                type="text"
                placeholder="Bijv. Rotterdam Centrum"
                value={location}
                onChange={(e) => setLocation(e.target.value)}
              />
            </div>
            <div className="hero__search-divider" aria-hidden="true" />
            <div className="hero__search-field">
              <label htmlFor="date">Datum</label>
              <input id="date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <button type="submit" className="hero__search-btn" disabled={status === 'loading'}>
              {status === 'loading' ? 'Zoeken…' : 'Zoek plek'}
            </button>
          </form>

          {status === 'error' && <p className="hero__search-error">{error}</p>}

          {status === 'done' && (
            <div className="hero__results" id="locaties">
              {results.length === 0 ? (
                <p className="hero__results-empty">
                  Geen parkeerplaatsen gevonden voor "{location}".
                </p>
              ) : (
                results.map((lot) => (
                  <div className="hero__result" key={lot.id}>
                    <div className="hero__result-info">
                      <strong>{lot.name}</strong>
                      <span>{lot.location}</span>
                      <span className="hero__result-availability">
                        {lot.capacity - lot.reserved} / {lot.capacity} vrij
                      </span>
                    </div>
                    <button
                      className="hero__result-btn"
                      onClick={() => startReservation(lot.id)}
                    >
                      Reserveer
                    </button>

                    {reservingId === lot.id && (
                      <form
                        className="hero__reserve-form"
                        onSubmit={(e) => submitReservation(e, lot)}
                      >
                        <input
                          required
                          placeholder="Kenteken (bv. AB-123-C)"
                          value={reservation.licensePlate}
                          onChange={(e) =>
                            setReservation({ ...reservation, licensePlate: e.target.value })
                          }
                        />
                        <select
                          value={reservation.hours}
                          onChange={(e) =>
                            setReservation({ ...reservation, hours: Number(e.target.value) })
                          }
                        >
                          <option value={1}>1 uur</option>
                          <option value={2}>2 uur</option>
                          <option value={4}>4 uur</option>
                          <option value={8}>8 uur</option>
                        </select>
                        <button type="submit">Bevestigen</button>
                      </form>
                    )}
                  </div>
                ))
              )}
            </div>
          )}

          {reservationMessage && (
            <p
              className={
                reservationMessage.type === 'success'
                  ? 'hero__reservation-success'
                  : 'hero__search-error'
              }
            >
              {reservationMessage.text}
            </p>
          )}

          <div className="hero__stats">
            <div>
              <strong>1.240+</strong>
              <span>plekken beschikbaar</span>
            </div>
            <div>
              <strong>38</strong>
              <span>locaties in NL</span>
            </div>
            <div>
              <strong>~8 sec</strong>
              <span>gem. reserveertijd</span>
            </div>
          </div>
        </div>

        <div className="hero__visual" aria-hidden="true">
          <div className="ticket">
            <div className="ticket__top">
              <span className="ticket__label">Parkeerticket</span>
              <span className="ticket__logo">P</span>
            </div>
            <div className="ticket__row">
              <span>Locatie</span>
              <strong>Rotterdam Centrum</strong>
            </div>
            <div className="ticket__row">
              <span>Plek</span>
              <strong>B-14</strong>
            </div>
            <div className="ticket__row">
              <span>Geldig tot</span>
              <strong>18:00</strong>
            </div>
            <div className="ticket__perf" />
            <div className="ticket__barcode">
              {Array.from({ length: 28 }).map((_, i) => (
                <span key={i} style={{ height: `${20 + ((i * 37) % 26)}px` }} />
              ))}
            </div>
          </div>
          <div className="hero__ring hero__ring--1" />
          <div className="hero__ring hero__ring--2" />
        </div>
      </div>
    </section>
  )
}

export default Hero
