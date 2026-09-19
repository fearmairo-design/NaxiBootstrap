-- Naxi Pro — multi-use invite codes ("waves")
-- Run in: Supabase Dashboard → SQL Editor. Safe to run twice (idempotent).
-- After this, set per-code limits with:  update pro_invites set max_uses = 50 where code = 'NBTW50';

alter table public.pro_invites add column if not exists max_uses int not null default 1;
alter table public.pro_invites add column if not exists uses_count int not null default 0;

create table if not exists public.pro_devices (
  code text not null,
  device_id text not null,
  redeemed_at timestamptz not null default now(),
  primary key (code, device_id)
);

-- Lock down: only the service role (edge function) may touch bindings.
alter table public.pro_devices enable row level security;

-- Backfill: keep existing single-use bindings working with the new scheme.
insert into public.pro_devices (code, device_id, redeemed_at)
select code, device_id, coalesce(redeemed_at, now())
from public.pro_invites
where device_id is not null and device_id <> ''
on conflict (code, device_id) do nothing;

update public.pro_invites i
set uses_count = d.cnt
from (select code, count(*)::int as cnt from public.pro_devices group by code) d
where d.code = i.code;
