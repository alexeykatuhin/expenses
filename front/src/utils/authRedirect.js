export async function redirectAfterAuth(router, redirect, fallbackRoute) {
  if (typeof redirect !== 'string') {
    await router.push(fallbackRoute)
    return
  }

  const target = new URL(redirect, window.location.origin)
  if (target.origin !== window.location.origin) {
    await router.push(fallbackRoute)
    return
  }

  const expensesBase = import.meta.env.BASE_URL.replace(/\/$/, '')
  if (!target.pathname.startsWith(`${expensesBase}/`)) {
    window.location.assign(`${target.pathname}${target.search}${target.hash}`)
    return
  }

  await router.push(`${target.pathname.slice(expensesBase.length)}${target.search}${target.hash}`)
}
