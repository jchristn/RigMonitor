export function StatCard({ label, value, tone = 'default' }) {
  return (
    <article className={`surface stat-card ${tone}`}>
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
    </article>
  )
}
