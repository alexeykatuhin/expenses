import { http } from './http'

function createResource(path) {
  return {
    list: (params = {}) => http.get(path, { query: params }),
    get: (id) => http.get(`${path}/${id}`),
    create: (payload) => http.post(path, payload),
    update: (id, payload) => http.patch(`${path}/${id}`, payload),
    delete: (id) => http.delete(`${path}/${id}`),
  }
}

export const incomeCategoriesApi = createResource('/income-categories')
export const incomeSubcategoriesApi = createResource('/income-subcategories')
export const expenseCategoriesApi = createResource('/expense-categories')
export const expenseSubcategoriesApi = createResource('/expense-subcategories')
