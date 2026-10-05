import { defineStore } from 'pinia'

import * as authApi from '../api/authApi'
import { configureAuthHandlers } from '../api/http'

const STORAGE_KEY = 'family-expenses.auth'
let pendingRefresh = null

function readStoredAuth() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY)) || {}
  } catch {
    return {}
  }
}

function persistAuth({ accessToken, refreshToken, user }) {
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      accessToken,
      refreshToken,
      user,
    }),
  )
}

const storedAuth = readStoredAuth()

export const useAuthStore = defineStore('auth', {
  state: () => ({
    accessToken: storedAuth.accessToken || null,
    refreshToken: storedAuth.refreshToken || null,
    user: storedAuth.user || null,
    isRefreshing: false,
  }),
  getters: {
    isAuthenticated: (state) => Boolean(state.accessToken),
  },
  actions: {
    setSession(session) {
      this.accessToken = session.access_token || session.accessToken || null
      this.refreshToken = session.refresh_token || session.refreshToken || null
      this.user = session.user || this.user
      persistAuth(this)
    },
    clearSession() {
      this.accessToken = null
      this.refreshToken = null
      this.user = null
      localStorage.removeItem(STORAGE_KEY)
    },
    async login(credentials) {
      const session = await authApi.login(credentials)
      this.setSession(session)
      return session
    },
    async register(payload) {
      const session = await authApi.register(payload)
      this.setSession(session)
      return session
    },
    async logout() {
      const refreshToken = this.refreshToken

      try {
        if (refreshToken) {
          await authApi.logout(refreshToken)
        }
      } finally {
        this.clearSession()
      }
    },
    async loadCurrentUser() {
      this.user = await authApi.getCurrentUser()
      persistAuth(this)
      return this.user
    },
    async refreshSession() {
      if (!this.refreshToken) {
        return false
      }

      if (pendingRefresh) {
        return pendingRefresh
      }

      this.isRefreshing = true
      pendingRefresh = this.refreshTokenRequest()

      try {
        return await pendingRefresh
      } finally {
        pendingRefresh = null
        this.isRefreshing = false
      }
    },
    async refreshTokenRequest() {
      try {
        const session = await authApi.refresh(this.refreshToken)
        this.setSession({
          ...session,
          user: session.user || this.user,
        })
        return true
      } catch {
        this.clearSession()
        return false
      }
    },
  },
})

export function installAuthHttpHandlers(pinia) {
  configureAuthHandlers({
    getAccessToken: () => useAuthStore(pinia).accessToken,
    refreshToken: () => useAuthStore(pinia).refreshSession(),
    onUnauthorized: () => useAuthStore(pinia).clearSession(),
  })
}
