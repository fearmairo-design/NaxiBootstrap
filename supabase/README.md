# Supabase — Naxi Pro

## One-time setup (≈2 minutes)

1. **SQL**: open Dashboard → SQL Editor → paste `migrations/0001_pro_waves.sql` → Run.
2. **Function**: open Dashboard → Edge Functions → `naxi-pro` → replace the code with
   `functions/naxi-pro/index.ts` → Deploy.

The function is backward compatible: it works before AND after the migration.

## Issuing invite codes ("waves")

Run in SQL Editor (codes must be 6 characters — the client UI enforces this):

```sql
-- a code for 3 activations (lifetime Pro)
insert into pro_invites (code, max_uses) values ('NBTW03', 3);

-- a code for 50 activations
insert into pro_invites (code, max_uses) values ('NBTW50', 50);

-- a code that grants Pro for 30 days per activation (1 month)
insert into pro_invites (code, max_uses, duration_days) values ('NBTM01', 10, 30);

-- a code that grants Pro for 90 days per activation (3 months)
insert into pro_invites (code, max_uses, duration_days) values ('NBTM03', 10, 90);

-- raise an existing code's limit
update pro_invites set max_uses = 5 where code = 'NBT468';

-- see who activated what (and when timed Pro ends)
select i.code, i.max_uses, i.duration_days, count(d.device_id) as used,
       i.max_uses - count(d.device_id) as left, max(d.expires_at) as soonest_expiry
from pro_invites i left join pro_devices d on d.code = i.code
group by i.code, i.max_uses
order by i.created_at desc;
```

### Revoking premium from a specific device

Deleting a device binding removes its Pro **immediately** (next `status` check returns
`pro: false`) and **frees the code slot** for someone else:

```sql
-- list bindings of a code
select * from pro_devices where code = 'NBT468';

-- revoke one device
delete from pro_devices where code = 'NBT468' and device_id = 'DEVICE_HASH_HERE';

-- or revoke everyone who used a code (slot fully freed)
delete from pro_devices where code = 'NBT468';
update pro_invites set uses_count = 0 where code = 'NBT468';
```

Or simply ask Cline — it can revoke/restore any device via the service key.

- Each unique device consumes one activation; the same device re-entering its code
  always returns `already: true` (no second activation consumed) — safe after reinstall.
- `reset a single device`: `delete from pro_devices where code = 'NBT468' and device_id = '...';`
  then `update pro_invites set uses_count = uses_count - 1 where code = 'NBT468';`

## API contract (edge function `naxi-pro`)

- `POST { action: "status", device_id }` → `{ ok: true, pro: bool, expires_at?: "ISO date" }`
  (`expires_at` present only when the device has timed Pro; absent for lifetime)
- `POST { action: "redeem", code, device_id }` →
  - `{ ok: true, pro: true, already: false, expires_at?: "ISO date" }` — activated
  - `{ ok: true, pro: true, already: true, expires_at?: "ISO date" }` — this device already owns Pro
  - `400 { ok: false, error: "used" | "expired" | "invalid" | "invalid_device" }`
- `device_id` = SHA256 hex (64 chars, case-insensitive) of `"naxi-pro:" + MachineGuid`.
- Expired bindings are deleted lazily (on status/redeem) and free the code slot.
