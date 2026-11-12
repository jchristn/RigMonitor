import { describe, expect, it } from 'vitest'
import { resources } from '../i18n/resources'

function keys(value, prefix = '') {
  return Object.entries(value).flatMap(([key, child]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return child && typeof child === 'object' ? keys(child, path) : [path]
  })
}

describe('locale parity', () => {
  const english = keys(resources.en.translation).sort()

  it.each(Object.keys(resources).filter((locale) => locale !== 'en'))('%s has exactly the English key set', (locale) => {
    expect(keys(resources[locale].translation).sort()).toEqual(english)
  })

  it('defines every GPU vendor section in English', () => {
    ;['nvidia', 'amd', 'apple', 'qualcomm'].forEach((vendor) => {
      expect(resources.en.translation.sections[vendor]).toBeTruthy()
      expect(resources.en.translation.descriptions[vendor]).toBeTruthy()
      expect(resources.en.translation.vendors[vendor]).toBeTruthy()
      expect(resources.en.translation.messages[`${vendor}Missing`]).toBeTruthy()
    })
  })
})
