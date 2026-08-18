import { useAuth } from '../context/AuthContext'
import './Cta.css'

function Cta({ onRegisterClick }) {
  const { isAuthenticated } = useAuth()

  const handleShowLocations = () => {
    document.getElementById('top')?.scrollIntoView({ behavior: 'smooth' })
    // Vraagt de Hero-component om de volledige locatielijst op te halen
    window.dispatchEvent(new CustomEvent('mobypark:load-locations'))
  }

  return (
    <section className="cta">
      <div className="section-inner cta__inner">
        <div>
          <span className="cta__eyebrow">Klaar om te vertrekken?</span>
          <h2 className="cta__title">Vind een plek voordat je in de auto stapt</h2>
        </div>
        <div className="cta__actions">
          <button className="cta__btn cta__btn--primary" onClick={handleShowLocations}>
            Bekijk locaties
          </button>
          {!isAuthenticated && (
            <button className="cta__btn cta__btn--ghost" onClick={onRegisterClick}>
              Account aanmaken
            </button>
          )}
        </div>
      </div>
    </section>
  )
}

export default Cta
