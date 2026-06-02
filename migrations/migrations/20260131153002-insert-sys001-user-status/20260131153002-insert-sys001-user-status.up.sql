-- 20260131153002-insert-sys001-user-status.up.sql

INSERT INTO system.sys001_domain (domain_name, item_name, item_value, item_description)
VALUES
    ('UserStatus', 'Active',  'active',  'User is active and can access the system'),
    ('UserStatus', 'Blocked', 'blocked', 'User is blocked due to security or policy issues');
