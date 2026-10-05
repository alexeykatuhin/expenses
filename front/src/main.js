import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { installAuthHttpHandlers } from './stores/auth'
import './style.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
installAuthHttpHandlers(pinia)
app.use(router)

app.mount('#app')
