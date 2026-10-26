export default function PagePlaceholder({ title, summary }) {
  return (
    <section className="page">
      <header className="page-header">
        <h1>{title}</h1>
        <p>{summary}</p>
      </header>
    </section>
  )
}
