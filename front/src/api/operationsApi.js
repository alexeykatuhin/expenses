import { http } from './http'

export function getOperations(params = {}) {
  return http.get('/operations', { query: params })
}
