export function SectionCard({ title, description, children, extra = null, className = '' }) {
  return (
    <section className={`surface section-card ${className}`.trim()}>
      <div className="section-header">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        {extra}
      </div>
      {children}
    </section>
  )
}
