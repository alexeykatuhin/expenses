<template>
  <section class="content-page content-page--wide">
    <header class="page__header">
      <div>
        <p class="page__eyebrow">{{ eyebrow }}</p>
        <h1>{{ title }}</h1>
      </div>
      <button class="button button--primary button--fit" type="button" @click="startCreate">
        <Plus :size="18" aria-hidden="true" />
        <span>{{ addLabel }}</span>
      </button>
    </header>

    <p v-if="state.message" class="success-alert">{{ state.message }}</p>
    <p v-if="state.error" class="form-alert operations-alert">{{ state.error }}</p>

    <section v-if="state.editorMode" class="work-panel">
      <div class="operations-editor__header">
        <h2>{{ editorTitle }}</h2>
        <button class="icon-button" title="Закрыть форму" type="button" @click="closeEditor">
          <X :size="18" aria-hidden="true" />
        </button>
      </div>
      <OperationForm
        ref="operationForm"
        :fixed-type="formFixedType"
        :mode="state.editorMode"
        :operation="state.editingOperation"
        @saved="handleSaved"
      />
    </section>

    <section class="work-panel">
      <div class="operations-toolbar" :class="{ 'operations-toolbar--fixed-type': fixedType }">
        <label class="field field--inline">
          <span>С</span>
          <input v-model="state.filters.date_from" type="date" @change="applyFilters" />
        </label>
        <label class="field field--inline">
          <span>По</span>
          <input v-model="state.filters.date_to" type="date" @change="applyFilters" />
        </label>
        <label v-if="!fixedType" class="field field--inline">
          <span>Тип</span>
          <select v-model="state.filters.type" @change="handleTypeFilterChange">
            <option value="">Все</option>
            <option value="income">Доход</option>
            <option value="expense">Расход</option>
          </select>
        </label>
        <label class="field field--inline">
          <span>Категория</span>
          <select v-model="state.filters.category_id" :disabled="!activeFilterType" @change="handleCategoryFilterChange">
            <option value="">Все</option>
            <option v-for="category in filteredCategories" :key="category.id" :value="category.id">
              {{ category.name }}
            </option>
          </select>
        </label>
        <label class="field field--inline">
          <span>Подкатегория</span>
          <select v-model="state.filters.subcategory_id" :disabled="!state.filters.category_id" @change="applyFilters">
            <option value="">Все</option>
            <option v-for="subcategory in filteredSubcategories" :key="subcategory.id" :value="subcategory.id">
              {{ subcategory.name }}
            </option>
          </select>
        </label>
        <label class="field field--inline operations-limit">
          <span>На странице</span>
          <select v-model.number="state.filters.limit" @change="applyFilters">
            <option :value="25">25</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
          </select>
        </label>
        <div class="operations-toolbar__actions">
          <button class="icon-button" :disabled="state.loading" title="Обновить" type="button" @click="loadOperations">
            <RefreshCcw :size="18" aria-hidden="true" />
          </button>
          <button class="button button--fit" :disabled="state.loading" type="button" @click="resetFilters">
            Сбросить
          </button>
        </div>
      </div>

      <div v-if="state.loading" class="empty-state">{{ activeLabels?.loading || 'Загружаем операции...' }}</div>
      <div v-else-if="!state.operations.length" class="empty-state">{{ activeLabels?.empty || 'Операции не найдены.' }}</div>

      <div v-else class="operations-table-wrap">
        <table class="operations-table" :class="{ 'operations-table--fixed-type': fixedType }">
          <thead>
            <tr>
              <th>Дата</th>
              <th v-if="!fixedType">Тип</th>
              <th>Категория</th>
              <th>Сумма RUB</th>
              <th>Сумма USD</th>
              <th>Сумма ввода</th>
              <th>Комментарий</th>
              <th aria-label="Действия"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="operation in state.operations" :key="operationKey(operation)">
              <td>{{ formatDate(operation.operation_date) }}</td>
              <td v-if="!fixedType">
                <span class="operation-type" :class="`operation-type--${getOperationType(operation)}`">
                  {{ getOperationType(operation) === 'income' ? 'Доход' : 'Расход' }}
                </span>
              </td>
              <td>
                <div class="operation-category">
                  <strong>{{ getCategoryName(operation) }}</strong>
                  <span v-if="getSubcategoryName(operation)">{{ getSubcategoryName(operation) }}</span>
                </div>
              </td>
              <td>
                <div class="operation-amount" :class="`operation-amount--${getOperationType(operation)}`">
                  <strong>{{ formatSignedAmount(operation) }}</strong>
                </div>
              </td>
              <td>{{ formatMoney(operation.amount_usd) }} USD</td>
              <td>{{ formatOriginalAmount(operation) }}</td>
              <td class="operations-table__comment">{{ operation.comment || 'Без комментария' }}</td>
              <td>
                <div class="operations-row-actions">
                  <button class="icon-button" title="Редактировать" type="button" @click="startEdit(operation)">
                    <Pencil :size="18" aria-hidden="true" />
                  </button>
                  <button
                    class="icon-button icon-button--danger"
                    :disabled="state.deletingKey === operationKey(operation)"
                    title="Удалить"
                    type="button"
                    @click="deleteOperation(operation)"
                  >
                    <Trash2 :size="18" aria-hidden="true" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="operations-pagination">
        <span>{{ pageSummary }}</span>
        <div class="operations-pagination__actions">
          <button class="button button--fit" :disabled="!canGoBack || state.loading" type="button" @click="previousPage">
            <ChevronLeft :size="18" aria-hidden="true" />
            <span>Назад</span>
          </button>
          <button class="button button--fit" :disabled="!canGoNext || state.loading" type="button" @click="nextPage">
            <span>Вперед</span>
            <ChevronRight :size="18" aria-hidden="true" />
          </button>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup>
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { ChevronLeft, ChevronRight, Pencil, Plus, RefreshCcw, Trash2, X } from 'lucide-vue-next'

import {
  expenseCategoriesApi,
  expenseSubcategoriesApi,
  incomeCategoriesApi,
  incomeSubcategoriesApi,
} from '../api/categoriesApi'
import { deleteExpense, getExpenses } from '../api/expensesApi'
import { deleteIncome, getIncomes } from '../api/incomesApi'
import { getOperations } from '../api/operationsApi'
import { getErrorMessage } from '../utils/apiErrors'
import OperationForm from './OperationForm.vue'

const props = defineProps({
  fixedType: {
    type: String,
    default: '',
    validator: (value) => ['', 'income', 'expense'].includes(value),
  },
  title: {
    type: String,
    required: true,
  },
  eyebrow: {
    type: String,
    required: true,
  },
})

const typeLabels = {
  income: {
    one: 'доход',
    many: 'доходов',
    add: 'Добавить доход',
    newTitle: 'Новый доход',
    editTitle: 'Редактирование дохода',
    saved: 'Доход сохранен.',
    deleted: 'Доход удален.',
    loadError: 'Не удалось загрузить доходы.',
    deleteError: 'Не удалось удалить доход.',
    empty: 'Доходы не найдены.',
    loading: 'Загружаем доходы...',
  },
  expense: {
    one: 'расход',
    many: 'расходов',
    add: 'Добавить расход',
    newTitle: 'Новый расход',
    editTitle: 'Редактирование расхода',
    saved: 'Расход сохранен.',
    deleted: 'Расход удален.',
    loadError: 'Не удалось загрузить расходы.',
    deleteError: 'Не удалось удалить расход.',
    empty: 'Расходы не найдены.',
    loading: 'Загружаем расходы...',
  },
}

const emptyFilters = () => ({
  date_from: '',
  date_to: '',
  type: '',
  category_id: '',
  subcategory_id: '',
  limit: 50,
  offset: 0,
})

const state = reactive({
  operations: [],
  total: 0,
  loading: false,
  loadingDirectories: false,
  deletingKey: '',
  error: '',
  message: '',
  editorMode: '',
  editingOperation: null,
  filters: emptyFilters(),
  directories: {
    income: {
      categories: [],
      subcategories: [],
    },
    expense: {
      categories: [],
      subcategories: [],
    },
  },
})

let clearTimer = null
const operationForm = ref(null)

const activeFilterType = computed(() => props.fixedType || state.filters.type)
const activeLabels = computed(() => typeLabels[props.fixedType] || null)
const addLabel = computed(() => activeLabels.value?.add || 'Добавить')
const formFixedType = computed(() => props.fixedType || state.editingOperation?.type || '')
const editorTitle = computed(() => {
  if (!activeLabels.value) {
    return state.editorMode === 'edit' ? 'Редактирование операции' : 'Новая операция'
  }

  return state.editorMode === 'edit' ? activeLabels.value.editTitle : activeLabels.value.newTitle
})

const filteredCategories = computed(() => {
  if (!activeFilterType.value) {
    return []
  }

  return state.directories[activeFilterType.value].categories
})

const filteredSubcategories = computed(() => {
  if (!activeFilterType.value || !state.filters.category_id) {
    return []
  }

  return state.directories[activeFilterType.value].subcategories.filter(
    (subcategory) => String(subcategory.category_id) === String(state.filters.category_id),
  )
})

const canGoBack = computed(() => state.filters.offset > 0)
const canGoNext = computed(() => state.filters.offset + state.filters.limit < state.total)
const pageSummary = computed(() => {
  if (!state.total) {
    return `0 ${activeLabels.value?.many || 'операций'}`
  }

  const first = state.filters.offset + 1
  const last = Math.min(state.filters.offset + state.filters.limit, state.total)
  return `${first}-${last} из ${state.total}`
})

onMounted(async () => {
  await Promise.all([loadDirectories(), loadOperations()])
})

async function loadDirectories() {
  state.loadingDirectories = true

  try {
    if (props.fixedType === 'income') {
      const [incomeCategories, incomeSubcategories] = await Promise.all([
        incomeCategoriesApi.list(),
        incomeSubcategoriesApi.list(),
      ])
      state.directories.income.categories = incomeCategories
      state.directories.income.subcategories = incomeSubcategories
      return
    }

    if (props.fixedType === 'expense') {
      const [expenseCategories, expenseSubcategories] = await Promise.all([
        expenseCategoriesApi.list(),
        expenseSubcategoriesApi.list(),
      ])
      state.directories.expense.categories = expenseCategories
      state.directories.expense.subcategories = expenseSubcategories
      return
    }

    const [incomeCategories, incomeSubcategories, expenseCategories, expenseSubcategories] = await Promise.all([
      incomeCategoriesApi.list(),
      incomeSubcategoriesApi.list(),
      expenseCategoriesApi.list(),
      expenseSubcategoriesApi.list(),
    ])

    state.directories.income.categories = incomeCategories
    state.directories.income.subcategories = incomeSubcategories
    state.directories.expense.categories = expenseCategories
    state.directories.expense.subcategories = expenseSubcategories
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить справочники для фильтров.')
  } finally {
    state.loadingDirectories = false
  }
}

async function loadOperations() {
  state.loading = true
  state.error = ''

  try {
    const response = await getListApi()(buildOperationQuery())
    const items = response.items || []
    state.operations = props.fixedType ? items.map((operation) => ({ ...operation, type: props.fixedType })) : items
    state.total = response.total || 0
    state.filters.limit = response.limit || state.filters.limit
    state.filters.offset = response.offset || state.filters.offset
  } catch (error) {
    state.error = getErrorMessage(error, activeLabels.value?.loadError || 'Не удалось загрузить операции.')
  } finally {
    state.loading = false
  }
}

function getListApi() {
  if (props.fixedType === 'income') {
    return getIncomes
  }

  if (props.fixedType === 'expense') {
    return getExpenses
  }

  return getOperations
}

function buildOperationQuery() {
  const query = {
    date_from: state.filters.date_from,
    date_to: state.filters.date_to,
    category_id: state.filters.category_id,
    subcategory_id: state.filters.subcategory_id,
    limit: state.filters.limit,
    offset: state.filters.offset,
  }

  if (!props.fixedType) {
    query.type = state.filters.type
  }

  return query
}

function handleTypeFilterChange() {
  state.filters.category_id = ''
  state.filters.subcategory_id = ''
  applyFilters()
}

function handleCategoryFilterChange() {
  state.filters.subcategory_id = ''
  applyFilters()
}

function applyFilters() {
  state.filters.offset = 0
  loadOperations()
}

function resetFilters() {
  Object.assign(state.filters, emptyFilters())
  loadOperations()
}

function nextPage() {
  if (!canGoNext.value) {
    return
  }

  state.filters.offset += state.filters.limit
  loadOperations()
}

function previousPage() {
  if (!canGoBack.value) {
    return
  }

  state.filters.offset = Math.max(0, state.filters.offset - state.filters.limit)
  loadOperations()
}

async function startCreate() {
  state.editorMode = 'create'
  state.editingOperation = null
  await nextTick()
  await operationForm.value?.focusAmountInput()
}

function startEdit(operation) {
  state.editorMode = 'edit'
  state.editingOperation = operation
}

function closeEditor() {
  state.editorMode = ''
  state.editingOperation = null
}

async function handleSaved({ type }) {
  const wasCreate = state.editorMode === 'create'
  showMessage(typeLabels[type]?.saved || 'Операция сохранена.')
  if (!wasCreate) {
    closeEditor()
  }
  await Promise.all([loadDirectories(), loadOperations()])
}

async function deleteOperation(operation) {
  const operationType = getOperationType(operation)
  const operationName = typeLabels[operationType]?.one || 'операцию'
  if (!window.confirm(`Удалить ${operationName}?`)) {
    return
  }

  state.deletingKey = operationKey(operation)
  state.error = ''

  try {
    if (operationType === 'income') {
      await deleteIncome(operation.id)
    } else {
      await deleteExpense(operation.id)
    }

    showMessage(typeLabels[operationType]?.deleted || 'Операция удалена.')

    if (state.operations.length === 1 && state.filters.offset > 0) {
      state.filters.offset = Math.max(0, state.filters.offset - state.filters.limit)
    }

    await loadOperations()
  } catch (error) {
    state.error = getErrorMessage(error, typeLabels[operationType]?.deleteError || 'Не удалось удалить операцию.')
  } finally {
    state.deletingKey = ''
  }
}

function getCategoryName(operation) {
  const operationType = getOperationType(operation)
  return findDirectoryItem(operationType, 'categories', operation.category_id)?.name || `Категория #${operation.category_id}`
}

function getSubcategoryName(operation) {
  if (!operation.subcategory_id) {
    return ''
  }

  const operationType = getOperationType(operation)
  return (
    findDirectoryItem(operationType, 'subcategories', operation.subcategory_id)?.name ||
    `Подкатегория #${operation.subcategory_id}`
  )
}

function findDirectoryItem(type, resource, id) {
  return state.directories[type]?.[resource].find((item) => String(item.id) === String(id))
}

function getOperationType(operation) {
  return props.fixedType || operation.type
}

function operationKey(operation) {
  return `${getOperationType(operation)}-${operation.id}`
}

function formatDate(value) {
  if (!value) {
    return ''
  }

  return new Intl.DateTimeFormat('ru-RU').format(new Date(`${value}T00:00:00`))
}

function formatSignedAmount(operation) {
  const sign = getOperationType(operation) === 'income' ? '+' : '-'
  return `${sign}${formatMoney(operation.amount_rub)} RUB`
}

function formatOriginalAmount(operation) {
  if (!operation.original_amount || operation.currency_code === 'RUB') {
    return 'В RUB'
  }

  return `${formatMoney(operation.original_amount)} ${operation.currency_code}`
}

function formatMoney(value) {
  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(value || 0))
}

function showMessage(message) {
  state.message = message
  window.clearTimeout(clearTimer)
  clearTimer = window.setTimeout(() => {
    state.message = ''
  }, 3500)
}
</script>
