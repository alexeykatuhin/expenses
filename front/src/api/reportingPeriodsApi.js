import { http } from './http'

export function getReportingPeriods() {
  return http.get('/reporting-periods')
}

export function getActiveReportingPeriod() {
  return http.get('/reporting-periods/active')
}

export function closeReportingPeriod(id) {
  return http.post(`/reporting-periods/${id}/close`)
}
