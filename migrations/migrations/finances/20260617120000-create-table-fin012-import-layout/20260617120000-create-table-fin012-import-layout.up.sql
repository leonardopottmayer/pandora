-- 20260617120000-create-table-fin012-import-layout.up.sql
-- Phase 09: import layout registry. System layouts (user_id NULL) are seeded here; user layouts
-- are reserved for a future phase. The config jsonb carries all parser-specific options so the
-- parsers stay generic and the quirks live in data.

CREATE TABLE finances.fin012_import_layout (
    id              uuid         NOT NULL DEFAULT uuid_generate_v7(),
    user_id         uuid         NULL,
    layout_code     varchar(60)  NOT NULL,
    name            varchar(100) NOT NULL,
    bank_name       varchar(60)  NULL,
    file_format     varchar(5)   NOT NULL,
    account_type    varchar(10)  NOT NULL,
    config          jsonb        NOT NULL,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT current_timestamp
);

ALTER TABLE finances.fin012_import_layout
ADD CONSTRAINT pk_fin012 PRIMARY KEY (id);

ALTER TABLE finances.fin012_import_layout
ADD CONSTRAINT ck_fin012_file_format CHECK (file_format IN ('ofx', 'csv'));

ALTER TABLE finances.fin012_import_layout
ADD CONSTRAINT ck_fin012_account_type CHECK (account_type IN ('account', 'card'));

-- System layout codes are globally unique (user layouts keyed by user_id + code in a future phase)
CREATE UNIQUE INDEX uq_fin012_system_layout_code
ON finances.fin012_import_layout (layout_code)
WHERE user_id IS NULL;

CREATE INDEX ix_fin012_user_id ON finances.fin012_import_layout (user_id);

-- ─────────────────────────────────────────────
-- Seed: system layouts
-- ─────────────────────────────────────────────

-- Viacredi bank account OFX
-- Quirks: TRNAMT uses comma decimal, FITID often empty, multiple BANKTRANLIST blocks,
--         non-standard BANKINFO block, amounts always positive (sign from TRNTYPE).
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'viacredi-ofx',
    'Viacredi — Bank Account (OFX)',
    'Viacredi',
    'ofx',
    'account',
    '{
        "descriptionField": "NAME",
        "amountIsAlwaysAbsolute": true,
        "invertAmount": false,
        "treatPaymentAsDebit": false,
        "quirks": ["multiple-banktranlist", "comma-decimal", "empty-fitid"]
    }'::jsonb
);

-- Nubank credit card OFX
-- TRNAMT is negative for purchases; CREDITCARDMSGSRSV1 wrapper; timezone in dates.
-- Quirk: IOF entries share FITID with the main transaction → dedup key includes description.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'nubank-card-ofx',
    'Nubank — Credit Card (OFX)',
    'Nubank',
    'ofx',
    'card',
    '{
        "descriptionField": "MEMO",
        "amountIsAlwaysAbsolute": false,
        "invertAmount": true,
        "treatPaymentAsDebit": false,
        "quirks": ["fitid-shared-with-secondary"]
    }'::jsonb
);

-- Nubank bank account OFX
-- Standard signs (positive=credit, negative=debit); MEMO field; timezone in dates; UTF-8.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'nubank-account-ofx',
    'Nubank — Bank Account (OFX)',
    'Nubank',
    'ofx',
    'account',
    '{
        "descriptionField": "MEMO",
        "amountIsAlwaysAbsolute": false,
        "invertAmount": false,
        "treatPaymentAsDebit": false,
        "quirks": []
    }'::jsonb
);

-- Banco Inter bank account OFX
-- Uses TRNTYPE=PAYMENT for debits instead of DEBIT; amounts signed; FITID date+sequence.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'inter-ofx',
    'Banco Inter — Bank Account (OFX)',
    'Banco Inter',
    'ofx',
    'account',
    '{
        "descriptionField": "MEMO",
        "amountIsAlwaysAbsolute": false,
        "invertAmount": false,
        "treatPaymentAsDebit": true,
        "quirks": []
    }'::jsonb
);

-- Itaú bank account OFX
-- SGML without closing tags (valid per OFX 1.x SGML spec); signed amounts; MEMO field.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'itau-account-ofx',
    'Itaú — Bank Account (OFX)',
    'Itaú',
    'ofx',
    'account',
    '{
        "descriptionField": "MEMO",
        "amountIsAlwaysAbsolute": false,
        "invertAmount": false,
        "treatPaymentAsDebit": false,
        "quirks": ["no-closing-tags"]
    }'::jsonb
);

-- Viacredi bank account CSV
-- Separator: semicolon; multi-section format (one header per date block); sign from Tipo column.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'viacredi-account-csv',
    'Viacredi — Bank Account (CSV)',
    'Viacredi',
    'csv',
    'account',
    '{
        "delimiter": ";",
        "encoding": "windows-1252",
        "isMultiSection": true,
        "dateColumn": "Data",
        "dateFormat": "dd/MM/yyyy HH:mm:ss",
        "amountColumn": "Valor",
        "amountDecimalSeparator": ",",
        "descriptionColumn": "Titulo",
        "identifierColumn": "ID",
        "signColumn": "Tipo",
        "creditSignValue": "Credito",
        "debitSignValue": "Debito",
        "amountIsAlwaysPositive": true,
        "installmentPatterns": ["(\\d+)/(\\d+)", "- Parcela (\\d+)/(\\d+)"]
    }'::jsonb
);

-- Nubank credit card CSV (fatura)
-- Header: date,title,amount; amounts always positive (all expenses); installments in title.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'nubank-card-csv',
    'Nubank — Credit Card (CSV)',
    'Nubank',
    'csv',
    'card',
    '{
        "delimiter": ",",
        "encoding": "UTF-8",
        "isMultiSection": false,
        "dateColumn": "date",
        "dateFormat": "yyyy-MM-dd",
        "amountColumn": "amount",
        "amountDecimalSeparator": ",",
        "descriptionColumn": "title",
        "identifierColumn": null,
        "signColumn": null,
        "amountIsAlwaysPositive": true,
        "installmentPatterns": ["- Parcela (\\d+)/(\\d+)", "(\\d+)/(\\d+)"]
    }'::jsonb
);

-- Nubank bank account CSV (extrato)
-- Header: Data,Valor,Identificador,Descrição; Identificador is UUID (use as FITID).
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'nubank-account-csv',
    'Nubank — Bank Account (CSV)',
    'Nubank',
    'csv',
    'account',
    '{
        "delimiter": ",",
        "encoding": "UTF-8",
        "isMultiSection": false,
        "dateColumn": "Data",
        "dateFormat": "dd/MM/yyyy",
        "amountColumn": "Valor",
        "amountDecimalSeparator": ".",
        "descriptionColumn": "Descrição",
        "identifierColumn": "Identificador",
        "signColumn": null,
        "amountIsAlwaysPositive": false,
        "installmentPatterns": ["- Parcela (\\d+)/(\\d+)", "(\\d+)/(\\d+)"]
    }'::jsonb
);

-- Itaú credit card CSV (fatura)
-- Header: data,lançamento,valor; positive = expense, negative = payment/refund.
INSERT INTO finances.fin012_import_layout
    (layout_code, name, bank_name, file_format, account_type, config)
VALUES (
    'itau-card-csv',
    'Itaú — Credit Card (CSV)',
    'Itaú',
    'csv',
    'card',
    '{
        "delimiter": ",",
        "encoding": "UTF-8",
        "isMultiSection": false,
        "dateColumn": "data",
        "dateFormat": "yyyy-MM-dd",
        "amountColumn": "valor",
        "amountDecimalSeparator": ".",
        "descriptionColumn": "lançamento",
        "identifierColumn": null,
        "signColumn": null,
        "amountIsAlwaysPositive": false,
        "positiveAmountIsExpense": true,
        "installmentPatterns": ["(\\d+)/(\\d+)", "- Parcela (\\d+)/(\\d+)"]
    }'::jsonb
);
