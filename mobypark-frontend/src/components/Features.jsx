import './Features.css'

const features = [
  {
    code: 'A-01',
    title: 'Vooraf reserveren',
    text: 'Boek je plek voordat je vertrekt. Geen rondjes rijden op zoek naar een vrije plaats.',
  },
  {
    code: 'A-02',
    title: 'Live beschikbaarheid',
    text: 'Zie per locatie in realtime hoeveel plekken er nog vrij zijn, tot op de minuut nauwkeurig.',
  },
  {
    code: 'A-03',
    title: 'Eén keer betalen',
    text: 'Afrekenen gebeurt automatisch via de app. Geen wachtrij bij de betaalautomaat.',
  },
]

function Features() {
  return (
    <section id="tarieven" className="features">
      <div className="section-inner">
        <div className="features__header">
          <span className="features__eyebrow">Waarom MobyPark</span>
          <h2 className="features__title">Alles voor een rustige rit</h2>
        </div>

        <div className="features__grid">
          {features.map((f) => (
            <article key={f.code} className="feature-card">
              <div className="feature-card__perf" />
              <span className="feature-card__code">{f.code}</span>
              <h3>{f.title}</h3>
              <p>{f.text}</p>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}

export default Features
