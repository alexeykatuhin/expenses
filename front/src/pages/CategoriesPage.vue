<template>
  <section class="content-page content-page--wide">
    <header class="page__header">
      <div>
        <p class="page__eyebrow">Справочники</p>
        <h1>Категории</h1>
      </div>

      <button class="button" :disabled="activeState.loading" type="button" @click="reloadActiveKind">
        <RefreshCw :size="18" aria-hidden="true" />
        <span>{{ activeState.loading ? 'Обновляем...' : 'Обновить' }}</span>
      </button>
    </header>

    <div class="tabs" role="tablist" aria-label="Тип справочника">
      <button
        v-for="kind in kinds"
        :key="kind.id"
        class="tabs__button"
        :class="{ 'tabs__button--active': activeKind === kind.id }"
        type="button"
        role="tab"
        :aria-selected="activeKind === kind.id"
        @click="setActiveKind(kind.id)"
      >
        <component :is="kind.icon" :size="18" aria-hidden="true" />
        <span>{{ kind.label }}</span>
      </button>
    </div>

    <p v-if="activeState.error" class="form-alert">{{ activeState.error }}</p>

    <div class="directory-layout">
      <section class="work-panel directory-panel">
        <div class="directory-panel__header">
          <h2>Категории {{ activeKindLabelGenitive }}</h2>
          <span class="directory-panel__counter">{{ activeState.categories.length }}</span>
        </div>

        <form class="inline-form" @submit.prevent="createCategory">
          <label class="field field--inline">
            <span>Название категории</span>
            <input
              v-model.trim="activeState.categoryName"
              :disabled="activeState.savingCategory"
              autocomplete="off"
              placeholder="Например, зарплата"
              type="text"
            />
          </label>
          <button class="button button--primary button--fit" :disabled="!canCreateCategory" type="submit">
            <Plus :size="18" aria-hidden="true" />
            <span>Добавить</span>
          </button>
        </form>

        <div v-if="activeState.loading" class="empty-state">Загружаем категории...</div>
        <div v-else-if="!activeState.categories.length" class="empty-state">
          Категорий пока нет.
        </div>
        <div v-else class="directory-list" aria-label="Список категорий">
          <article
            v-for="category in activeState.categories"
            :key="category.id"
            class="directory-row"
            :class="{
              'directory-row--selected': activeState.selectedCategoryId === category.id,
              'directory-row--inactive': !category.is_active,
            }"
          >
            <button class="directory-row__main" type="button" @click="selectCategory(category.id)">
              <strong>{{ category.name }}</strong>
              <span>{{ category.is_active ? 'Активна' : 'Неактивна' }}</span>
            </button>

            <div v-if="activeState.editingCategoryId === category.id" class="directory-row__editor">
              <input
                v-model.trim="activeState.editingCategoryName"
                :disabled="activeState.savingCategory"
                aria-label="Новое название категории"
                type="text"
                @keyup.enter="saveCategory(category)"
                @keyup.esc="cancelCategoryEdit"
              />
              <button
                class="icon-button"
                :disabled="!activeState.editingCategoryName || activeState.savingCategory"
                title="Сохранить"
                type="button"
                @click="saveCategory(category)"
              >
                <Check :size="17" aria-hidden="true" />
              </button>
              <button class="icon-button" title="Отменить" type="button" @click="cancelCategoryEdit">
                <X :size="17" aria-hidden="true" />
              </button>
            </div>

            <div v-else class="directory-row__actions">
              <button class="icon-button" title="Переименовать" type="button" @click="startCategoryEdit(category)">
                <Pencil :size="17" aria-hidden="true" />
              </button>
              <button
                class="icon-button"
                :title="category.is_active ? 'Деактивировать' : 'Активировать'"
                type="button"
                @click="toggleCategory(category)"
              >
                <Power :size="17" aria-hidden="true" />
              </button>
              <button class="icon-button icon-button--danger" title="Удалить" type="button" @click="deleteCategory(category)">
                <Trash2 :size="17" aria-hidden="true" />
              </button>
            </div>
          </article>
        </div>
      </section>

      <section class="work-panel directory-panel">
        <div class="directory-panel__header">
          <h2>Подкатегории</h2>
          <span class="directory-panel__counter">{{ activeState.subcategories.length }}</span>
        </div>

        <p v-if="!selectedCategory" class="empty-state empty-state--compact">
          Выберите категорию, чтобы управлять подкатегориями.
        </p>

        <template v-else>
          <div class="selected-category">
            <span>Выбрана категория</span>
            <strong>{{ selectedCategory.name }}</strong>
          </div>

          <form class="inline-form" @submit.prevent="createSubcategory">
            <label class="field field--inline">
              <span>Название подкатегории</span>
              <input
                v-model.trim="activeState.subcategoryName"
                :disabled="activeState.savingSubcategory || !selectedCategory"
                autocomplete="off"
                placeholder="Например, аванс"
                type="text"
              />
            </label>
            <button class="button button--primary button--fit" :disabled="!canCreateSubcategory" type="submit">
              <Plus :size="18" aria-hidden="true" />
              <span>Добавить</span>
            </button>
          </form>

          <div v-if="activeState.loadingSubcategories" class="empty-state">
            Загружаем подкатегории...
          </div>
          <div v-else-if="!activeState.subcategories.length" class="empty-state">
            У выбранной категории пока нет подкатегорий.
          </div>
          <div v-else class="directory-list" aria-label="Список подкатегорий">
            <article
              v-for="subcategory in activeState.subcategories"
              :key="subcategory.id"
              class="directory-row"
              :class="{ 'directory-row--inactive': !subcategory.is_active }"
            >
              <div class="directory-row__main directory-row__main--static">
                <strong>{{ subcategory.name }}</strong>
                <span>{{ subcategory.is_active ? 'Активна' : 'Неактивна' }}</span>
              </div>

              <div v-if="activeState.editingSubcategoryId === subcategory.id" class="directory-row__editor">
                <input
                  v-model.trim="activeState.editingSubcategoryName"
                  :disabled="activeState.savingSubcategory"
                  aria-label="Новое название подкатегории"
                  type="text"
                  @keyup.enter="saveSubcategory(subcategory)"
                  @keyup.esc="cancelSubcategoryEdit"
                />
                <button
                  class="icon-button"
                  :disabled="!activeState.editingSubcategoryName || activeState.savingSubcategory"
                  title="Сохранить"
                  type="button"
                  @click="saveSubcategory(subcategory)"
                >
                  <Check :size="17" aria-hidden="true" />
                </button>
                <button class="icon-button" title="Отменить" type="button" @click="cancelSubcategoryEdit">
                  <X :size="17" aria-hidden="true" />
                </button>
              </div>

              <div v-else class="directory-row__actions">
                <button class="icon-button" title="Переименовать" type="button" @click="startSubcategoryEdit(subcategory)">
                  <Pencil :size="17" aria-hidden="true" />
                </button>
                <button
                  class="icon-button"
                  :title="subcategory.is_active ? 'Деактивировать' : 'Активировать'"
                  type="button"
                  @click="toggleSubcategory(subcategory)"
                >
                  <Power :size="17" aria-hidden="true" />
                </button>
                <button
                  class="icon-button icon-button--danger"
                  title="Удалить"
                  type="button"
                  @click="deleteSubcategory(subcategory)"
                >
                  <Trash2 :size="17" aria-hidden="true" />
                </button>
              </div>
            </article>
          </div>
        </template>
      </section>
    </div>
  </section>
</template>

<script setup>
import {
  Banknote,
  Check,
  CreditCard,
  Pencil,
  Plus,
  Power,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-vue-next'
import { computed, onMounted, reactive } from 'vue'

import {
  expenseCategoriesApi,
  expenseSubcategoriesApi,
  incomeCategoriesApi,
  incomeSubcategoriesApi,
} from '../api/categoriesApi'
import { getErrorMessage } from '../utils/apiErrors'

const resources = {
  income: {
    categoryApi: incomeCategoriesApi,
    subcategoryApi: incomeSubcategoriesApi,
    label: 'Доходы',
    genitive: 'доходов',
  },
  expense: {
    categoryApi: expenseCategoriesApi,
    subcategoryApi: expenseSubcategoriesApi,
    label: 'Расходы',
    genitive: 'расходов',
  },
}

const kinds = [
  { id: 'expense', label: 'Расходы', icon: CreditCard },
  { id: 'income', label: 'Доходы', icon: Banknote },
]

const state = reactive({
  activeKind: 'expense',
  income: createKindState(),
  expense: createKindState(),
})

const activeKind = computed(() => state.activeKind)
const activeState = computed(() => state[state.activeKind])
const activeResource = computed(() => resources[state.activeKind])
const activeKindLabelGenitive = computed(() => activeResource.value.genitive)
const selectedCategory = computed(() =>
  activeState.value.categories.find((category) => category.id === activeState.value.selectedCategoryId),
)
const canCreateCategory = computed(
  () => Boolean(activeState.value.categoryName) && !activeState.value.savingCategory,
)
const canCreateSubcategory = computed(
  () =>
    Boolean(selectedCategory.value) &&
    Boolean(activeState.value.subcategoryName) &&
    !activeState.value.savingSubcategory,
)

onMounted(() => {
  loadKind('income')
  loadKind('expense')
})

function createKindState() {
  return {
    categories: [],
    subcategories: [],
    selectedCategoryId: null,
    categoryName: '',
    subcategoryName: '',
    editingCategoryId: null,
    editingCategoryName: '',
    editingSubcategoryId: null,
    editingSubcategoryName: '',
    loading: false,
    loadingSubcategories: false,
    savingCategory: false,
    savingSubcategory: false,
    error: '',
  }
}

function setError(kind, error, fallback) {
  state[kind].error = getErrorMessage(error, fallback)
}

async function loadKind(kind) {
  const target = state[kind]
  target.loading = true
  target.error = ''

  try {
    target.categories = await resources[kind].categoryApi.list()
    if (!target.selectedCategoryId && target.categories.length) {
      target.selectedCategoryId = target.categories[0].id
    }

    if (target.selectedCategoryId) {
      await loadSubcategories(kind, target.selectedCategoryId)
    } else {
      target.subcategories = []
    }
  } catch (error) {
    setError(kind, error, 'Не удалось загрузить категории.')
  } finally {
    target.loading = false
  }
}

async function loadSubcategories(kind, categoryId) {
  const target = state[kind]
  target.loadingSubcategories = true
  target.error = ''

  try {
    target.subcategories = await resources[kind].subcategoryApi.list({ category_id: categoryId })
  } catch (error) {
    setError(kind, error, 'Не удалось загрузить подкатегории.')
  } finally {
    target.loadingSubcategories = false
  }
}

function setActiveKind(kind) {
  state.activeKind = kind
}

function reloadActiveKind() {
  return loadKind(state.activeKind)
}

async function selectCategory(categoryId) {
  const kind = state.activeKind
  const target = state[kind]

  if (target.selectedCategoryId === categoryId) {
    return
  }

  target.selectedCategoryId = categoryId
  target.subcategoryName = ''
  cancelSubcategoryEdit()
  await loadSubcategories(kind, categoryId)
}

async function createCategory() {
  const kind = state.activeKind
  const target = state[kind]
  target.savingCategory = true
  target.error = ''

  try {
    const category = await resources[kind].categoryApi.create({
      name: target.categoryName,
      is_active: true,
    })

    target.categories = [...target.categories, category]
    target.categoryName = ''
    target.selectedCategoryId = category.id
    target.subcategories = []
  } catch (error) {
    setError(kind, error, 'Не удалось создать категорию.')
  } finally {
    target.savingCategory = false
  }
}

function startCategoryEdit(category) {
  const target = activeState.value
  target.editingCategoryId = category.id
  target.editingCategoryName = category.name
}

function cancelCategoryEdit() {
  const target = activeState.value
  target.editingCategoryId = null
  target.editingCategoryName = ''
}

async function saveCategory(category) {
  const kind = state.activeKind
  const target = state[kind]

  if (!target.editingCategoryName) {
    return
  }

  target.savingCategory = true
  target.error = ''

  try {
    const updated = await resources[kind].categoryApi.update(category.id, {
      name: target.editingCategoryName,
    })
    replaceById(target.categories, updated)
    cancelCategoryEdit()
  } catch (error) {
    setError(kind, error, 'Не удалось обновить категорию.')
  } finally {
    target.savingCategory = false
  }
}

async function toggleCategory(category) {
  const kind = state.activeKind
  const target = state[kind]
  target.savingCategory = true
  target.error = ''

  try {
    const updated = await resources[kind].categoryApi.update(category.id, {
      is_active: !category.is_active,
    })
    replaceById(target.categories, updated)
  } catch (error) {
    setError(kind, error, 'Не удалось изменить активность категории.')
  } finally {
    target.savingCategory = false
  }
}

async function deleteCategory(category) {
  if (!window.confirm(`Деактивировать категорию "${category.name}"?`)) {
    return
  }

  const kind = state.activeKind
  const target = state[kind]
  target.savingCategory = true
  target.error = ''

  try {
    await resources[kind].categoryApi.delete(category.id)
    replaceById(target.categories, { ...category, is_active: false })
  } catch (error) {
    setError(kind, error, 'Не удалось деактивировать категорию.')
  } finally {
    target.savingCategory = false
  }
}

async function createSubcategory() {
  const kind = state.activeKind
  const target = state[kind]

  if (!target.selectedCategoryId) {
    return
  }

  target.savingSubcategory = true
  target.error = ''

  try {
    const subcategory = await resources[kind].subcategoryApi.create({
      category_id: target.selectedCategoryId,
      name: target.subcategoryName,
      is_active: true,
    })

    target.subcategories = [...target.subcategories, subcategory]
    target.subcategoryName = ''
  } catch (error) {
    setError(kind, error, 'Не удалось создать подкатегорию.')
  } finally {
    target.savingSubcategory = false
  }
}

function startSubcategoryEdit(subcategory) {
  const target = activeState.value
  target.editingSubcategoryId = subcategory.id
  target.editingSubcategoryName = subcategory.name
}

function cancelSubcategoryEdit() {
  const target = activeState.value
  target.editingSubcategoryId = null
  target.editingSubcategoryName = ''
}

async function saveSubcategory(subcategory) {
  const kind = state.activeKind
  const target = state[kind]

  if (!target.editingSubcategoryName) {
    return
  }

  target.savingSubcategory = true
  target.error = ''

  try {
    const updated = await resources[kind].subcategoryApi.update(subcategory.id, {
      name: target.editingSubcategoryName,
    })
    replaceById(target.subcategories, updated)
    cancelSubcategoryEdit()
  } catch (error) {
    setError(kind, error, 'Не удалось обновить подкатегорию.')
  } finally {
    target.savingSubcategory = false
  }
}

async function toggleSubcategory(subcategory) {
  const kind = state.activeKind
  const target = state[kind]
  target.savingSubcategory = true
  target.error = ''

  try {
    const updated = await resources[kind].subcategoryApi.update(subcategory.id, {
      is_active: !subcategory.is_active,
    })
    replaceById(target.subcategories, updated)
  } catch (error) {
    setError(kind, error, 'Не удалось изменить активность подкатегории.')
  } finally {
    target.savingSubcategory = false
  }
}

async function deleteSubcategory(subcategory) {
  if (!window.confirm(`Деактивировать подкатегорию "${subcategory.name}"?`)) {
    return
  }

  const kind = state.activeKind
  const target = state[kind]
  target.savingSubcategory = true
  target.error = ''

  try {
    await resources[kind].subcategoryApi.delete(subcategory.id)
    replaceById(target.subcategories, { ...subcategory, is_active: false })
  } catch (error) {
    setError(kind, error, 'Не удалось деактивировать подкатегорию.')
  } finally {
    target.savingSubcategory = false
  }
}

function replaceById(items, updatedItem) {
  const index = items.findIndex((item) => item.id === updatedItem.id)
  if (index >= 0) {
    items.splice(index, 1, updatedItem)
  }
}
</script>
