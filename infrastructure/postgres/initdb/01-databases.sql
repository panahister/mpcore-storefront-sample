-- Runs once, when the PostgreSQL container is first created. storefront_commerce is the container's own
-- database; the Fulfillment backend gets one of its own. A backend never reads another's database.
CREATE DATABASE storefront_fulfillment;
