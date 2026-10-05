<template>
  <section class="content-page">
    <header class="page__header">
      <div>
        <p class="page__eyebrow">Аккаунт</p>
        <h1>Профиль</h1>
      </div>
    </header>

    <section class="work-panel profile-panel">
      <div>
        <span class="summary-panel__label">Имя</span>
        <strong>{{ userName }}</strong>
      </div>
      <div>
        <span class="summary-panel__label">Email</span>
        <strong>{{ authStore.user?.email || 'Не указан' }}</strong>
      </div>
    </section>
  </section>
</template>

<script setup>
import { computed } from 'vue'

import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const userName = computed(
  () => authStore.user?.display_name || authStore.user?.displayName || 'Не указано',
)
</script>
