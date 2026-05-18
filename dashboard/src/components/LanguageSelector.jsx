import { useTranslation } from 'react-i18next'
import { supportedLocales } from '../i18n/localeRegistry'

export function LanguageSelector() {
  const { i18n, t } = useTranslation()

  return (
    <label className="toggle language-selector">
      <span>{t('language.label')}</span>
      <select
        aria-label={t('language.label')}
        className="select select-compact"
        value={i18n.resolvedLanguage}
        onChange={(event) => {
          void i18n.changeLanguage(event.target.value)
        }}
      >
        {supportedLocales.map((locale) => (
          <option key={locale.code} value={locale.code}>
            {locale.label}
          </option>
        ))}
      </select>
    </label>
  )
}
