import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getOpenApi } from '../utils/api'

function StateBox({ title, body }) {
  return (
    <section className="surface state-box">
      <h2>{title}</h2>
      <p>{body}</p>
    </section>
  )
}

export function OpenApiView() {
  const { t } = useTranslation()
  const [documentText, setDocumentText] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  async function loadDocument() {
    try {
      setError('')
      setLoading(true)
      const documentObject = await getOpenApi()
      setDocumentText(JSON.stringify(documentObject, null, 2))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadDocument()
  }, [])

  if (loading) {
    return <StateBox title={t('api.loadingTitle')} body={t('api.loadingBody')} />
  }

  if (error) {
    return <StateBox title={t('api.errorTitle')} body={`${t('api.errorBody')} ${error}`} />
  }

  return (
    <div className="view-grid">
      <section className="surface api-shell">
        <pre className="api-viewer">{documentText}</pre>
      </section>
    </div>
  )
}
