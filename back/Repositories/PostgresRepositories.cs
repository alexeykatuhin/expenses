using FamilyExpenses.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace FamilyExpenses.Api.Repositories;

internal static class Pg
{
    public static void AddNullable(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
}

public sealed class PostgresDatabaseInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS users (
            id BIGSERIAL PRIMARY KEY,
            email VARCHAR(255) NOT NULL UNIQUE,
            password_hash VARCHAR(255) NOT NULL,
            display_name VARCHAR(150),
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            last_login_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS refresh_tokens (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            token_hash VARCHAR(255) NOT NULL UNIQUE,
            user_agent TEXT,
            ip_address TEXT,
            expires_at TIMESTAMPTZ NOT NULL,
            revoked_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS income_categories (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            name VARCHAR(150) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            CONSTRAINT uq_income_categories_user_name UNIQUE (user_id, name),
            CONSTRAINT uq_income_categories_id_user UNIQUE (id, user_id)
        );

        CREATE TABLE IF NOT EXISTS expense_categories (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            name VARCHAR(150) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            CONSTRAINT uq_expense_categories_user_name UNIQUE (user_id, name),
            CONSTRAINT uq_expense_categories_id_user UNIQUE (id, user_id)
        );

        CREATE TABLE IF NOT EXISTS income_subcategories (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            category_id BIGINT NOT NULL,
            name VARCHAR(150) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            CONSTRAINT fk_income_subcategories_category_user
                FOREIGN KEY (category_id, user_id)
                REFERENCES income_categories(id, user_id)
                ON DELETE CASCADE,
            CONSTRAINT uq_income_subcategories_category_name UNIQUE (category_id, name),
            CONSTRAINT uq_income_subcategories_id_category_user UNIQUE (id, category_id, user_id)
        );

        CREATE TABLE IF NOT EXISTS expense_subcategories (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            category_id BIGINT NOT NULL,
            name VARCHAR(150) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            CONSTRAINT fk_expense_subcategories_category_user
                FOREIGN KEY (category_id, user_id)
                REFERENCES expense_categories(id, user_id)
                ON DELETE CASCADE,
            CONSTRAINT uq_expense_subcategories_category_name UNIQUE (category_id, name),
            CONSTRAINT uq_expense_subcategories_id_category_user UNIQUE (id, category_id, user_id)
        );

        CREATE TABLE IF NOT EXISTS reporting_periods (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            start_date DATE NOT NULL,
            end_date DATE NOT NULL,
            is_closed BOOLEAN NOT NULL DEFAULT FALSE,
            closed_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uq_reporting_periods_user_start UNIQUE (user_id, start_date),
            CONSTRAINT uq_reporting_periods_id_user UNIQUE (id, user_id),
            CONSTRAINT chk_reporting_periods_dates CHECK (end_date >= start_date)
        );

        CREATE TABLE IF NOT EXISTS currencies (
            code CHAR(3) PRIMARY KEY,
            name VARCHAR(150) NOT NULL,
            minor_unit SMALLINT,
            is_active BOOLEAN NOT NULL DEFAULT TRUE
        );

        INSERT INTO currencies (code, name, minor_unit, is_active)
        VALUES
            ('AED', 'UAE Dirham', 2, TRUE),
            ('AMD', 'Armenian Dram', 2, TRUE),
            ('AUD', 'Australian Dollar', 2, TRUE),
            ('AZN', 'Azerbaijan Manat', 2, TRUE),
            ('BYN', 'Belarusian Ruble', 2, TRUE),
            ('BRL', 'Brazilian Real', 2, TRUE),
            ('CAD', 'Canadian Dollar', 2, TRUE),
            ('CHF', 'Swiss Franc', 2, TRUE),
            ('CNY', 'Yuan Renminbi', 2, TRUE),
            ('EUR', 'Euro', 2, TRUE),
            ('GBP', 'Pound Sterling', 2, TRUE),
            ('GEL', 'Lari', 2, TRUE),
            ('HKD', 'Hong Kong Dollar', 2, TRUE),
            ('ILS', 'New Israeli Sheqel', 2, TRUE),
            ('INR', 'Indian Rupee', 2, TRUE),
            ('JPY', 'Japanese Yen', 0, TRUE),
            ('KHR', 'Riel', 2, TRUE),
            ('KGS', 'Som', 2, TRUE),
            ('KRW', 'South Korean Won', 0, TRUE),
            ('KZT', 'Tenge', 2, TRUE),
            ('RUB', 'Russian Ruble', 2, TRUE),
            ('SGD', 'Singapore Dollar', 2, TRUE),
            ('THB', 'Baht', 2, TRUE),
            ('TRY', 'Turkish Lira', 2, TRUE),
            ('TWD', 'New Taiwan Dollar', 2, TRUE),
            ('USD', 'US Dollar', 2, TRUE),
            ('VND', 'Vietnamese Dong', 0, TRUE),
            ('UZS', 'Uzbekistan Sum', 2, TRUE)
        ON CONFLICT (code) DO UPDATE
        SET name = EXCLUDED.name,
            minor_unit = EXCLUDED.minor_unit,
            is_active = EXCLUDED.is_active;

        CREATE TABLE IF NOT EXISTS incomes (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            category_id BIGINT NOT NULL,
            subcategory_id BIGINT,
            period_id BIGINT NOT NULL,
            original_amount NUMERIC(14, 2) NOT NULL DEFAULT 0 CHECK (original_amount >= 0),
            currency_code CHAR(3) NOT NULL DEFAULT 'RUB' REFERENCES currencies(code),
            amount_rub NUMERIC(14, 2) NOT NULL CHECK (amount_rub >= 0),
            amount_usd NUMERIC(14, 2) NOT NULL CHECK (amount_usd >= 0),
            exchange_rate_date DATE,
            rub_rate NUMERIC(18, 8),
            usd_rate NUMERIC(18, 8),
            operation_date DATE NOT NULL,
            comment TEXT,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
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

        CREATE TABLE IF NOT EXISTS expenses (
            id BIGSERIAL PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            category_id BIGINT NOT NULL,
            subcategory_id BIGINT,
            period_id BIGINT NOT NULL,
            original_amount NUMERIC(14, 2) NOT NULL DEFAULT 0 CHECK (original_amount >= 0),
            currency_code CHAR(3) NOT NULL DEFAULT 'RUB' REFERENCES currencies(code),
            amount_rub NUMERIC(14, 2) NOT NULL CHECK (amount_rub >= 0),
            amount_usd NUMERIC(14, 2) NOT NULL CHECK (amount_usd >= 0),
            exchange_rate_date DATE,
            rub_rate NUMERIC(18, 8),
            usd_rate NUMERIC(18, 8),
            operation_date DATE NOT NULL,
            comment TEXT,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
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

        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS period_id BIGINT;
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS period_id BIGINT;
        ALTER TABLE incomes ALTER COLUMN subcategory_id DROP NOT NULL;
        ALTER TABLE expenses ALTER COLUMN subcategory_id DROP NOT NULL;
        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS original_amount NUMERIC(14, 2) NOT NULL DEFAULT 0 CHECK (original_amount >= 0);
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS original_amount NUMERIC(14, 2) NOT NULL DEFAULT 0 CHECK (original_amount >= 0);
        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS currency_code CHAR(3) NOT NULL DEFAULT 'RUB' REFERENCES currencies(code);
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS currency_code CHAR(3) NOT NULL DEFAULT 'RUB' REFERENCES currencies(code);
        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS exchange_rate_date DATE;
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS exchange_rate_date DATE;
        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS rub_rate NUMERIC(18, 8);
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS rub_rate NUMERIC(18, 8);
        ALTER TABLE incomes ADD COLUMN IF NOT EXISTS usd_rate NUMERIC(18, 8);
        ALTER TABLE expenses ADD COLUMN IF NOT EXISTS usd_rate NUMERIC(18, 8);
        UPDATE incomes SET original_amount = amount_rub WHERE original_amount = 0 AND amount_rub > 0;
        UPDATE expenses SET original_amount = amount_rub WHERE original_amount = 0 AND amount_rub > 0;

        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON refresh_tokens (user_id);
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires_at ON refresh_tokens (expires_at);
        CREATE INDEX IF NOT EXISTS ix_income_categories_user_id ON income_categories (user_id);
        CREATE INDEX IF NOT EXISTS ix_expense_categories_user_id ON expense_categories (user_id);
        CREATE INDEX IF NOT EXISTS ix_income_subcategories_user_category ON income_subcategories (user_id, category_id);
        CREATE INDEX IF NOT EXISTS ix_expense_subcategories_user_category ON expense_subcategories (user_id, category_id);
        CREATE INDEX IF NOT EXISTS ix_reporting_periods_user_start ON reporting_periods (user_id, start_date DESC);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_reporting_periods_one_active ON reporting_periods (user_id) WHERE is_closed = FALSE;
        CREATE INDEX IF NOT EXISTS ix_incomes_user_operation_date ON incomes (user_id, operation_date);
        CREATE INDEX IF NOT EXISTS ix_expenses_user_operation_date ON expenses (user_id, operation_date);
        CREATE INDEX IF NOT EXISTS ix_incomes_user_currency_created ON incomes (user_id, currency_code, created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_expenses_user_currency_created ON expenses (user_id, currency_code, created_at DESC);

        SET TIME ZONE 'UTC';

        ALTER TABLE users
            ALTER COLUMN last_login_at TYPE TIMESTAMPTZ,
            ALTER COLUMN created_at TYPE TIMESTAMPTZ,
            ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

        ALTER TABLE refresh_tokens
            ALTER COLUMN expires_at TYPE TIMESTAMPTZ,
            ALTER COLUMN revoked_at TYPE TIMESTAMPTZ,
            ALTER COLUMN created_at TYPE TIMESTAMPTZ;

        ALTER TABLE reporting_periods
            ALTER COLUMN closed_at TYPE TIMESTAMPTZ,
            ALTER COLUMN created_at TYPE TIMESTAMPTZ;

        ALTER TABLE incomes
            ALTER COLUMN created_at TYPE TIMESTAMPTZ,
            ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

        ALTER TABLE expenses
            ALTER COLUMN created_at TYPE TIMESTAMPTZ,
            ALTER COLUMN updated_at TYPE TIMESTAMPTZ;
        """;

        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class PostgresUserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    public IReadOnlyCollection<User> GetAll()
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, email, password_hash, display_name, is_active, created_at, updated_at, last_login_at
            FROM users
            ORDER BY id
            """);
        using var reader = command.ExecuteReader();
        var users = new List<User>();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    public User? GetById(long id)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, email, password_hash, display_name, is_active, created_at, updated_at, last_login_at
            FROM users
            WHERE id = @id
            """);
        command.Parameters.AddWithValue("id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public User? GetActiveByEmail(string email)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, email, password_hash, display_name, is_active, created_at, updated_at, last_login_at
            FROM users
            WHERE lower(email) = lower(@email) AND is_active = TRUE
            """);
        command.Parameters.AddWithValue("email", email.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public bool EmailExists(string email, long? exceptId = null)
    {
        using var command = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1
                FROM users
                WHERE lower(email) = lower(@email)
                  AND (@except_id IS NULL OR id <> @except_id)
            )
            """);
        command.Parameters.AddWithValue("email", email.Trim());
        Pg.AddNullable(command, "except_id", NpgsqlDbType.Bigint, exceptId);
        return (bool)command.ExecuteScalar()!;
    }

    public User Add(string email, string passwordHash, string? displayName)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO users (email, password_hash, display_name)
            VALUES (@email, @password_hash, @display_name)
            RETURNING id, email, password_hash, display_name, is_active, created_at, updated_at, last_login_at
            """);
        command.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("password_hash", passwordHash);
        Pg.AddNullable(command, "display_name", NpgsqlDbType.Varchar, displayName);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadUser(reader);
    }

    public User Update(User user)
    {
        using var command = dataSource.CreateCommand("""
            UPDATE users
            SET email = @email,
                password_hash = @password_hash,
                display_name = @display_name,
                is_active = @is_active,
                last_login_at = @last_login_at,
                updated_at = @updated_at
            WHERE id = @id
            RETURNING id, email, password_hash, display_name, is_active, created_at, updated_at, last_login_at
            """);
        command.Parameters.AddWithValue("id", user.Id);
        command.Parameters.AddWithValue("email", user.Email);
        command.Parameters.AddWithValue("password_hash", user.PasswordHash);
        Pg.AddNullable(command, "display_name", NpgsqlDbType.Varchar, user.DisplayName);
        command.Parameters.AddWithValue("is_active", user.IsActive);
        Pg.AddNullable(command, "last_login_at", NpgsqlDbType.TimestampTz, user.LastLoginAt);
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = user.UpdatedAt;
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadUser(reader);
    }

    private static User ReadUser(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetBoolean(4),
        reader.GetDateTime(5),
        reader.GetDateTime(6),
        reader.IsDBNull(7) ? null : reader.GetDateTime(7));
}

public sealed class PostgresRefreshTokenRepository(NpgsqlDataSource dataSource) : IRefreshTokenRepository
{
    public RefreshToken? GetValidByHash(string tokenHash)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, user_id, token_hash, user_agent, ip_address, expires_at, revoked_at, created_at
            FROM refresh_tokens
            WHERE token_hash = @token_hash AND revoked_at IS NULL AND expires_at > @now
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = DateTime.UtcNow;
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRefreshToken(reader) : null;
    }

    public RefreshToken? GetByUserAndHash(long userId, string tokenHash)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, user_id, token_hash, user_agent, ip_address, expires_at, revoked_at, created_at
            FROM refresh_tokens
            WHERE user_id = @user_id AND token_hash = @token_hash
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRefreshToken(reader) : null;
    }

    public RefreshToken Add(long userId, string tokenHash, string? userAgent, string? ipAddress, DateTime expiresAt)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO refresh_tokens (user_id, token_hash, user_agent, ip_address, expires_at)
            VALUES (@user_id, @token_hash, @user_agent, @ip_address, @expires_at)
            RETURNING id, user_id, token_hash, user_agent, ip_address, expires_at, revoked_at, created_at
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        Pg.AddNullable(command, "user_agent", NpgsqlDbType.Text, userAgent);
        Pg.AddNullable(command, "ip_address", NpgsqlDbType.Text, ipAddress);
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value = expiresAt;
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadRefreshToken(reader);
    }

    public void Revoke(long id, DateTime revokedAt)
    {
        using var command = dataSource.CreateCommand("UPDATE refresh_tokens SET revoked_at = @revoked_at WHERE id = @id");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = revokedAt;
        command.ExecuteNonQuery();
    }

    private static RefreshToken ReadRefreshToken(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetDateTime(5),
        reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        reader.GetDateTime(7));
}

public sealed class PostgresCategoryRepository(NpgsqlDataSource dataSource) : ICategoryRepository
{
    public IReadOnlyCollection<Category> GetByUser(long userId, EntryKind kind, bool? isActive = null)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, name, is_active
            FROM {CategoryTable(kind)}
            WHERE user_id = @user_id AND (@is_active IS NULL OR is_active = @is_active)
            ORDER BY name
            """);
        command.Parameters.AddWithValue("user_id", userId);
        Pg.AddNullable(command, "is_active", NpgsqlDbType.Boolean, isActive);
        using var reader = command.ExecuteReader();
        var categories = new List<Category>();
        while (reader.Read())
        {
            categories.Add(ReadCategory(reader, kind));
        }

        return categories;
    }

    public Category? GetById(long userId, EntryKind kind, long id)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, name, is_active
            FROM {CategoryTable(kind)}
            WHERE id = @id AND user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCategory(reader, kind) : null;
    }

    public bool NameExists(long userId, EntryKind kind, string name, long? exceptId = null)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT EXISTS (
                SELECT 1
                FROM {CategoryTable(kind)}
                WHERE user_id = @user_id
                  AND lower(name) = lower(@name)
                  AND (@except_id IS NULL OR id <> @except_id)
            )
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("name", name.Trim());
        Pg.AddNullable(command, "except_id", NpgsqlDbType.Bigint, exceptId);
        return (bool)command.ExecuteScalar()!;
    }

    public Category Add(long userId, EntryKind kind, string name, bool isActive)
    {
        using var command = dataSource.CreateCommand($"""
            INSERT INTO {CategoryTable(kind)} (user_id, name, is_active)
            VALUES (@user_id, @name, @is_active)
            RETURNING id, user_id, name, is_active
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("name", name.Trim());
        command.Parameters.AddWithValue("is_active", isActive);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadCategory(reader, kind);
    }

    public Category Update(Category category)
    {
        using var command = dataSource.CreateCommand($"""
            UPDATE {CategoryTable(category.Kind)}
            SET name = @name, is_active = @is_active
            WHERE id = @id AND user_id = @user_id
            RETURNING id, user_id, name, is_active
            """);
        command.Parameters.AddWithValue("id", category.Id);
        command.Parameters.AddWithValue("user_id", category.UserId);
        command.Parameters.AddWithValue("name", category.Name.Trim());
        command.Parameters.AddWithValue("is_active", category.IsActive);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadCategory(reader, category.Kind);
    }

    private static Category ReadCategory(NpgsqlDataReader reader, EntryKind kind) =>
        new(reader.GetInt64(0), reader.GetInt64(1), kind, reader.GetString(2), reader.GetBoolean(3));

    private static string CategoryTable(EntryKind kind) =>
        kind == EntryKind.Income ? "income_categories" : "expense_categories";
}

public sealed class PostgresSubcategoryRepository(NpgsqlDataSource dataSource) : ISubcategoryRepository
{
    public IReadOnlyCollection<Subcategory> GetByUser(long userId, EntryKind kind, long? categoryId = null, bool? isActive = null)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, category_id, name, is_active
            FROM {SubcategoryTable(kind)}
            WHERE user_id = @user_id
              AND (@category_id IS NULL OR category_id = @category_id)
              AND (@is_active IS NULL OR is_active = @is_active)
            ORDER BY name
            """);
        command.Parameters.AddWithValue("user_id", userId);
        Pg.AddNullable(command, "category_id", NpgsqlDbType.Bigint, categoryId);
        Pg.AddNullable(command, "is_active", NpgsqlDbType.Boolean, isActive);
        using var reader = command.ExecuteReader();
        var subcategories = new List<Subcategory>();
        while (reader.Read())
        {
            subcategories.Add(ReadSubcategory(reader, kind));
        }

        return subcategories;
    }

    public Subcategory? GetById(long userId, EntryKind kind, long id)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, category_id, name, is_active
            FROM {SubcategoryTable(kind)}
            WHERE id = @id AND user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSubcategory(reader, kind) : null;
    }

    public bool NameExists(long userId, EntryKind kind, long categoryId, string name, long? exceptId = null)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT EXISTS (
                SELECT 1
                FROM {SubcategoryTable(kind)}
                WHERE user_id = @user_id
                  AND category_id = @category_id
                  AND lower(name) = lower(@name)
                  AND (@except_id IS NULL OR id <> @except_id)
            )
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("name", name.Trim());
        Pg.AddNullable(command, "except_id", NpgsqlDbType.Bigint, exceptId);
        return (bool)command.ExecuteScalar()!;
    }

    public Subcategory Add(long userId, EntryKind kind, long categoryId, string name, bool isActive)
    {
        using var command = dataSource.CreateCommand($"""
            INSERT INTO {SubcategoryTable(kind)} (user_id, category_id, name, is_active)
            VALUES (@user_id, @category_id, @name, @is_active)
            RETURNING id, user_id, category_id, name, is_active
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("name", name.Trim());
        command.Parameters.AddWithValue("is_active", isActive);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadSubcategory(reader, kind);
    }

    public Subcategory Update(Subcategory subcategory)
    {
        using var command = dataSource.CreateCommand($"""
            UPDATE {SubcategoryTable(subcategory.Kind)}
            SET category_id = @category_id, name = @name, is_active = @is_active
            WHERE id = @id AND user_id = @user_id
            RETURNING id, user_id, category_id, name, is_active
            """);
        command.Parameters.AddWithValue("id", subcategory.Id);
        command.Parameters.AddWithValue("user_id", subcategory.UserId);
        command.Parameters.AddWithValue("category_id", subcategory.CategoryId);
        command.Parameters.AddWithValue("name", subcategory.Name.Trim());
        command.Parameters.AddWithValue("is_active", subcategory.IsActive);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadSubcategory(reader, subcategory.Kind);
    }

    private static Subcategory ReadSubcategory(NpgsqlDataReader reader, EntryKind kind) =>
        new(reader.GetInt64(0), reader.GetInt64(1), kind, reader.GetInt64(2), reader.GetString(3), reader.GetBoolean(4));

    private static string SubcategoryTable(EntryKind kind) =>
        kind == EntryKind.Income ? "income_subcategories" : "expense_subcategories";
}

public sealed class PostgresCurrencyRepository(NpgsqlDataSource dataSource) : ICurrencyRepository
{
    public IReadOnlyCollection<CurrencyUsage> GetActiveWithLastUsage(long userId)
    {
        using var command = dataSource.CreateCommand("""
            WITH usage AS (
                SELECT currency_code, max(created_at) AS last_used_at
                FROM (
                    SELECT currency_code, created_at FROM incomes WHERE user_id = @user_id
                    UNION ALL
                    SELECT currency_code, created_at FROM expenses WHERE user_id = @user_id
                ) rows
                GROUP BY currency_code
            )
            SELECT c.code, c.name, c.minor_unit, c.is_active, u.last_used_at
            FROM currencies c
            LEFT JOIN usage u ON u.currency_code = c.code
            WHERE c.is_active = TRUE
            ORDER BY (u.last_used_at IS NULL), u.last_used_at DESC, c.code ASC
            """);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        var currencies = new List<CurrencyUsage>();
        while (reader.Read())
        {
            currencies.Add(new CurrencyUsage(ReadCurrency(reader), reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
        }

        return currencies;
    }

    public Currency? GetActiveByCode(string code)
    {
        using var command = dataSource.CreateCommand("""
            SELECT code, name, minor_unit, is_active
            FROM currencies
            WHERE code = @code AND is_active = TRUE
            """);
        command.Parameters.AddWithValue("code", NormalizeCode(code));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCurrency(reader) : null;
    }

    public Currency AddOrActivate(string code, string name, int? minorUnit)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO currencies (code, name, minor_unit, is_active)
            VALUES (@code, @name, @minor_unit, TRUE)
            ON CONFLICT (code) DO UPDATE
            SET name = EXCLUDED.name,
                minor_unit = EXCLUDED.minor_unit,
                is_active = TRUE
            RETURNING code, name, minor_unit, is_active
            """);
        command.Parameters.AddWithValue("code", NormalizeCode(code));
        command.Parameters.AddWithValue("name", name.Trim());
        Pg.AddNullable(command, "minor_unit", NpgsqlDbType.Smallint, minorUnit is null ? null : (short)minorUnit.Value);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadCurrency(reader);
    }

    private static Currency ReadCurrency(NpgsqlDataReader reader) => new(
        reader.GetString(0).Trim(),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetInt16(2),
        reader.GetBoolean(3));

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

public sealed class PostgresOperationRepository(NpgsqlDataSource dataSource) : IOperationRepository
{
    public IReadOnlyCollection<Operation> GetByUser(long userId, EntryKind? kind = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, long? categoryId = null, long? subcategoryId = null, long? periodId = null)
    {
        if (kind is not null)
        {
            return GetByUserFromTable(userId, kind.Value, dateFrom, dateTo, categoryId, subcategoryId, periodId);
        }

        return GetByUserFromTable(userId, EntryKind.Income, dateFrom, dateTo, categoryId, subcategoryId, periodId)
            .Concat(GetByUserFromTable(userId, EntryKind.Expense, dateFrom, dateTo, categoryId, subcategoryId, periodId))
            .OrderByDescending(x => x.OperationDate)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    public Operation? GetById(long userId, EntryKind kind, long id)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, category_id, subcategory_id, period_id, original_amount, currency_code, amount_rub, amount_usd, exchange_rate_date, rub_rate, usd_rate, operation_date, comment, created_at, updated_at
            FROM {OperationTable(kind)}
            WHERE id = @id AND user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOperation(reader, kind) : null;
    }

    public Operation Add(long userId, EntryKind kind, long categoryId, long? subcategoryId, long periodId, decimal originalAmount, string currencyCode, ConvertedMoney converted, DateOnly operationDate, string? comment)
    {
        using var command = dataSource.CreateCommand($"""
            INSERT INTO {OperationTable(kind)}
                (user_id, category_id, subcategory_id, period_id, original_amount, currency_code, amount_rub, amount_usd, exchange_rate_date, rub_rate, usd_rate, operation_date, comment)
            VALUES
                (@user_id, @category_id, @subcategory_id, @period_id, @original_amount, @currency_code, @amount_rub, @amount_usd, @exchange_rate_date, @rub_rate, @usd_rate, @operation_date, @comment)
            RETURNING id, user_id, category_id, subcategory_id, period_id, original_amount, currency_code, amount_rub, amount_usd, exchange_rate_date, rub_rate, usd_rate, operation_date, comment, created_at, updated_at
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("category_id", categoryId);
        Pg.AddNullable(command, "subcategory_id", NpgsqlDbType.Bigint, subcategoryId);
        command.Parameters.AddWithValue("period_id", periodId);
        command.Parameters.AddWithValue("original_amount", originalAmount);
        command.Parameters.AddWithValue("currency_code", currencyCode.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("amount_rub", converted.AmountRub);
        command.Parameters.AddWithValue("amount_usd", converted.AmountUsd);
        Pg.AddNullable(command, "exchange_rate_date", NpgsqlDbType.Date, converted.ExchangeRateDate);
        Pg.AddNullable(command, "rub_rate", NpgsqlDbType.Numeric, converted.RubRate);
        Pg.AddNullable(command, "usd_rate", NpgsqlDbType.Numeric, converted.UsdRate);
        command.Parameters.AddWithValue("operation_date", operationDate);
        Pg.AddNullable(command, "comment", NpgsqlDbType.Text, comment);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadOperation(reader, kind);
    }

    public Operation Update(Operation operation)
    {
        using var command = dataSource.CreateCommand($"""
            UPDATE {OperationTable(operation.Kind)}
            SET category_id = @category_id,
                subcategory_id = @subcategory_id,
                period_id = @period_id,
                original_amount = @original_amount,
                currency_code = @currency_code,
                amount_rub = @amount_rub,
                amount_usd = @amount_usd,
                exchange_rate_date = @exchange_rate_date,
                rub_rate = @rub_rate,
                usd_rate = @usd_rate,
                operation_date = @operation_date,
                comment = @comment,
                updated_at = @updated_at
            WHERE id = @id AND user_id = @user_id
            RETURNING id, user_id, category_id, subcategory_id, period_id, original_amount, currency_code, amount_rub, amount_usd, exchange_rate_date, rub_rate, usd_rate, operation_date, comment, created_at, updated_at
            """);
        command.Parameters.AddWithValue("id", operation.Id);
        command.Parameters.AddWithValue("user_id", operation.UserId);
        command.Parameters.AddWithValue("category_id", operation.CategoryId);
        Pg.AddNullable(command, "subcategory_id", NpgsqlDbType.Bigint, operation.SubcategoryId);
        Pg.AddNullable(command, "period_id", NpgsqlDbType.Bigint, operation.PeriodId);
        command.Parameters.AddWithValue("original_amount", operation.OriginalAmount);
        command.Parameters.AddWithValue("currency_code", operation.CurrencyCode);
        command.Parameters.AddWithValue("amount_rub", operation.AmountRub);
        command.Parameters.AddWithValue("amount_usd", operation.AmountUsd);
        Pg.AddNullable(command, "exchange_rate_date", NpgsqlDbType.Date, operation.ExchangeRateDate);
        Pg.AddNullable(command, "rub_rate", NpgsqlDbType.Numeric, operation.RubRate);
        Pg.AddNullable(command, "usd_rate", NpgsqlDbType.Numeric, operation.UsdRate);
        command.Parameters.AddWithValue("operation_date", operation.OperationDate);
        Pg.AddNullable(command, "comment", NpgsqlDbType.Text, operation.Comment);
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = operation.UpdatedAt;
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadOperation(reader, operation.Kind);
    }

    public bool Remove(long id)
    {
        using var command = dataSource.CreateCommand("""
            WITH deleted_income AS (
                DELETE FROM incomes WHERE id = @id RETURNING id
            ),
            deleted_expense AS (
                DELETE FROM expenses WHERE id = @id RETURNING id
            )
            SELECT (SELECT count(*) FROM deleted_income) + (SELECT count(*) FROM deleted_expense)
            """);
        command.Parameters.AddWithValue("id", id);
        return (long)command.ExecuteScalar()! > 0;
    }

    private IReadOnlyCollection<Operation> GetByUserFromTable(long userId, EntryKind kind, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, long? periodId)
    {
        using var command = dataSource.CreateCommand($"""
            SELECT id, user_id, category_id, subcategory_id, period_id, original_amount, currency_code, amount_rub, amount_usd, exchange_rate_date, rub_rate, usd_rate, operation_date, comment, created_at, updated_at
            FROM {OperationTable(kind)}
            WHERE user_id = @user_id
              AND (@date_from IS NULL OR operation_date >= @date_from)
              AND (@date_to IS NULL OR operation_date <= @date_to)
              AND (@category_id IS NULL OR category_id = @category_id)
              AND (@subcategory_id IS NULL OR subcategory_id = @subcategory_id)
              AND (@period_id IS NULL OR period_id = @period_id)
            ORDER BY operation_date DESC, id DESC
            """);
        command.Parameters.AddWithValue("user_id", userId);
        Pg.AddNullable(command, "date_from", NpgsqlDbType.Date, dateFrom);
        Pg.AddNullable(command, "date_to", NpgsqlDbType.Date, dateTo);
        Pg.AddNullable(command, "category_id", NpgsqlDbType.Bigint, categoryId);
        Pg.AddNullable(command, "subcategory_id", NpgsqlDbType.Bigint, subcategoryId);
        Pg.AddNullable(command, "period_id", NpgsqlDbType.Bigint, periodId);
        using var reader = command.ExecuteReader();
        var operations = new List<Operation>();
        while (reader.Read())
        {
            operations.Add(ReadOperation(reader, kind));
        }

        return operations;
    }

    private static Operation ReadOperation(NpgsqlDataReader reader, EntryKind kind) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        kind,
        reader.GetInt64(2),
        reader.IsDBNull(3) ? null : reader.GetInt64(3),
        reader.IsDBNull(4) ? null : reader.GetInt64(4),
        reader.GetDecimal(5),
        reader.GetString(6).Trim(),
        reader.GetDecimal(7),
        reader.GetDecimal(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9),
        reader.IsDBNull(10) ? null : reader.GetDecimal(10),
        reader.IsDBNull(11) ? null : reader.GetDecimal(11),
        reader.GetFieldValue<DateOnly>(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.GetDateTime(14),
        reader.GetDateTime(15));

    private static string OperationTable(EntryKind kind) =>
        kind == EntryKind.Income ? "incomes" : "expenses";
}

public sealed class PostgresReportingPeriodRepository(NpgsqlDataSource dataSource) : IReportingPeriodRepository
{
    public IReadOnlyCollection<ReportingPeriod> GetByUser(long userId)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, user_id, start_date, end_date, is_closed, closed_at, created_at
            FROM reporting_periods
            WHERE user_id = @user_id
            ORDER BY start_date DESC
            """);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        var periods = new List<ReportingPeriod>();
        while (reader.Read())
        {
            periods.Add(ReadReportingPeriod(reader));
        }

        return periods;
    }

    public ReportingPeriod? GetActive(long userId)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, user_id, start_date, end_date, is_closed, closed_at, created_at
            FROM reporting_periods
            WHERE user_id = @user_id AND is_closed = FALSE
            ORDER BY start_date DESC
            LIMIT 1
            """);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadReportingPeriod(reader) : null;
    }

    public ReportingPeriod? GetById(long userId, long id)
    {
        using var command = dataSource.CreateCommand("""
            SELECT id, user_id, start_date, end_date, is_closed, closed_at, created_at
            FROM reporting_periods
            WHERE id = @id AND user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadReportingPeriod(reader) : null;
    }

    public ReportingPeriod Add(long userId, DateOnly startDate, DateOnly endDate)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO reporting_periods (user_id, start_date, end_date)
            VALUES (@user_id, @start_date, @end_date)
            RETURNING id, user_id, start_date, end_date, is_closed, closed_at, created_at
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("start_date", startDate);
        command.Parameters.AddWithValue("end_date", endDate);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadReportingPeriod(reader);
    }

    public ReportingPeriod Update(ReportingPeriod period)
    {
        using var command = dataSource.CreateCommand("""
            UPDATE reporting_periods
            SET is_closed = @is_closed,
                closed_at = @closed_at
            WHERE id = @id AND user_id = @user_id
            RETURNING id, user_id, start_date, end_date, is_closed, closed_at, created_at
            """);
        command.Parameters.AddWithValue("id", period.Id);
        command.Parameters.AddWithValue("user_id", period.UserId);
        command.Parameters.AddWithValue("is_closed", period.IsClosed);
        Pg.AddNullable(command, "closed_at", NpgsqlDbType.TimestampTz, period.ClosedAt);
        using var reader = command.ExecuteReader();
        reader.Read();
        return ReadReportingPeriod(reader);
    }

    private static ReportingPeriod ReadReportingPeriod(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        reader.GetFieldValue<DateOnly>(2),
        reader.GetFieldValue<DateOnly>(3),
        reader.GetBoolean(4),
        reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        reader.GetDateTime(6));
}
