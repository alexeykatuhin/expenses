import { createRouter, createWebHistory } from 'vue-router'

import AppLayout from '../components/AppLayout.vue'
import CategoriesPage from '../pages/CategoriesPage.vue'
import ClosedPeriodReportPage from '../pages/ClosedPeriodReportPage.vue'
import ExpensesPage from '../pages/ExpensesPage.vue'
import HomePage from '../pages/HomePage.vue'
import IncomesPage from '../pages/IncomesPage.vue'
import LoginPage from '../pages/LoginPage.vue'
import OperationsPage from '../pages/OperationsPage.vue'
import ProfilePage from '../pages/ProfilePage.vue'
import RegisterPage from '../pages/RegisterPage.vue'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/',
    component: AppLayout,
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'home',
        component: HomePage,
      },
      {
        path: 'operations',
        name: 'operations',
        component: OperationsPage,
      },
      {
        path: 'incomes',
        name: 'incomes',
        component: IncomesPage,
      },
      {
        path: 'expenses',
        name: 'expenses',
        component: ExpensesPage,
      },
      {
        path: 'categories',
        name: 'categories',
        component: CategoriesPage,
      },
      {
        path: 'period-report',
        name: 'periodReport',
        component: ClosedPeriodReportPage,
      },
      {
        path: 'profile',
        name: 'profile',
        component: ProfilePage,
      },
    ],
  },
  {
    path: '/login',
    name: 'login',
    component: LoginPage,
    meta: { guestOnly: true },
  },
  {
    path: '/register',
    name: 'register',
    component: RegisterPage,
    meta: { guestOnly: true },
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

async function ensureAuthenticated(authStore) {
  if (!authStore.accessToken) {
    return false
  }

  try {
    await authStore.loadCurrentUser()
    return true
  } catch {
    authStore.clearSession()
    return false
  }
}

router.beforeEach(async (to) => {
  const authStore = useAuthStore()
  const isAuthenticated = await ensureAuthenticated(authStore)

  if (to.meta.requiresAuth && !isAuthenticated) {
    return {
      name: 'login',
      query: { redirect: to.fullPath },
    }
  }

  if (to.meta.guestOnly && isAuthenticated) {
    return { name: 'home' }
  }

  return true
})

export default router
