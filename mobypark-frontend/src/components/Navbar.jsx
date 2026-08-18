import { useState } from 'react'
import { useAuth } from '../context/AuthContext'
import './Navbar.css'

const links = [
  { label: 'Home', href: '#top' },
  { label: 'Locaties', href: '#locaties' },
  { label: 'Hoe het werkt', href: '#werkwijze' },
  { label: 'Tarieven', href: '#tarieven' },
]

function Navbar({ onLoginClick }) {
  const [open, setOpen] = useState(false)
  const { isAuthenticated, username, logout } = useAuth()

  return (
    <header className="navbar">
      <div className="section-inner navbar__inner">
        <a href="#top" className="navbar__logo">
          <span className="navbar__logo-mark">P</span>
          MobyPark
        </a>

        <nav className={`navbar__links ${open ? 'is-open' : ''}`}>
          {links.map((link) => (
            <a key={link.href} href={link.href} onClick={() => setOpen(false)}>
              {link.label}
            </a>
          ))}
          {isAuthenticated ? (
            <button
              className="navbar__cta navbar__cta--mobile"
              onClick={() => {
                logout()
                setOpen(false)
              }}
            >
              Uitloggen ({username})
            </button>
          ) : (
            <button
              className="navbar__cta navbar__cta--mobile"
              onClick={() => {
                onLoginClick()
                setOpen(false)
              }}
            >
              Inloggen
            </button>
          )}
        </nav>

        <div className="navbar__actions">
          {isAuthenticated ? (
            <button className="navbar__cta navbar__cta--ghost" onClick={logout}>
              Uitloggen ({username})
            </button>
          ) : (
            <button className="navbar__cta" onClick={onLoginClick}>
              Inloggen
            </button>
          )}
          <button
            className={`navbar__toggle ${open ? 'is-open' : ''}`}
            aria-label="Menu openen"
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
          >
            <span />
            <span />
            <span />
          </button>
        </div>
      </div>
    </header>
  )
}

export default Navbar
