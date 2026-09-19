-- Naxi Pro — code durations (1/3/6 months or any number of days)
-- Run in: Supabase Dashboard → SQL Editor. Safe to run twice (idempotent).
alter table public.pro_invites add column if not exists duration_days int not null default 0;
alter table public.pro_devices add column if not exists expires_at timestamptz;
