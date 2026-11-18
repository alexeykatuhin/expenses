<template>
  <section class="auth-page">
    <form class="auth-form" @submit.prevent="submit">
      <header class="auth-form__header">
        <p class="page__eyebrow">Family Expenses</p>
        <h1>Вход</h1>
      </header>

      <p v-if="formError" class="form-alert">{{ formError }}</p>

      <label class="field">
        <span>Email</span>
        <input
          v-model.trim="form.email"
          autocomplete="email"
          inputmode="email"
          name="email"
          required
          type="email"
        />
        <span v-if="fieldErrors.email" class="field__error">{{ fieldErrors.email }}</span>
      </label>

      <label class="field">
        <span>Пароль</span>
        <input
          v-model="form.password"
          autocomplete="current-password"
          name="password"
          required
          type="password"
        />
        <span v-if="fieldErrors.password" class="field__error">{{ fieldErrors.password }}</span>
      </label>

      <button class="button button--primary" :disabled="isSubmitting" type="submit">
        {{ isSubmitting ? 'Входим...' : 'Войти' }}
      </button>

      <p class="auth-form__switch">
        Нет аккаунта?
        <RouterLink :to="{ name: 'register', query: redirectQuery }">Зарегистрироваться</RouterLink>
      </p>
    </form>
  </section>
</template>

<script setup>
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '../stores/auth'
import { getErrorMessage, getFieldErrors } from '../utils/apiErrors'
import { redirectAfterAuth } from '../utils/authRedirect'

const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()

const form = reactive({
  email: '',
  password: '',
})

const fieldErrors = ref({})
const formError = ref('')
const isSubmitting = ref(false)
const redirectQuery = computed(() =>
  route.query.redirect ? { redirect: route.query.redirect } : {},
)

async function submit() {
  fieldErrors.value = {}
  formError.value = ''
  isSubmitting.value = true

  try {
    await authStore.login({
      email: form.email,
      password: form.password,
    })

    await redirectAfterAuth(router, route.query.redirect, { name: 'home' })
  } catch (error) {
    fieldErrors.value = getFieldErrors(error)
    formError.value =
      error?.status === 401
        ? 'Не удалось войти. Проверьте email и пароль.'
        : getErrorMessage(error, 'Не удалось войти. Проверьте email и пароль.')
  } finally {
    isSubmitting.value = false
  }
}
</script>
