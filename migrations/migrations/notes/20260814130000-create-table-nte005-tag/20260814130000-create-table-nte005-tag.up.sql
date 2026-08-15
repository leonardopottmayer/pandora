-- 20260814130000-create-table-nte005-tag.up.sql

-- A label owned by the user. The row is not authored through a CRUD screen: it appears because some
-- page's markdown mentions #it, and the text stays in charge of which pages carry it (nte006).
-- What the row adds on top of the text is the color -- the one thing a markdown file cannot
-- remember -- which is also why a colored tag survives losing its last page while a plain one is
-- swept away.
CREATE TABLE notes.nte005_tag (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	slug VARCHAR(50) NOT NULL,
	name VARCHAR(50) NOT NULL,
	color VARCHAR(20) NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	created_by uuid NULL,
	updated_at TIMESTAMPTZ NULL,
	updated_by uuid NULL
);

ALTER TABLE notes.nte005_tag
ADD CONSTRAINT pk_nte005 PRIMARY KEY (id);

-- The slug is the tag's identity within the user: "#Café" and "#cafe" are one tag, and the name
-- only records how it was first written.
CREATE UNIQUE INDEX uq_nte005_user_slug
ON notes.nte005_tag (user_id, slug);
