import { useState } from 'react'
import { useAuth } from '../context/AuthContext'
import './AuthModal.css'

const emptyRegister = {
  name: '',
  username: '',
  password: '',
  email: '',
  phoneNumber: '',
  birthYear: '',
}

function AuthModal({ mode, onClose, onSwitchMode }) {
  const { login, register } = useAuth()
  const [loginForm, setLoginForm] = useState({ username: '', password: '' })
  const [registerForm, setRegisterForm] = useState(emptyRegister)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)
  const [loading, setLoading] = useState(false)

  if (!mode) return null

  const handleLogin = async (e) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await login(loginForm.username, loginForm.password)
      onClose()
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const handleRegister = async (e) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    setLoading(true)
    try {
      await register({
        ...registerForm,
        birthYear: Number(registerForm.birthYear),
      })
      setSuccess('Account aangemaakt! Je kan nu inloggen.')
      setRegisterForm(emptyRegister)
      setTimeout(() => onSwitchMode('login'), 900)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-modal__backdrop" onClick={onClose}>
      <div
        className="auth-modal"
        role="dialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
      >
        <button className="auth-modal__close" onClick={onClose} aria-label="Sluiten">
          &times;
        </button>

        <div className="auth-modal__tabs">
          <button
            className={mode === 'login' ? 'is-active' : ''}
            onClick={() => {
              setError(null)
              onSwitchMode('login')
            }}
          >
            Inloggen
          </button>
          <button
            className={mode === 'register' ? 'is-active' : ''}
            onClick={() => {
              setError(null)
              onSwitchMode('register')
            }}
          >
            Account aanmaken
          </button>
        </div>

        {error && <p className="auth-modal__error">{error}</p>}
        {success && <p className="auth-modal__success">{success}</p>}

        {mode === 'login' ? (
          <form onSubmit={handleLogin} className="auth-modal__form">
            <label>
              Gebruikersnaam
              <input
                required
                value={loginForm.username}
                onChange={(e) => setLoginForm({ ...loginForm, username: e.target.value })}
              />
            </label>
            <label>
              Wachtwoord
              <input
                required
                type="password"
                value={loginForm.password}
                onChange={(e) => setLoginForm({ ...loginForm, password: e.target.value })}
              />
            </label>
            <button type="submit" className="auth-modal__submit" disabled={loading}>
              {loading ? 'Bezig…' : 'Inloggen'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleRegister} className="auth-modal__form">
            <label>
              Naam
              <input
                required
                value={registerForm.name}
                onChange={(e) => setRegisterForm({ ...registerForm, name: e.target.value })}
              />
            </label>
            <label>
              Gebruikersnaam
              <input
                required
                minLength={5}
                value={registerForm.username}
                onChange={(e) => setRegisterForm({ ...registerForm, username: e.target.value })}
              />
            </label>
            <label>
              Wachtwoord
              <input
                required
                type="password"
                minLength={8}
                placeholder="Min. 8 tekens, 1 hoofdletter, 1 cijfer"
                value={registerForm.password}
                onChange={(e) => setRegisterForm({ ...registerForm, password: e.target.value })}
              />
            </label>
            <label>
              E-mail
              <input
                required
                type="email"
                value={registerForm.email}
                onChange={(e) => setRegisterForm({ ...registerForm, email: e.target.value })}
              />
            </label>
            <label>
              Telefoonnummer
              <input
                required
                placeholder="06-12345678"
                value={registerForm.phoneNumber}
                onChange={(e) => setRegisterForm({ ...registerForm, phoneNumber: e.target.value })}
              />
            </label>
            <label>
              Geboortejaar
              <input
                required
                type="number"
                min="1900"
                max="2010"
                value={registerForm.birthYear}
                onChange={(e) => setRegisterForm({ ...registerForm, birthYear: e.target.value })}
              />
            </label>
            <button type="submit" className="auth-modal__submit" disabled={loading}>
              {loading ? 'Bezig…' : 'Account aanmaken'}
            </button>
          </form>
        )}
      </div>
    </div>
  )
}

export default AuthModal
