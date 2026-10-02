-- SneakMart Sprint 2: catalog schema (MySQL 8.0.16+, InnoDB, utf8mb4)
-- Mirrors the EF Core model in src/SneakMart.Api/Data/AppDbContext.cs.
-- Use either this script on an empty database OR `dotnet ef database update` (see README), not both.

SET NAMES utf8mb4;

CREATE TABLE users (
  id            INT          NOT NULL AUTO_INCREMENT,
  full_name     VARCHAR(100) NOT NULL,
  email         VARCHAR(150) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  role          VARCHAR(20)  NOT NULL DEFAULT 'customer',
  created_at    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_users_email (email),
  CONSTRAINT ck_users_role CHECK (role IN ('customer','admin'))
) ENGINE=InnoDB;

CREATE TABLE categories (
  id          INT          NOT NULL AUTO_INCREMENT,
  parent_id   INT          NULL,
  name        VARCHAR(50)  NOT NULL,
  slug        VARCHAR(80)  NOT NULL,
  description VARCHAR(255) NULL,
  is_active   TINYINT(1)   NOT NULL DEFAULT 1,
  created_at  DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at  DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_categories_slug (slug),
  CONSTRAINT fk_categories_parent FOREIGN KEY (parent_id) REFERENCES categories (id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_categories_not_self_parent CHECK (parent_id IS NULL OR parent_id <> id)
) ENGINE=InnoDB;

CREATE TABLE products (
  id             INT          NOT NULL AUTO_INCREMENT,
  category_id    INT          NOT NULL,
  name           VARCHAR(150) NOT NULL,
  slug           VARCHAR(180) NOT NULL,
  brand          VARCHAR(50)  NOT NULL,
  description    TEXT         NULL,
  status         VARCHAR(20)  NOT NULL DEFAULT 'draft',
  specifications JSON         NULL,
  created_at     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_products_slug (slug),
  CONSTRAINT fk_products_category FOREIGN KEY (category_id) REFERENCES categories (id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_products_status CHECK (status IN ('draft','active','archived')),
  CONSTRAINT ck_products_specs_object CHECK (specifications IS NULL OR JSON_TYPE(specifications) = 'OBJECT')
) ENGINE=InnoDB;

CREATE TABLE variants (
  id            INT         NOT NULL AUTO_INCREMENT,
  product_id    INT         NOT NULL,
  name          VARCHAR(80) NOT NULL,
  color         VARCHAR(40) NOT NULL,
  option_values JSON        NULL,
  created_at    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_variants_product_name (product_id, name),
  UNIQUE KEY ux_variants_id_product (id, product_id),   -- target of the composite FK from skus
  CONSTRAINT fk_variants_product FOREIGN KEY (product_id) REFERENCES products (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT ck_variants_options_object CHECK (option_values IS NULL OR JSON_TYPE(option_values) = 'OBJECT')
) ENGINE=InnoDB;

CREATE TABLE skus (
  id               INT           NOT NULL AUTO_INCREMENT,
  product_id       INT           NOT NULL,
  variant_id       INT           NULL,                      -- NULL = product without variants
  sku_code         VARCHAR(40)   NOT NULL,
  size_label       VARCHAR(10)   NOT NULL,
  price            DECIMAL(10,2) NOT NULL,
  compare_at_price DECIMAL(10,2) NULL,
  stock_quantity   INT           NOT NULL DEFAULT 0,
  is_active        TINYINT(1)    NOT NULL DEFAULT 1,
  variant_key      INT GENERATED ALWAYS AS (IFNULL(variant_id, 0)) STORED,
  created_at       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_skus_sku_code (sku_code),
  UNIQUE KEY ux_skus_combo (product_id, variant_key, size_label),
  CONSTRAINT fk_skus_product FOREIGN KEY (product_id) REFERENCES products (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  -- composite FK: the variant must belong to the SAME product (not enforced when variant_id IS NULL)
  CONSTRAINT fk_skus_variant_product FOREIGN KEY (variant_id, product_id) REFERENCES variants (id, product_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_skus_price_nonneg CHECK (price >= 0),
  CONSTRAINT ck_skus_stock_nonneg CHECK (stock_quantity >= 0),
  CONSTRAINT ck_skus_compare_price CHECK (compare_at_price IS NULL OR compare_at_price >= price)
) ENGINE=InnoDB;

CREATE TABLE assets (
  id          INT          NOT NULL AUTO_INCREMENT,
  product_id  INT          NOT NULL,
  variant_id  INT          NULL,
  storage_key VARCHAR(255) NOT NULL,
  role        VARCHAR(20)  NOT NULL DEFAULT 'gallery',
  alt_text    VARCHAR(255) NULL,
  sort_order  INT          NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  CONSTRAINT fk_assets_product FOREIGN KEY (product_id) REFERENCES products (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT fk_assets_variant FOREIGN KEY (variant_id) REFERENCES variants (id)
    ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB;

-- ---- Sprint 1 tables, adapted to reference SKUs ----

CREATE TABLE cart (
  id         INT         NOT NULL AUTO_INCREMENT,
  user_id    INT         NOT NULL,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_cart_user (user_id),
  CONSTRAINT fk_cart_user FOREIGN KEY (user_id) REFERENCES users (id)
    ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE cart_items (
  id       INT NOT NULL AUTO_INCREMENT,
  cart_id  INT NOT NULL,
  sku_id   INT NOT NULL,
  quantity INT NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ux_cart_items_cart_sku (cart_id, sku_id),
  CONSTRAINT fk_cart_items_cart FOREIGN KEY (cart_id) REFERENCES cart (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT fk_cart_items_sku FOREIGN KEY (sku_id) REFERENCES skus (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT ck_cart_items_qty CHECK (quantity > 0)
) ENGINE=InnoDB;

CREATE TABLE orders (
  id               INT           NOT NULL AUTO_INCREMENT,
  user_id          INT           NOT NULL,
  total_amount     DECIMAL(10,2) NOT NULL,
  status           VARCHAR(20)   NOT NULL DEFAULT 'pending',
  shipping_address VARCHAR(255)  NULL,
  created_at       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  CONSTRAINT fk_orders_user FOREIGN KEY (user_id) REFERENCES users (id)
    ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE order_items (
  id                    INT           NOT NULL AUTO_INCREMENT,
  order_id              INT           NOT NULL,
  sku_id                INT           NOT NULL,
  product_name_snapshot VARCHAR(150)  NOT NULL,
  sku_code_snapshot     VARCHAR(40)   NOT NULL,
  quantity              INT           NOT NULL,
  unit_price            DECIMAL(10,2) NOT NULL,
  PRIMARY KEY (id),
  CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders (id)
    ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT fk_order_items_sku FOREIGN KEY (sku_id) REFERENCES skus (id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,   -- a sold SKU can never be hard-deleted
  CONSTRAINT ck_order_items_qty CHECK (quantity > 0)
) ENGINE=InnoDB;
