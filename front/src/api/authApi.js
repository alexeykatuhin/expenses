import { http } from './http'

export function register(payload) {
  return http.post('/auth/register', payload, { auth: false })
}

export function login(payload) {
  return http.post('/auth/login', payload, { auth: false })
}

export function refresh(refreshToken) {
  return http.post('/auth/refresh', { refresh_token: refreshToken }, { auth: false })
}

export function logout(refreshToken) {
  return http.post('/auth/logout', { refresh_token: refreshToken })
}

export function getCurrentUser() {
  return http.get('/users/me')
}

export function updateCurrentUser(payload) {
  return http.patch('/users/me', payload)
}
