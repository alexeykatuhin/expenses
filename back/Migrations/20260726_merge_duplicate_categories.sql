-- Merge duplicate categories for user_id = 2.
--
-- Approved mappings:
--   income:  Avito (3)        -> avito (7)
--   income:  Lesson (6)       -> lesson (14)
--   income:  stock (22)       -> stocks (23)
--   expense: nanny (99)       -> Nanny (16), then normalize to nanny
--   expense: Pharma (71)      -> pharma (3)
--   expense: entertaiment (10)-> entertainment (87)
--   expense: taxes (108)      -> tax (107)
--
-- The script is transactional and safe to rerun. It deliberately validates
-- the expected IDs and names before changing data.

BEGIN;

LOCK TABLE
    incomes,
    expenses,
    income_subcategories,
    expense_subcategories,
    income_categories,
    expense_categories
IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE category_merge_baseline ON COMMIT DROP AS
SELECT
    (SELECT COUNT(*) FROM incomes) AS income_count,
    (SELECT COALESCE(SUM(original_amount), 0) FROM incomes) AS income_original_amount,
    (SELECT COALESCE(SUM(amount_rub), 0) FROM incomes) AS income_amount_rub,
    (SELECT COALESCE(SUM(amount_usd), 0) FROM incomes) AS income_amount_usd,
    (SELECT COUNT(*) FROM expenses) AS expense_count,
    (SELECT COALESCE(SUM(original_amount), 0) FROM expenses) AS expense_original_amount,
    (SELECT COALESCE(SUM(amount_rub), 0) FROM expenses) AS expense_amount_rub,
    (SELECT COALESCE(SUM(amount_usd), 0) FROM expenses) AS expense_amount_usd;

DO $validation$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM income_categories
        WHERE id = 7 AND user_id = 2 AND name = 'avito'
    ) THEN
        RAISE EXCEPTION 'Expected target income category avito (id=7, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 3 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_categories
           WHERE id = 3 AND user_id = 2 AND name = 'Avito'
       ) THEN
        RAISE EXCEPTION 'Unexpected source income category id=3';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 3 AND user_id = 2)
       AND (
           SELECT COUNT(*) FROM income_subcategories
           WHERE category_id = 3 AND user_id = 2
       ) <> 1 THEN
        RAISE EXCEPTION 'Avito (id=3) has unexpected subcategories';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 3 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_subcategories
           WHERE id = 2 AND category_id = 3 AND user_id = 2 AND name = 'default'
       ) THEN
        RAISE EXCEPTION 'Expected Avito/default source subcategory id=2';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM income_subcategories
        WHERE id = 11 AND category_id = 7 AND user_id = 2 AND name = 'default'
    ) THEN
        RAISE EXCEPTION 'Expected avito/default target subcategory id=11';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM income_categories
        WHERE id = 14 AND user_id = 2 AND name = 'lesson'
    ) THEN
        RAISE EXCEPTION 'Expected target income category lesson (id=14, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 6 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_categories
           WHERE id = 6 AND user_id = 2 AND name = 'Lesson'
       ) THEN
        RAISE EXCEPTION 'Unexpected source income category id=6';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 6 AND user_id = 2)
       AND (
           SELECT COUNT(*) FROM income_subcategories
           WHERE category_id = 6 AND user_id = 2
       ) <> 1 THEN
        RAISE EXCEPTION 'Lesson (id=6) has unexpected subcategories';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 6 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_subcategories
           WHERE id = 5 AND category_id = 6 AND user_id = 2 AND name = 'default'
       ) THEN
        RAISE EXCEPTION 'Expected Lesson/default source subcategory id=5';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM income_subcategories
        WHERE id = 71 AND category_id = 14 AND user_id = 2 AND name = 'default'
    ) THEN
        RAISE EXCEPTION 'Expected lesson/default target subcategory id=71';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM income_categories
        WHERE id = 23 AND user_id = 2 AND name = 'stocks'
    ) THEN
        RAISE EXCEPTION 'Expected target income category stocks (id=23, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 22 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_categories
           WHERE id = 22 AND user_id = 2 AND name = 'stock'
       ) THEN
        RAISE EXCEPTION 'Unexpected source income category id=22';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 22 AND user_id = 2)
       AND (
           SELECT COUNT(*) FROM income_subcategories
           WHERE category_id = 22 AND user_id = 2
       ) <> 1 THEN
        RAISE EXCEPTION 'stock (id=22) has unexpected subcategories';
    END IF;

    IF EXISTS (SELECT 1 FROM income_categories WHERE id = 22 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM income_subcategories
           WHERE id = 90 AND category_id = 22 AND user_id = 2 AND name = 'lukoil'
       ) THEN
        RAISE EXCEPTION 'Expected stock/lukoil source subcategory id=90';
    END IF;

    IF EXISTS (
        SELECT 1 FROM income_categories WHERE id = 22 AND user_id = 2
    ) AND EXISTS (
        SELECT 1 FROM income_subcategories
        WHERE category_id = 23 AND user_id = 2 AND name = 'lukoil'
    ) THEN
        RAISE EXCEPTION 'Target stocks category already has a lukoil subcategory';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM expense_categories
        WHERE id = 16 AND user_id = 2 AND name IN ('Nanny', 'nanny')
    ) THEN
        RAISE EXCEPTION 'Expected target expense category Nanny/nanny (id=16, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM expense_categories WHERE id = 99 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM expense_categories
           WHERE id = 99 AND user_id = 2 AND name = 'nanny'
       ) THEN
        RAISE EXCEPTION 'Unexpected source expense category id=99';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM expense_categories
        WHERE id = 3 AND user_id = 2 AND name = 'pharma'
    ) THEN
        RAISE EXCEPTION 'Expected target expense category pharma (id=3, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM expense_categories WHERE id = 71 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM expense_categories
           WHERE id = 71 AND user_id = 2 AND name = 'Pharma'
       ) THEN
        RAISE EXCEPTION 'Unexpected source expense category id=71';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM expense_categories
        WHERE id = 87 AND user_id = 2 AND name = 'entertainment'
    ) THEN
        RAISE EXCEPTION 'Expected target expense category entertainment (id=87, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM expense_categories WHERE id = 10 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM expense_categories
           WHERE id = 10 AND user_id = 2 AND name = 'entertaiment'
       ) THEN
        RAISE EXCEPTION 'Unexpected source expense category id=10';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM expense_categories
        WHERE id = 107 AND user_id = 2 AND name = 'tax'
    ) THEN
        RAISE EXCEPTION 'Expected target expense category tax (id=107, user_id=2)';
    END IF;

    IF EXISTS (SELECT 1 FROM expense_categories WHERE id = 108 AND user_id = 2)
       AND NOT EXISTS (
           SELECT 1 FROM expense_categories
           WHERE id = 108 AND user_id = 2 AND name = 'taxes'
       ) THEN
        RAISE EXCEPTION 'Unexpected source expense category id=108';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM expense_subcategories
        WHERE user_id = 2
          AND category_id IN (10, 71, 99, 108)
    ) THEN
        RAISE EXCEPTION 'An expense source category has unexpected subcategories';
    END IF;
END
$validation$;

-- Case-only duplicates with duplicate "default" subcategories.
UPDATE incomes
SET category_id = 7,
    subcategory_id = CASE WHEN subcategory_id = 2 THEN 11 ELSE subcategory_id END
WHERE user_id = 2
  AND category_id = 3;

DELETE FROM income_subcategories
WHERE id = 2 AND category_id = 3 AND user_id = 2;

DELETE FROM income_categories
WHERE id = 3 AND user_id = 2 AND name = 'Avito';

UPDATE incomes
SET category_id = 14,
    subcategory_id = CASE WHEN subcategory_id = 5 THEN 71 ELSE subcategory_id END
WHERE user_id = 2
  AND category_id = 6;

DELETE FROM income_subcategories
WHERE id = 5 AND category_id = 6 AND user_id = 2;

DELETE FROM income_categories
WHERE id = 6 AND user_id = 2 AND name = 'Lesson';

-- Move stock operations and its unique lukoil subcategory to stocks.
-- Operations are temporarily detached from their subcategory because the
-- composite foreign key is not deferrable.
CREATE TEMP TABLE stock_operation_subcategories ON COMMIT DROP AS
SELECT id AS operation_id, subcategory_id
FROM incomes
WHERE user_id = 2
  AND category_id = 22;

UPDATE incomes
SET subcategory_id = NULL
WHERE id IN (
    SELECT operation_id FROM stock_operation_subcategories
);

UPDATE incomes
SET category_id = 23
WHERE id IN (
    SELECT operation_id FROM stock_operation_subcategories
);

UPDATE income_subcategories
SET category_id = 23
WHERE user_id = 2
  AND category_id = 22;

UPDATE incomes AS operation
SET subcategory_id = previous.subcategory_id
FROM stock_operation_subcategories AS previous
WHERE operation.id = previous.operation_id;

DELETE FROM income_categories
WHERE id = 22 AND user_id = 2 AND name = 'stock';

-- Expense categories do not have subcategories in the approved source rows.
UPDATE expenses SET category_id = 16 WHERE user_id = 2 AND category_id = 99;
DELETE FROM expense_categories WHERE id = 99 AND user_id = 2 AND name = 'nanny';
UPDATE expense_categories SET name = 'nanny' WHERE id = 16 AND user_id = 2;

UPDATE expenses SET category_id = 3 WHERE user_id = 2 AND category_id = 71;
DELETE FROM expense_categories WHERE id = 71 AND user_id = 2 AND name = 'Pharma';

UPDATE expenses SET category_id = 87 WHERE user_id = 2 AND category_id = 10;
DELETE FROM expense_categories WHERE id = 10 AND user_id = 2 AND name = 'entertaiment';

UPDATE expenses SET category_id = 107 WHERE user_id = 2 AND category_id = 108;
DELETE FROM expense_categories WHERE id = 108 AND user_id = 2 AND name = 'taxes';

INSERT INTO schema_migrations (id)
VALUES ('20260726_merge_duplicate_categories')
ON CONFLICT (id) DO NOTHING;

DO $postcheck$
DECLARE
    baseline category_merge_baseline%ROWTYPE;
BEGIN
    SELECT * INTO baseline FROM category_merge_baseline;

    IF baseline.income_count <> (SELECT COUNT(*) FROM incomes)
       OR baseline.income_original_amount <> (SELECT COALESCE(SUM(original_amount), 0) FROM incomes)
       OR baseline.income_amount_rub <> (SELECT COALESCE(SUM(amount_rub), 0) FROM incomes)
       OR baseline.income_amount_usd <> (SELECT COALESCE(SUM(amount_usd), 0) FROM incomes)
       OR baseline.expense_count <> (SELECT COUNT(*) FROM expenses)
       OR baseline.expense_original_amount <> (SELECT COALESCE(SUM(original_amount), 0) FROM expenses)
       OR baseline.expense_amount_rub <> (SELECT COALESCE(SUM(amount_rub), 0) FROM expenses)
       OR baseline.expense_amount_usd <> (SELECT COALESCE(SUM(amount_usd), 0) FROM expenses) THEN
        RAISE EXCEPTION 'Operation counts or monetary totals changed during category merge';
    END IF;

    IF EXISTS (
        SELECT 1 FROM income_categories WHERE user_id = 2 AND id IN (3, 6, 22)
    ) OR EXISTS (
        SELECT 1 FROM expense_categories WHERE user_id = 2 AND id IN (10, 71, 99, 108)
    ) THEN
        RAISE EXCEPTION 'One or more source categories were not removed';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM incomes
        WHERE user_id = 2 AND category_id IN (3, 6, 22)
    ) OR EXISTS (
        SELECT 1
        FROM expenses
        WHERE user_id = 2 AND category_id IN (10, 71, 99, 108)
    ) THEN
        RAISE EXCEPTION 'One or more operations still reference source categories';
    END IF;
END
$postcheck$;

COMMIT;
