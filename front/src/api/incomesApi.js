import { http } from './http'

export function getIncomes(params = {}) {
  return http.get('/incomes', { query: params })
}

export function getIncome(id) {
  return http.get(`/incomes/${id}`)
}

export function createIncome(payload) {
  return http.post('/incomes', payload)
}

export function updateIncome(id, payload) {
  return http.patch(`/incomes/${id}`, payload)
}

export function deleteIncome(id) {
  return http.delete(`/incomes/${id}`)
}
