-- 20260612120003-insert-fin002-system-categories.up.sql
-- Idempotent seed of the system category tree (2 levels). Re-runnable: ON CONFLICT (code) DO NOTHING.

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('housing', 'Housing', 'expense', NULL, 1, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('rent', 'Rent', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 1, false, true),
	('mortgage', 'Mortgage', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 2, false, true),
	('utilities', 'Utilities', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 3, false, true),
	('condo-fee', 'Condo Fee', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 4, false, true),
	('home-maintenance', 'Home Maintenance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 5, false, true),
	('property-tax', 'Property Tax', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 6, false, true),
	('home-insurance', 'Home Insurance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 7, false, true),
	('other-housing', 'Other Housing', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'housing'), 8, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('food', 'Food', 'expense', NULL, 2, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('groceries', 'Groceries', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'food'), 1, false, true),
	('restaurants', 'Restaurants', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'food'), 2, false, true),
	('food-delivery', 'Food Delivery', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'food'), 3, false, true),
	('coffee-snacks', 'Coffee & Snacks', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'food'), 4, false, true),
	('other-food', 'Other Food', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'food'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('transport', 'Transport', 'expense', NULL, 3, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('fuel', 'Fuel', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 1, false, true),
	('public-transport', 'Public Transport', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 2, false, true),
	('ride-hailing', 'Ride-hailing', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 3, false, true),
	('parking-tolls', 'Parking & Tolls', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 4, false, true),
	('vehicle-maintenance', 'Vehicle Maintenance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 5, false, true),
	('vehicle-insurance', 'Vehicle Insurance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 6, false, true),
	('car-payment', 'Car Payment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 7, false, true),
	('other-transport', 'Other Transport', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'transport'), 8, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('health', 'Health', 'expense', NULL, 4, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('health-insurance', 'Health Insurance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 1, false, true),
	('doctor', 'Doctor', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 2, false, true),
	('pharmacy', 'Pharmacy', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 3, false, true),
	('dentist', 'Dentist', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 4, false, true),
	('therapy', 'Therapy', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 5, false, true),
	('gym-fitness', 'Gym & Fitness', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 6, false, true),
	('other-health', 'Other Health', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 7, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('education', 'Education', 'expense', NULL, 5, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('tuition', 'Tuition', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'education'), 1, false, true),
	('courses', 'Courses', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'education'), 2, false, true),
	('books-materials', 'Books & Materials', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'education'), 3, false, true),
	('school-supplies', 'School Supplies', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'education'), 4, false, true),
	('other-education', 'Other Education', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'education'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('personal-care', 'Personal Care', 'expense', NULL, 6, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('haircut-beauty', 'Haircut & Beauty', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'personal-care'), 1, false, true),
	('cosmetics', 'Cosmetics', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'personal-care'), 2, false, true),
	('spa-wellness', 'Spa & Wellness', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'personal-care'), 3, false, true),
	('other-personal-care', 'Other Personal Care', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'personal-care'), 4, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('family', 'Family', 'expense', NULL, 7, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('childcare', 'Childcare', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'family'), 1, false, true),
	('child-education', 'Child Education', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'family'), 2, false, true),
	('allowance', 'Allowance', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'family'), 3, false, true),
	('elder-care', 'Elder Care', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'family'), 4, false, true),
	('other-family', 'Other Family', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'family'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('shopping', 'Shopping', 'expense', NULL, 8, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('clothing', 'Clothing', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 1, false, true),
	('electronics', 'Electronics', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 2, false, true),
	('home-goods', 'Home Goods', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 3, false, true),
	('gifts', 'Gifts', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 4, false, true),
	('hobbies', 'Hobbies', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 5, false, true),
	('other-shopping', 'Other Shopping', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'shopping'), 6, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('entertainment', 'Entertainment', 'expense', NULL, 9, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('streaming', 'Streaming', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 1, false, true),
	('movies-shows', 'Movies & Shows', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 2, false, true),
	('games', 'Games', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 3, false, true),
	('events-tickets', 'Events & Tickets', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 4, false, true),
	('bars-nightlife', 'Bars & Nightlife', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 5, false, true),
	('other-entertainment', 'Other Entertainment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'entertainment'), 6, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('travel', 'Travel', 'expense', NULL, 10, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('flights', 'Flights', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 1, false, true),
	('lodging', 'Lodging', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 2, false, true),
	('travel-transport', 'Travel Transport', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 3, false, true),
	('travel-food', 'Travel Food', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 4, false, true),
	('travel-activities', 'Travel Activities', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 5, false, true),
	('other-travel', 'Other Travel', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'travel'), 6, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('financial-expenses', 'Financial Expenses', 'expense', NULL, 11, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('bank-fees', 'Bank Fees', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 1, false, true),
	('interest-paid', 'Interest Paid', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 2, false, true),
	('taxes', 'Taxes', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 3, false, true),
	('fines', 'Fines', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 4, false, true),
	('loan-payment', 'Loan Payment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 5, false, true),
	('credit-card-fee', 'Credit Card Fee', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 6, false, true),
	('credit-card-payment', 'Credit Card Payment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 7, false, true),
	('other-financial', 'Other Financial', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 8, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('subscriptions', 'Subscriptions', 'expense', NULL, 12, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('software', 'Software', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 1, false, true),
	('cloud-storage', 'Cloud Storage', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 2, false, true),
	('news-media', 'News & Media', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 3, false, true),
	('memberships', 'Memberships', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 4, false, true),
	('other-subscriptions', 'Other Subscriptions', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('work-expenses', 'Work Expenses', 'expense', NULL, 13, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('office-supplies', 'Office Supplies', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'work-expenses'), 1, false, true),
	('business-travel', 'Business Travel', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'work-expenses'), 2, false, true),
	('professional-services', 'Professional Services', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'work-expenses'), 3, false, true),
	('equipment', 'Equipment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'work-expenses'), 4, false, true),
	('other-work', 'Other Work', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'work-expenses'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('pets', 'Pets', 'expense', NULL, 14, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('pet-food', 'Pet Food', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'pets'), 1, false, true),
	('vet', 'Vet', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'pets'), 2, false, true),
	('pet-supplies', 'Pet Supplies', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'pets'), 3, false, true),
	('pet-grooming', 'Pet Grooming', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'pets'), 4, false, true),
	('other-pets', 'Other Pets', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'pets'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('misc-expense', 'Misc Expense', 'expense', NULL, 15, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('donations', 'Donations', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-expense'), 1, false, true),
	('cash-withdrawal', 'Cash Withdrawal', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-expense'), 2, false, true),
	('other-misc-expense', 'Other Misc Expense', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-expense'), 3, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('primary-income', 'Primary Income', 'income', NULL, 16, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('salary', 'Salary', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'primary-income'), 1, false, true),
	('bonus', 'Bonus', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'primary-income'), 2, false, true),
	('overtime', 'Overtime', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'primary-income'), 3, false, true),
	('commission', 'Commission', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'primary-income'), 4, false, true),
	('other-primary-income', 'Other Primary Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'primary-income'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('investment-income', 'Investment Income', 'income', NULL, 17, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('dividends', 'Dividends', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 1, false, true),
	('interest-income', 'Interest Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 2, false, true),
	('capital-gains', 'Capital Gains', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 3, false, true),
	('rental-income', 'Rental Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 4, false, true),
	('crypto-gains', 'Crypto Gains', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 5, false, true),
	('other-investment-income', 'Other Investment Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'investment-income'), 6, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('sales-income', 'Sales Income', 'income', NULL, 18, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('product-sales', 'Product Sales', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'sales-income'), 1, false, true),
	('service-income', 'Service Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'sales-income'), 2, false, true),
	('freelance', 'Freelance', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'sales-income'), 3, false, true),
	('other-sales-income', 'Other Sales Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'sales-income'), 4, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('support-income', 'Support Income', 'income', NULL, 19, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('gifts-received', 'Gifts Received', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'support-income'), 1, false, true),
	('government-benefit', 'Government Benefit', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'support-income'), 2, false, true),
	('pension', 'Pension', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'support-income'), 3, false, true),
	('alimony', 'Alimony', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'support-income'), 4, false, true),
	('other-support-income', 'Other Support Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'support-income'), 5, true, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('misc-income', 'Misc Income', 'income', NULL, 20, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('refunds', 'Refunds', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-income'), 1, false, true),
	('cashback', 'Cashback', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-income'), 2, false, true),
	('reimbursements', 'Reimbursements', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-income'), 3, false, true),
	('other-misc-income', 'Other Misc Income', 'income', (SELECT id FROM finances.fin002_system_category WHERE code = 'misc-income'), 4, true, true)
ON CONFLICT (code) DO NOTHING;

-- seeded: 20 parents / 110 children
