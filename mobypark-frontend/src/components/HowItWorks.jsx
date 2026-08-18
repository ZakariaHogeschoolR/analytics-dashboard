import './HowItWorks.css'

const steps = [
  {
    title: 'Zoek je locatie',
    text: 'Vul je bestemming en datum in. MobyPark toont direct welke parkeerlocaties plek hebben.',
  },
  {
    title: 'Reserveer je plek',
    text: 'Kies een plek en bevestig. Je ticket staat meteen klaar in de app, geen printje nodig.',
  },
  {
    title: 'Rijd naar binnen',
    text: 'De slagboom herkent je kenteken. Geen pasje, geen wachtrij, gewoon doorrijden.',
  },
]

function HowItWorks() {
  return (
    <section id="werkwijze" className="how">
      <div className="section-inner">
        <div className="how__header">
          <span className="how__eyebrow">Werkwijze</span>
          <h2 className="how__title">Van zoeken tot binnenrijden</h2>
        </div>

        <div className="how__gate" aria-hidden="true">
          <div className="how__gate-post" />
          <div className="how__gate-arm" />
          <div className="how__gate-post how__gate-post--right" />
        </div>

        <ol className="how__steps">
          {steps.map((step, i) => (
            <li key={step.title} className="how__step">
              <span className="how__step-num">{String(i + 1).padStart(2, '0')}</span>
              <h3>{step.title}</h3>
              <p>{step.text}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}

export default HowItWorks
