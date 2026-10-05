# Схема БД для учета семейных доходов и расходов

## Валюты

Справочник валют глобальный. Операции хранят исходную сумму и валюту, а `amount_rub` и `amount_usd` являются сохраненным результатом конвертации на момент создания или пересчета операции.

```sql
CREATE TABLE currencies (
    code        CHAR(3) PRIMARY KEY,
    name        VARCHAR(150) NOT NULL,
    minor_unit  SMALLINT,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE
);
```

Новые поля в `incomes` и `expenses`:

```sql
original_amount     NUMERIC(14, 2) NOT NULL DEFAULT 0 CHECK (original_amount >= 0),
currency_code       CHAR(3) NOT NULL DEFAULT 'RUB' REFERENCES currencies(code),
exchange_rate_date  DATE,
rub_rate            NUMERIC(18, 8),
usd_rate            NUMERIC(18, 8)
```

Миграция существующих операций засевает `currencies`, выставляет `currency_code = 'RUB'`, копирует `original_amount = amount_rub` и не пересчитывает уже сохраненные `amount_rub` / `amount_usd`.

```sql
CREATE INDEX ix_incomes_user_currency_created
    ON incomes (user_id, currency_code, created_at DESC);

CREATE INDEX ix_expenses_user_currency_created
    ON expenses (user_id, currency_code, created_at DESC);
```

Схема разделяет доходы и расходы как разные сущности. Для них используются отдельные таблицы операций, категорий и подкатегорий.

Данные справочников и операций привязаны к пользователю через `user_id`. Это позволяет авторизованному пользователю видеть и изменять только свои доходы, расходы и категории.

## Авторизация

```sql
CREATE TABLE users (
    id              BIGSERIAL PRIMARY KEY,
    email           VARCHAR(255) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    display_name    VARCHAR(150),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at   TIMESTAMP,
    created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE refresh_tokens (
    id           BIGSERIAL PRIMARY KEY,
    user_id      BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash   VARCHAR(255) NOT NULL UNIQUE,
    user_agent   TEXT,
    ip_address   INET,
    expires_at   TIMESTAMP NOT NULL,
    revoked_at   TIMESTAMP,
    created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

## Справочники доходов

```sql
CREATE TABLE income_categories (
    id          BIGSERIAL PRIMARY KEY,
    user_id     BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name        VARCHAR(150) NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_income_categories_user_name
        UNIQUE (user_id, name),
    CONSTRAINT uq_income_categories_id_user
        UNIQUE (id, user_id)
);

CREATE TABLE income_subcategories (
    id           BIGSERIAL PRIMARY KEY,
    user_id      BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    category_id  BIGINT NOT NULL,
    name         VARCHAR(150) NOT NULL,
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_income_subcategories_category_user
        FOREIGN KEY (category_id, user_id)
        REFERENCES income_categories(id, user_id)
        ON DELETE CASCADE,
    CONSTRAINT uq_income_subcategories_category_name
        UNIQUE (category_id, name),
    CONSTRAINT uq_income_subcategories_id_category_user
        UNIQUE (id, category_id, user_id)
);
```

## Справочники расходов

```sql
CREATE TABLE expense_categories (
    id          BIGSERIAL PRIMARY KEY,
    user_id     BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name        VARCHAR(150) NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_expense_categories_user_name
        UNIQUE (user_id, name),
    CONSTRAINT uq_expense_categories_id_user
        UNIQUE (id, user_id)
);

CREATE TABLE expense_subcategories (
    id           BIGSERIAL PRIMARY KEY,
    user_id      BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    category_id  BIGINT NOT NULL,
    name         VARCHAR(150) NOT NULL,
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_expense_subcategories_category_user
        FOREIGN KEY (category_id, user_id)
        REFERENCES expense_categories(id, user_id)
        ON DELETE CASCADE,
    CONSTRAINT uq_expense_subcategories_category_name
        UNIQUE (category_id, name),
    CONSTRAINT uq_expense_subcategories_id_category_user
        UNIQUE (id, category_id, user_id)
);
```

## Отчетные периоды

Отчетный период принадлежит пользователю и длится с 15 числа одного месяца по 14 число следующего месяца включительно. Одновременно у пользователя должен быть только один незакрытый период. Все новые операции записываются в текущий активный период до его закрытия.

```sql
CREATE TABLE reporting_periods (
    id          BIGSERIAL PRIMARY KEY,
    user_id     BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    start_date  DATE NOT NULL,
    end_date    DATE NOT NULL,
    is_closed   BOOLEAN NOT NULL DEFAULT FALSE,
    closed_at   TIMESTAMPTZ,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT uq_reporting_periods_user_start
        UNIQUE (user_id, start_date),
    CONSTRAINT uq_reporting_periods_id_user
        UNIQUE (id, user_id),
    CONSTRAINT chk_reporting_periods_dates
        CHECK (end_date >= start_date)
);
```

## Доходы

```sql
CREATE TABLE incomes (
    id              BIGSERIAL PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    category_id      BIGINT NOT NULL,
    subcategory_id   BIGINT NOT NULL,
    period_id        BIGINT NOT NULL,

    amount_rub       NUMERIC(14, 2) NOT NULL CHECK (amount_rub >= 0),
    amount_usd       NUMERIC(14, 2) NOT NULL CHECK (amount_usd >= 0),

    -- Заполняется сервером датой начала отчетного периода для совместимости с фильтрами по датам.
    operation_date   DATE NOT NULL,
    comment          TEXT,

    created_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_incomes_category_user
        FOREIGN KEY (category_id, user_id)
        REFERENCES income_categories(id, user_id),
    CONSTRAINT fk_incomes_subcategory_category_user
        FOREIGN KEY (subcategory_id, category_id, user_id)
        REFERENCES income_subcategories(id, category_id, user_id),
    CONSTRAINT fk_incomes_period
        FOREIGN KEY (period_id, user_id)
        REFERENCES reporting_periods(id, user_id)
);
```

## Расходы

```sql
CREATE TABLE expenses (
    id              BIGSERIAL PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    category_id      BIGINT NOT NULL,
    subcategory_id   BIGINT,
    period_id        BIGINT NOT NULL,

    amount_rub       NUMERIC(14, 2) NOT NULL CHECK (amount_rub >= 0),
    amount_usd       NUMERIC(14, 2) NOT NULL CHECK (amount_usd >= 0),

    -- Заполняется сервером датой начала отчетного периода для совместимости с фильтрами по датам.
    operation_date   DATE NOT NULL,
    comment          TEXT,

    created_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_expenses_category_user
        FOREIGN KEY (category_id, user_id)
        REFERENCES expense_categories(id, user_id),
    CONSTRAINT fk_expenses_subcategory_category_user
        FOREIGN KEY (subcategory_id, category_id, user_id)
        REFERENCES expense_subcategories(id, category_id, user_id),
    CONSTRAINT fk_expenses_period
        FOREIGN KEY (period_id, user_id)
        REFERENCES reporting_periods(id, user_id)
);
```

## Индексы

```sql
CREATE INDEX ix_refresh_tokens_user_id
    ON refresh_tokens (user_id);

CREATE INDEX ix_refresh_tokens_expires_at
    ON refresh_tokens (expires_at);

CREATE INDEX ix_income_categories_user_id
    ON income_categories (user_id);

CREATE INDEX ix_income_subcategories_user_category
    ON income_subcategories (user_id, category_id);

CREATE INDEX ix_expense_categories_user_id
    ON expense_categories (user_id);

CREATE INDEX ix_expense_subcategories_user_category
    ON expense_subcategories (user_id, category_id);

CREATE INDEX ix_reporting_periods_user_start
    ON reporting_periods (user_id, start_date DESC);

CREATE UNIQUE INDEX ux_reporting_periods_one_active
    ON reporting_periods (user_id)
    WHERE is_closed = FALSE;

CREATE INDEX ix_incomes_user_operation_date
    ON incomes (user_id, operation_date);

CREATE INDEX ix_incomes_user_category_date
    ON incomes (user_id, category_id, operation_date);

CREATE INDEX ix_incomes_user_subcategory_date
    ON incomes (user_id, subcategory_id, operation_date);

CREATE INDEX ix_expenses_user_operation_date
    ON expenses (user_id, operation_date);

CREATE INDEX ix_expenses_user_category_date
    ON expenses (user_id, category_id, operation_date);

CREATE INDEX ix_expenses_user_subcategory_date
    ON expenses (user_id, subcategory_id, operation_date);
```

## Проверка принадлежности подкатегории категории

Принадлежность подкатегории категории уже проверяется составными внешними ключами:

- `incomes(subcategory_id, category_id, user_id)` ссылается на `income_subcategories(id, category_id, user_id)`;
- `expenses(subcategory_id, category_id, user_id)` ссылается на `expense_subcategories(id, category_id, user_id)`.

Такая схема одновременно проверяет, что:

- подкатегория относится к выбранной категории;
- категория и подкатегория принадлежат тому же пользователю, что и операция.

## Примеры данных

```sql
INSERT INTO users (email, password_hash, display_name) VALUES
('user@example.com', '$2b$12$example_hash', 'Семейный бюджет');

INSERT INTO income_categories (user_id, name) VALUES
(1, 'Зарплата'),
(1, 'Подарки'),
(1, 'Инвестиции'),
(1, 'Подработка');

INSERT INTO expense_categories (user_id, name) VALUES
(1, 'Продукты'),
(1, 'Транспорт'),
(1, 'Жилье'),
(1, 'Здоровье'),
(1, 'Развлечения'),
(1, 'Одежда'),
(1, 'Образование');
```
