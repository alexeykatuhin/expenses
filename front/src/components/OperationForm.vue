<template>
  <form class="operation-form" @submit.prevent="submitForm">
    <p v-if="state.error" class="form-alert">{{ state.error }}</p>

    <div v-if="state.activePeriod" class="operation-form__period">
      <div>
        <span>Отчетный период</span>
        <strong>{{ formatDate(state.activePeriod.start_date) }} - {{ formatDate(state.activePeriod.end_date) }}</strong>
      </div>
      <button
        class="button button--fit"
        :disabled="state.saving || state.closingPeriod || !canCloseActivePeriod"
        :title="closePeriodTitle"
        type="button"
        @click="closeActivePeriod"
      >
        <CalendarCheck :size="18" aria-hidden="true" />
        <span>{{ state.closingPeriod ? 'Закрываем...' : 'Закрыть период' }}</span>
      </button>
    </div>

    <div class="operation-form__hero">
      <label class="operation-form__amount field">
        <span>Сумма</span>
        <div class="operation-form__amount-shell">
          <input
            ref="amountInput"
            v-model="state.originalAmount"
            :class="{ 'field-control--invalid': state.fieldErrors.original_amount }"
            :disabled="state.saving"
            autofocus
            enterkeyhint="done"
            inputmode="decimal"
            min="0"
            :step="amountInputStep"
            type="number"
            @focus="selectAmount"
          />
          <small>{{ amountCurrencyLabel }}</small>
        </div>
        <small v-if="isVietnameseDong" class="field__hint">Введите сумму в тысячах: 3 = 3000 VND.</small>
        <span v-if="state.fieldErrors.original_amount" class="field__error">
          {{ state.fieldErrors.original_amount }}
        </span>
      </label>
    </div>

    <div class="operation-form__priority">
      <div class="field field--category-priority">
        <span>Категория</span>
        <CategoryCombobox
          v-model="state.categoryId"
          :options="state.categories"
          :disabled="state.saving || state.loadingCategories"
          :invalid="Boolean(state.fieldErrors.category_id)"
          :loading="state.loadingCategories"
        />
        <span v-if="state.fieldErrors.category_id" class="field__error">
          {{ state.fieldErrors.category_id }}
        </span>
      </div>
    </div>

    <div class="operation-form__grid operation-form__grid--secondary">
      <label v-if="!fixedType" class="field">
        <span>Тип</span>
        <select v-model="state.type" :disabled="state.saving || state.loadingCategories">
          <option value="income">Доход</option>
          <option value="expense">Расход</option>
        </select>
      </label>

      <label class="field">
        <span>Подкатегория</span>
        <select
          v-model="state.subcategoryId"
          :disabled="state.saving || state.loadingSubcategories || !state.categoryId"
          :class="{ 'field-control--invalid': state.fieldErrors.subcategory_id }"
        >
          <option value="">Выберите подкатегорию</option>
          <option v-for="subcategory in state.subcategories" :key="subcategory.id" :value="subcategory.id">
            {{ subcategory.name }}
          </option>
        </select>
        <span v-if="state.fieldErrors.subcategory_id" class="field__error">
          {{ state.fieldErrors.subcategory_id }}
        </span>
      </label>

      <label class="field">
        <span>Валюта</span>
        <div class="currency-select">
        <select
          v-model="state.currencyCode"
          :class="{ 'field-control--invalid': state.fieldErrors.currency_code }"
          :disabled="state.saving || state.loadingCurrencies"
        >
          <option value="">Выберите валюту</option>
          <option v-for="currency in state.currencies" :key="currency.code" :value="currency.code">
            {{ currency.code }} - {{ currency.name }}
          </option>
        </select>
          <button
            class="icon-button"
            :disabled="state.saving || state.loadingCurrencies"
            title="Add currency"
            type="button"
            @click="toggleCurrencyEditor"
          >
            <Plus :size="18" aria-hidden="true" />
          </button>
        </div>
        <span v-if="state.fieldErrors.currency_code" class="field__error">
          {{ state.fieldErrors.currency_code }}
        </span>
      </label>

    </div>

    <div v-if="state.addingCurrency" class="currency-editor">
      <label class="field field--inline">
        <span>Code</span>
        <input
          v-model.trim="state.newCurrencyCode"
          :class="{ 'field-control--invalid': state.currencyFieldErrors.code }"
          :disabled="state.creatingCurrency"
          maxlength="3"
          placeholder="VND"
          type="text"
        />
        <span v-if="state.currencyFieldErrors.code" class="field__error">{{ state.currencyFieldErrors.code }}</span>
      </label>
      <label class="field field--inline">
        <span>Name</span>
        <input
          v-model.trim="state.newCurrencyName"
          :class="{ 'field-control--invalid': state.currencyFieldErrors.name }"
          :disabled="state.creatingCurrency"
          placeholder="Vietnamese Dong"
          type="text"
        />
        <span v-if="state.currencyFieldErrors.name" class="field__error">{{ state.currencyFieldErrors.name }}</span>
      </label>
      <label class="field field--inline">
        <span>Дробных знаков</span>
        <input
          v-model="state.newCurrencyMinorUnit"
          :class="{ 'field-control--invalid': state.currencyFieldErrors.minor_unit }"
          :disabled="state.creatingCurrency"
          max="8"
          min="0"
          step="1"
          type="number"
        />
        <span v-if="state.currencyFieldErrors.minor_unit" class="field__error">{{ state.currencyFieldErrors.minor_unit }}</span>
      </label>
      <div class="currency-editor__actions">
        <button class="icon-button" :disabled="state.creatingCurrency" title="Save" type="button" @click="createMissingCurrency">
          <Save :size="18" aria-hidden="true" />
        </button>
        <button class="icon-button" :disabled="state.creatingCurrency" title="Cancel" type="button" @click="toggleCurrencyEditor">
          <X :size="18" aria-hidden="true" />
        </button>
      </div>
    </div>

    <label class="field">
      <span>Комментарий</span>
      <textarea v-model.trim="state.comment" :disabled="state.saving" rows="3" />
    </label>

    <div class="operation-form__actions">
      <button class="button button--primary button--fit" :disabled="!canSubmit" type="submit">
        <Save :size="18" aria-hidden="true" />
        <span>{{ state.saving ? 'Сохраняем...' : submitLabel }}</span>
      </button>
      <button v-if="mode === 'create'" class="button button--fit" :disabled="state.saving" type="button" @click="resetForm">
        <RotateCcw :size="18" aria-hidden="true" />
        <span>Очистить</span>
      </button>
    </div>
  </form>
</template>

<script setup>
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { CalendarCheck, Plus, RotateCcw, Save, X } from 'lucide-vue-next'

import {
  expenseCategoriesApi,
  expenseSubcategoriesApi,
  incomeCategoriesApi,
  incomeSubcategoriesApi,
} from '../api/categoriesApi'
import { createExpense, updateExpense } from '../api/expensesApi'
import { createIncome, updateIncome } from '../api/incomesApi'
import { createCurrency, getCurrencies } from '../api/currenciesApi'
import { closeReportingPeriod, getActiveReportingPeriod } from '../api/reportingPeriodsApi'
import { getErrorMessage, getFieldErrors } from '../utils/apiErrors'
import CategoryCombobox from './CategoryCombobox.vue'

const props = defineProps({
  fixedType: {
    type: String,
    default: '',
    validator: (value) => ['', 'income', 'expense'].includes(value),
  },
  mode: {
    type: String,
    default: 'create',
    validator: (value) => ['create', 'edit'].includes(value),
  },
  operation: {
    type: Object,
    default: null,
  },
})

const emit = defineEmits(['saved'])
const lastCurrencyStorageKey = 'family-expenses:last-operation-currency'
const lastCategoryStorageKeyPrefix = 'family-expenses:last-operation-category'
const vietnameseDongInputMultiplier = 1000
const amountInput = ref(null)

const resources = {
  income: {
    categoryApi: incomeCategoriesApi,
    subcategoryApi: incomeSubcategoriesApi,
    create: createIncome,
    update: updateIncome,
  },
  expense: {
    categoryApi: expenseCategoriesApi,
    subcategoryApi: expenseSubcategoriesApi,
    create: createExpense,
    update: updateExpense,
  },
}

const state = reactive({
  type: props.fixedType || props.operation?.type || 'expense',
  categoryId: '',
  subcategoryId: '',
  originalAmount: '0',
  currencyCode: '',
  comment: '',
  activePeriod: null,
  categories: [],
  subcategories: [],
  currencies: [],
  addingCurrency: false,
  creatingCurrency: false,
  newCurrencyCode: '',
  newCurrencyName: '',
  newCurrencyMinorUnit: '2',
  currencyFieldErrors: {},
  loadingCategories: false,
  loadingSubcategories: false,
  loadingCurrencies: false,
  saving: false,
  closingPeriod: false,
  applyingOperation: false,
  error: '',
  fieldErrors: {},
})

const submitLabel = computed(() => (props.mode === 'edit' ? 'Сохранить' : 'Добавить операцию'))
const isVietnameseDong = computed(() => state.currencyCode === 'VND')
const amountCurrencyLabel = computed(() => (isVietnameseDong.value ? 'тыс. VND' : state.currencyCode || 'RUB'))
const amountInputStep = computed(() => (isVietnameseDong.value ? '0.001' : '0.01'))
const canCloseActivePeriod = computed(() => Boolean(state.activePeriod) && isPeriodEnded(state.activePeriod))
const closePeriodTitle = computed(() => {
  if (!state.activePeriod) {
    return ''
  }

  return canCloseActivePeriod.value
    ? 'Закрыть отчетный период'
    : `Период можно закрыть с ${formatDate(addDays(parseLocalDate(state.activePeriod.end_date), 1))}`
})
const canSubmit = computed(
  () =>
    !state.saving &&
    !state.loadingCategories &&
    !state.loadingSubcategories &&
    !state.loadingCurrencies &&
    Boolean(state.type) &&
    Boolean(state.categoryId) &&
    Boolean(state.currencyCode) &&
    Boolean(state.activePeriod) &&
    isValidMoney(state.originalAmount),
)

onMounted(async () => {
  applyOperation(props.operation)
  await focusAmountInput()
  await Promise.all([loadActivePeriod(), loadCategories(), loadCurrencies()])
  await focusAmountInput()
})

watch(
  () => props.fixedType,
  async (type) => {
    if (!type || type === state.type) {
      return
    }

    state.type = type
    state.categoryId = ''
    state.subcategoryId = ''
    await loadCategories()
    await focusAmountInput()
  },
)

watch(
  () => props.operation,
  async (operation) => {
    applyOperation(operation)
    await loadCategories()
    await focusAmountInput()
  },
)

watch(
  () => state.type,
  async () => {
    if (state.applyingOperation) {
      return
    }

    state.categoryId = ''
    state.subcategoryId = ''
    state.subcategories = []
    await loadCategories()
    await focusAmountInput()
  },
  { flush: 'sync' },
)

watch(
  () => state.categoryId,
  async (categoryId) => {
    if (state.applyingOperation) {
      return
    }

    state.subcategoryId = ''

    if (categoryId) {
      await loadSubcategories(categoryId)
    } else {
      state.subcategories = []
    }
  },
  { flush: 'sync' },
)

async function loadCategories() {
  state.loadingCategories = true
  state.error = ''

  try {
    state.categories = await resources[state.type].categoryApi.list({ is_active: true })

    if (!state.categoryId) {
      state.categoryId = getInitialCategoryId()
    }

    if (state.categoryId) {
      await loadSubcategories(state.categoryId)
    }
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить категории.')
  } finally {
    state.loadingCategories = false
  }
}

async function loadSubcategories(categoryId) {
  state.loadingSubcategories = true
  state.error = ''

  try {
    state.subcategories = await resources[state.type].subcategoryApi.list({
      category_id: categoryId,
      is_active: true,
    })
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить подкатегории.')
  } finally {
    state.loadingSubcategories = false
  }
}

async function loadCurrencies() {
  state.loadingCurrencies = true
  state.error = ''

  try {
    state.currencies = await getCurrencies()

    if (!state.currencyCode) {
      state.currencyCode = getInitialCurrencyCode()
    }
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить валюты.')
  } finally {
    state.loadingCurrencies = false
  }
}

function toggleCurrencyEditor() {
  state.addingCurrency = !state.addingCurrency
  state.currencyFieldErrors = {}
}

async function createMissingCurrency() {
  state.currencyFieldErrors = validateCurrencyForm()
  if (Object.keys(state.currencyFieldErrors).length) {
    return
  }

  state.creatingCurrency = true
  state.error = ''

  try {
    const currency = await createCurrency({
      code: state.newCurrencyCode.trim().toUpperCase(),
      name: state.newCurrencyName.trim(),
      minor_unit: state.newCurrencyMinorUnit === '' ? null : Number(state.newCurrencyMinorUnit),
    })

    await loadCurrencies()
    state.currencyCode = currency.code
    state.newCurrencyCode = ''
    state.newCurrencyName = ''
    state.newCurrencyMinorUnit = '2'
    state.addingCurrency = false
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось добавить валюту.')
    state.currencyFieldErrors = getFieldErrors(error)
  } finally {
    state.creatingCurrency = false
  }
}

async function submitForm() {
  state.fieldErrors = validateForm()

  if (Object.keys(state.fieldErrors).length) {
    return
  }

  state.saving = true
  state.error = ''
  let shouldFocusAfterSave = false

  try {
    const payload = buildPayload()
    const saved =
      props.mode === 'edit'
        ? await resources[state.type].update(props.operation.id, payload)
        : await resources[state.type].create(payload)

    rememberLastCurrency(state.currencyCode)
    rememberLastCategory(state.type, state.categoryId)
    emit('saved', { type: state.type, operation: saved })
    await loadCurrencies()

    if (props.mode === 'create') {
      await resetForm({ focus: false })
      shouldFocusAfterSave = true
    }
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось сохранить операцию.')
    state.fieldErrors = getFieldErrors(error)
  } finally {
    state.saving = false
    if (shouldFocusAfterSave) {
      await focusAmountInput()
    }
  }
}

async function loadActivePeriod() {
  state.error = ''

  try {
    state.activePeriod = await getActiveReportingPeriod()
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить активный отчетный период.')
  }
}

async function closeActivePeriod() {
  if (!state.activePeriod) {
    return
  }

  if (!isPeriodEnded(state.activePeriod)) {
    state.error = 'Период можно закрыть не раньше даты окончания.'
    return
  }

  if (!window.confirm(getClosePeriodConfirmation(state.activePeriod))) {
    return
  }

  state.closingPeriod = true
  state.error = ''

  try {
    state.activePeriod = await closeReportingPeriod(state.activePeriod.id)
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось закрыть отчетный период.')
  } finally {
    state.closingPeriod = false
  }
}

function validateForm() {
  const errors = {}

  if (!state.categoryId) {
    errors.category_id = 'Выберите категорию.'
  }

  if (!isValidMoney(state.originalAmount)) {
    errors.original_amount = 'Сумма должна быть больше или равна 0.'
  }

  if (!state.currencyCode) {
    errors.currency_code = 'Выберите валюту.'
  }

  if (!state.activePeriod) {
    errors.period_id = 'Активный отчетный период не найден.'
  }

  return errors
}

function validateCurrencyForm() {
  const errors = {}
  const code = state.newCurrencyCode.trim().toUpperCase()
  const minorUnit = state.newCurrencyMinorUnit === '' ? null : Number(state.newCurrencyMinorUnit)

  if (!/^[A-Z]{3}$/.test(code)) {
    errors.code = 'Код должен состоять из трех латинских букв.'
  }

  if (!state.newCurrencyName.trim()) {
    errors.name = 'Введите название валюты.'
  }

  if (minorUnit !== null && (!Number.isInteger(minorUnit) || minorUnit < 0 || minorUnit > 8)) {
    errors.minor_unit = 'Значение должно быть от 0 до 8.'
  }

  return errors
}

function buildPayload() {
  return {
    category_id: Number(state.categoryId),
    subcategory_id: state.subcategoryId ? Number(state.subcategoryId) : null,
    original_amount: formatMoney(toStoredOriginalAmount(state.originalAmount, state.currencyCode)),
    currency_code: state.currencyCode,
    comment: state.comment || null,
  }
}

async function resetForm({ focus = true } = {}) {
  state.categoryId = getInitialCategoryId()
  state.subcategoryId = ''
  state.originalAmount = '0'
  state.comment = ''
  state.fieldErrors = {}

  if (state.categoryId) {
    await loadSubcategories(state.categoryId)
  } else {
    state.subcategories = []
  }

  if (focus) {
    await focusAmountInput()
  }
}

function applyOperation(operation) {
  state.applyingOperation = true
  const operationType = props.fixedType || operation?.type
  const currencyCode = operation?.currency_code ?? operation?.currencyCode ?? state.currencyCode
  const originalAmount = operation?.original_amount ?? operation?.originalAmount ?? operation?.amount_rub ?? operation?.amountRub ?? '0'
  state.type = operationType || state.type || 'expense'
  state.categoryId = operation?.category_id ?? operation?.categoryId ?? ''
  state.subcategoryId = operation?.subcategory_id ?? operation?.subcategoryId ?? ''
  state.originalAmount = toDisplayOriginalAmount(originalAmount, currencyCode)
  state.currencyCode = currencyCode
  state.comment = operation?.comment ?? ''
  state.fieldErrors = {}
  state.applyingOperation = false
}

function getInitialCurrencyCode() {
  const lastCurrencyCode = getRememberedLastCurrency()

  if (lastCurrencyCode && state.currencies.some((currency) => currency.code === lastCurrencyCode)) {
    return lastCurrencyCode
  }

  return state.currencies[0]?.code || 'RUB'
}

function getInitialCategoryId() {
  const lastCategoryId = getRememberedLastCategory(state.type)

  if (lastCategoryId && state.categories.some((category) => String(category.id) === String(lastCategoryId))) {
    return lastCategoryId
  }

  return state.categories[0]?.id || ''
}

function getRememberedLastCurrency() {
  try {
    return window.localStorage.getItem(lastCurrencyStorageKey) || ''
  } catch {
    return ''
  }
}

function getRememberedLastCategory(type) {
  if (!type) {
    return ''
  }

  try {
    return window.localStorage.getItem(getLastCategoryStorageKey(type)) || ''
  } catch {
    return ''
  }
}

function rememberLastCurrency(currencyCode) {
  if (!currencyCode) {
    return
  }

  try {
    window.localStorage.setItem(lastCurrencyStorageKey, currencyCode)
  } catch {
    // The form should still work if browser storage is unavailable.
  }
}

function isValidMoney(value) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed >= 0
}

function toStoredOriginalAmount(value, currencyCode) {
  const amount = Number(value)
  return currencyCode === 'VND' ? amount * vietnameseDongInputMultiplier : amount
}

function toDisplayOriginalAmount(value, currencyCode) {
  const amount = Number(value)
  if (!Number.isFinite(amount)) {
    return value ?? '0'
  }

  return String(currencyCode === 'VND' ? amount / vietnameseDongInputMultiplier : amount)
}

function rememberLastCategory(type, categoryId) {
  if (!type || !categoryId) {
    return
  }

  try {
    window.localStorage.setItem(getLastCategoryStorageKey(type), String(categoryId))
  } catch {
    // The form should still work if browser storage is unavailable.
  }
}

function getLastCategoryStorageKey(type) {
  return `${lastCategoryStorageKeyPrefix}:${type}`
}

async function focusAmountInput() {
  if (props.mode !== 'create') {
    return
  }

  await nextTick()
  focusAmountControl()
  window.requestAnimationFrame(focusAmountControl)
}

function focusAmountControl() {
  if (!amountInput.value || state.saving) {
    return
  }

  amountInput.value.focus({ preventScroll: true })
  amountInput.value.select()
}

function selectAmount(event) {
  event.target.select()
}

defineExpose({
  focusAmountInput,
})

function isPeriodEnded(period) {
  const endDate = parseLocalDate(period?.end_date)
  if (!endDate) {
    return false
  }

  return startOfToday().getTime() > endDate.getTime()
}

function addDays(date, days) {
  if (!date) {
    return null
  }

  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days)
}

function parseLocalDate(value) {
  if (!value) {
    return null
  }

  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) {
    return null
  }

  return new Date(year, month - 1, day)
}

function startOfToday() {
  const today = new Date()
  return new Date(today.getFullYear(), today.getMonth(), today.getDate())
}

function getClosePeriodConfirmation(period) {
  return `Закрыть отчетный период ${formatDate(period.start_date)} - ${formatDate(period.end_date)}? После закрытия операции этого периода нельзя будет изменить.`
}

function formatMoney(value) {
  return Number(value).toFixed(2)
}

function formatDate(value) {
  if (!value) {
    return ''
  }

  const date = value instanceof Date ? value : parseLocalDate(value)
  if (!date || Number.isNaN(date.getTime())) {
    return ''
  }

  return new Intl.DateTimeFormat('ru-RU').format(date)
}
</script>
