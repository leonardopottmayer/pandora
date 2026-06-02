-- 20260131153002-insert-sys001-user-status.down.sql

DELETE FROM system.sys001_domain
WHERE domain_name = 'UserStatus'
  AND item_value IN ('active', 'blocked');
