import { useState } from 'react'
import { AuthProvider } from './context/AuthContext'
import Navbar from './components/Navbar'
import Hero from './components/Hero'
import Features from './components/Features'
import HowItWorks from './components/HowItWorks'
import Cta from './components/Cta'
import Footer from './components/Footer'
import AuthModal from './components/AuthModal'

function App() {
  const [authMode, setAuthMode] = useState(null) // null | 'login' | 'register'

  return (
    <AuthProvider>
      <Navbar onLoginClick={() => setAuthMode('login')} />
      <main>
        <Hero onRequireLogin={() => setAuthMode('login')} />
        <Features />
        <HowItWorks />
        <Cta onRegisterClick={() => setAuthMode('register')} />
      </main>
      <Footer
        onLoginClick={() => setAuthMode('login')}
        onRegisterClick={() => setAuthMode('register')}
      />

      <AuthModal
        mode={authMode}
        onClose={() => setAuthMode(null)}
        onSwitchMode={(mode) => setAuthMode(mode)}
      />
    </AuthProvider>
  )
}

export default App
