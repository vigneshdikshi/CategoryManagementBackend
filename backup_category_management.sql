--
-- PostgreSQL database dump
--

\restrict TXz1TmnNv28iyqhtAPadEqoWqemV12mAY7BFJFUgJK9VcKTwWJGmwgXZG7dhlf6

-- Dumped from database version 18.6
-- Dumped by pg_dump version 18.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: categories; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.categories (
    id bigint NOT NULL,
    category_code character varying(5) NOT NULL,
    name character varying(100) NOT NULL,
    parent_id bigint,
    level smallint NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at_utc timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at_utc timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT "CK_categories_level" CHECK (((level >= 1) AND (level <= 3)))
);


ALTER TABLE public.categories OWNER TO postgres;

--
-- Name: category_code_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.category_code_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.category_code_seq OWNER TO postgres;

--
-- Name: products; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.products (
    id bigint NOT NULL,
    name character varying(150) NOT NULL,
    description character varying(2000),
    price numeric(12,2) NOT NULL,
    image_url character varying(1000),
    stock_quantity integer DEFAULT 0 NOT NULL,
    category_id bigint NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at_utc timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at_utc timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT "CK_products_price" CHECK ((price >= (0)::numeric)),
    CONSTRAINT "CK_products_stock" CHECK ((stock_quantity >= 0))
);


ALTER TABLE public.products OWNER TO postgres;

--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20261013050614_InitialCreate	10.0.0
20261013084715_ManualIdsAndCategoryCodeSeq	10.0.0
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.categories (id, category_code, name, parent_id, level, is_active, created_at_utc, updated_at_utc) FROM stdin;
1001	00083	Electronics	\N	1	t	2026-10-13 15:08:44.249815+05:30	2026-10-13 15:08:44.249815+05:30
1002	00084	Mobile Phones	1001	2	t	2026-10-13 15:09:36.270825+05:30	2026-10-13 15:09:36.270825+05:30
1003	00085	Android Phones	1002	3	t	2026-10-13 15:10:01.102382+05:30	2026-10-13 15:10:01.102382+05:30
2001	00086	Fashion	\N	1	t	2026-10-13 15:11:41.344648+05:30	2026-10-13 15:11:41.344648+05:30
2002	00087	Men's Clothing	2001	2	t	2026-10-13 15:12:04.567839+05:30	2026-10-13 15:12:04.567839+05:30
2003	00088	T-Shirts	2002	3	t	2026-10-13 15:12:32.82482+05:30	2026-10-13 15:12:32.82482+05:30
\.


--
-- Data for Name: products; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.products (id, name, description, price, image_url, stock_quantity, category_id, is_active, created_at_utc, updated_at_utc) FROM stdin;
5001	Samsung Galaxy S25	Premium Android smartphone with advanced camera features	79999.00	\N	0	1003	t	2026-10-13 15:22:43.682956+05:30	2026-10-13 15:22:43.68307+05:30
\.


--
-- Name: category_code_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.category_code_seq', 158, true);


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: categories PK_categories; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "PK_categories" PRIMARY KEY (id);


--
-- Name: products PK_products; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "PK_products" PRIMARY KEY (id);


--
-- Name: IX_categories_category_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_categories_category_code" ON public.categories USING btree (category_code);


--
-- Name: IX_categories_parent_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_categories_parent_id" ON public.categories USING btree (parent_id);


--
-- Name: IX_products_category_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_products_category_id" ON public.products USING btree (category_id);


--
-- Name: categories FK_categories_categories_parent_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "FK_categories_categories_parent_id" FOREIGN KEY (parent_id) REFERENCES public.categories(id) ON DELETE RESTRICT;


--
-- Name: products FK_products_categories_category_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "FK_products_categories_category_id" FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict TXz1TmnNv28iyqhtAPadEqoWqemV12mAY7BFJFUgJK9VcKTwWJGmwgXZG7dhlf6

