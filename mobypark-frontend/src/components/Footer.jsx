import { useAuth } from '../context/AuthContext'
import './Footer.css'

function Footer({ onLoginClick, onRegisterClick }) {
  const { isAuthenticated, username, logout } = useAuth()

  return (
    <footer className="footer">
      <div className="section-inner footer__inner">
        <div className="footer__brand">
          <span className="footer__logo">
            <span>P</span> MobyPark
          </span>
          <p>Parkeren reserveren voordat je vertrekt.</p>
        </div>

        <div className="footer__col">
          <h4>Product</h4>
          <a href="#locaties">Locaties</a>
          <a href="#werkwijze">Hoe het werkt</a>
          <a href="#tarieven">Tarieven</a>
        </div>

        <div className="footer__col">
          <h4>Bedrijf</h4>
          <a href="#over">Over ons</a>
          <a href="#contact">Contact</a>
          <a href="#vacatures">Vacatures</a>
        </div>

        <div className="footer__col">
          <h4>Account</h4>
          {isAuthenticated ? (
            <>
              <span className="footer__signed-in">Ingelogd als {username}</span>
              <button className="footer__link-btn" onClick={logout}>
                Uitloggen
              </button>
            </>
          ) : (
            <>
              <button className="footer__link-btn" onClick={onLoginClick}>
                Inloggen
              </button>
              <button className="footer__link-btn" onClick={onRegisterClick}>
                Registreren
              </button>
            </>
          )}
        </div>
      </div>

      <div className="section-inner footer__bottom">
        <span>&copy; {new Date().getFullYear()} MobyPark</span>
        <span>Gemaakt met MobyParkApi</span>
      </div>
    </footer>
  )
}

export default Footer
