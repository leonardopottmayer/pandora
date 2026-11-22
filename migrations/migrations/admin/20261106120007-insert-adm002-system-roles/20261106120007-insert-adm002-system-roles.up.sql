-- 20261106120007-insert-adm002-system-roles.up.sql

-- The two roles every install starts with. Their ids are fixed: the code refers to them
-- (AdminModule.AdminRoleId, AdminModule.MemberRoleId).
INSERT INTO admin.adm002_role (id, name, description, is_system, is_default) VALUES
	('019a5a00-0000-7000-8000-000000000001', 'admin', 'Everything, in every module, including this panel.', true, false),
	('019a5a00-0000-7000-8000-000000000002', 'member', 'Every user module. Given to every new account.', false, true);

INSERT INTO admin.adm003_role_permission (role_id, pattern) VALUES
	('019a5a00-0000-7000-8000-000000000001', '*'),
	('019a5a00-0000-7000-8000-000000000002', 'agenda.*'),
	('019a5a00-0000-7000-8000-000000000002', 'assistant.*'),
	('019a5a00-0000-7000-8000-000000000002', 'channels.*'),
	('019a5a00-0000-7000-8000-000000000002', 'communications.*'),
	('019a5a00-0000-7000-8000-000000000002', 'files.*'),
	('019a5a00-0000-7000-8000-000000000002', 'finances.*'),
	('019a5a00-0000-7000-8000-000000000002', 'integrations.*'),
	('019a5a00-0000-7000-8000-000000000002', 'notes.*');

-- Rollout: every account that already exists keeps doing what it did. A one-off read of Identity's
-- table at migration time; the code never reads it.
INSERT INTO admin.adm004_user_role (user_id, role_id)
SELECT id, '019a5a00-0000-7000-8000-000000000002' FROM identity.idt001_user;
