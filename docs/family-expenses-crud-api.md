# CRUD API для учета семейных доходов и расходов

API построен как REST API с отдельными ресурсами для доходов и расходов. Все ответы и запросы используют JSON.

Базовый префикс:

```text
/api/v1
```

## Валюты и автоматическая конвертация

```http
GET /api/v1/currencies
Authorization: Bearer {access_token}
```

Ответ отсортирован так: недавно использованные пользователем валюты сверху по `last_used_at DESC`, затем остальные по `code ASC`.

```json
[
  {
    "code": "USD",
    "name": "US Dollar",
    "minor_unit": 2,
    "last_used_at": "2026-05-28T19:10:00Z"
  },
  {
    "code": "RUB",
    "name": "Russian Ruble",
    "minor_unit": 2,
    "last_used_at": null
  }
]
```

Создание доходов и расходов принимает одну исходную сумму и валюту:

```json
{
  "category_id": 1,
  "subcategory_id": 2,
  "original_amount": "100.00",
  "currency_code": "EUR",
  "comment": "Покупка"
}
```

## Добавление валюты

Если нужной валюты нет в списке, клиент может завести ее в глобальном справочнике:

```http
POST /api/v1/currencies
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "code": "KHR",
  "name": "Riel",
  "minor_unit": 2
}
```

`code` должен состоять из трех латинских букв. Если код уже есть, запись активируется и обновляется переданными `name` / `minor_unit`, дубль не создается.

`PATCH /api/v1/incomes/{id}` и `PATCH /api/v1/expenses/{id}` принимают те же поля опционально. Если изменились `original_amount` или `currency_code`, сервер заново рассчитывает `amount_rub` и `amount_usd`; если меняются только категория, подкатегория или комментарий, внешний provider курсов не вызывается.

Ответ операции содержит исходную сумму, валюту и сохраненный результат конвертации:

```json
{
  "id": 1,
  "category_id": 1,
  "subcategory_id": 2,
  "period_id": 1,
  "original_amount": "100.00",
  "currency_code": "EUR",
  "amount_rub": "9700.00",
  "amount_usd": "108.00",
  "exchange_rate_date": "2026-05-29",
  "rub_rate": "97",
  "usd_rate": "1.08",
  "operation_date": "2026-05-15",
  "comment": "Покупка",
  "created_at": "2026-05-29T10:30:00Z",
  "updated_at": "2026-05-29T10:30:00Z"
}
```

Ошибки конвертации:

- `400 VALIDATION_ERROR` для неверного или неактивного `currency_code`.
- `422 CURRENCY_RATE_NOT_AVAILABLE`, если для валюты нет курса к RUB или USD.
- `503 EXCHANGE_RATE_PROVIDER_UNAVAILABLE`, если внешний provider курсов недоступен.

Все endpoint'ы, кроме `/auth/register`, `/auth/login` и `/auth/refresh`, требуют access token:

```http
Authorization: Bearer {access_token}
```

## Общие соглашения

- `id` передается в URL как целое число.
- Денежные значения передаются строкой или числом с двумя знаками после запятой.
- Даты передаются в формате `YYYY-MM-DD`.
- `user_id` не передается в телах запросов. Сервер определяет пользователя по access token.
- Списки, детальные просмотры, обновления и удаления выполняются только в области данных текущего пользователя.
- Операции создаются без даты: сервер записывает их в текущий активный отчетный период. Отчетный период длится с 15 числа одного месяца по 14 число следующего включительно.
- У пользователя всегда есть один активный период. Закрытие периода выполняется отдельным endpoint'ом и сразу создает следующий активный период.
- В закрытый период нельзя добавлять, обновлять или удалять операции.
- `created_at` и `updated_at` заполняются сервером.
- При обновлении записи сервер должен менять `updated_at`.
- Удаление справочников лучше делать мягким: менять `is_active` на `false`, чтобы не ломать старые операции.
- Удаление операций можно делать физическим, если нет требований к аудиту.

## Коды ответов

```text
200 OK                  успешное получение или обновление
201 Created             успешное создание
204 No Content          успешное удаление
400 Bad Request         ошибка валидации
401 Unauthorized        пользователь не авторизован или token недействителен
403 Forbidden           пользователь авторизован, но не имеет доступа к ресурсу
404 Not Found           запись не найдена
409 Conflict            конфликт уникальности или связей
422 Unprocessable Entity корректный JSON, но бизнес-правило нарушено
```

## Формат ошибки

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Некорректные данные запроса",
    "details": [
      {
        "field": "amount_rub",
        "message": "Значение должно быть больше или равно 0"
      }
    ]
  }
}
```

## Авторизация

Ресурсы работают с таблицами `users` и `refresh_tokens`.

Access token используется для доступа к API. Refresh token используется для получения новой пары токенов и хранится на сервере только в виде хэша.

### Зарегистрировать пользователя

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "StrongPassword123",
  "display_name": "Семейный бюджет"
}
```

Валидация:

- `email` обязателен и должен быть уникальным.
- `password` обязателен.
- `display_name` необязателен.
- Пароль сохраняется только как `password_hash`.

Ответ:

```json
{
  "user": {
    "id": 1,
    "email": "user@example.com",
    "display_name": "Семейный бюджет",
    "is_active": true
  },
  "access_token": "eyJ...",
  "refresh_token": "eyJ..."
}
```

### Войти

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "StrongPassword123"
}
```

Ответ:

```json
{
  "user": {
    "id": 1,
    "email": "user@example.com",
    "display_name": "Семейный бюджет",
    "is_active": true
  },
  "access_token": "eyJ...",
  "refresh_token": "eyJ..."
}
```

### Обновить token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "eyJ..."
}
```

Сервер проверяет хэш refresh token в `refresh_tokens`, срок действия `expires_at` и отсутствие `revoked_at`.

Ответ:

```json
{
  "access_token": "eyJ...",
  "refresh_token": "eyJ..."
}
```

Рекомендуемое поведение: при refresh выпускать новый refresh token, а старый помечать через `revoked_at`.

### Выйти

```http
POST /api/v1/auth/logout
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "refresh_token": "eyJ..."
}
```

Сервер помечает refresh token как отозванный через `revoked_at`.

Ответ:

```http
204 No Content
```

### Получить текущего пользователя

```http
GET /api/v1/users/me
Authorization: Bearer {access_token}
```

Ответ:

```json
{
  "id": 1,
  "email": "user@example.com",
  "display_name": "Семейный бюджет",
  "is_active": true,
  "last_login_at": "2026-01-15T10:30:00",
  "created_at": "2026-01-10T09:00:00",
  "updated_at": "2026-01-15T10:30:00"
}
```

### Обновить текущего пользователя

```http
PATCH /api/v1/users/me
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "display_name": "Домашний бюджет"
}
```

Можно обновлять `display_name`, `email` и пароль. При смене `email` сервер должен проверить уникальность. При смене пароля сервер принимает текущий пароль и новый пароль, затем обновляет `password_hash`.

## Категории доходов

Ресурс работает с таблицей `income_categories`. Все операции выполняются только с категориями текущего пользователя.

### Получить список категорий доходов

```http
GET /api/v1/income-categories?is_active=true
```

Параметры:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `is_active` | boolean | Фильтр по активности. Если не передан, возвращаются все категории. |

Ответ:

```json
[
  {
    "id": 1,
    "name": "Зарплата",
    "is_active": true
  }
]
```

### Получить категорию доходов

```http
GET /api/v1/income-categories/{id}
```

### Создать категорию доходов

```http
POST /api/v1/income-categories
Content-Type: application/json

{
  "name": "Зарплата",
  "is_active": true
}
```

Валидация:

- `name` обязателен.
- `name` должен быть уникален в рамках текущего пользователя.
- `is_active` необязателен, по умолчанию `true`.

### Обновить категорию доходов

```http
PATCH /api/v1/income-categories/{id}
Content-Type: application/json

{
  "name": "Основная зарплата",
  "is_active": true
}
```

### Удалить категорию доходов

```http
DELETE /api/v1/income-categories/{id}
```

Рекомендуемое поведение: установить `is_active = false`. Если нужно физическое удаление, сервер должен вернуть `409 Conflict`, когда категория используется в `income_subcategories` или `incomes`.

## Подкатегории доходов

Ресурс работает с таблицей `income_subcategories`. Все операции выполняются только с подкатегориями текущего пользователя.

### Получить список подкатегорий доходов

```http
GET /api/v1/income-subcategories?category_id=1&is_active=true
```

Параметры:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `category_id` | integer | Фильтр по категории доходов. |
| `is_active` | boolean | Фильтр по активности. |

Ответ:

```json
[
  {
    "id": 1,
    "category_id": 1,
    "name": "Аванс",
    "is_active": true
  }
]
```

### Получить подкатегорию доходов

```http
GET /api/v1/income-subcategories/{id}
```

### Создать подкатегорию доходов

```http
POST /api/v1/income-subcategories
Content-Type: application/json

{
  "category_id": 1,
  "name": "Аванс",
  "is_active": true
}
```

Валидация:

- `category_id` обязателен и должен ссылаться на существующую категорию доходов текущего пользователя.
- `name` обязателен.
- Пара `category_id` и `name` должна быть уникальной.

### Обновить подкатегорию доходов

```http
PATCH /api/v1/income-subcategories/{id}
Content-Type: application/json

{
  "category_id": 1,
  "name": "Премия",
  "is_active": true
}
```

### Удалить подкатегорию доходов

```http
DELETE /api/v1/income-subcategories/{id}
```

Рекомендуемое поведение: установить `is_active = false`. При физическом удалении сервер должен вернуть `409 Conflict`, если подкатегория используется в `incomes`.

## Доходы

Ресурс работает с таблицей `incomes`. Все операции выполняются только с доходами текущего пользователя.

## Отчетные периоды

Ресурс работает с таблицей `reporting_periods`. Период начинается 15 числа и заканчивается 14 числа следующего месяца включительно.

```http
GET  /api/v1/reporting-periods
GET  /api/v1/reporting-periods/active
POST /api/v1/reporting-periods/{id}/close
```

Пример ответа:

```json
{
  "id": 1,
  "start_date": "2026-05-15",
  "end_date": "2026-06-14",
  "is_closed": false,
  "closed_at": null,
  "created_at": "2026-05-15T00:00:00Z"
}
```

`GET /reporting-periods/active` возвращает активный период. Если у пользователя еще нет периода, сервер создает период, соответствующий текущей дате.

`POST /reporting-periods/{id}/close` закрывает указанный активный период и возвращает новый активный период. Повторное закрытие уже закрытого периода возвращает `409 Conflict`.

### Получить список доходов

```http
GET /api/v1/incomes?date_from=2026-01-01&date_to=2026-01-31&category_id=1&subcategory_id=2&limit=50&offset=0
```

Параметры:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `date_from` | date | Начало периода по `operation_date`, включительно. |
| `date_to` | date | Конец периода по `operation_date`, включительно. |
| `category_id` | integer | Фильтр по категории доходов. |
| `subcategory_id` | integer | Фильтр по подкатегории доходов. |
| `limit` | integer | Размер страницы. |
| `offset` | integer | Смещение страницы. |

Ответ:

```json
{
  "items": [
    {
      "id": 1,
      "category_id": 1,
      "subcategory_id": 2,
      "period_id": 1,
      "amount_rub": "150000.00",
      "amount_usd": "1650.00",
      "operation_date": "2026-01-15",
      "comment": "Зарплата за январь",
      "created_at": "2026-01-15T10:30:00",
      "updated_at": "2026-01-15T10:30:00"
    }
  ],
  "limit": 50,
  "offset": 0,
  "total": 1
}
```

### Получить доход

```http
GET /api/v1/incomes/{id}
```

### Создать доход

```http
POST /api/v1/incomes
Content-Type: application/json

{
  "category_id": 1,
  "subcategory_id": 2,
  "amount_rub": "150000.00",
  "amount_usd": "1650.00",
  "comment": "Зарплата за январь"
}
```

Валидация:

- `category_id` обязателен и должен ссылаться на существующую категорию доходов текущего пользователя.
- `subcategory_id` обязателен и должен ссылаться на существующую подкатегорию доходов текущего пользователя.
- `subcategory_id` должен принадлежать указанному `category_id`.
- `amount_rub` и `amount_usd` обязательны и должны быть больше или равны 0.
- Активный `period_id` и `operation_date` назначаются сервером. Клиент не передает дату операции.
- `comment` необязателен.

### Обновить доход

```http
PATCH /api/v1/incomes/{id}
Content-Type: application/json

{
  "amount_rub": "155000.00",
  "amount_usd": "1700.00",
  "comment": "Зарплата и премия"
}
```

Если в запросе меняется `category_id` или `subcategory_id`, сервер должен повторно проверить принадлежность подкатегории категории.

### Удалить доход

```http
DELETE /api/v1/incomes/{id}
```

## Категории расходов

Ресурс работает с таблицей `expense_categories`. Все операции выполняются только с категориями текущего пользователя.

```http
GET    /api/v1/expense-categories?is_active=true
GET    /api/v1/expense-categories/{id}
POST   /api/v1/expense-categories
PATCH  /api/v1/expense-categories/{id}
DELETE /api/v1/expense-categories/{id}
```

Тело создания и обновления такое же, как у категорий доходов:

```json
{
  "name": "Продукты",
  "is_active": true
}
```

Правила валидации такие же, как у `income_categories`, но применяются к `expense_categories`.

## Подкатегории расходов

Ресурс работает с таблицей `expense_subcategories`. Все операции выполняются только с подкатегориями текущего пользователя.

```http
GET    /api/v1/expense-subcategories?category_id=1&is_active=true
GET    /api/v1/expense-subcategories/{id}
POST   /api/v1/expense-subcategories
PATCH  /api/v1/expense-subcategories/{id}
DELETE /api/v1/expense-subcategories/{id}
```

Тело создания и обновления такое же, как у подкатегорий доходов:

```json
{
  "category_id": 1,
  "name": "Супермаркеты",
  "is_active": true
}
```

Правила валидации такие же, как у `income_subcategories`, но применяются к `expense_subcategories`.

## Расходы

Ресурс работает с таблицей `expenses`. Все операции выполняются только с расходами текущего пользователя.

### Получить список расходов

```http
GET /api/v1/expenses?date_from=2026-01-01&date_to=2026-01-31&category_id=1&subcategory_id=2&limit=50&offset=0
```

Параметры такие же, как у списка доходов.

Ответ:

```json
{
  "items": [
    {
      "id": 1,
      "category_id": 1,
      "subcategory_id": 2,
      "period_id": 1,
      "amount_rub": "8500.00",
      "amount_usd": "93.50",
      "operation_date": "2026-01-16",
      "comment": "Покупки на неделю",
      "created_at": "2026-01-16T18:45:00",
      "updated_at": "2026-01-16T18:45:00"
    }
  ],
  "limit": 50,
  "offset": 0,
  "total": 1
}
```

### Получить расход

```http
GET /api/v1/expenses/{id}
```

### Создать расход

```http
POST /api/v1/expenses
Content-Type: application/json

{
  "category_id": 1,
  "subcategory_id": 2,
  "amount_rub": "8500.00",
  "amount_usd": "93.50",
  "comment": "Покупки на неделю"
}
```

Валидация:

- `category_id` обязателен и должен ссылаться на существующую категорию расходов текущего пользователя.
- `subcategory_id` необязателен для расходов.
- Если `subcategory_id` передан, он должен ссылаться на существующую подкатегорию расходов текущего пользователя и принадлежать указанному `category_id`.
- `amount_rub` и `amount_usd` обязательны и должны быть больше или равны 0.
- Активный `period_id` и `operation_date` назначаются сервером. Клиент не передает дату операции.
- `comment` необязателен.

### Обновить расход

```http
PATCH /api/v1/expenses/{id}
Content-Type: application/json

{
  "amount_rub": "9200.00",
  "amount_usd": "101.20",
  "comment": "Покупки на неделю и бытовая химия"
}
```

Если в запросе меняется `category_id` или `subcategory_id`, сервер должен повторно проверить принадлежность подкатегории категории.

### Удалить расход

```http
DELETE /api/v1/expenses/{id}
```

## Сводные операции

Для клиентского приложения может быть удобен общий read-only endpoint, который объединяет доходы и расходы в одну ленту.

```http
GET /api/v1/operations?type=expense&date_from=2026-01-01&date_to=2026-01-31&limit=50&offset=0
```

Параметры:

| Параметр | Тип | Описание |
| --- | --- | --- |
| `type` | string | `income`, `expense` или пусто для всех операций. |
| `date_from` | date | Начало периода, включительно. |
| `date_to` | date | Конец периода, включительно. |
| `category_id` | integer | Фильтр по категории внутри выбранного типа. |
| `subcategory_id` | integer | Фильтр по подкатегории внутри выбранного типа. |
| `limit` | integer | Размер страницы. |
| `offset` | integer | Смещение страницы. |

Ответ:

```json
{
  "items": [
    {
      "type": "expense",
      "id": 1,
      "category_id": 1,
      "subcategory_id": 2,
      "period_id": 1,
      "amount_rub": "8500.00",
      "amount_usd": "93.50",
      "operation_date": "2026-01-16",
      "comment": "Покупки на неделю"
    }
  ],
  "limit": 50,
  "offset": 0,
  "total": 1
}
```

## Отчеты

Отчеты не являются CRUD-ресурсами, но обычно нужны поверх этой схемы.

```http
GET /api/v1/reports/summary?date_from=2026-01-01&date_to=2026-01-31
GET /api/v1/reports/by-category?type=expense&date_from=2026-01-01&date_to=2026-01-31
GET /api/v1/reports/by-subcategory?type=income&date_from=2026-01-01&date_to=2026-01-31
```

Пример ответа для сводки:

```json
{
  "income": {
    "amount_rub": "150000.00",
    "amount_usd": "1650.00"
  },
  "expense": {
    "amount_rub": "8500.00",
    "amount_usd": "93.50"
  },
  "balance": {
    "amount_rub": "141500.00",
    "amount_usd": "1556.50"
  }
}
```
