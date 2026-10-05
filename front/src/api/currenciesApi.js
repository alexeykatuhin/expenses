import { http } from './http'

export function getCurrencies() {
  return http.get('/currencies')
}

export function createCurrency(payload) {
  return http.post('/currencies', payload)
}
