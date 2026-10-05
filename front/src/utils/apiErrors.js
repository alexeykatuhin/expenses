export function getErrorMessage(error, fallback = 'Что-то пошло не так. Попробуйте еще раз.') {
  return error?.message || fallback
}

export function getFieldErrors(error) {
  const fieldErrors = {}

  if (!Array.isArray(error?.details)) {
    return fieldErrors
  }

  error.details.forEach((detail) => {
    if (!detail?.field || !detail?.message) {
      return
    }

    fieldErrors[detail.field] = detail.message
  })

  return fieldErrors
}
