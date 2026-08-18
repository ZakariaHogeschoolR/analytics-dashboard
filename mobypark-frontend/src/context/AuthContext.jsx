import { createContext, useContext, useState } from 'react'
import { api, saveToken, clearToken, getToken } from '../lib/api'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [username, setUsername] = useState(() => localStorage.getItem('mobypark_username'))
  const [role, setRole] = useState(() => localStorage.getItem('mobypark_role'))
  const [token, setToken] = useState(() => getToken())

  const login = async (usernameInput, password) => {
    const data = await api.login(usernameInput, password)
    saveToken(data.accessToken)
    localStorage.setItem('mobypark_username', usernameInput)
    localStorage.setItem('mobypark_role', data.role || 'User')
    setToken(data.accessToken)
    setUsername(usernameInput)
    setRole(data.role || 'User')
    return data
  }

  const register = (payload) => api.register(payload)

  const logout = () => {
    clearToken()
    localStorage.removeItem('mobypark_username')
    localStorage.removeItem('mobypark_role')
    setToken(null)
    setUsername(null)
    setRole(null)
  }

  const value = {
    isAuthenticated: !!token,
    username,
    role,
    login,
    register,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth moet binnen <AuthProvider> gebruikt worden')
  return ctx
}
