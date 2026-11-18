<template>
  <section class="content-page content-page--wide">
    <header class="page__header">
      <div>
        <p class="page__eyebrow">Family Expenses</p>
        <h1>Обзор бюджета</h1>
      </div>
      <button class="icon-button" :disabled="state.loading" title="Обновить" type="button" @click="loadDashboard">
        <RefreshCcw :size="18" aria-hidden="true" />
      </button>
    </header>

    <section class="work-panel dashboard-filters">
      <label class="field field--inline">
        <span>С</span>
        <input v-model="state.filters.period_from" type="month" @change="loadDashboard" />
      </label>
      <label class="field field--inline">
        <span>По</span>
        <input v-model="state.filters.period_to" type="month" @change="loadDashboard" />
      </label>
      <div class="dashboard-filters__actions">
        <button class="button button--fit" :disabled="state.loading" type="button" @click="setCurrentPeriod">
          Текущий период
        </button>
      </div>
    </section>

    <p v-if="state.error" class="form-alert operations-alert">{{ state.error }}</p>

    <div class="metric-grid" aria-label="Сводка бюджета">
      <article class="metric-card">
        <span class="metric-card__label">Доходы</span>
        <strong>{{ formatMoney(summary.income.amount_rub) }} RUB</strong>
        <small>{{ formatMoney(summary.income.amount_usd) }} USD</small>
      </article>
      <article class="metric-card">
        <span class="metric-card__label">Расходы</span>
        <strong>{{ formatMoney(summary.expense.amount_rub) }} RUB</strong>
        <small>{{ formatMoney(summary.expense.amount_usd) }} USD</small>
      </article>
      <article class="metric-card" :class="balanceClass">
        <span class="metric-card__label">Баланс</span>
        <strong>{{ formatSignedMoney(summary.balance.amount_rub) }} RUB</strong>
        <small>{{ formatSignedMoney(summary.balance.amount_usd) }} USD</small>
      </article>
    </div>

    <section class="work-panel chart-panel" aria-label="Budget charts">
      <div class="report-panel__header">
        <h2>Графики доходов и расходов</h2>
      </div>

      <div v-if="state.loading" class="empty-state">Загружаем графики...</div>
      <div v-else class="chart-grid">
        <article class="chart-card chart-card--wide">
          <header class="chart-card__header">
            <div>
              <h3>Накопления за 12 месяцев</h3>
              <p class="chart-card__description">Доходы минус расходы за каждый месяц</p>
            </div>
            <strong
              class="savings-chart__total"
              :class="savingsChart.total >= 0 ? 'savings-chart__total--positive' : 'savings-chart__total--negative'"
            >
              {{ formatSignedMoney(savingsChart.total) }} RUB
            </strong>
          </header>
          <div v-if="!savingsChart.hasData" class="empty-state empty-state--compact">
            За последние 12 месяцев доходов и расходов нет.
          </div>
          <div v-else class="line-chart-wrap">
            <svg class="line-chart savings-chart" viewBox="0 0 760 340" role="img" aria-label="Накопления по месяцам за последние 12 месяцев">
              <line class="line-chart__axis" :x1="chartBounds.left" :y1="chartBounds.top" :x2="chartBounds.left" :y2="chartBounds.bottom" />
              <g v-for="tick in savingsChart.yTicks" :key="`savings-y-${tick.value}`">
                <line
                  :class="tick.value === 0 ? 'savings-chart__zero' : 'line-chart__grid'"
                  :x1="chartBounds.left"
                  :y1="tick.y"
                  :x2="chartBounds.right"
                  :y2="tick.y"
                />
                <text class="line-chart__tick line-chart__tick--y" :x="chartBounds.left - 12" :y="tick.y + 4">
                  {{ formatCompactMoney(tick.value) }}
                </text>
              </g>
              <g v-for="bar in savingsChart.bars" :key="`savings-${bar.key}`">
                <rect
                  class="savings-chart__bar"
                  :class="bar.value >= 0 ? 'savings-chart__bar--positive' : 'savings-chart__bar--negative'"
                  :x="bar.x"
                  :y="bar.y"
                  :width="bar.width"
                  :height="bar.height"
                  rx="4"
                >
                  <title>{{ bar.label }}: {{ formatSignedMoney(bar.value) }} RUB (доходы {{ formatMoney(bar.income) }}, расходы {{ formatMoney(bar.expense) }})</title>
                </rect>
                <text
                  class="savings-chart__amount"
                  :class="bar.value >= 0 ? 'savings-chart__amount--positive' : 'savings-chart__amount--negative'"
                  :x="bar.labelX"
                  :y="bar.amountLabelY"
                >
                  {{ formatCompactMoney(bar.value) }}
                </text>
                <text class="line-chart__tick line-chart__tick--x" :x="bar.labelX" y="300">
                  <tspan :x="bar.labelX">{{ bar.monthLabel }}</tspan>
                  <tspan class="line-chart__tick-year" :x="bar.labelX" dy="14">{{ bar.yearLabel }}</tspan>
                </text>
              </g>
              <line
                class="savings-chart__average"
                :x1="chartBounds.left"
                :y1="savingsChart.averageY"
                :x2="chartBounds.right"
                :y2="savingsChart.averageY"
              >
                <title>Среднее накопление за 12 месяцев: {{ formatSignedMoney(savingsChart.average) }} RUB</title>
              </line>
            </svg>
            <div class="savings-chart__legend" aria-hidden="true">
              <span><i class="savings-chart__legend-dot savings-chart__legend-dot--positive"></i>Положительные накопления</span>
              <span><i class="savings-chart__legend-dot savings-chart__legend-dot--negative"></i>Перерасход</span>
              <span><i class="savings-chart__legend-line"></i>Среднее: {{ formatSignedMoney(savingsChart.average) }} RUB</span>
            </div>
          </div>
        </article>

        <article class="chart-card chart-card--wide">
          <header class="chart-card__header">
            <h3>Расходы по категориям по периодам</h3>
            <span>food = cafe + grocery</span>
          </header>
          <div v-if="!expensePeriodChart.series.length" class="empty-state empty-state--compact">
            За выбранный период расходов нет.
          </div>
          <div v-else class="line-chart-wrap">
            <div class="line-chart-stage" @mouseleave="hideExpensePointTooltip">
              <svg class="line-chart" viewBox="0 0 760 340" role="img" aria-label="Расходы по категориям по периодам">
                <line class="line-chart__axis" :x1="chartBounds.left" :y1="chartBounds.top" :x2="chartBounds.left" :y2="chartBounds.bottom" />
                <line class="line-chart__axis" :x1="chartBounds.left" :y1="chartBounds.bottom" :x2="chartBounds.right" :y2="chartBounds.bottom" />
                <g v-for="tick in expensePeriodChart.yTicks" :key="`expense-y-${tick.value}`">
                  <line class="line-chart__grid" :x1="chartBounds.left" :y1="tick.y" :x2="chartBounds.right" :y2="tick.y" />
                  <text class="line-chart__tick line-chart__tick--y" :x="chartBounds.left - 12" :y="tick.y + 4">{{ formatCompactMoney(tick.value) }}</text>
                </g>
                <g v-for="period in expensePeriodChart.periods" :key="`expense-x-${period.key}`">
                  <line class="line-chart__grid line-chart__grid--x" :x1="period.x" :y1="chartBounds.top" :x2="period.x" :y2="chartBounds.bottom" />
                  <text class="line-chart__tick line-chart__tick--x" :x="period.x" y="300">
                    <tspan :x="period.x">{{ period.monthLabel }}</tspan>
                    <tspan class="line-chart__tick-year" :x="period.x" dy="14">{{ period.yearLabel }}</tspan>
                  </text>
                </g>
                <g v-for="line in expensePeriodChart.visibleSeries" :key="`expense-line-${line.key}`">
                  <path class="line-chart__line" :d="line.path" :stroke="line.color" />
                  <circle
                    v-for="point in line.points"
                    :key="`expense-point-${line.key}-${point.periodKey}`"
                    class="line-chart__point"
                    :cx="point.x"
                    :cy="point.y"
                    r="3.5"
                    :fill="line.color"
                    tabindex="0"
                    @focus="showExpensePointTooltip(line, point)"
                    @mouseenter="showExpensePointTooltip(line, point)"
                    @click="selectLineSeries('expense', line.key)"
                    @blur="hideExpensePointTooltip"
                  />
                </g>
              </svg>
              <div
                v-if="state.expensePointTooltip"
                class="line-chart__tooltip"
                :style="{
                  left: `${state.expensePointTooltip.left}%`,
                  top: `${state.expensePointTooltip.top}%`,
                }"
              >
                <strong>{{ state.expensePointTooltip.category }}</strong>
                <span>{{ formatMoney(state.expensePointTooltip.amount) }} RUB</span>
              </div>
            </div>
            <div class="line-chart__legend">
              <button
                v-for="line in expensePeriodChart.series"
                :key="`expense-legend-${line.key}`"
                class="line-chart__legend-item"
                :class="{
                  'line-chart__legend-item--hidden': line.hidden,
                  'line-chart__legend-item--selected': line.selected,
                }"
                :aria-pressed="line.selected"
                type="button"
                @click="selectLineSeries('expense', line.key)"
              >
                <i :style="{ backgroundColor: line.color }"></i>{{ line.name }}
              </button>
            </div>
          </div>
        </article>

        <article class="chart-card chart-card--wide">
          <header class="chart-card__header">
            <h3>Доходы по категориям по периодам</h3>
            <span>RUB</span>
          </header>
          <div v-if="!incomePeriodChart.series.length" class="empty-state empty-state--compact">
            За выбранный период доходов нет.
          </div>
          <div v-else class="line-chart-wrap">
            <svg class="line-chart" viewBox="0 0 760 340" role="img" aria-label="Доходы по категориям по периодам">
              <line class="line-chart__axis" :x1="chartBounds.left" :y1="chartBounds.top" :x2="chartBounds.left" :y2="chartBounds.bottom" />
              <line class="line-chart__axis" :x1="chartBounds.left" :y1="chartBounds.bottom" :x2="chartBounds.right" :y2="chartBounds.bottom" />
              <g v-for="tick in incomePeriodChart.yTicks" :key="`income-y-${tick.value}`">
                <line class="line-chart__grid" :x1="chartBounds.left" :y1="tick.y" :x2="chartBounds.right" :y2="tick.y" />
                <text class="line-chart__tick line-chart__tick--y" :x="chartBounds.left - 12" :y="tick.y + 4">{{ formatCompactMoney(tick.value) }}</text>
              </g>
              <g v-for="period in incomePeriodChart.periods" :key="`income-x-${period.key}`">
                <line class="line-chart__grid line-chart__grid--x" :x1="period.x" :y1="chartBounds.top" :x2="period.x" :y2="chartBounds.bottom" />
                <text class="line-chart__tick line-chart__tick--x" :x="period.x" y="300">
                  <tspan :x="period.x">{{ period.monthLabel }}</tspan>
                  <tspan class="line-chart__tick-year" :x="period.x" dy="14">{{ period.yearLabel }}</tspan>
                </text>
              </g>
              <g v-for="line in incomePeriodChart.visibleSeries" :key="`income-line-${line.key}`">
                <path class="line-chart__line" :d="line.path" :stroke="line.color" />
                <circle
                  v-for="point in line.points"
                  :key="`income-point-${line.key}-${point.periodKey}`"
                  class="line-chart__point"
                  :cx="point.x"
                  :cy="point.y"
                  r="3.5"
                  :fill="line.color"
                />
              </g>
            </svg>
            <div class="line-chart__legend">
              <button
                v-for="line in incomePeriodChart.series"
                :key="`income-legend-${line.key}`"
                class="line-chart__legend-item"
                :class="{
                  'line-chart__legend-item--hidden': line.hidden,
                  'line-chart__legend-item--selected': line.selected,
                }"
                :aria-pressed="line.selected"
                type="button"
                @click="selectLineSeries('income', line.key)"
              >
                <i :style="{ backgroundColor: line.color }"></i>{{ line.name }}
              </button>
            </div>
          </div>
        </article>

        <article class="chart-card chart-card--wide">
          <header class="chart-card__header">
            <h3>Суммарно</h3>
            <span>RUB</span>
          </header>
          <div class="summary-chart" role="list">
            <div v-for="item in summaryChartRows" :key="item.type" class="summary-chart__row" role="listitem">
              <div class="summary-chart__label">
                <strong>{{ item.label }}</strong>
                <span>{{ formatMoney(item.amountRub) }} RUB</span>
              </div>
              <div class="summary-chart__track" aria-hidden="true">
                <span :class="`summary-chart__bar summary-chart__bar--${item.type}`" :style="{ width: `${item.share}%` }"></span>
              </div>
            </div>
          </div>
        </article>

        <article class="chart-card">
          <header class="chart-card__header">
            <h3>Расходы по категориям</h3>
            <span>food = cafe + grocery</span>
          </header>
          <div v-if="!expenseCategoryChartRows.length" class="empty-state empty-state--compact">
            За выбранный период расходов нет.
          </div>
          <div v-else class="category-chart" role="list">
            <div v-for="item in expenseCategoryChartRows" :key="`expense-chart-${item.key}`" class="category-chart__row" role="listitem">
              <div class="category-chart__label">
                <strong>{{ item.name }}</strong>
                <span>{{ formatMoney(item.amountRub) }} RUB</span>
              </div>
              <div class="category-chart__track" aria-hidden="true">
                <span class="category-chart__bar category-chart__bar--expense" :style="{ width: `${item.share}%` }"></span>
              </div>
              <span class="category-chart__share">{{ item.totalShare }}%</span>
            </div>
          </div>
        </article>

        <article class="chart-card">
          <header class="chart-card__header">
            <h3>Доходы по категориям</h3>
            <span>RUB</span>
          </header>
          <div v-if="!incomeCategoryChartRows.length" class="empty-state empty-state--compact">
            За выбранный период доходов нет.
          </div>
          <div v-else class="category-chart" role="list">
            <div v-for="item in incomeCategoryChartRows" :key="`income-chart-${item.key}`" class="category-chart__row" role="listitem">
              <div class="category-chart__label">
                <strong>{{ item.name }}</strong>
                <span>{{ formatMoney(item.amountRub) }} RUB</span>
              </div>
              <div class="category-chart__track" aria-hidden="true">
                <span class="category-chart__bar category-chart__bar--income" :style="{ width: `${item.share}%` }"></span>
              </div>
              <span class="category-chart__share">{{ item.totalShare }}%</span>
            </div>
          </div>
        </article>
      </div>
    </section>

    <div class="dashboard-reports">
      <section class="work-panel report-panel">
        <div class="report-panel__header">
          <h2>Топ категорий расходов</h2>
          <span>{{ expenseCategories.length }}</span>
        </div>

        <div v-if="state.loading" class="empty-state">Загружаем расходы...</div>
        <div v-else-if="!expenseCategories.length" class="empty-state">За выбранный период расходов нет.</div>
        <ul v-else class="report-list">
          <li v-for="item in expenseCategories" :key="`expense-${item.id ?? 'none'}`" class="report-row">
            <div class="report-row__main">
              <strong>{{ getCategoryName('expense', item.id) }}</strong>
              <span>{{ formatMoney(item.amount_usd) }} USD</span>
            </div>
            <div class="report-row__amount">
              <strong>{{ formatMoney(item.amount_rub) }} RUB</strong>
              <span>{{ getShare(item, expenseTotalRub) }}%</span>
            </div>
            <div class="report-row__bar" aria-hidden="true">
              <span :style="{ width: `${getShare(item, expenseTotalRub)}%` }"></span>
            </div>
          </li>
        </ul>
      </section>

      <section class="work-panel report-panel">
        <div class="report-panel__header">
          <h2>Топ категорий доходов</h2>
          <span>{{ incomeCategories.length }}</span>
        </div>

        <div v-if="state.loading" class="empty-state">Загружаем доходы...</div>
        <div v-else-if="!incomeCategories.length" class="empty-state">За выбранный период доходов нет.</div>
        <ul v-else class="report-list">
          <li v-for="item in incomeCategories" :key="`income-${item.id ?? 'none'}`" class="report-row">
            <div class="report-row__main">
              <strong>{{ getCategoryName('income', item.id) }}</strong>
              <span>{{ formatMoney(item.amount_usd) }} USD</span>
            </div>
            <div class="report-row__amount">
              <strong>{{ formatMoney(item.amount_rub) }} RUB</strong>
              <span>{{ getShare(item, incomeTotalRub) }}%</span>
            </div>
            <div class="report-row__bar report-row__bar--income" aria-hidden="true">
              <span :style="{ width: `${getShare(item, incomeTotalRub)}%` }"></span>
            </div>
          </li>
        </ul>
      </section>
    </div>
  </section>
</template>

<script setup>
import { computed, onMounted, reactive } from 'vue'
import { RefreshCcw } from 'lucide-vue-next'

import { expenseCategoriesApi, incomeCategoriesApi } from '../api/categoriesApi'
import { getReportingPeriods } from '../api/reportingPeriodsApi'
import { getReportByCategory, getReportByPeriodCategory, getSummary } from '../api/reportsApi'
import { getErrorMessage } from '../utils/apiErrors'

const chartBounds = {
  left: 92,
  right: 734,
  top: 24,
  bottom: 278,
}
const chartColors = ['#0f766e', '#ef4444', '#2563eb', '#ca8a04', '#9333ea', '#db2777', '#0891b2', '#65a30d', '#f97316', '#475569']
const monthNames = ['янв', 'фев', 'мар', 'апр', 'май', 'июн', 'июл', 'авг', 'сен', 'окт', 'ноя', 'дек']

const emptySummary = () => ({
  income: { amount_rub: '0.00', amount_usd: '0.00' },
  expense: { amount_rub: '0.00', amount_usd: '0.00' },
  balance: { amount_rub: '0.00', amount_usd: '0.00' },
})

const state = reactive({
  loading: false,
  error: '',
  filters: getLastTwelveMonthsRange(),
  summary: emptySummary(),
  reports: {
    income: [],
    expense: [],
    periodIncome: [],
    periodExpense: [],
    savingsIncome: [],
    savingsExpense: [],
  },
  savingsRange: getLastTwelveMonthsRange(),
  directories: {
    income: [],
    expense: [],
  },
  selectedSeries: {
    income: '',
    expense: '',
  },
  expensePointTooltip: null,
})

const summary = computed(() => state.summary || emptySummary())
const incomeCategories = computed(() => state.reports.income.slice(0, 5))
const expenseCategories = computed(() => state.reports.expense.slice(0, 5))
const incomeTotalRub = computed(() => Number(summary.value.income.amount_rub || 0))
const expenseTotalRub = computed(() => Number(summary.value.expense.amount_rub || 0))
const summaryChartRows = computed(() => {
  const rows = [
    { type: 'income', label: 'Доходы', amountRub: incomeTotalRub.value },
    { type: 'expense', label: 'Расходы', amountRub: expenseTotalRub.value },
  ]
  const maxAmount = Math.max(...rows.map((item) => item.amountRub), 0)

  return rows.map((item) => ({
    ...item,
    share: getChartShare(item.amountRub, maxAmount),
  }))
})
const incomeCategoryChartRows = computed(() => buildCategoryChartRows('income'))
const expenseCategoryChartRows = computed(() => buildCategoryChartRows('expense'))
const incomePeriodChart = computed(() => buildPeriodLineChart('income'))
const expensePeriodChart = computed(() => buildPeriodLineChart('expense'))
const savingsChart = computed(buildSavingsChart)
const balanceClass = computed(() => ({
  'metric-card--positive': Number(summary.value.balance.amount_rub || 0) >= 0,
  'metric-card--negative': Number(summary.value.balance.amount_rub || 0) < 0,
}))

onMounted(initializeDashboard)

async function initializeDashboard() {
  await setDefaultClosedPeriodRange()
  await loadDashboard()
}

async function loadDashboard() {
  state.loading = true
  state.error = ''

  try {
    const query = buildReportQuery()
    const savingsQuery = getRangeReportQuery(state.savingsRange)
    const [
      summaryResponse,
      incomeReport,
      expenseReport,
      incomePeriodReport,
      expensePeriodReport,
      savingsIncomeReport,
      savingsExpenseReport,
      incomeDirectories,
      expenseDirectories,
    ] = await Promise.all([
      getSummary(query),
      getReportByCategory({ ...query, type: 'income' }),
      getReportByCategory({ ...query, type: 'expense' }),
      getReportByPeriodCategory({ ...query, type: 'income' }),
      getReportByPeriodCategory({ ...query, type: 'expense' }),
      getReportByPeriodCategory({ ...savingsQuery, type: 'income' }),
      getReportByPeriodCategory({ ...savingsQuery, type: 'expense' }),
      incomeCategoriesApi.list(),
      expenseCategoriesApi.list(),
    ])

    state.summary = normalizeSummary(summaryResponse)
    state.reports.income = Array.isArray(incomeReport) ? incomeReport : []
    state.reports.expense = Array.isArray(expenseReport) ? expenseReport : []
    state.reports.periodIncome = Array.isArray(incomePeriodReport) ? incomePeriodReport : []
    state.reports.periodExpense = Array.isArray(expensePeriodReport) ? expensePeriodReport : []
    state.reports.savingsIncome = Array.isArray(savingsIncomeReport) ? savingsIncomeReport : []
    state.reports.savingsExpense = Array.isArray(savingsExpenseReport) ? savingsExpenseReport : []
    state.directories.income = incomeDirectories
    state.directories.expense = expenseDirectories
  } catch (error) {
    state.error = getErrorMessage(error, 'Не удалось загрузить сводку и отчеты.')
  } finally {
    state.loading = false
  }
}

function buildReportQuery() {
  return getRangeReportQuery(state.filters)
}

function getRangeReportQuery(range) {
  const selectedRange = getPeriodRange(range)

  return {
    date_from: selectedRange.date_from,
    date_to: selectedRange.date_to,
  }
}

function setCurrentPeriod() {
  Object.assign(state.filters, getCurrentPeriodRange())
  loadDashboard()
}

function getCurrentPeriodRange() {
  const today = new Date()
  const currentPeriod = getPeriodStart(today)
  const period = formatMonthInput(new Date(currentPeriod.getFullYear(), currentPeriod.getMonth() + 1, 14))

  return {
    period_from: period,
    period_to: period,
  }
}

function getLastTwelveMonthsRange() {
  return getLastTwelveMonthsRangeFrom(getLastCompletedPeriodEnd(new Date()))
}

function getLastTwelveMonthsRangeFrom(endDate) {
  const startMonth = new Date(endDate.getFullYear(), endDate.getMonth(), 1)
  startMonth.setMonth(startMonth.getMonth() - 11)

  return {
    period_from: formatMonthInput(startMonth),
    period_to: formatMonthInput(endDate),
  }
}

async function setDefaultClosedPeriodRange() {
  try {
    const periods = await getReportingPeriods()
    const lastClosedPeriod = Array.isArray(periods)
      ? periods
          .filter((period) => period?.is_closed)
          .sort((left, right) => parseLocalDate(right.end_date) - parseLocalDate(left.end_date))[0]
      : null

    if (lastClosedPeriod?.start_date) {
      const range = getLastTwelveMonthsRangeFrom(parseLocalDate(lastClosedPeriod.end_date))
      Object.assign(state.filters, range)
      Object.assign(state.savingsRange, range)
    }
  } catch {
    const range = getLastTwelveMonthsRange()
    Object.assign(state.filters, range)
    Object.assign(state.savingsRange, range)
  }
}

function getPeriodRange(range) {
  const periodFrom = parseMonthInput(range.period_from)
  const periodTo = parseMonthInput(range.period_to)
  const start = periodFrom <= periodTo ? periodFrom : periodTo
  const end = periodFrom <= periodTo ? periodTo : periodFrom

  return {
    date_from: formatDateInput(getPeriodRangeStart(start)),
    date_to: formatDateInput(getPeriodRangeEnd(end)),
  }
}

function getPeriodRangeStart(date) {
  return new Date(date.getFullYear(), date.getMonth() - 1, 15)
}

function getPeriodRangeEnd(date) {
  return new Date(date.getFullYear(), date.getMonth(), 14)
}

function formatDateInput(date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatMonthInput(date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  return `${year}-${month}`
}

function normalizeSummary(value) {
  const fallback = emptySummary()

  return {
    income: value?.income || fallback.income,
    expense: value?.expense || fallback.expense,
    balance: value?.balance || fallback.balance,
  }
}

function getCategoryName(type, id) {
  if (id === null || id === undefined) {
    return 'Без категории'
  }

  return state.directories[type].find((category) => String(category.id) === String(id))?.name || `Категория #${id}`
}

function buildCategoryChartRows(type) {
  const total = type === 'income' ? incomeTotalRub.value : expenseTotalRub.value
  const grouped = state.reports[type].reduce((acc, item) => {
    const name = getChartCategoryName(type, item.id)
    const key = normalizeCategoryKey(name)
    const current = acc.get(key) || {
      key,
      name,
      amountRub: 0,
      amountUsd: 0,
    }

    current.amountRub += Number(item.amount_rub || 0)
    current.amountUsd += Number(item.amount_usd || 0)
    acc.set(key, current)

    return acc
  }, new Map())
  const rows = Array.from(grouped.values()).sort((left, right) => right.amountRub - left.amountRub)
  const maxAmount = Math.max(...rows.map((item) => item.amountRub), 0)

  return rows.map((item) => ({
    ...item,
    share: getChartShare(item.amountRub, maxAmount),
    totalShare: getShare({ amount_rub: item.amountRub }, total),
  }))
}

function buildPeriodLineChart(type) {
  const rows = type === 'income' ? state.reports.periodIncome : state.reports.periodExpense
  const periods = buildChartPeriods(state.filters)
  const selectedKey = state.selectedSeries[type]
  const grouped = new Map()

  rows.forEach((item) => {
    const periodKey = item.period_start
    const amountRub = Number(item.amount_rub || 0)
    const seriesDefinitions = getPeriodChartSeriesDefinitions(type, item.id)

    seriesDefinitions.forEach((seriesDefinition) => {
      addPeriodSeriesAmount(grouped, seriesDefinition, periodKey, amountRub)
    })
  })

  const rawSeries = Array.from(grouped.values()).filter((item) => item.total > 0)
  const selectedRawSeries = selectedKey ? rawSeries.filter((series) => series.key === selectedKey) : rawSeries
  const visibleRawSeries = selectedRawSeries.length ? selectedRawSeries : rawSeries
  const maxAmount = Math.max(
    ...visibleRawSeries.flatMap((series) => periods.map((period) => series.values.get(period.key) || 0)),
    0,
  )
  const scaleMax = maxAmount || 1
  const yTicks = [0, 0.25, 0.5, 0.75, 1].map((ratio) => {
    const value = scaleMax * ratio
    return {
      value,
      y: getChartY(value, scaleMax),
    }
  })

  const series = rawSeries
    .sort((left, right) => right.total - left.total)
    .map((line, index) => {
      const points = periods.map((period) => {
        const value = line.values.get(period.key) || 0

        return {
          periodKey: period.key,
          value,
          x: period.x,
          y: getChartY(value, scaleMax),
        }
      })

      return {
        ...line,
        color: chartColors[index % chartColors.length],
        hidden: Boolean(selectedKey && line.key !== selectedKey),
        selected: line.key === selectedKey,
        points,
        path: points.map((point, pointIndex) => `${pointIndex === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' '),
      }
    })

  return {
    periods,
    series,
    visibleSeries: series.filter((line) => !line.hidden),
    yTicks,
  }
}

function buildSavingsChart() {
  const periods = buildChartPeriods(state.savingsRange)
  const incomeByPeriod = sumReportsByPeriod(state.reports.savingsIncome)
  const expenseByPeriod = sumReportsByPeriod(state.reports.savingsExpense)
  const values = periods.map((period) => ({
    ...period,
    income: incomeByPeriod.get(period.key) || 0,
    expense: expenseByPeriod.get(period.key) || 0,
  })).map((period) => ({
    ...period,
    value: period.income - period.expense,
  }))
  const maxAbsoluteValue = Math.max(...values.map((period) => Math.abs(period.value)), 1)
  const scaleMax = maxAbsoluteValue * 1.2
  const zeroY = getSavingsChartY(0, scaleMax)
  const barSlotWidth = (chartBounds.right - chartBounds.left) / Math.max(values.length, 1)
  const barWidth = Math.min(34, barSlotWidth * 0.62)
  const yTicks = [-1, -0.5, 0, 0.5, 1].map((ratio) => ({
    value: scaleMax * ratio,
    y: getSavingsChartY(scaleMax * ratio, scaleMax),
  }))
  const bars = values.map((period, index) => {
    const valueY = getSavingsChartY(period.value, scaleMax)
    const y = Math.min(valueY, zeroY)
    const height = Math.max(Math.abs(zeroY - valueY), 1)
    const labelX = chartBounds.left + barSlotWidth * (index + 0.5)

    return {
      ...period,
      x: labelX - barWidth / 2,
      labelX,
      y,
      width: barWidth,
      height,
      amountLabelY: period.value >= 0 ? y - 7 : y + height + 15,
    }
  })
  const total = values.reduce((sum, period) => sum + period.value, 0)
  const average = total / Math.max(values.length, 1)

  return {
    bars,
    yTicks,
    total,
    average,
    averageY: getSavingsChartY(average, scaleMax),
    hasData: values.some((period) => period.income !== 0 || period.expense !== 0),
  }
}

function sumReportsByPeriod(rows) {
  return rows.reduce((totals, item) => {
    const key = item.period_start
    totals.set(key, (totals.get(key) || 0) + Number(item.amount_rub || 0))
    return totals
  }, new Map())
}

function getSavingsChartY(value, scaleMax) {
  const height = chartBounds.bottom - chartBounds.top
  return chartBounds.top + ((scaleMax - Number(value || 0)) / (scaleMax * 2)) * height
}

function selectLineSeries(type, key) {
  state.selectedSeries[type] = state.selectedSeries[type] === key ? '' : key
  hideExpensePointTooltip()
}

function showExpensePointTooltip(line, point) {
  state.expensePointTooltip = {
    category: line.name,
    amount: point.value,
    left: Math.min(88, Math.max(12, (point.x / 760) * 100)),
    top: (point.y / 340) * 100,
  }
}

function hideExpensePointTooltip() {
  state.expensePointTooltip = null
}

function getPeriodChartSeriesDefinitions(type, categoryId) {
  const name = getCategoryName(type, categoryId)
  const normalized = normalizeCategoryKey(name)
  const series = [
    {
      key: normalized,
      name,
    },
  ]

  if (type === 'expense' && ['cafe', 'grocery'].includes(normalized)) {
    series.push({
      key: 'food',
      name: 'food',
    })
  }

  return series
}

function addPeriodSeriesAmount(grouped, seriesDefinition, periodKey, amountRub) {
  const series = grouped.get(seriesDefinition.key) || {
    ...seriesDefinition,
    values: new Map(),
    total: 0,
  }

  series.values.set(periodKey, (series.values.get(periodKey) || 0) + amountRub)
  series.total += amountRub
  grouped.set(seriesDefinition.key, series)
}

function buildChartPeriods(range) {
  const periodFrom = parseMonthInput(range.period_from)
  const periodTo = parseMonthInput(range.period_to)
  const start = periodFrom <= periodTo ? periodFrom : periodTo
  const end = periodFrom <= periodTo ? periodTo : periodFrom
  const periods = []
  const current = new Date(start)

  while (current <= end) {
    periods.push({
      key: formatDateInput(getPeriodRangeStart(current)),
      label: formatPeriodLabel(current),
      monthLabel: monthNames[current.getMonth()],
      yearLabel: String(current.getFullYear()),
      x: 0,
    })
    current.setMonth(current.getMonth() + 1)
  }

  const width = chartBounds.right - chartBounds.left
  return periods.map((period, index) => ({
    ...period,
    x: periods.length === 1 ? chartBounds.left + width / 2 : chartBounds.left + (width / (periods.length - 1)) * index,
  }))
}

function getChartY(value, maxAmount) {
  const height = chartBounds.bottom - chartBounds.top
  return chartBounds.bottom - (Number(value || 0) / maxAmount) * height
}

function getPeriodStart(date) {
  const value = new Date(date)

  if (value.getDate() >= 15) {
    return new Date(value.getFullYear(), value.getMonth(), 15)
  }

  return new Date(value.getFullYear(), value.getMonth() - 1, 15)
}

function getLastCompletedPeriodEnd(date) {
  const currentPeriod = getPeriodStart(date)
  return new Date(currentPeriod.getFullYear(), currentPeriod.getMonth(), 14)
}

function parseLocalDate(value) {
  const [year, month, day] = String(value).split('-').map(Number)
  return new Date(year, month - 1, day || 1)
}

function formatPeriodLabel(date) {
  return `${monthNames[date.getMonth()]} ${date.getFullYear()}`
}

function parseMonthInput(value) {
  const [year, month] = String(value).split('-').map(Number)
  return new Date(year, month - 1, 1)
}

function getChartCategoryName(type, id) {
  const name = getCategoryName(type, id)
  const normalized = normalizeCategoryKey(name)

  if (type === 'expense' && ['cafe', 'grocery'].includes(normalized)) {
    return 'food'
  }

  return name
}

function normalizeCategoryKey(value) {
  return String(value || '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
}

function getShare(item, total) {
  if (!total) {
    return 0
  }

  return Math.min(100, Math.round((Number(item.amount_rub || 0) / total) * 100))
}

function getChartShare(amount, maxAmount) {
  if (!maxAmount || !amount) {
    return 0
  }

  return Math.max(4, Math.min(100, Math.round((amount / maxAmount) * 100)))
}

function formatMoney(value) {
  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(value || 0))
}

function formatCompactMoney(value) {
  return new Intl.NumberFormat('ru-RU', {
    notation: 'compact',
    maximumFractionDigits: 1,
  })
    .format(Number(value || 0))
    .replace(/\s*тыс\./u, 'k')
}

function formatSignedMoney(value) {
  const amount = Number(value || 0)
  const sign = amount > 0 ? '+' : ''
  return `${sign}${formatMoney(amount)}`
}
</script>
