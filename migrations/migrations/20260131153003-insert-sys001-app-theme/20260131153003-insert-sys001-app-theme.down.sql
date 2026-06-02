-- 20260131153003-insert-sys001-app-theme.down.sql

DELETE FROM system.sys001_domain
WHERE domain_name = 'AppTheme'
  AND item_value IN ('light', 'dark', 'system');
