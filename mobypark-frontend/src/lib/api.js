// Basis-URL van je .NET backend (MobyParkApi).
// Standaard http, want de dev-cert van https://localhost:7098 is self-signed
// en wordt door de browser geblokkeerd tenzij je 'm zelf vertrouwt.
// Overschrijf desgewenst via een .env-bestand: VITE_API_URL=http://localhost:5041/api
const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5041/api'

const TOKEN_KEY = 'mobypark_token'

async function request(path, options = {}) {
  const token = localStorage.getItem(TOKEN_KEY)

  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  }

  let res
  try {
    res = await fetch(`${API_BASE}${path}`, { ...options, headers })
  } catch (err) {
    throw new Error(
      'Kan de MobyPark API niet bereiken. Draait de backend op ' +
        `${API_BASE.replace('/api', '')}? Controleer ook of CORS aanstaat.`
    )
  }

  const contentType = res.headers.get('content-type') || ''
  const isJson = contentType.includes('application/json')
  const body = isJson ? await res.json().catch(() => null) : await res.text()

  if (!res.ok) {
    const message =
      (typeof body === 'string' && body) ||
      body?.error ||
      body?.message ||
      body?.title ||
      `Er ging iets mis (${res.status})`
    throw new Error(message)
  }

  return body
}

export const api = {
  // --- Auth ---
  login: (username, password) =>
    request('/Users/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),

  register: ({ name, username, password, email, phoneNumber, birthYear }) =>
    request('/Users/register', {
      method: 'POST',
      body: JSON.stringify({ name, username, password, email, phoneNumber, birthYear }),
    }),

  // --- Parkeerplaatsen ---
  getParkingLots: ({ sortBy = 'name', order = 'asc', page = 1, pageSize = 20 } = {}) =>
    request(`/parking-lots?${new URLSearchParams({ sortBy, order, page, pageSize })}`),

  // --- Reserveringen ---
  createReservation: ({ licensePlate, startDate, endDate, parkingLotId, discountCode }) =>
    request('/Reservation', {
      method: 'POST',
      body: JSON.stringify({
        licensePlate,
        startDate,
        endDate,
        parkingLotId,
        discountCode: discountCode || null,
      }),
    }),
}

export function saveToken(token) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}
