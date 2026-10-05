<template>
  <div class="app-layout">
    <aside class="app-sidebar" aria-label="Основная навигация">
      <RouterLink class="app-sidebar__brand" :to="{ name: 'home' }">
        Family Expenses
      </RouterLink>

      <nav class="app-nav">
        <RouterLink
          v-for="item in navigation"
          :key="item.name"
          class="app-nav__link"
          :to="{ name: item.name }"
        >
          <component :is="item.icon" :size="18" aria-hidden="true" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>
    </aside>

    <div class="app-layout__body">
      <header class="app-topbar">
        <div class="app-topbar__user">
          <span class="app-topbar__label">Пользователь</span>
          <strong>{{ userName }}</strong>
        </div>

        <button class="button" :disabled="isLoggingOut" type="button" @click="logout">
          <LogOut :size="18" aria-hidden="true" />
          <span>{{ isLoggingOut ? 'Выходим...' : 'Выйти' }}</span>
        </button>
      </header>

      <main class="app-content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup>
import {
  Banknote,
  BarChart3,
  CreditCard,
  FolderTree,
  LayoutDashboard,
  LogOut,
  ReceiptText,
  UserRound,
} from 'lucide-vue-next'
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const router = useRouter()
const isLoggingOut = ref(false)

const navigation = [
  { name: 'home', label: 'Обзор', icon: LayoutDashboard },
  { name: 'operations', label: 'Операции', icon: ReceiptText },
  { name: 'incomes', label: 'Доходы', icon: Banknote },
  { name: 'expenses', label: 'Расходы', icon: CreditCard },
  { name: 'categories', label: 'Категории', icon: FolderTree },
  { name: 'periodReport', label: 'Период', icon: BarChart3 },
  { name: 'profile', label: 'Профиль', icon: UserRound },
]

const userName = computed(
  () =>
    authStore.user?.display_name ||
    authStore.user?.displayName ||
    authStore.user?.email ||
    'Семейный бюджет',
)

async function logout() {
  isLoggingOut.value = true

  try {
    await authStore.logout()
    await router.push({ name: 'login' })
  } finally {
    isLoggingOut.value = false
  }
}
</script>
