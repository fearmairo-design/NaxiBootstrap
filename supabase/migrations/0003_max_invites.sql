-- Naxi MAX — separate invite table (mirror of pro_invites).
-- Every code inserted here is a Naxi MAX subscription code.
-- Run in: Supabase Dashboard → SQL Editor. Safe to run twice (idempotent).

create table if not exists public.max_invites (
  code text primary key,
  device_id text,
  redeemed_at timestamptz,
  max_uses int not null default 1,
  uses_count int not null default 0,
  duration_days int not null default 0
);

create table if not exists public.max_devices (
  code text not null,
  device_id text not null,
  redeemed_at timestamptz not null default now(),
  expires_at timestamptz,
  primary key (code, device_id)
);

-- Lock down: only the service role (edge function) may touch bindings.
alter table public.max_devices enable row level security;
alter table public.max_invites enable row level security;

-- Create MAX codes like this (same style as Pro codes):
--   insert into max_invites (code, max_uses, duration_days) values ('NBMAX01', 5, 180);
