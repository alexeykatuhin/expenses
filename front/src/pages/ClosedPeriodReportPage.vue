<template>
  <section class="content-page content-page--wide">
    <header class="page__header">
      <div>
        <p class="page__eyebrow">Отчетный период</p>
        <h1>Агрегированный отчет</h1>
      </div>
      <button class="icon-button" :disabled="state.loading || state.closingPeriod" title="Обновить" type="button" @click="loadReport">
        <RefreshCcw :size="18" aria-hidden="true" />
      </button>
    </header>

    <section class="work-panel period-report-filters">
      <label class="field field--inline">
        <span>Период</span>
        <select v-model="state.selectedPeriodId" :disabled="state.loading || state.closingPeriod || !reportPeriods.length" @change="loadReport">
          <option v-for="period in reportPeriods" :key="period.id" :value="String(period.id)">
            {{ formatPeriodOption(period) }}
          </option>
        </select>
      </label>
      <div v-if="selectedPeriod" class="period-report-filters__meta">
        <span>{{ selectedPeriod.is_closed ? 'Закрыт' : 'Текущий' }}</span>
        <strong>{{ selectedPeriod.is_closed ? formatDateTime(selectedPeriod.closed_at) : 'Не закрыт' }}</strong>
      </div>
      <button
        v-if="activeReportingPeriod"
        class="button button--fit"
        :disabled="state.loading || state.closingPeriod || !canCloseActivePeriod"
        :title="closePeriodTitle"
        type="button"
        @click="closeActivePeriod"
      >
        <CalendarCheck :size="18" aria-hidden="true" />
        <span>{{ state.closingPeriod ? 'Закрываем...' : 'Закрыть период' }}</span>
      </button>
    </section>

    <p v-if="state.error" class="form-alert operations-alert">{{ state.error }}</p>

    <div v-if="state.loading" class="empty-state">Загружаем отчет по периоду...</div>
    <div v-else-if="!reportPeriods.length" class="empty-state">
      Отчетных периодов пока нет.
    </div>
    <template v-else>
      <div class="metric-grid" aria-label="Сводка закрытого периода">
        <article class="metric-card">
          <span class="metric-card__label">Доходы</span>
          <strong>{{ formatMoney(totals.incomeRub) }} RUB</strong>
          <small>{{ formatMoney(totals.incomeUsd) }} USD</small>
        </article>
        <article class="metric-card">
          <span class="metric-card__label">Расходы</span>
          <strong>{{ formatMoney(totals.expenseRub) }} RUB</strong>
          <small>{{ formatMoney(totals.expenseUsd) }} USD</small>
        </article>
        <article class="metric-card" :class="spendingShareClass">
          <span class="metric-card__label">Потрачено от дохода</span>
          <strong>{{ formatPercent(spendingShare.rub) }}</strong>
          <small>{{ formatPercent(spendingShare.usd) }} в USD</small>
        </article>
        <article class="metric-card" :class="balanceClass">
          <span class="metric-card__label">Баланс</span>
          <strong>{{ formatSignedMoney(totals.balanceRub) }} RUB</strong>
          <small>{{ formatSignedMoney(totals.balanceUsd) }} USD</small>
        </article>
      </div>

      <div class="tabs period-report-tabs" role="tablist" aria-label="Тип операций">
        <button
          v-for="tab in tabs"
          :key="tab.type"
          class="tabs__button"
          :class="{ 'tabs__button--active': state.activeTab === tab.type }"
          type="button"
          role="tab"
          :aria-selected="state.activeTab === tab.type"
          @click="state.activeTab = tab.type"
        >
          <component :is="tab.icon" :size="18" aria-hidden="true" />
          <span>{{ tab.label }}</span>
        </button>
      </div>

      <section class="work-panel period-report-chart" :aria-label="`Столбчатая диаграмма: ${activeTabLabel}`">
        <div class="report-panel__header">
          <h2>{{ activeTabLabel }} по категориям</h2>
          <span>{{ chartRows.length }}</span>
        </div>

        <div v-if="!chartRows.length" class="empty-state">
          {{ emptyActiveTabMessage }}
        </div>
        <div v-else class="bar-chart" role="list">
          <div v-for="item in chartRows" :key="item.key" class="bar-chart__row" role="listitem">
            <div class="bar-chart__label">
              <strong>{{ item.name }}</strong>
              <span>{{ getShare(item.amountRub, activeTotalRub) }}% от суммы</span>
            </div>
            <div class="bar-chart__track" aria-hidden="true">
              <span
                class="bar-chart__bar"
                :class="`bar-chart__bar--${state.activeTab}`"
                :style="{ width: `${item.share}%` }"
              ></span>
            </div>
            <strong class="bar-chart__amount">{{ formatMoney(item.amountRub) }} RUB</strong>
          </div>
        </div>
      </section>

      <section class="work-panel report-panel deviation-panel" aria-label="Отклонения расходов от среднего за год">
        <div class="report-panel__header">
          <h2>Отклонения расходов от среднего за год</h2>
          <span>{{ expenseDeviationRows.length }}</span>
        </div>

        <div v-if="!expenseDeviationRows.length" class="empty-state">
          Недостаточно данных для сравнения расходов за последний год.
        </div>
        <template v-else>
          <div class="deviation-summary" aria-label="Сводка отклонений расходов">
            <article class="metric-card" :class="totalExpenseDeviationClass">
              <span class="metric-card__label">Текущий период</span>
              <strong>{{ formatMoney(totals.expenseRub) }} RUB</strong>
              <small>{{ formatDeviation(totalExpenseDeviation) }} к среднему</small>
            </article>
            <article class="metric-card">
              <span class="metric-card__label">Среднее за год</span>
              <strong>{{ formatMoney(totalAverageExpenseRub) }} RUB</strong>
              <small>{{ expenseDeviationBaselineCount }} периодов в сравнении</small>
            </article>
            <article class="metric-card" :class="largestExpenseIncrease ? 'metric-card--negative' : ''">
              <span class="metric-card__label">Самый большой рост</span>
              <strong>{{ largestExpenseIncrease?.name || '-' }}</strong>
              <small>{{ largestExpenseIncrease ? formatDeviation(largestExpenseIncrease.deviationRub) : 'Нет отклонений' }}</small>
            </article>
          </div>

          <div class="deviation-charts">
            <div class="deviation-chart-group">
              <div class="chart-card__header">
                <h3>Выше или на уровне среднего</h3>
                <span>{{ positiveExpenseDeviationRows.length }}</span>
              </div>
              <div v-if="!positiveExpenseDeviationRows.length" class="empty-state empty-state--compact">
                Категорий с ростом расходов нет.
              </div>
              <div v-else class="deviation-chart deviation-chart--over" role="list">
                <div v-for="item in positiveExpenseDeviationRows" :key="item.key" class="deviation-chart__row" role="listitem">
                  <div class="deviation-chart__label">
                    <strong>{{ item.name }}</strong>
                    <span>
                      {{ formatMoney(item.amountRub) }} RUB сейчас · {{ formatMoney(item.averageRub) }} RUB в среднем
                    </span>
                  </div>
                  <div class="deviation-chart__track" aria-hidden="true">
                    <span class="deviation-chart__bar deviation-chart__bar--over" :style="getDeviationBarStyle(item)"></span>
                  </div>
                  <div class="deviation-chart__amount deviation-chart__amount--over">
                    <strong>{{ formatDeviation(item.deviationRub) }}</strong>
                    <span>{{ formatDeviationPercent(item.deviationPercent) }}</span>
                  </div>
                </div>
              </div>
            </div>

            <div class="deviation-chart-group">
              <div class="chart-card__header">
                <h3>Ниже среднего</h3>
                <span>{{ negativeExpenseDeviationRows.length }}</span>
              </div>
              <div v-if="!negativeExpenseDeviationRows.length" class="empty-state empty-state--compact">
                Категорий со снижением расходов нет.
              </div>
              <div v-else class="deviation-chart deviation-chart--under" role="list">
                <div v-for="item in negativeExpenseDeviationRows" :key="item.key" class="deviation-chart__row" role="listitem">
                  <div class="deviation-chart__label">
                    <strong>{{ item.name }}</strong>
                    <span>
                      {{ formatMoney(item.amountRub) }} RUB сейчас · {{ formatMoney(item.averageRub) }} RUB в среднем
                    </span>
                  </div>
                  <div class="deviation-chart__track" aria-hidden="true">
                    <span class="deviation-chart__bar deviation-chart__bar--under" :style="getDeviationBarStyle(item)"></span>
                  </div>
                  <div class="deviation-chart__amount deviation-chart__amount--under">
                    <strong>{{ formatDeviation(item.deviationRub) }}</strong>
                    <span>{{ formatDeviationPercent(item.deviationPercent) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>
      </section>

      <section class="work-panel report-panel">
        <div class="report-panel__header">
          <h2>{{ activeTabLabel }}</h2>
          <span>{{ activeRows.length }}</span>
        </div>

        <div v-if="!activeRows.length" class="empty-state">{{ emptyActiveTabMessage }}</div>
        <div v-else class="operations-table-wrap">
          <table class="operations-table period-report-table">
            <thead>
              <tr>
                <th>Категория</th>
                <th>Сумма RUB</th>
                <th>Сумма USD</th>
                <th>Доля</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in activeRows" :key="item.key">
                <td>{{ item.name }}</td>
                <td><strong>{{ formatMoney(item.amountRub) }}</strong></td>
                <td>{{ formatMoney(item.amountUsd) }}</td>
                <td>{{ getShare(item.amountRub, activeTotalRub) }}%</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </section>
</template>

<script setup>
import { computed, onMounted, reactive } from 'vue'
import { Banknote, CalendarCheck, CreditCard, RefreshCcw } from 'lucide-vue-next'

import { expenseCategoriesApi, incomeCategoriesApi } from '../api/categoriesApi'
import { closeReportingPeriod, getActiveReportingPeriod, getReportingPeriods } from '../api/reportingPeriodsApi'
import { getReportByCategory, getReportByPeriodCategory, getSummary } from '../api/reportsApi'
import { getErrorMessage } from '../utils/apiErrors'

const state = reactive({
  loading: false,
  error: '',
  closingPeriod: false,
  periods: [],
  selectedPeriodId: '',
  activeTab: 'expense',
  reports: {
    income: [],
    expense: [],
    expenseHistory: [],
  },
  directories: {
    income: [],
    expense: [],
  },
  summary: emptySummary(),
})
const tabs = [
  { type: 'expense', label: 'Расходы', icon: CreditCard },
  { type: 'income', label: 'Доходы', icon: Banknote },
]

const reportPeriods = computed(() =>
  state.periods
    .filter(Boolean)
    .sort((left, right) => parseLocalDate(right.end_date) - parseLocalDate(left.end_date)),
)
const selectedPeriod = computed(() =>
  reportPeriods.value.find((period) => String(period.id) === String(state.selectedPeriodId)) || null,
)
const activeReportingPeriod = computed(() => reportPeriods.value.find((period) => !period.is_closed) || null)
const canCloseActivePeriod = computed(() => Boolean(activeReportingPeriod.value) && isPeriodEnded(activeReportingPeriod.value))
const closePeriodTitle = computed(() => {
  if (!activeReportingPeriod.value) {
    return ''
  }

  return canCloseActivePeriod.value
    ? 'Закрыть отчетный период'
    : `Период можно закрыть с ${formatDate(addDays(parseLocalDate(activeReportingPeriod.value.end_date), 1))}`
})
const incomeRows = computed(() => buildRows('income'))
const expenseRows = computed(() => buildRows('expense'))
const activeRows = computed(() => (state.activeTab === 'income' ? incomeRows.value : expenseRows.value))
const activeTotalRub = computed(() => (state.activeTab === 'income' ? totals.value.incomeRub : totals.value.expenseRub))
const activeTabLabel = computed(() => (state.activeTab === 'income' ? 'Доходы' : 'Расходы'))
const emptyActiveTabMessage = computed(() => `${state.activeTab === 'income' ? 'Доходов' : 'Расходов'} за период нет.`)
const totals = computed(() => ({
  incomeRub: Number(state.summary.income.amount_rub || 0),
  incomeUsd: Number(state.summary.income.amount_usd || 0),
  expenseRub: Number(state.summary.expense.amount_rub || 0),
  expenseUsd: Number(state.summary.expense.amount_usd || 0),
  balanceRub: Number(state.summary.balance.amount_rub || 0),
  balanceUsd: Number(state.summary.balance.amount_usd || 0),
}))
const chartRows = computed(() => {
  const rows = activeRows.value
  const maxAmount = Math.max(...rows.map((item) => item.amountRub), 0)

  return rows.map((item) => ({
    ...item,
    share: getChartShare(item.amountRub, maxAmount),
  }))
})
const expenseDeviationBaselineCount = computed(() => getBaselinePeriodStarts().length)
const allExpenseDeviationRows = computed(() => {
  const baselinePeriods = getBaselinePeriodStarts()
  if (!baselinePeriods.length) {
    return []
  }

  const baselineCount = baselinePeriods.length
  const historyByCategory = state.reports.expenseHistory.reduce((acc, item) => {
    const periodKey = getPeriodKey(item.period_start)
    if (!baselinePeriods.includes(periodKey)) {
      return acc
    }

    getExpenseDeviationCategories(item.id).forEach((category) => {
      acc.set(category.key, (acc.get(category.key) || 0) + Number(item.amount_rub || 0))
    })
    return acc
  }, new Map())
  const currentByCategory = state.reports.expense.reduce((acc, item) => {
    getExpenseDeviationCategories(item.id).forEach((category) => {
      const current = acc.get(category.key) || {
        key: category.key,
        name: category.name,
        amountRub: 0,
        amountUsd: 0,
        isPseudo: category.isPseudo,
      }

      current.amountRub += Number(item.amount_rub || 0)
      current.amountUsd += Number(item.amount_usd || 0)
      acc.set(category.key, current)
    })

    return acc
  }, new Map())
  const categoryKeys = new Set([...historyByCategory.keys(), ...currentByCategory.keys()])
  const maxAbsDeviation = Math.max(
    ...Array.from(categoryKeys).map((key) => {
      const current = currentByCategory.get(key)?.amountRub || 0
      const average = (historyByCategory.get(key) || 0) / baselineCount
      return Math.abs(current - average)
    }),
    0,
  )

  return Array.from(categoryKeys)
    .map((key) => {
      const current = currentByCategory.get(key)
      const amountRub = current?.amountRub || 0
      const averageRub = (historyByCategory.get(key) || 0) / baselineCount
      const deviationRub = amountRub - averageRub

      return {
        key,
        name: current?.name || getExpenseDeviationCategoryName(key),
        amountRub,
        averageRub,
        deviationRub,
        deviationPercent: averageRub ? (deviationRub / averageRub) * 100 : null,
        isPseudo: Boolean(current?.isPseudo) || key === 'food',
        share: getChartShare(Math.abs(deviationRub), maxAbsDeviation),
      }
    })
    .filter((item) => item.amountRub > 0 || item.averageRub > 0)
    .sort((left, right) => Math.abs(right.deviationRub) - Math.abs(left.deviationRub))
})
const expenseDeviationRows = computed(() => allExpenseDeviationRows.value)
const positiveExpenseDeviationRows = computed(() =>
  expenseDeviationRows.value
    .filter((item) => item.deviationRub >= 0)
    .sort((left, right) => right.deviationRub - left.deviationRub),
)
const negativeExpenseDeviationRows = computed(() =>
  expenseDeviationRows.value
    .filter((item) => item.deviationRub < 0)
    .sort((left, right) => left.deviationRub - right.deviationRub),
)
const totalAverageExpenseRub = computed(() =>
  allExpenseDeviationRows.value
    .filter((item) => !item.isPseudo)
    .reduce((sum, item) => sum + item.averageRub, 0),
)
const totalExpenseDeviation = computed(() => totals.value.expenseRub - totalAverageExpenseRub.value)
const totalExpenseDeviationClass = computed(() => ({
  'metric-card--positive': totalExpenseDeviation.value <= 0,
  'metric-card--negative': totalExpenseDeviation.value > 0,
}))
const largestExpenseIncrease = computed(() =>
  positiveExpenseDeviationRows.value.find((item) => item.deviationRub > 0) || null,
)
const balanceClass = computed(() => ({
  'metric-card--positive': totals.value.balanceRub >= 0,
  'metric-card--negative': totals.value.balanceRub < 0,
}))
const spendingShare = computed(() => ({
  rub: getRatioPercent(totals.value.expenseRub, totals.value.incomeRub),
  usd: getRatioPercent(totals.value.expenseUsd, totals.value.incomeUsd),
}))
const spendingShareClass = computed(() => ({
  'metric-card--positive': spendingShare.value.rub <= 100,
  'metric-card--negative': spendingShare.value.rub > 100,
}))

onMounted(initializePage)

async function initializePage() {
  state.loading = true
  state.error = ''

  try {
    const [periods, activePeriod, incomeDirectories, expenseDirectories] = await Promise.all([
      getReportingPeriods(),
      getActiveReportingPeriod(),
      incomeCategoriesApi.list(),
      expenseCategoriesApi.list(),
    ])

    state.periods = mergePeriods(Array.isArray(periods) ? periods : [], activePeriod)
    state.directories.income = incomeDirectories
    state.directories.expense = expenseDirectories
    state.selectedPeriodId = getDefaultPeriodId()

    await loadReport()
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить отчетные периоды.')
  } finally {
    state.loading = false
  }
}

async function loadReport() {
  if (!selectedPeriod.value) {
    state.reports.income = []
    state.reports.expense = []
    state.reports.expenseHistory = []
    state.summary = emptySummary()
    return
  }

  state.loading = true
  state.error = ''

  try {
    const query = {
      date_from: selectedPeriod.value.start_date,
      date_to: selectedPeriod.value.end_date,
      period_id: selectedPeriod.value.id,
    }
    const historyQuery = {
      date_from: formatIsoDate(addMonths(parseLocalDate(selectedPeriod.value.start_date), -12)),
      date_to: formatIsoDate(addDays(parseLocalDate(selectedPeriod.value.start_date), -1)),
      type: 'expense',
    }
    const [summaryResponse, incomeReport, expenseReport, expenseHistoryReport] = await Promise.all([
      getSummary(query),
      getReportByCategory({ ...query, type: 'income' }),
      getReportByCategory({ ...query, type: 'expense' }),
      getReportByPeriodCategory(historyQuery),
    ])

    state.summary = normalizeSummary(summaryResponse)
    state.reports.income = Array.isArray(incomeReport) ? incomeReport : []
    state.reports.expense = Array.isArray(expenseReport) ? expenseReport : []
    state.reports.expenseHistory = Array.isArray(expenseHistoryReport) ? expenseHistoryReport : []
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить отчет по выбранному периоду.')
  } finally {
    state.loading = false
  }
}

async function closeActivePeriod() {
  if (!activeReportingPeriod.value) {
    return
  }

  if (!isPeriodEnded(activeReportingPeriod.value)) {
    state.error = 'Период можно закрыть не раньше даты окончания.'
    return
  }

  const closedPeriodId = String(activeReportingPeriod.value.id)
  if (!window.confirm(getClosePeriodConfirmation(activeReportingPeriod.value))) {
    return
  }

  state.closingPeriod = true
  state.error = ''

  try {
    await closeReportingPeriod(activeReportingPeriod.value.id)
    await reloadPeriods()
    state.selectedPeriodId = reportPeriods.value.some((period) => String(period.id) === closedPeriodId)
      ? closedPeriodId
      : reportPeriods.value[0]
        ? String(reportPeriods.value[0].id)
        : ''
    await loadReport()
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось закрыть отчетный период.')
  } finally {
    state.closingPeriod = false
  }
}

async function reloadPeriods() {
  const [periods, activePeriod] = await Promise.all([
    getReportingPeriods(),
    getActiveReportingPeriod(),
  ])

  state.periods = mergePeriods(Array.isArray(periods) ? periods : [], activePeriod)
}

function buildRows(type) {
  return state.reports[type]
    .map((item) => ({
      key: String(item.id ?? 'none'),
      name: getCategoryName(type, item.id),
      amountRub: Number(item.amount_rub || 0),
      amountUsd: Number(item.amount_usd || 0),
    }))
    .sort((left, right) => right.amountRub - left.amountRub)
}

function getCategoryName(type, id) {
  if (id === null || id === undefined) {
    return 'Без категории'
  }

  return state.directories[type].find((category) => String(category.id) === String(id))?.name || `Категория #${id}`
}

function normalizeSummary(value) {
  const fallback = emptySummary()

  return {
    income: value?.income || fallback.income,
    expense: value?.expense || fallback.expense,
    balance: value?.balance || fallback.balance,
  }
}

function mergePeriods(periods, activePeriod) {
  if (!activePeriod?.id) {
    return periods
  }

  const byId = new Map(periods.map((period) => [String(period.id), period]))
  byId.set(String(activePeriod.id), activePeriod)
  return Array.from(byId.values())
}

function getDefaultPeriodId() {
  const period = reportPeriods.value.find((item) => item.is_closed) || reportPeriods.value[0]
  return period ? String(period.id) : ''
}

function emptySummary() {
  return {
    income: { amount_rub: '0.00', amount_usd: '0.00' },
    expense: { amount_rub: '0.00', amount_usd: '0.00' },
    balance: { amount_rub: '0.00', amount_usd: '0.00' },
  }
}

function getShare(amount, total) {
  if (!total) {
    return 0
  }

  return Math.min(100, Math.round((Number(amount || 0) / total) * 100))
}

function getChartShare(amount, maxAmount) {
  if (!maxAmount || !amount) {
    return 0
  }

  return Math.max(4, Math.min(100, Math.round((amount / maxAmount) * 100)))
}

function getDeviationBarStyle(item) {
  return { width: `${item.share}%` }
}

function getExpenseDeviationCategories(id) {
  const name = getCategoryName('expense', id)
  const key = String(id ?? 'none')
  const categories = [
    {
      key,
      name,
      isPseudo: false,
    },
  ]

  if (isFoodCategoryName(name)) {
    categories.push({
      key: 'food',
      name: 'food',
      isPseudo: true,
    })
  }

  return categories
}

function getExpenseDeviationCategoryName(key) {
  if (key === 'food') {
    return 'food'
  }

  return getCategoryName('expense', key === 'none' ? null : key)
}

function isFoodCategoryName(name) {
  return ['cafe', 'grocery'].includes(normalizeCategoryKey(name))
}

function normalizeCategoryKey(value) {
  return String(value || '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
}

function getBaselinePeriodStarts() {
  if (!selectedPeriod.value) {
    return []
  }

  const selectedStart = parseLocalDate(selectedPeriod.value.start_date)
  if (!selectedStart) {
    return []
  }

  return Array.from({ length: 12 }, (_, index) => getPeriodKey(addMonths(selectedStart, index - 12)))
}

function getRatioPercent(value, total) {
  if (!total) {
    return 0
  }

  return Math.round((Number(value || 0) / Number(total || 0)) * 100)
}

function isPeriodEnded(period) {
  const endDate = parseLocalDate(period?.end_date)
  if (!endDate) {
    return false
  }

  return startOfToday().getTime() > endDate.getTime()
}

function startOfToday() {
  const today = new Date()
  return new Date(today.getFullYear(), today.getMonth(), today.getDate())
}

function parseLocalDate(value) {
  if (!value) {
    return null
  }

  const [year, month, day] = String(value).split('-').map(Number)
  if (!year || !month || !day) {
    return null
  }

  return new Date(year, month - 1, day || 1)
}

function addMonths(date, months) {
  if (!date) {
    return null
  }

  return new Date(date.getFullYear(), date.getMonth() + months, date.getDate())
}

function addDays(date, days) {
  if (!date) {
    return null
  }

  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days)
}

function getPeriodKey(value) {
  return formatIsoDate(typeof value === 'string' ? parseLocalDate(value) : value)
}

function formatIsoDate(date) {
  if (!date) {
    return ''
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatPeriodRange(period) {
  return `${formatDate(period.start_date)} - ${formatDate(period.end_date)}`
}

function formatPeriodOption(period) {
  const status = period.is_closed ? '' : ' (текущий)'
  return `${formatPeriodRange(period)}${status}`
}

function getClosePeriodConfirmation(period) {
  return `Закрыть отчетный период ${formatPeriodRange(period)}? После закрытия операции этого периода нельзя будет изменить.`
}

function formatDate(value) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat('ru-RU').format(parseLocalDate(value))
}

function formatDateTime(value) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat('ru-RU', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function formatMoney(value) {
  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(value || 0))
}

function formatSignedMoney(value) {
  const amount = Number(value || 0)
  const sign = amount > 0 ? '+' : ''
  return `${sign}${formatMoney(amount)}`
}

function formatDeviation(value) {
  return `${formatSignedMoney(value)} RUB`
}

function formatDeviationPercent(value) {
  if (value === null || value === undefined) {
    return 'новая трата'
  }

  return `${value > 0 ? '+' : ''}${formatPercent(value)}`
}

function formatPercent(value) {
  return `${new Intl.NumberFormat('ru-RU', {
    maximumFractionDigits: 0,
  }).format(Number(value || 0))}%`
}
</script>
