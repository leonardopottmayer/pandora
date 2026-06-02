-- 20260131153003-insert-sys001-app-theme.up.sql

INSERT INTO system.sys001_domain (domain_name, item_name, item_value, item_description)
VALUES
    ('AppTheme', 'Light',  'light',  'Light color scheme'),
    ('AppTheme', 'Dark',   'dark',   'Dark color scheme'),
    ('AppTheme', 'System', 'system', 'Follows the operating system color scheme preference');
