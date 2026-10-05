<template>
  <div class="category-combobox" :class="{ 'category-combobox--open': isOpen }">
    <div class="category-combobox__control">
      <Search class="category-combobox__search" :size="18" aria-hidden="true" />
      <input
        ref="inputElement"
        v-model="query"
        :aria-activedescendant="activeOptionId"
        :aria-controls="listboxId"
        :aria-expanded="isOpen"
        :aria-invalid="invalid || undefined"
        :aria-label="label"
        :class="{ 'field-control--invalid': invalid }"
        :disabled="disabled"
        :placeholder="loading ? 'Загружаем категории...' : placeholder"
        aria-autocomplete="list"
        autocomplete="off"
        role="combobox"
        type="text"
        @blur="handleBlur"
        @focus="handleFocus"
        @input="handleInput"
        @keydown="handleKeydown"
      />
      <button
        class="category-combobox__toggle"
        :aria-label="isOpen ? 'Закрыть список категорий' : 'Открыть список категорий'"
        :disabled="disabled"
        tabindex="-1"
        type="button"
        @mousedown.prevent="toggleList"
      >
        <ChevronDown :size="18" aria-hidden="true" />
      </button>
    </div>

    <div v-if="isOpen" :id="listboxId" class="category-combobox__list" role="listbox">
      <button
        v-for="(option, index) in filteredOptions"
        :id="getOptionId(option)"
        :key="option.id"
        :aria-selected="isSelected(option)"
        :class="{
          'category-combobox__option--active': index === activeIndex,
          'category-combobox__option--selected': isSelected(option),
        }"
        class="category-combobox__option"
        role="option"
        tabindex="-1"
        type="button"
        @mouseenter="activeIndex = index"
        @mousedown.prevent="selectOption(option)"
      >
        <span>{{ option.name }}</span>
        <Check v-if="isSelected(option)" :size="17" aria-hidden="true" />
      </button>

      <div v-if="!filteredOptions.length" class="category-combobox__empty">
        Ничего не найдено
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, ref, useId, watch } from 'vue'
import { Check, ChevronDown, Search } from 'lucide-vue-next'

const props = defineProps({
  modelValue: {
    type: [String, Number],
    default: '',
  },
  options: {
    type: Array,
    default: () => [],
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  invalid: {
    type: Boolean,
    default: false,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  label: {
    type: String,
    default: 'Категория',
  },
  placeholder: {
    type: String,
    default: 'Начните вводить категорию',
  },
})

const emit = defineEmits(['update:modelValue'])
const inputElement = ref(null)
const query = ref('')
const isOpen = ref(false)
const hasTyped = ref(false)
const activeIndex = ref(-1)
const valueOnFocus = ref('')
const componentId = useId()
const listboxId = `${componentId}-listbox`

const selectedOption = computed(() =>
  props.options.find((option) => String(option.id) === String(props.modelValue)),
)

const filteredOptions = computed(() => {
  const search = hasTyped.value ? normalize(query.value) : ''
  if (!search) {
    return props.options
  }

  return props.options
    .filter((option) => normalize(option.name).includes(search))
    .sort((left, right) => {
      const leftStartsWith = normalize(left.name).startsWith(search)
      const rightStartsWith = normalize(right.name).startsWith(search)
      return Number(rightStartsWith) - Number(leftStartsWith)
    })
})

const activeOptionId = computed(() => {
  const option = filteredOptions.value[activeIndex.value]
  return isOpen.value && option ? getOptionId(option) : undefined
})

watch(
  () => [props.modelValue, props.options],
  () => {
    if (selectedOption.value) {
      query.value = selectedOption.value.name
    } else if (!isOpen.value) {
      query.value = ''
    }
  },
  { immediate: true, deep: true },
)

watch(filteredOptions, (options) => {
  activeIndex.value = options.length ? 0 : -1
})

function handleFocus(event) {
  valueOnFocus.value = props.modelValue
  hasTyped.value = false
  openList()
  event.target.select()
}

function handleInput() {
  hasTyped.value = true
  isOpen.value = true
  activeIndex.value = filteredOptions.value.length ? 0 : -1

  if (!selectedOption.value || normalize(query.value) !== normalize(selectedOption.value.name)) {
    emit('update:modelValue', '')
  }
}

function handleBlur() {
  const exactMatch = props.options.find((option) => normalize(option.name) === normalize(query.value))
  if (exactMatch) {
    selectOption(exactMatch, false)
  }

  closeList()
}

function handleKeydown(event) {
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    const wasOpen = isOpen.value
    openList()
    if (wasOpen) {
      moveActive(1)
    }
    return
  }

  if (event.key === 'ArrowUp') {
    event.preventDefault()
    const wasOpen = isOpen.value
    openList()
    if (wasOpen) {
      moveActive(-1)
    } else if (filteredOptions.value.length) {
      activeIndex.value = filteredOptions.value.length - 1
    }
    return
  }

  if (event.key === 'Enter' && isOpen.value) {
    const option = filteredOptions.value[activeIndex.value]
    if (option) {
      event.preventDefault()
      selectOption(option)
    }
    return
  }

  if (event.key === 'Escape' && isOpen.value) {
    event.preventDefault()
    emit('update:modelValue', valueOnFocus.value)
    query.value = props.options.find((option) => String(option.id) === String(valueOnFocus.value))?.name || ''
    closeList()
  }
}

function toggleList() {
  if (isOpen.value) {
    closeList()
    return
  }

  inputElement.value?.focus()
}

function openList() {
  if (props.disabled) {
    return
  }

  isOpen.value = true
  const selectedIndex = filteredOptions.value.findIndex(isSelected)
  activeIndex.value = selectedIndex >= 0 ? selectedIndex : filteredOptions.value.length ? 0 : -1
}

function closeList() {
  isOpen.value = false
  hasTyped.value = false
  activeIndex.value = -1
}

async function selectOption(option, focus = true) {
  emit('update:modelValue', option.id)
  query.value = option.name
  closeList()

  if (focus) {
    await nextTick()
    inputElement.value?.focus()
  }
}

function moveActive(direction) {
  const count = filteredOptions.value.length
  if (!count) {
    activeIndex.value = -1
    return
  }

  activeIndex.value = (activeIndex.value + direction + count) % count
}

function isSelected(option) {
  return String(option.id) === String(props.modelValue)
}

function getOptionId(option) {
  return `${componentId}-option-${option.id}`
}

function normalize(value) {
  return String(value || '').trim().toLocaleLowerCase('ru-RU')
}
</script>
