import { http } from './http'

export function getExpenses(params = {}) {
  return http.get('/expenses', { query: params })
}

export function getExpense(id) {
  return http.get(`/expenses/${id}`)
}

export function createExpense(payload) {
  return http.post('/expenses', payload)
}

export function updateExpense(id, payload) {
  return http.patch(`/expenses/${id}`, payload)
}

export function deleteExpense(id) {
  return http.delete(`/expenses/${id}`)
}
