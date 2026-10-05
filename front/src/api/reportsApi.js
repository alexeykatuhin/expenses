import { http } from './http'

export function getSummary(params = {}) {
  return http.get('/reports/summary', { query: params })
}

export function getReportByCategory(params = {}) {
  return http.get('/reports/by-category', { query: params })
}

export function getReportBySubcategory(params = {}) {
  return http.get('/reports/by-subcategory', { query: params })
}

export function getReportByPeriodCategory(params = {}) {
  return http.get('/reports/by-period-category', { query: params })
}
