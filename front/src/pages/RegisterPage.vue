<template>
  <section class="auth-page">
    <form class="auth-form" @submit.prevent="submit">
      <header class="auth-form__header">
        <p class="page__eyebrow">Family Expenses</p>
        <h1>Регистрация</h1>
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
        <span>Имя</span>
        <input
          v-model.trim="form.displayName"
          autocomplete="name"
          name="display_name"
          type="text"
        />
        <span v-if="fieldErrors.display_name" class="field__error">
          {{ fieldErrors.display_name }}
        </span>
      </label>

      <label class="field">
        <span>Пароль</span>
        <input
          v-model="form.password"
          autocomplete="new-password"
          name="password"
          required
          type="password"
        />
        <span v-if="fieldErrors.password" class="field__error">{{ fieldErrors.password }}</span>
      </label>

      <button class="button button--primary" :disabled="isSubmitting" type="submit">
        {{ isSubmitting ? 'Создаем...' : 'Создать аккаунт' }}
      </button>

      <p class="auth-form__switch">
        Уже есть аккаунт?
        <RouterLink :to="{ name: 'login', query: redirectQuery }">Войти</RouterLink>
      </p>
    </form>
  </section>
</template>

<script setup>
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '../stores/auth'
import { getErrorMessage, getFieldErrors } from '../utils/apiErrors'

const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()

const form = reactive({
  email: '',
  displayName: '',
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
    await authStore.register({
      email: form.email,
      display_name: form.displayName || null,
      password: form.password,
    })

    await router.push(typeof route.query.redirect === 'string' ? route.query.redirect : { name: 'home' })
  } catch (error) {
    fieldErrors.value = getFieldErrors(error)
    formError.value = getErrorMessage(error, 'Не удалось создать аккаунт.')
  } finally {
    isSubmitting.value = false
  }
}
</script>
