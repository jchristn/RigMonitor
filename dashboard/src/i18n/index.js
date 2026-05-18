import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import { defaultLocale, localeStorageKey } from './localeRegistry'
import { resources } from './resources'

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: defaultLocale,
    supportedLngs: Object.keys(resources),
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: localeStorageKey,
      caches: ['localStorage'],
    },
  })

function syncDocument() {
  document.documentElement.lang = i18n.resolvedLanguage || defaultLocale
  document.documentElement.dir = 'ltr'
}

syncDocument()
i18n.on('languageChanged', syncDocument)

export default i18n
